# Shared helpers for the ExifGlass local scripts (dot-sourced).
$ErrorActionPreference = 'Stop'

$script:SourceDir = Split-Path -Parent $PSScriptRoot
$script:ArtifactsDir = Join-Path $SourceDir '__artifacts'

function Get-HostRid {
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
    if ($IsWindows) { return "win-$arch" }
    if ($IsMacOS) { return "osx-$arch" }
    return "linux-$arch"
}

function Get-HostOsName {
    if ($IsWindows) { return 'win' }
    if ($IsMacOS) { return 'mac' }
    return 'linux'
}

# Maps a RID to its head project, MSBuild platform and executable name.
function Get-RidInfo([string]$Rid) {
    $platform = if ($Rid.EndsWith('-arm64')) { 'ARM64' } else { 'x64' }
    $head = switch -Wildcard ($Rid) {
        'win-*' { 'ExifGlass.Win32' }
        'osx-*' { 'ExifGlass.Mac' }
        'linux-*' { 'ExifGlass.Linux' }
        default { throw "Unsupported RID: $Rid" }
    }
    $exe = if ($Rid.StartsWith('win-')) { 'ExifGlass.exe' } else { 'ExifGlass' }
    return [pscustomobject]@{ Rid = $Rid; Platform = $platform; Head = $head; Exe = $exe }
}

# Returns this run's report file: __artifacts/reports/<date>/<name>-<os>.txt
function New-ReportFile([string]$Name) {
    $dir = Join-Path $ArtifactsDir ("reports/" + (Get-Date -Format 'yyyy-MM-dd'))
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $file = Join-Path $dir "$Name-$(Get-HostOsName).txt"
    "# $Name · $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') · $([System.Runtime.InteropServices.RuntimeInformation]::OSDescription) · $(Get-HostRid)" | Set-Content -Path $file -Encoding utf8
    return $file
}

# Prints a line and appends it to the report.
function Write-Report([string]$ReportFile, [string]$Line) {
    Write-Host $Line
    Add-Content -Path $ReportFile -Value $Line -Encoding utf8
}
