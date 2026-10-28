<#
.SYNOPSIS
    Checks Markdown documentation against the source code and between language versions.

.DESCRIPTION
    Scans README*.md and every Markdown file under the documentation root, then reports:
      ERROR    [option]  option cited in the documentation but absent from the source code (-SourcePath)
      WARNING  [option]  option accepted by the source code but never documented (-SourcePath)
      ERROR    [parity]  language versions (name.md / name.fr.md / name.en.md) with different
                         heading structure, code block count or cited options
      ERROR    [style]   word forbidden by Rule 6 of the engineering charter
      WARNING  [style]   marketing vocabulary
      ERROR    [link]    relative link or image pointing to a missing file

    Options are long options (--name). They are collected from inline code spans, from code blocks
    without a shell language (text, console output) and, in shell code blocks, from the lines that
    call -CommandName (continuation lines included). Without -CommandName, every shell line counts,
    which reports options of unrelated tools such as dotnet or git.

    Exit code: 1 when at least one ERROR is reported, 0 otherwise.

    -HistoricalFile takes file name patterns (upgrade-to-2.0*.md) whose options are left out of the
    comparison with the code, because such pages cite removed options on purpose.

.EXAMPLE
    ./Test-Documentation.ps1 -Path . -SourcePath . -CommandName 'NetFlowAudit.Cli' -HistoricalFile 'upgrade-to-*.md'
#>
[CmdletBinding()]
param(
    [string]$Path = '.',
    [string]$SourcePath,
    [string[]]$SourceInclude = @('*.cs'),
    [string]$CommandName,
    [string[]]$IgnoreOption = @(),
    # Pages that cite removed options on purpose (upgrade guides): their options are not compared with the code.
    [string[]]$HistoricalFile = @(),
    [string[]]$ExcludeFile = @('CLAUDE.md', 'AGENTS.md', 'CHANGELOG.md'),
    [string[]]$ExcludeDirectory = @('bin', 'obj', 'node_modules', '.git', '.vs', '.claude', 'TestResults', 'publish', 'dist', '.sonarqube'),
    [switch]$IncludeTestSources
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $Path).Path
$findings = New-Object System.Collections.Generic.List[object]

$optionPattern = '(?<![\w-])--[a-z][a-z0-9]*(?:-[a-z0-9]+)*'
$shellLanguages = @('powershell', 'pwsh', 'ps1', 'bash', 'sh', 'shell', 'zsh', 'cmd', 'bat', 'batch')
$proseLanguages = @('', 'text', 'txt', 'console', 'plaintext', 'output')

# Rule 6 of the charter: blocking. Accented French letters are written as \u escapes so that
# Windows PowerShell 5.1 reads this file correctly without a byte order mark.
$forbiddenWords = @(
    '\brobust(ness)?\b', '\bresilien(t|cy|ce)\b', '\bseamless(ly)?\b', '\bsmart\b',
    '\badvanced\b', '\bcomprehensive\b', '\bultimate\b',
    '\brobuste(s)?\b', '\brobustesse\b', '\brésilien\w*', '\bintelligent(e|s|es)?\b',
    '\bavancé(e|s|es)?\b', '\bexhausti(f|ve|fs|ves)\b', '\bultime(s)?\b'
)
$marketingWords = @(
    '\bpowerful\b', '\bcutting-edge\b', '\bstate-of-the-art\b', '\bblazing(ly)?\b', '\beffortless(ly)?\b',
    '\bleverag(e|es|ed|ing)\b', '\bworld-class\b', '\bbest-in-class\b', '\bnext-generation\b', '\bgame-chang\w*',
    '\bultra-\w+', '\bpuissant(e|s|es)?\b', '\bde pointe\b', '\bsans effort\b', '\brévolutionnaire(s)?\b',
    '\bincontournable(s)?\b', '\brigoureusement\b', '\bscrupuleusement\b'
)

function Add-Finding {
    param([string]$Severity, [string]$Category, [string]$File, [int]$Line, [string]$Message)
    $findings.Add([pscustomobject]@{ Severity = $Severity; Category = $Category; File = $File; Line = $Line; Message = $Message })
}

function Get-RelativePath {
    param([string]$FullName)
    return $FullName.Substring($root.Length).TrimStart('\', '/')
}

function Test-ExcludedPath {
    param([string]$FullName)
    foreach ($directory in $ExcludeDirectory) {
        if ($FullName -match ('[\\/]' + [regex]::Escape($directory) + '[\\/]')) { return $true }
    }
    return $false
}

function Add-Option {
    param($Document, [string]$Text, [int]$Line)
    foreach ($match in [regex]::Matches($Text, $optionPattern)) {
        if (-not $Document.Options.ContainsKey($match.Value)) { $Document.Options[$match.Value] = $Line }
    }
}

function Read-MarkdownDocument {
    param([System.IO.FileInfo]$File)

    $document = [pscustomobject]@{
        File           = $File
        Relative       = Get-RelativePath $File.FullName
        Headings       = New-Object System.Collections.Generic.List[object]
        CodeBlockCount = 0
        Options        = @{}
        Prose          = New-Object System.Collections.Generic.List[object]
    }

    $lines = @(Get-Content -LiteralPath $File.FullName -Encoding UTF8)
    $inCode = $false
    $language = ''
    $continuation = $false
    # The tool must be the invoked command (optionally behind sudo, &, a path or quotes), so that
    # "dotnet publish Tool/Tool.csproj --self-contained" is not read as an invocation of Tool.
    $commandPattern = $null
    if ($CommandName) {
        $commandPattern = '^\s*(?:sudo\s+)?(?:&\s*)?["'']?(?:[^\s"'']*[\\/])?' + [regex]::Escape($CommandName) + '(?:\.exe)?["'']?(?=\s|$)'
    }

    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = [string]$lines[$index]
        $number = $index + 1

        if ($line -match '^\s*(```|~~~)\s*([\w+-]*)') {
            if ($inCode) {
                $inCode = $false
            }
            else {
                $inCode = $true
                $language = $Matches[2].ToLowerInvariant()
                $document.CodeBlockCount++
            }
            $continuation = $false
            continue
        }

        if ($inCode) {
            if ($proseLanguages -contains $language) {
                Add-Option $document $line $number
            }
            elseif ($shellLanguages -contains $language) {
                $isCommand = $continuation -or (-not $commandPattern) -or ($line -match $commandPattern)
                if ($isCommand) { Add-Option $document $line $number }
                $continuation = $isCommand -and ($line -match '[`\\^]\s*$')
            }
            continue
        }

        if ($line -match '^(#{1,6})\s+\S') {
            $document.Headings.Add([pscustomobject]@{ Level = $Matches[1].Length; Line = $number; Text = $line.Trim() })
        }

        foreach ($span in [regex]::Matches($line, '`([^`]+)`')) {
            $code = $span.Groups[1].Value
            $isOptionSpan = $code.TrimStart().StartsWith('-')
            $isToolSpan = (-not $commandPattern) -or ($code -match $commandPattern)
            if ($isOptionSpan -or $isToolSpan) { Add-Option $document $code $number }
        }

        $document.Prose.Add([pscustomobject]@{ Line = $number; Text = [regex]::Replace($line, '`[^`]+`', '') })
    }

    return $document
}

function Test-Style {
    param($Document)
    foreach ($entry in $Document.Prose) {
        foreach ($pattern in $forbiddenWords) {
            foreach ($match in [regex]::Matches($entry.Text, $pattern, 'IgnoreCase')) {
                Add-Finding 'ERROR' 'style' $Document.Relative $entry.Line "forbidden word (Rule 6): '$($match.Value)'"
            }
        }
        foreach ($pattern in $marketingWords) {
            foreach ($match in [regex]::Matches($entry.Text, $pattern, 'IgnoreCase')) {
                Add-Finding 'WARNING' 'style' $Document.Relative $entry.Line "marketing vocabulary: '$($match.Value)'"
            }
        }
    }
}

function Test-Link {
    param($Document)
    $directory = $Document.File.DirectoryName
    foreach ($entry in $Document.Prose) {
        $targets = @()
        $targets += [regex]::Matches($entry.Text, '!?\[[^\]]*\]\(\s*<?([^)\s>]+)>?(?:\s+"[^"]*")?\s*\)') | ForEach-Object { $_.Groups[1].Value }
        $targets += [regex]::Matches($entry.Text, '\bsrc\s*=\s*"([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
        foreach ($target in $targets) {
            if ($target -match '^(https?:|mailto:|#|data:)') { continue }
            $localPath = [uri]::UnescapeDataString(($target -split '[#?]')[0])
            if (-not $localPath) { continue }
            if (-not (Test-Path -LiteralPath (Join-Path $directory $localPath))) {
                Add-Finding 'ERROR' 'link' $Document.Relative $entry.Line "relative link target not found: '$target'"
            }
        }
    }
}

function Test-Parity {
    param([object[]]$Group)

    $reference = $Group[0]
    foreach ($other in ($Group | Select-Object -Skip 1)) {
        $referenceLevels = ($reference.Headings | ForEach-Object { $_.Level }) -join ','
        $otherLevels = ($other.Headings | ForEach-Object { $_.Level }) -join ','
        if ($referenceLevels -ne $otherLevels) {
            $count = [Math]::Min($reference.Headings.Count, $other.Headings.Count)
            $position = 0
            while ($position -lt $count -and $reference.Headings[$position].Level -eq $other.Headings[$position].Level) { $position++ }
            if ($position -lt $count) {
                $line = $other.Headings[$position].Line
                $message = "heading structure differs from $($reference.Relative): heading #$($position + 1) is H$($other.Headings[$position].Level) here, H$($reference.Headings[$position].Level) there ('$($reference.Headings[$position].Text)')"
            }
            else {
                $line = 1
                $message = "heading count differs from $($reference.Relative): $($other.Headings.Count) here, $($reference.Headings.Count) there"
            }
            Add-Finding 'ERROR' 'parity' $other.Relative $line $message
        }

        if ($reference.CodeBlockCount -ne $other.CodeBlockCount) {
            Add-Finding 'ERROR' 'parity' $other.Relative 1 "code block count differs from $($reference.Relative): $($other.CodeBlockCount) here, $($reference.CodeBlockCount) there"
        }

        foreach ($option in ($reference.Options.Keys | Sort-Object)) {
            if (-not $other.Options.ContainsKey($option)) {
                Add-Finding 'ERROR' 'parity' $other.Relative 1 "option '$option' cited in $($reference.Relative):$($reference.Options[$option]) but not here"
            }
        }
        foreach ($option in ($other.Options.Keys | Sort-Object)) {
            if (-not $reference.Options.ContainsKey($option)) {
                Add-Finding 'ERROR' 'parity' $other.Relative $other.Options[$option] "option '$option' cited here but not in $($reference.Relative)"
            }
        }
    }
}

function Get-SourceOption {
    $sourceRoot = (Resolve-Path -LiteralPath $SourcePath).Path
    $options = @{}
    $files = Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Include $SourceInclude |
        Where-Object { -not (Test-ExcludedPath $_.FullName) } |
        Where-Object { $IncludeTestSources -or ($_.FullName -notmatch '[\\/][^\\/]*Tests?[^\\/]*[\\/]') }
    foreach ($file in $files) {
        $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
        if (-not $content) { continue }
        foreach ($match in [regex]::Matches($content, '"(' + $optionPattern + ')"')) {
            $options[$match.Groups[1].Value] = $file.FullName
        }
    }
    return $options
}

# Collect documents
$markdownFiles = @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.md' |
    Where-Object { -not (Test-ExcludedPath $_.FullName) -and ($ExcludeFile -notcontains $_.Name) })
if ($markdownFiles.Count -eq 0) {
    Write-Output "No Markdown file found under $root."
    exit 0
}
$documents = @($markdownFiles | ForEach-Object { Read-MarkdownDocument $_ })

foreach ($document in $documents) {
    Test-Style $document
    Test-Link $document
}

# Language groups: name.md, name.fr.md, name.en.md in the same directory
$groups = $documents | Group-Object { Join-Path $_.File.DirectoryName ($_.File.Name -replace '(\.[a-z]{2})?\.md$', '') }
foreach ($group in $groups) {
    if ($group.Count -lt 2) { continue }
    $ordered = @($group.Group | Sort-Object { if ($_.File.Name -match '\.[a-z]{2}\.md$') { 1 } else { 0 } }, { $_.File.Name })
    Test-Parity $ordered
}

if ($SourcePath) {
    $sourceOptions = Get-SourceOption
    $documented = @{}
    foreach ($document in $documents) {
        $isHistorical = @($HistoricalFile | Where-Object { $document.File.Name -like $_ }).Count -gt 0
        if ($isHistorical) { continue }
        foreach ($option in $document.Options.Keys) {
            if ($IgnoreOption -contains $option) { continue }
            if (-not $documented.ContainsKey($option)) { $documented[$option] = @() }
            $documented[$option] += "$($document.Relative):$($document.Options[$option])"
        }
    }
    foreach ($option in ($documented.Keys | Sort-Object)) {
        if (-not $sourceOptions.ContainsKey($option)) {
            $first = $documented[$option][0] -split ':(?=\d+$)'
            Add-Finding 'ERROR' 'option' $first[0] ([int]$first[1]) "option '$option' is documented but not found in the source code (also in: $($documented[$option].Count - 1) other place(s))"
        }
    }
    foreach ($option in ($sourceOptions.Keys | Sort-Object)) {
        if ($IgnoreOption -contains $option) { continue }
        if (-not $documented.ContainsKey($option)) {
            Add-Finding 'WARNING' 'option' (Get-RelativePath $sourceOptions[$option]) 0 "option '$option' is accepted by the code but not documented"
        }
    }
}

$sorted = $findings | Sort-Object @{ Expression = { if ($_.Severity -eq 'ERROR') { 0 } else { 1 } } }, Category, File, Line
foreach ($finding in $sorted) {
    $location = $finding.File
    if ($finding.Line -gt 0) { $location = "$($finding.File):$($finding.Line)" }
    Write-Output ('{0,-7} [{1}] {2} {3}' -f $finding.Severity, $finding.Category, $location, $finding.Message)
}

$errorCount = @($findings | Where-Object Severity -eq 'ERROR').Count
$warningCount = @($findings | Where-Object Severity -eq 'WARNING').Count
Write-Output ''
Write-Output "$($documents.Count) Markdown file(s) checked: $errorCount error(s), $warningCount warning(s)."
if ($errorCount -gt 0) { exit 1 }
exit 0
