# Publishes a trimmed NativeAOT build into __artifacts/publish/<rid>; trim and AOT warnings are errors.
# Usage: scripts/publish-aot.ps1 [win-x64|win-arm64|osx-arm64|linux-x64] [-RuntimeAsync off]
param(
    [string]$Rid = '',
    [ValidateSet('on', 'off')][string]$RuntimeAsync = 'on'
)

. (Join-Path $PSScriptRoot '_common.ps1')

if (-not $Rid) { $Rid = Get-HostRid }
$info = Get-RidInfo $Rid
$outDir = Join-Path $ArtifactsDir "publish/$Rid"
$report = New-ReportFile "publish-aot-$Rid"
$asyncFlag = if ($RuntimeAsync -eq 'off') { 'false' } else { 'true' }

# ILC finds the MSVC linker through vswhere, which isn't on PATH by default
if ($IsWindows) {
    $vsInstaller = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
    if ((Test-Path $vsInstaller) -and ($env:PATH -notlike "*$vsInstaller*")) { $env:PATH = "$vsInstaller;$env:PATH" }
}

Write-Report $report "Publishing $($info.Head) for $Rid (Platform=$($info.Platform), RuntimeAsync=$RuntimeAsync)"
Get-Process -Name ExifGlass -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($outDir) } | Stop-Process -Force -Confirm:$false
Remove-Item $outDir -Recurse -Force -ErrorAction SilentlyContinue

$project = Join-Path $SourceDir "$($info.Head)/$($info.Head).csproj"
$watch = [System.Diagnostics.Stopwatch]::StartNew()
$output = & dotnet publish $project -c Release -r $Rid "-p:Platform=$($info.Platform)" "-p:ExifGlassRuntimeAsync=$asyncFlag" -o $outDir 2>&1
$code = $LASTEXITCODE
$output | Where-Object { $_ -match 'error|warning|->' } | ForEach-Object { Write-Report $report "  $($_.ToString().Trim())" }

if ($code -ne 0) {
    Write-Report $report "FAILED: dotnet publish exit code $code"
    exit $code
}

$exePath = Join-Path $outDir $info.Exe
$exeSize = (Get-Item $exePath).Length
$total = (Get-ChildItem $outDir -Recurse -File | Where-Object { $_.Extension -notin '.pdb', '.dbg' } | Measure-Object Length -Sum).Sum
$exifTool = if (Test-Path (Join-Path $outDir 'exiftool.exe')) { 'exiftool.exe' } elseif (Test-Path (Join-Path $outDir 'exiftool')) { 'exiftool' } else { 'MISSING (see source/__assets/exiftool/README.md)' }

Write-Report $report ("Executable: {0:N0} bytes ({1:N1} MB); payload without symbols: {2:N1} MB; ExifTool: {3}; {4:N0} s" -f $exeSize, ($exeSize / 1MB), ($total / 1MB), $exifTool, $watch.Elapsed.TotalSeconds)
Write-Report $report "PASSED: $outDir"
Write-Host "Report: $report"
