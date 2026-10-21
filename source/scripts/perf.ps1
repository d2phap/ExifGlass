# Runs --perf on the published binary, on the GPU path and the software reference (plan 8): startup, idle memory, a file's load, panel motion, 50 repeated loads.
# Usage: scripts/perf.ps1 [-File <path>] [-Arguments '<exiftool arguments>'] [-Rid <rid>]
# Example (about 2,150 rows): scripts/perf.ps1 -File ../__temp/fixtures-private/general/RAW_SONY_ILCE-7M2.ARW -Arguments '-fast -G -t -m -q -H -charset UTF8 -u -a'
param(
    [string]$File = '',
    [string]$Arguments = '',
    [string]$Rid = ''
)

. (Join-Path $PSScriptRoot '_common.ps1')

if (-not $Rid) { $Rid = Get-HostRid }
$info = Get-RidInfo $Rid
$exePath = Join-Path $ArtifactsDir "publish/$Rid/$($info.Exe)"
$report = New-ReportFile "perf-$Rid"

if (-not (Test-Path $exePath)) {
    Write-Report $report "FAILED: $exePath not found; run scripts/publish-aot first"
    exit 1
}

$code = 0
foreach ($render in @('gpu', 'software')) {
    # each run starts from fresh settings, so the window opens at the first-run size (D15) and the user's settings stay untouched
    $settings = Join-Path ([System.IO.Path]::GetTempPath()) ("eg-perf-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $settings | Out-Null
    $env:EG_SETTINGS_DIR = $settings

    $argsList = @('--perf')
    if ($render -eq 'software') { $argsList += @('--render', 'software') }
    if ($Arguments) { $argsList += "`"-p:ExifToolArguments=$Arguments`"" }
    if ($File) { $argsList += "`"$((Resolve-Path $File).Path)`"" }

    Write-Report $report ""
    Write-Report $report "## render $render"

    # A WinExe has no console, so its output comes back through redirected files
    $outFile = [System.IO.Path]::GetTempFileName()
    $errFile = [System.IO.Path]::GetTempFileName()
    $proc = Start-Process -FilePath $exePath -ArgumentList $argsList -NoNewWindow -Wait -PassThru -RedirectStandardOutput $outFile -RedirectStandardError $errFile

    Get-Content $outFile -Encoding utf8 | ForEach-Object { Write-Report $report $_ }
    Get-Content $errFile -Encoding utf8 | Where-Object { $_ } | ForEach-Object { Write-Report $report "stderr: $_" }
    Remove-Item $outFile, $errFile -ErrorAction SilentlyContinue
    Remove-Item $settings -Recurse -Force -ErrorAction SilentlyContinue

    Write-Report $report "Exit code: $($proc.ExitCode)"
    if ($proc.ExitCode -ne 0) { $code = $proc.ExitCode }
}

Remove-Item Env:EG_SETTINGS_DIR -ErrorAction SilentlyContinue
Write-Host "Report: $report"
exit $code
