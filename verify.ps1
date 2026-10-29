[CmdletBinding()]
param(
    [string]$ProjectKey = "PortAtlas",
    [string]$SonarHost = $(if ($env:SONAR_HOST_URL) { $env:SONAR_HOST_URL } else { "http://localhost:9000" }),
    [string]$SonarToken,
    [switch]$SkipFormat,
    [switch]$SkipSonar
)

$ErrorActionPreference = "Stop"
$ScriptRoot = $PSScriptRoot
Set-Location $ScriptRoot

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Quality & SonarQube pipeline - PortAtlas" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$SolutionPath = Join-Path $ScriptRoot "PortAtlas.slnx"

# Clears previous test and analysis results before any step, so that the logs of this run
# survive until the end.
Remove-Item -Path "$ScriptRoot/TestResults" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$ScriptRoot/.sonarqube" -Recurse -Force -ErrorAction SilentlyContinue

# The full dotnet and SonarScanner output runs to several thousand lines: it floods the console
# and the context of the agents that run this script. It is written to TestResults/logs and only the useful
# lines (errors, test summary, Quality Gate status) are shown.
$LogDirectory = Join-Path $ScriptRoot "TestResults/logs"
New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null

function Invoke-LoggedStep {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Command,
        [Parameter(Mandatory)][string]$FailurePattern,
        [int]$FailureContext = 0,
        [string]$SuccessPattern
    )

    $logPath = Join-Path $LogDirectory "$Name.log"
    # Under Windows PowerShell 5.1, redirecting the stderr of an executable turns each line into an ErrorRecord,
    # which would stop the script under the Stop preference; the exit code stays the only source of failure.
    $ErrorActionPreference = "Continue"
    & $Command 2>&1 | ForEach-Object { "$_" } | Set-Content -Path $logPath -Encoding utf8
    $exitCode = $LASTEXITCODE

    if ($exitCode -eq 0) {
        if ($SuccessPattern) {
            Select-String -Path $logPath -Pattern $SuccessPattern | ForEach-Object { Write-Host "  $($_.Line.Trim())" }
        }
        return $exitCode
    }

    $failureLines = @(Select-String -Path $logPath -Pattern $FailurePattern -Context 0, $FailureContext | Select-Object -First 30)
    if ($failureLines.Count -gt 0) {
        $failureLines | ForEach-Object {
            Write-Host "  $($_.Line.Trim())" -ForegroundColor Red
            $_.Context.PostContext | Where-Object { $_.Trim() } | ForEach-Object { Write-Host "    $_" }
        }
    } else {
        Get-Content -Path $logPath -Tail 40 | ForEach-Object { Write-Host "  $_" }
    }
    Write-Host "  Full log: $logPath" -ForegroundColor DarkGray
    return $exitCode
}

# SonarQube token lookup
if (-not $SonarToken) {
    if ($env:SONAR_TOKEN) {
        $SonarToken = $env:SONAR_TOKEN
    } elseif (Test-Path "$ScriptRoot/.sonar-token") {
        $SonarToken = (Get-Content "$ScriptRoot/.sonar-token" -Raw).Trim()
    } elseif (Test-Path "$ScriptRoot/../.sonar-token") {
        $SonarToken = (Get-Content "$ScriptRoot/../.sonar-token" -Raw).Trim()
    }
}

# 1. Restore the local dotnet tools
Write-Host "`n[1/6] Restoring local tools..." -ForegroundColor Yellow
$exitCode = Invoke-LoggedStep -Name "1-tool-restore" -FailurePattern "error" -Command { dotnet tool restore }
if ($exitCode -ne 0) {
    Write-Error "'dotnet tool restore' failed."
    exit $exitCode
}

# 2. Code formatting check
if (-not $SkipFormat) {
    Write-Host "`n[2/6] Checking code formatting (dotnet format)..." -ForegroundColor Yellow
    $exitCode = Invoke-LoggedStep -Name "2-format" -FailurePattern "error" -Command { dotnet format $SolutionPath --verify-no-changes }
    if ($exitCode -ne 0) {
        Write-Error "Formatting check failed. Run 'dotnet format' to fix the differences."
        exit $exitCode
    }
    Write-Host "  Formatting compliant." -ForegroundColor Green
} else {
    Write-Host "`n[2/6] Formatting check skipped." -ForegroundColor DarkGray
}

# Documentation check: every cited option must exist in the code, the English and French versions
# must have the same structure, relative links must resolve and Rule 6 is enforced. Upgrade guides,
# ADRs and release notes cite removed options on purpose, hence -HistoricalFile.
Write-Host "`n[2/6] Checking the documentation (scripts/Test-Documentation.ps1)..." -ForegroundColor Yellow
& "$ScriptRoot/scripts/Test-Documentation.ps1" -Path $ScriptRoot -SourcePath $ScriptRoot -CommandName "PortAtlas" `
    -HistoricalFile "upgrade-to-*.md", "[0-9][0-9][0-9][0-9]-*.md", "v[0-9]*.md" -IgnoreOption "--help", "--version"
if ($LASTEXITCODE -ne 0) {
    Write-Error "The documentation does not match the code. Fix the errors listed above."
    exit $LASTEXITCODE
}
Write-Host "  Documentation compliant." -ForegroundColor Green

# 3. SonarScanner Begin
$useSonar = (-not $SkipSonar) -and (-not [string]::IsNullOrWhiteSpace($SonarToken))
if ($useSonar) {
    Write-Host "`n[3/6] Starting SonarScanner on $SonarHost..." -ForegroundColor Yellow
    $sonarArgs = @(
        "begin",
        "/k:$ProjectKey",
        "/n:$ProjectKey",
        "/d:sonar.host.url=$SonarHost",
        "/d:sonar.token=$SonarToken",
        "/d:sonar.qualitygate.wait=true",
        "/d:sonar.cs.opencover.reportsPaths=**/TestResults/**/coverage.opencover.xml",
        "/d:sonar.cs.vstest.reportsPaths=**/TestResults/**/*.trx",
        "/d:sonar.exclusions=**/bin/**,**/obj/**,verify.ps1",
        "/d:sonar.coverage.exclusions=verify.ps1",
        "/d:sonar.cpd.exclusions=verify.ps1",
        "/d:sonar.cs.roslyn.ignoreIssues=true"
    )
    $exitCode = Invoke-LoggedStep -Name "3-sonar-begin" -FailurePattern "ERROR" -Command { dotnet sonarscanner @sonarArgs }
    if ($exitCode -ne 0) {
        Write-Error "SonarScanner initialisation failed."
        exit $exitCode
    }
} else {
    Write-Host "`n[3/6] SonarScanner skipped (no token provided or -SkipSonar set)." -ForegroundColor DarkGray
}

# 4. Release build with zero warnings
Write-Host "`n[4/6] Non-incremental Release build (-warnaserror)..." -ForegroundColor Yellow
$exitCode = Invoke-LoggedStep -Name "4-build" -FailurePattern ": (error|warning) " -Command {
    dotnet build $SolutionPath -warnaserror --configuration Release --no-incremental -nologo -clp:ErrorsOnly
}
if ($exitCode -ne 0) {
    Write-Error "Build failed or warnings detected."
    exit $exitCode
}
Write-Host "  Build succeeded with no warnings." -ForegroundColor Green

# 5. Tests and OpenCover collection
Write-Host "`n[5/6] Running unit tests with OpenCover coverage..." -ForegroundColor Yellow
$exitCode = Invoke-LoggedStep -Name "5-test" -FailurePattern "^\s*(Failed|Échoué) |: error " -FailureContext 12 `
    -SuccessPattern "^(Passed|Failed|Réussi|Échec)!" -Command {
        dotnet test $SolutionPath --no-build --configuration Release --logger "trx" --collect:"XPlat Code Coverage;Format=opencover" --results-directory "$ScriptRoot/TestResults"
    }
if ($exitCode -ne 0) {
    Write-Error "Unit or architecture tests failed."
    exit $exitCode
}
Write-Host "  Tests passed." -ForegroundColor Green

# 6. SonarScanner End
if ($useSonar) {
    Write-Host "`n[6/6] Finalising the SonarScanner analysis..." -ForegroundColor Yellow
    $exitCode = Invoke-LoggedStep -Name "6-sonar-end" -FailurePattern "ERROR|QUALITY GATE STATUS" `
        -SuccessPattern "QUALITY GATE STATUS:" -Command { dotnet sonarscanner end /d:sonar.token=$SonarToken }
    if ($exitCode -ne 0) {
        Write-Error "SonarScanner finalisation failed or Quality Gate not passed."
        exit $exitCode
    }
    Write-Host "  Quality Gate passed on SonarQube." -ForegroundColor Green
} else {
    Write-Host "`n[6/6] SonarScanner End skipped." -ForegroundColor DarkGray
}

Write-Host "`n==========================================" -ForegroundColor Green
Write-Host "  Pipeline passed!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
