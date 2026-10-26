# Plays ImageGlass 10 and ImageGlass 9 against the published binary (P10): live navigation, SHUTDOWN, the app or the host killed, Unicode, no ExifTool left behind.
# Usage: scripts/ipc.ps1 [-Command all|ig10|ig9] [-Scenario all|<name>] [-Runs 3] [-Rid <rid>] [-Exe <path>]
# Each session opens an ExifGlass window, which ImageGlass's EXECUTE brings to the front.
param(
    [string]$Command = 'all',
    [string]$Scenario = 'all',
    [int]$Runs = 3,
    [string]$Rid = '',
    [string]$Exe = ''
)

. (Join-Path $PSScriptRoot '_common.ps1')

if (-not $Rid) { $Rid = Get-HostRid }
$info = Get-RidInfo $Rid
if (-not $Exe) { $Exe = Join-Path $ArtifactsDir "publish/$Rid/$($info.Exe)" }
$report = New-ReportFile "ipc-$Rid"

if (-not (Test-Path $Exe)) {
    Write-Report $report "FAILED: $Exe not found; run scripts/publish-aot first"
    exit 1
}

$project = Join-Path $SourceDir 'tests/ExifGlass.IpcHost/ExifGlass.IpcHost.csproj'
dotnet build $project -c Release -v q -nologo | Out-Host
if ($LASTEXITCODE -ne 0) {
    Write-Report $report "FAILED: the IPC host didn't build"
    exit 1
}

$hostExe = Get-ChildItem (Join-Path $SourceDir 'tests/ExifGlass.IpcHost/bin/Release') -Recurse -Filter 'ExifGlass.IpcHost.exe' | Select-Object -First 1
$out = Join-Path $ArtifactsDir ("ipc/" + (Get-HostOsName))
Write-Report $report "app: $((Resolve-Path $Exe).Path)"

& $hostExe.FullName $Command $Scenario --app (Resolve-Path $Exe).Path --out $out --runs $Runs | ForEach-Object { Write-Report $report $_ }
$code = $LASTEXITCODE

Write-Report $report "Tables: $out"
Write-Host "Report: $report"
exit $code
