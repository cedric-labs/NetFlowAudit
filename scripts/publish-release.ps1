# scripts/publish-release.ps1
[CmdletBinding()]
param (
    [string]$Tag = "",
    [string]$DistDir = "./dist",
    [string]$Repo = "curvecoder/PortAtlas",
    [switch]$Prerelease,
    [string]$NotesFile = "",
    [switch]$BuildOnly
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Tag)) {
    $Tag = (git describe --tags --abbrev=0 2>$null)
    if ([string]::IsNullOrWhiteSpace($Tag)) {
        $Tag = "v1.1.0"
    }
}

if ($Tag -match "-(preview|beta|alpha|rc)") {
    $Prerelease = $true
}

$version = $Tag.TrimStart('v')

# Release notes are written by hand for each version, so that a release never ships the notes of a previous one.
$notesPath = if ([string]::IsNullOrWhiteSpace($NotesFile)) { "./release-notes/$Tag.md" } else { $NotesFile }
if (-not (Test-Path $notesPath)) {
    throw "Release notes not found: $notesPath"
}

Write-Host "=== Preparing PortAtlas Release $Tag (Version: $version) ===" -ForegroundColor Cyan

# 1. Clean and prepare dist directory
if (Test-Path $DistDir) {
    Remove-Item -Path $DistDir -Recurse -Force
}
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

$winPublishDir = "./publish/win-x64"
$linuxPublishDir = "./publish/linux-x64"

# 2. Build Windows x64 self-contained package
Write-Host "`n[1/5] Building Windows x64 self-contained package..." -ForegroundColor Yellow
if (Test-Path $winPublishDir) {
    Remove-Item -Path $winPublishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $winPublishDir -Force | Out-Null

dotnet publish PortAtlas.Cli/PortAtlas.Cli.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $winPublishDir

if ($LASTEXITCODE -ne 0) { throw "Windows publish failed." }

& (Join-Path $PSScriptRoot "test-published-executable.ps1") -ExecutablePath (Join-Path $winPublishDir "PortAtlas.exe")

Get-ChildItem -Path $winPublishDir -Filter "*.pdb" -Recurse | Remove-Item -Force
Copy-Item "README.md" (Join-Path $winPublishDir "README.md") -Force
Copy-Item "README.fr.md" (Join-Path $winPublishDir "README.fr.md") -Force
Copy-Item "LICENSE" (Join-Path $winPublishDir "LICENSE") -Force

$winZip = Join-Path $DistDir "PortAtlas-$version-win-x64.zip"
Write-Host "Creating $winZip ..." -ForegroundColor Yellow
Compress-Archive -Path "$winPublishDir\*" -DestinationPath $winZip -CompressionLevel Optimal

$winStandaloneExe = Join-Path $DistDir "PortAtlas-$version-win-x64.exe"
Copy-Item (Join-Path $winPublishDir "PortAtlas.exe") $winStandaloneExe -Force

# 3. Build Linux x64 self-contained package
Write-Host "`n[2/5] Building Linux x64 self-contained package..." -ForegroundColor Yellow
if (Test-Path $linuxPublishDir) {
    Remove-Item -Path $linuxPublishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $linuxPublishDir -Force | Out-Null

dotnet publish PortAtlas.Cli/PortAtlas.Cli.csproj `
    -c Release `
    -r linux-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $linuxPublishDir

if ($LASTEXITCODE -ne 0) { throw "Linux publish failed." }

Get-ChildItem -Path $linuxPublishDir -Filter "*.pdb" -Recurse | Remove-Item -Force
Copy-Item "README.md" (Join-Path $linuxPublishDir "README.md") -Force
Copy-Item "README.fr.md" (Join-Path $linuxPublishDir "README.fr.md") -Force
Copy-Item "LICENSE" (Join-Path $linuxPublishDir "LICENSE") -Force

$linuxTar = Join-Path $DistDir "PortAtlas-$version-linux-x64.tar.gz"
Write-Host "Creating $linuxTar ..." -ForegroundColor Yellow
tar -czf $linuxTar -C $linuxPublishDir .

$linuxZip = Join-Path $DistDir "PortAtlas-$version-linux-x64.zip"
Write-Host "Creating $linuxZip ..." -ForegroundColor Yellow
Compress-Archive -Path "$linuxPublishDir\*" -DestinationPath $linuxZip -CompressionLevel Optimal

$linuxStandaloneBin = Join-Path $DistDir "PortAtlas-$version-linux-x64"
Copy-Item (Join-Path $linuxPublishDir "PortAtlas") $linuxStandaloneBin -Force

# Graphical interface (Windows x64 only: the project targets the Avalonia desktop head)
Write-Host "`n[2b/5] Building Windows x64 self-contained GUI package..." -ForegroundColor Yellow
$guiPublishDir = "./publish/gui-win-x64"
if (Test-Path $guiPublishDir) {
    Remove-Item -Path $guiPublishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $guiPublishDir -Force | Out-Null

dotnet publish PortAtlas.Gui/PortAtlas.Gui.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $guiPublishDir

if ($LASTEXITCODE -ne 0) { throw "GUI publish failed." }

Get-ChildItem -Path $guiPublishDir -Filter "*.pdb" -Recurse | Remove-Item -Force
Copy-Item "README.md" (Join-Path $guiPublishDir "README.md") -Force
Copy-Item "README.fr.md" (Join-Path $guiPublishDir "README.fr.md") -Force
Copy-Item "LICENSE" (Join-Path $guiPublishDir "LICENSE") -Force

$guiZip = Join-Path $DistDir "PortAtlas.Gui-$version-win-x64.zip"
Write-Host "Creating $guiZip ..." -ForegroundColor Yellow
Compress-Archive -Path "$guiPublishDir\*" -DestinationPath $guiZip -CompressionLevel Optimal

$guiStandaloneExe = Join-Path $DistDir "PortAtlas.Gui-$version-win-x64.exe"
Copy-Item (Join-Path $guiPublishDir "PortAtlas.Gui.exe") $guiStandaloneExe -Force

# 4. Calculate SHA256 checksums
Write-Host "`n[3/5] Calculating SHA256 checksums..." -ForegroundColor Yellow
$checksumFile = Join-Path $DistDir "SHA256SUMS.txt"
$lines = [System.Collections.Generic.List[string]]::new()
$hashes = @{}

foreach ($f in (Get-ChildItem -Path $DistDir -File)) {
    if ($f.Name -eq "SHA256SUMS.txt") { continue }
    $hash = (Get-FileHash -Path $f.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $lines.Add($hash + "  " + $f.Name)
    $hashes[$f.Name] = $hash
}
[System.IO.File]::WriteAllLines($checksumFile, $lines)

Write-Host "Generated artifacts:" -ForegroundColor Green
Get-ChildItem -Path $DistDir | Select-Object Name, Length | Format-Table -AutoSize
Get-Content $checksumFile

if ($BuildOnly) {
    Write-Host "`nBuild only: artifacts are in $DistDir, nothing was uploaded." -ForegroundColor Green
    return
}

# 5. Acquire GitHub Token
Write-Host "`n[4/5] Acquiring GitHub credentials..." -ForegroundColor Yellow
$token = $null
$tempCredFile = [System.IO.Path]::GetTempFileName()
try {
    [System.IO.File]::WriteAllText($tempCredFile, "protocol=https`nhost=github.com`n`n", [System.Text.Encoding]::ASCII)
    $credOutput = cmd.exe /c "type `"$tempCredFile`" | git credential fill"
    $tokenLine = ($credOutput -split "`r?`n") | Where-Object { $_ -match "^password=" }
    if ($tokenLine) {
        $token = ($tokenLine -replace "^password=", "").Trim()
    }
}
finally {
    if (Test-Path $tempCredFile) { Remove-Item $tempCredFile -Force }
}

if ([string]::IsNullOrWhiteSpace($token)) {
    throw "Unable to acquire GitHub Token from git credential helper."
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Accept" = "application/vnd.github.v3+json"
    "User-Agent" = "PortAtlas-Release-Script"
}

# 6. Create or Update GitHub Release
Write-Host "`n[5/5] Creating GitHub Release $Tag on $Repo..." -ForegroundColor Yellow

$checksumsContent = [System.IO.File]::ReadAllText($checksumFile)

$hWinZip = $hashes["PortAtlas-" + $version + "-win-x64.zip"]
$hWinExe = $hashes["PortAtlas-" + $version + "-win-x64.exe"]
$hLinTar = $hashes["PortAtlas-" + $version + "-linux-x64.tar.gz"]
$hLinZip = $hashes["PortAtlas-" + $version + "-linux-x64.zip"]
$hLinBin = $hashes["PortAtlas-" + $version + "-linux-x64"]
$hGuiZip = $hashes["PortAtlas.Gui-" + $version + "-win-x64.zip"]
$hGuiExe = $hashes["PortAtlas.Gui-" + $version + "-win-x64.exe"]

$bodyList = [System.Collections.Generic.List[string]]::new()
$bodyList.Add('# PortAtlas ' + $Tag)
$bodyList.Add('')
$bodyList.Add('PortAtlas is a cross-platform (Windows & Linux) network flow auditing agent designed to run interactively in the terminal or continuously as a background daemon/service.')
$bodyList.Add('')
$bodyList.Add([System.IO.File]::ReadAllText($notesPath).Trim())
$bodyList.Add('')
$bodyList.Add('## Downloads')
$bodyList.Add('| OS / Architecture | Package | Checksum (SHA256) |')
$bodyList.Add('|---|---|---|')
$bodyList.Add('| **Windows x64** | [PortAtlas-' + $version + '-win-x64.zip](https://github.com/' + $Repo + '/releases/download/' + $Tag + '/PortAtlas-' + $version + '-win-x64.zip) | `' + $hWinZip + '` |')
$bodyList.Add('| **Windows x64 (Standalone Exe)** | [PortAtlas-' + $version + '-win-x64.exe](https://github.com/' + $Repo + '/releases/download/' + $Tag + '/PortAtlas-' + $version + '-win-x64.exe) | `' + $hWinExe + '` |')
$bodyList.Add('| **Linux x64 (tar.gz)** | [PortAtlas-' + $version + '-linux-x64.tar.gz](https://github.com/' + $Repo + '/releases/download/' + $Tag + '/PortAtlas-' + $version + '-linux-x64.tar.gz) | `' + $hLinTar + '` |')
$bodyList.Add('| **Linux x64 (zip)** | [PortAtlas-' + $version + '-linux-x64.zip](https://github.com/' + $Repo + '/releases/download/' + $Tag + '/PortAtlas-' + $version + '-linux-x64.zip) | `' + $hLinZip + '` |')
$bodyList.Add('| **Linux x64 (Standalone Binary)** | [PortAtlas-' + $version + '-linux-x64](https://github.com/' + $Repo + '/releases/download/' + $Tag + '/PortAtlas-' + $version + '-linux-x64) | `' + $hLinBin + '` |')
$bodyList.Add('| **Windows x64 GUI** | [PortAtlas.Gui-' + $version + '-win-x64.zip](https://github.com/' + $Repo + '/releases/download/' + $Tag + '/PortAtlas.Gui-' + $version + '-win-x64.zip) | `' + $hGuiZip + '` |')
$bodyList.Add('| **Windows x64 GUI (Standalone Exe)** | [PortAtlas.Gui-' + $version + '-win-x64.exe](https://github.com/' + $Repo + '/releases/download/' + $Tag + '/PortAtlas.Gui-' + $version + '-win-x64.exe) | `' + $hGuiExe + '` |')
$bodyList.Add('')
$bodyList.Add('### SHA256 Integrity Checksums')
$bodyList.Add('```text')
$bodyList.Add($checksumsContent.Trim())
$bodyList.Add('```')

$releaseBody = [string]::Join([Environment]::NewLine, $bodyList)

$release = $null
try {
    $release = Invoke-RestMethod -Uri ('https://api.github.com/repos/' + $Repo + '/releases/tags/' + $Tag) -Headers $headers
    Write-Host ("Found existing release " + $release.id + ". Updating...") -ForegroundColor Cyan
    $updatePayload = @{
        name = "PortAtlas $Tag"
        body = $releaseBody
    } | ConvertTo-Json
    $updateBytes = [System.Text.Encoding]::UTF8.GetBytes($updatePayload)
    $release = Invoke-RestMethod -Uri ('https://api.github.com/repos/' + $Repo + '/releases/' + $release.id) -Method Patch -Headers $headers -Body $updateBytes -ContentType "application/json; charset=utf-8"
}
catch {
    Write-Host ("Creating new release for " + $Tag + "...") -ForegroundColor Cyan
    $createPayload = @{
        tag_name = $Tag
        name = "PortAtlas $Tag"
        body = $releaseBody
        draft = $false
        prerelease = [bool]$Prerelease
    } | ConvertTo-Json
    $createBytes = [System.Text.Encoding]::UTF8.GetBytes($createPayload)
    $release = Invoke-RestMethod -Uri ('https://api.github.com/repos/' + $Repo + '/releases') -Method Post -Headers $headers -Body $createBytes -ContentType "application/json; charset=utf-8"
}

Write-Host ("Release created/updated with ID: " + $release.id) -ForegroundColor Green

# 7. Upload Assets to GitHub Release
$uploadUrl = $release.upload_url -replace '\{\?name,label\}', ''

# Delete existing assets if any
if ($release.assets) {
    foreach ($asset in $release.assets) {
        Write-Host ("Deleting existing asset: " + $asset.name + "...") -ForegroundColor DarkGray
        Invoke-RestMethod -Uri $asset.url -Method Delete -Headers $headers | Out-Null
    }
}

foreach ($assetFile in (Get-ChildItem -Path $DistDir -File)) {
    $sizeMb = [System.Math]::Round($assetFile.Length / 1MB, 2)
    Write-Host ("Uploading asset: " + $assetFile.Name + " (" + $sizeMb + " MB)...") -ForegroundColor Yellow
    
    $uploadUri = $uploadUrl + "?name=" + $assetFile.Name
    $fileBytes = [System.IO.File]::ReadAllBytes($assetFile.FullName)
    
    $uploadHeaders = @{
        "Authorization" = "Bearer $token"
        "Content-Type" = "application/octet-stream"
        "User-Agent" = "PortAtlas-Release-Script"
    }
    
    Invoke-RestMethod -Uri $uploadUri -Method Post -Headers $uploadHeaders -Body $fileBytes | Out-Null
    Write-Host ("Uploaded: " + $assetFile.Name) -ForegroundColor Green
}

Write-Host ("`n=== Release " + $Tag + " published successfully on GitHub! ===") -ForegroundColor Green
Write-Host ("Release URL: " + $release.html_url) -ForegroundColor Cyan
