#Requires -Version 7.0
# Packs ExifGlass.Win32 as an MSIX (plan 3), in two flavours:
#   sideload (default)  ExifGlass_<label>_win-<arch>.msix: identity DuongDieuPhap.ExifGlass; with -Sign the payload and the package are
#                       Authenticode-signed and the Publisher is the certificate's exact Subject (no certificate: built unsigned, with a warning)
#   msstore (-MsStore)  ExifGlass_<label>_win-msstore.msixbundle: x64 + arm64, the Partner Center identity, never signed (the Store signs it)
# Each run publishes a fresh NativeAOT build through scripts/publish-aot.ps1; -SkipPublish reuses the last one when it carries this version.
#
# Usage: pwsh __assets/win/pack-win-msix.ps1 [-Platform x64|arm64] [-Sign] [-MsStore] [-CertSubject <s> | -CertFile <pfx> -CertPassword <pw>]
[CmdletBinding()]
param(
    [ValidateSet('x64', 'arm64')]
    [string]$Platform = 'x64',

    [switch]$MsStore,
    [switch]$Sign,

    [string]$CertSubject = 'Duong Dieu Phap',
    [string]$CertFile = '',
    [string]$CertPassword = '',
    [string]$TimestampUrl = 'http://timestamp.sectigo.com',

    [string]$PackageVersion = '',

    [string]$IdentityName = 'DuongDieuPhap.ExifGlass',
    [string]$PublisherDisplayName = 'Duong Dieu Phap',

    # Partner Center's reservation; ChannelPolicy.Detect reads the Store build from this name's prefix
    [string]$MsStoreIdentityName = '9662DuongDieuPhap.ExifGlass',
    [string]$MsStorePublisher = 'CN=29F1B9EC-D220-4DC3-BEDB-01A9CCA51904',

    [switch]$SkipPublish
)

. (Join-Path $PSScriptRoot '_pack-win.ps1')

$ManifestTpl = Join-Path $PSScriptRoot 'appxmanifest\AppxManifest.xml'
$AssetsDir   = Join-Path $PSScriptRoot 'appxmanifest\Assets'


# publish -> stage -> manifest -> sign our binaries -> resources.pri -> pack; the caller signs the package itself
function New-MsixPackage([string]$Arch, [string]$OutMsixPath) {
    Write-Host ''
    Write-Host "==> [$Arch] MSIX"
    $publishDir = Invoke-FreshPublish -Arch $Arch -Reuse:$SkipPublish
    $stagingDir = Join-Path $StagingRoot "win-$Arch-msix"
    $payloadDir = Join-Path $stagingDir 'ExifGlass'
    Copy-Payload $publishDir $payloadDir

    # the package folder is read-only: a portable marker there would keep the settings for one run only
    if (Test-Path (Join-Path $payloadDir $PortableMarker)) { throw "$PortableMarker must never ship in an MSIX ($publishDir)" }

    Copy-Item -Path $AssetsDir -Destination (Join-Path $stagingDir 'Assets') -Recurse -Force

    # UTF-8 with BOM, as the SDK tools want it
    $manifest = (Get-Content -Path $ManifestTpl -Raw).
        Replace('{{IDENTITY_NAME}}', $script:identityName).
        Replace('{{PUBLISHER}}', $script:publisher).
        Replace('{{PUBLISHER_DISPLAY_NAME}}', $PublisherDisplayName).
        Replace('{{VERSION}}', $script:pkgVersion).
        Replace('{{ARCH}}', $Arch)
    $manifestPath = Join-Path $stagingDir 'AppxManifest.xml'
    [System.IO.File]::WriteAllText($manifestPath, $manifest, [System.Text.UTF8Encoding]::new($true))

    if ($script:cert) {
        $binaries = Get-OwnBinaries $payloadDir
        Write-Host "    signing $($binaries.Count) payload binaries"
        if (-not (Invoke-SignTool $script:cert $binaries $TimestampUrl)) {
            Write-Warning 'Could not sign the payload: the package is left UNSIGNED.'
            $script:cert = $null
        }
    }

    # resources.pri resolves the manifest's plain logo names to the scale and target-size files
    $priConfig = Join-Path $StagingRoot "win-$Arch-msix.priconfig.xml"
    & $script:makepri createconfig /cf $priConfig /dq en-US /o | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "makepri createconfig failed (exit $LASTEXITCODE)" }
    & $script:makepri new /pr $stagingDir /cf $priConfig /mn $manifestPath /of (Join-Path $stagingDir 'resources.pri') /o | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "makepri new failed (exit $LASTEXITCODE)" }

    New-Item -ItemType Directory -Path (Split-Path $OutMsixPath) -Force | Out-Null
    if (Test-Path $OutMsixPath) { Remove-Item $OutMsixPath -Force }
    & $script:makeappx pack /o /d $stagingDir /p $OutMsixPath | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed for win-$Arch (exit $LASTEXITCODE)" }
    Write-Host "    packed $OutMsixPath"
}


$label = Get-ReleaseLabel
$script:pkgVersion = if ($PackageVersion) { $PackageVersion } else { Get-PackageVersion }
$script:cert = $null

if ($MsStore) {
    $script:identityName = $MsStoreIdentityName
    $script:publisher = $MsStorePublisher
    $outArtifact = Join-Path $BundleDir "ExifGlass_${label}_win-msstore.msixbundle"
}
else {
    $script:identityName = $IdentityName
    $script:publisher = "CN=$PublisherDisplayName"
    if ($Sign) {
        $script:cert = Resolve-SigningCert $CertSubject $CertFile $CertPassword
        if ($script:cert) { $script:publisher = $script:cert.Subject }
        else { Write-Warning 'No signing certificate found: building an UNSIGNED package.' }
    }
    $outArtifact = Join-Path $BundleDir "ExifGlass_${label}_win-$Platform.msix"
}

$script:makeappx = Find-SdkTool 'makeappx.exe'
$script:makepri = Find-SdkTool 'makepri.exe'

Write-Host "==> ExifGlass $label as $(if ($MsStore) { 'an x64 + arm64 .msixbundle (Microsoft Store)' } else { "an MSIX ($Platform)" })"
Write-Host "    Identity  : $($script:identityName)"
Write-Host "    Publisher : $($script:publisher)"
Write-Host "    Version   : $($script:pkgVersion)"
Write-Host "    Output    : $outArtifact"

New-Item -ItemType Directory -Path $BundleDir -Force | Out-Null
if (Test-Path $outArtifact) { Remove-Item $outArtifact -Force }

try {
    if ($MsStore) {
        # makeappx bundle takes a folder holding only the packages to bundle
        $bundleInput = Join-Path $StagingRoot 'win-msstore-bundle'
        if (Test-Path $bundleInput) { Remove-Item $bundleInput -Recurse -Force }
        New-Item -ItemType Directory -Path $bundleInput -Force | Out-Null
        foreach ($arch in @('x64', 'arm64')) { New-MsixPackage -Arch $arch -OutMsixPath (Join-Path $bundleInput "ExifGlass-$arch.msix") }

        & $script:makeappx bundle /o /d $bundleInput /bv $script:pkgVersion /p $outArtifact | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "makeappx bundle failed (exit $LASTEXITCODE)" }
    }
    else {
        New-MsixPackage -Arch $Platform -OutMsixPath $outArtifact
        if ($script:cert) {
            Write-Host '==> Signing the package'
            if (Invoke-SignTool $script:cert @($outArtifact) $TimestampUrl) {
                & (Find-SdkTool 'signtool.exe') verify /pa $outArtifact | Out-Host
                if ($LASTEXITCODE -ne 0) { throw "signtool verify failed (exit $LASTEXITCODE)" }
            }
            else {
                Write-Warning 'Could not sign the package: it is left UNSIGNED.'
                $script:cert = $null
            }
        }
    }
}
finally {
    if (Test-Path $StagingRoot) { Remove-Item $StagingRoot -Recurse -Force -ErrorAction SilentlyContinue }
}

$hash = (Get-FileHash $outArtifact -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host ''
Write-Host "Done: $outArtifact ($([math]::Round((Get-Item $outArtifact).Length / 1MB, 1)) MB)"
Write-Host "  sha256 : $hash"
if ($MsStore) {
    Write-Host '  Signed : no; upload it to Partner Center as it is, and raise <ExifGlassBundleBuild> before the next submission.'
}
elseif ($script:cert) {
    Write-Host '  Signed : yes (payload binaries and the package)'
}
else {
    Write-Host '  Signed : no; Windows installs it only once it is signed (or with Developer Mode from its unpacked layout, see README.md).'
}
