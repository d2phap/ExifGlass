#Requires -Version 7.0
# Packs the portable ZIP of ExifGlass.Win32 (plan 3): ExifGlass_<label>_win-<arch>.zip, everything under one folder of the same name.
# Portable by default (D19): the empty .egportable marker next to ExifGlass.exe keeps the settings in that folder; -NoPortable leaves
# them in %LOCALAPPDATA%\ExifGlass. A ZIP can't be signed, so -Sign signs the payload's binaries only.
# Each run publishes a fresh NativeAOT build through scripts/publish-aot.ps1; -SkipPublish reuses the last one when it carries this version.
#
# Usage: pwsh __assets/win/pack-win-zip.ps1 [-Platform x64|arm64] [-Sign] [-NoPortable] [-CertSubject <s> | -CertFile <pfx> -CertPassword <pw>]
[CmdletBinding()]
param(
    [ValidateSet('x64', 'arm64')]
    [string]$Platform = 'x64',

    [switch]$Sign,
    [string]$CertSubject = 'Duong Dieu Phap',
    [string]$CertFile = '',
    [string]$CertPassword = '',
    [string]$TimestampUrl = 'http://timestamp.sectigo.com',

    [switch]$NoPortable,
    [switch]$SkipPublish
)

. (Join-Path $PSScriptRoot '_pack-win.ps1')

$label = Get-ReleaseLabel
$archiveName = "ExifGlass_${label}_win-$Platform"
$stagingDir = Join-Path $StagingRoot "win-$Platform-zip"
$payloadDir = Join-Path $stagingDir $archiveName
$outZip = Join-Path $BundleDir "$archiveName.zip"

$cert = $null
if ($Sign) {
    $cert = Resolve-SigningCert $CertSubject $CertFile $CertPassword
    if (-not $cert) { Write-Warning 'No signing certificate found: the binaries stay UNSIGNED.' }
}

Write-Host "==> ExifGlass $label as a portable ZIP ($Platform)"
Write-Host "    Portable : $(if ($NoPortable) { 'no' } else { "yes ($PortableMarker)" })"
Write-Host "    Output   : $outZip"

try {
    $publishDir = Invoke-FreshPublish -Arch $Platform -Reuse:$SkipPublish
    Copy-Payload $publishDir $payloadDir

    # empty: only its presence counts
    if (-not $NoPortable) { [System.IO.File]::WriteAllBytes((Join-Path $payloadDir $PortableMarker), @()) }

    if ($cert) {
        $binaries = Get-OwnBinaries $payloadDir
        Write-Host "    signing $($binaries.Count) payload binaries"
        if (-not (Invoke-SignTool $cert $binaries $TimestampUrl)) {
            Write-Warning 'Could not sign the payload: the binaries stay UNSIGNED.'
            $cert = $null
        }
    }

    New-Item -ItemType Directory -Path $BundleDir -Force | Out-Null
    if (Test-Path $outZip) { Remove-Item $outZip -Force }

    # ZipFile rather than Compress-Archive: faster on ExifTool's thousands of files, and a wildcard Compress-Archive skips the dot-named marker
    [System.IO.Compression.ZipFile]::CreateFromDirectory($payloadDir, $outZip, [System.IO.Compression.CompressionLevel]::Optimal, $true)
}
finally {
    if (Test-Path $StagingRoot) { Remove-Item $StagingRoot -Recurse -Force -ErrorAction SilentlyContinue }
}

$hash = (Get-FileHash $outZip -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host ''
Write-Host "Done: $outZip ($([math]::Round((Get-Item $outZip).Length / 1MB, 1)) MB)"
Write-Host "  sha256 : $hash"
Write-Host "  Signed : $(if ($cert) { 'yes (payload binaries)' } else { 'no' })"
if (-not $NoPortable) { Write-Host '  Extract it to a folder you can write to: the settings live next to ExifGlass.exe.' }
