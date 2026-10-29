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
Write-Host "  Pipeline Qualité & SonarQube - PortAtlas" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$SolutionPath = Join-Path $ScriptRoot "PortAtlas.slnx"

# Nettoyage des résultats de tests et d'analyses précédents, avant toute étape, pour que les journaux de cette
# exécution survivent jusqu'à la fin.
Remove-Item -Path "$ScriptRoot/TestResults" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$ScriptRoot/.sonarqube" -Recurse -Force -ErrorAction SilentlyContinue

# Les sorties complètes de dotnet et de SonarScanner font plusieurs milliers de lignes : elles saturent la console
# et le contexte des agents qui lancent ce script. Elles sont écrites dans TestResults/logs et seules les lignes
# utiles (erreurs, résumé des tests, statut de la Quality Gate) sont affichées.
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
    # Sous Windows PowerShell 5.1, la redirection de stderr d'un exécutable transforme chaque ligne en ErrorRecord,
    # qui interromprait le script avec la préférence Stop ; le code de sortie reste la seule source d'échec.
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
    Write-Host "  Journal complet : $logPath" -ForegroundColor DarkGray
    return $exitCode
}

# Détection automatique du token SonarQube
if (-not $SonarToken) {
    if ($env:SONAR_TOKEN) {
        $SonarToken = $env:SONAR_TOKEN
    } elseif (Test-Path "$ScriptRoot/.sonar-token") {
        $SonarToken = (Get-Content "$ScriptRoot/.sonar-token" -Raw).Trim()
    } elseif (Test-Path "$ScriptRoot/../.sonar-token") {
        $SonarToken = (Get-Content "$ScriptRoot/../.sonar-token" -Raw).Trim()
    }
}

# 1. Restauration des outils dotnet locaux
Write-Host "`n[1/6] Restauration des outils locaux..." -ForegroundColor Yellow
$exitCode = Invoke-LoggedStep -Name "1-tool-restore" -FailurePattern "error" -Command { dotnet tool restore }
if ($exitCode -ne 0) {
    Write-Error "Échec de 'dotnet tool restore'."
    exit $exitCode
}

# 2. Vérification du formatage du code
if (-not $SkipFormat) {
    Write-Host "`n[2/6] Vérification du formatage du code (dotnet format)..." -ForegroundColor Yellow
    $exitCode = Invoke-LoggedStep -Name "2-format" -FailurePattern "error" -Command { dotnet format $SolutionPath --verify-no-changes }
    if ($exitCode -ne 0) {
        Write-Error "Échec du formatage. Exécutez 'dotnet format' pour corriger les écarts."
        exit $exitCode
    }
    Write-Host "  Formatage conforme." -ForegroundColor Green
} else {
    Write-Host "`n[2/6] Vérification du formatage ignorée." -ForegroundColor DarkGray
}

# Contrôle de la documentation : chaque option citée doit exister dans le code, les versions anglaise et française
# doivent avoir la même structure, les liens relatifs doivent aboutir et la Règle 6 est appliquée. Les guides de
# migration, les ADR et les notes de version citent volontairement des options supprimées, d'où -HistoricalFile.
Write-Host "`n[2/6] Contrôle de la documentation (scripts/Test-Documentation.ps1)..." -ForegroundColor Yellow
& "$ScriptRoot/scripts/Test-Documentation.ps1" -Path $ScriptRoot -SourcePath $ScriptRoot -CommandName "PortAtlas" `
    -HistoricalFile "upgrade-to-*.md", "[0-9][0-9][0-9][0-9]-*.md", "v[0-9]*.md" -IgnoreOption "--help", "--version"
if ($LASTEXITCODE -ne 0) {
    Write-Error "La documentation ne correspond pas au code. Corrigez les erreurs listées ci-dessus."
    exit $LASTEXITCODE
}
Write-Host "  Documentation conforme." -ForegroundColor Green

# 3. SonarScanner Begin
$useSonar = (-not $SkipSonar) -and (-not [string]::IsNullOrWhiteSpace($SonarToken))
if ($useSonar) {
    Write-Host "`n[3/6] Initialisation SonarScanner sur $SonarHost..." -ForegroundColor Yellow
    $sonarArgs = @(
        "begin",
        "/k:$ProjectKey",
        "/n:$ProjectKey",
        "/d:sonar.host.url=$SonarHost",
        "/d:sonar.token=$SonarToken",
        "/d:sonar.qualitygate.wait=true",
        "/d:sonar.cs.opencover.reportsPaths=**/TestResults/**/coverage.opencover.xml",
        "/d:sonar.cs.vstest.reportsPaths=**/TestResults/**/*.trx",
        "/d:sonar.exclusions=**/bin/**,**/obj/**,verify.ps1,**/PortAtlas.Cli/**",
        "/d:sonar.coverage.exclusions=verify.ps1",
        "/d:sonar.cpd.exclusions=verify.ps1",
        "/d:sonar.cs.roslyn.ignoreIssues=true"
    )
    $exitCode = Invoke-LoggedStep -Name "3-sonar-begin" -FailurePattern "ERROR" -Command { dotnet sonarscanner @sonarArgs }
    if ($exitCode -ne 0) {
        Write-Error "Échec de l'initialisation SonarScanner."
        exit $exitCode
    }
} else {
    Write-Host "`n[3/6] SonarScanner ignoré (aucun token fourni ou -SkipSonar activé)." -ForegroundColor DarkGray
}

# 4. Compilation Release avec zéro avertissement
Write-Host "`n[4/6] Compilation Release sans incrémentation (-warnaserror)..." -ForegroundColor Yellow
$exitCode = Invoke-LoggedStep -Name "4-build" -FailurePattern ": (error|warning) " -Command {
    dotnet build $SolutionPath -warnaserror --configuration Release --no-incremental -nologo -clp:ErrorsOnly
}
if ($exitCode -ne 0) {
    Write-Error "Échec de la compilation ou avertissements détectés."
    exit $exitCode
}
Write-Host "  Compilation réussie sans aucun avertissement." -ForegroundColor Green

# 5. Exécution des tests et collecte OpenCover
Write-Host "`n[5/6] Exécution des tests unitaires et de couverture OpenCover..." -ForegroundColor Yellow
$exitCode = Invoke-LoggedStep -Name "5-test" -FailurePattern "^\s*(Failed|Échoué) |: error " -FailureContext 12 `
    -SuccessPattern "^(Passed|Failed|Réussi|Échec)!" -Command {
        dotnet test $SolutionPath --no-build --configuration Release --logger "trx" --collect:"XPlat Code Coverage;Format=opencover" --results-directory "$ScriptRoot/TestResults"
    }
if ($exitCode -ne 0) {
    Write-Error "Des tests unitaires ou d'architecture ont échoué."
    exit $exitCode
}
Write-Host "  Tests passés avec succès." -ForegroundColor Green

# 6. SonarScanner End
if ($useSonar) {
    Write-Host "`n[6/6] Finalisation de l'analyse SonarScanner..." -ForegroundColor Yellow
    $exitCode = Invoke-LoggedStep -Name "6-sonar-end" -FailurePattern "ERROR|QUALITY GATE STATUS" `
        -SuccessPattern "QUALITY GATE STATUS:" -Command { dotnet sonarscanner end /d:sonar.token=$SonarToken }
    if ($exitCode -ne 0) {
        Write-Error "Échec de la finalisation SonarScanner ou Quality Gate non passée."
        exit $exitCode
    }
    Write-Host "  Quality Gate validée sur SonarQube." -ForegroundColor Green
} else {
    Write-Host "`n[6/6] SonarScanner End ignoré." -ForegroundColor DarkGray
}

Write-Host "`n==========================================" -ForegroundColor Green
Write-Host "  Pipeline validé avec succès !" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
