#Requires -Version 7.0
# Packs the whole Windows release: x64 and arm64 are published once, then the sideload MSIX of each, the Store bundle and the portable ZIPs.
# Usage: pwsh __assets/win/pack-win-all.ps1 [-Sign] [-CertSubject <s> | -CertFile <pfx> -CertPassword <pw>]
[CmdletBinding()]
param(
    [switch]$Sign,
    [string]$CertSubject = 'Duong Dieu Phap',
    [string]$CertFile = '',
    [string]$CertPassword = ''
)

. (Join-Path $PSScriptRoot '_pack-win.ps1')

$signArgs = @()
if ($Sign) {
    $signArgs = @('-Sign', '-CertSubject', $CertSubject)
    if ($CertFile) { $signArgs += @('-CertFile', $CertFile, '-CertPassword', $CertPassword) }
}

function Invoke-Packer([string]$Script, [string[]]$Arguments) {
    & pwsh -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $Script) @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "$Script $($Arguments -join ' ') failed (exit $LASTEXITCODE)" }
}

foreach ($arch in @('x64', 'arm64')) { Invoke-FreshPublish -Arch $arch | Out-Null }

foreach ($arch in @('x64', 'arm64')) {
    Invoke-Packer 'pack-win-msix.ps1' (@('-Platform', $arch, '-SkipPublish') + $signArgs)
    Invoke-Packer 'pack-win-zip.ps1' (@('-Platform', $arch, '-SkipPublish') + $signArgs)
}
Invoke-Packer 'pack-win-msix.ps1' @('-MsStore', '-SkipPublish')

$label = Get-ReleaseLabel
Write-Host ''
Write-Host "Windows release $label in $($BundleDir):"
Get-ChildItem $BundleDir -File -Filter "ExifGlass_${label}_win-*" | ForEach-Object {
    Write-Host ("  {0}  {1,7:N1} MB  sha256 {2}" -f $_.Name, ($_.Length / 1MB), (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())
}
