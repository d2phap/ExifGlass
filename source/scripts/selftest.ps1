# Runs --selftest on the published binary in __artifacts/publish/<rid>; a non-zero exit fails the script.
# Usage: scripts/selftest.ps1 [<rid>]
param(
    [string]$Rid = ''
)

. (Join-Path $PSScriptRoot '_common.ps1')

if (-not $Rid) { $Rid = Get-HostRid }
$info = Get-RidInfo $Rid
$exePath = Join-Path $ArtifactsDir "publish/$Rid/$($info.Exe)"
$report = New-ReportFile "selftest-$Rid"

if (-not (Test-Path $exePath)) {
    Write-Report $report "FAILED: $exePath not found; run scripts/publish-aot first"
    exit 1
}

# A WinExe has no console, so its output comes back through redirected files
$outFile = [System.IO.Path]::GetTempFileName()
$errFile = [System.IO.Path]::GetTempFileName()
$proc = Start-Process -FilePath $exePath -ArgumentList '--selftest' -NoNewWindow -Wait -PassThru -RedirectStandardOutput $outFile -RedirectStandardError $errFile

Get-Content $outFile -Encoding utf8 | ForEach-Object { Write-Report $report $_ }
Get-Content $errFile -Encoding utf8 | Where-Object { $_ } | ForEach-Object { Write-Report $report "stderr: $_" }
Remove-Item $outFile, $errFile -ErrorAction SilentlyContinue

Write-Report $report "Exit code: $($proc.ExitCode)"
Write-Host "Report: $report"
exit $proc.ExitCode
