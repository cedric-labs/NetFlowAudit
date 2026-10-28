# scripts/test-published-executable.ps1
# v2.0.0 to v2.0.2 shipped an executable that crashed at launch on a framework assembly version mismatch that no test
# caught. The audit command loads every framework as soon as it starts, so the published executable must still be
# scanning after a few seconds; it is then stopped, since an audit never ends on its own.
[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,
    [int]$StartupSeconds = 8
)

$ErrorActionPreference = 'Stop'

Write-Host "Smoke-testing $ExecutablePath ..." -ForegroundColor Yellow
$executable = (Resolve-Path $ExecutablePath).Path
$smokeDir = Join-Path ([System.IO.Path]::GetTempPath()) ("netflowaudit-smoke-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $smokeDir -Force | Out-Null
try {
    $smokeArgs = @('audit', '--no-tui', '--no-dns', '--no-rdap', '--interval', '1', '--output', (Join-Path $smokeDir 'smoke.csv'))
    $stderrPath = Join-Path $smokeDir 'stderr.txt'
    $process = Start-Process -FilePath $executable -ArgumentList $smokeArgs -WorkingDirectory $smokeDir -PassThru -NoNewWindow `
        -RedirectStandardOutput (Join-Path $smokeDir 'stdout.txt') -RedirectStandardError $stderrPath
    # Reading the handle keeps it open: without it, ExitCode stays empty once the process has exited.
    $null = $process.Handle
    if ($process.WaitForExit($StartupSeconds * 1000)) {
        Get-Content $stderrPath | Write-Host -ForegroundColor Red
        throw "Smoke test failed: $executable exited with code $($process.ExitCode) instead of auditing."
    }

    $process.Kill()
    $process.WaitForExit()
    Write-Host "Smoke test passed: the audit was still running after $StartupSeconds seconds." -ForegroundColor Green
}
finally {
    Remove-Item -Path $smokeDir -Recurse -Force
}
