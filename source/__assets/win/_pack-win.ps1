# Shared helpers for the Windows packers (dot-sourced): version and label, a fresh publish, the SDK tools, Authenticode signing.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$script:SourceDir    = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$script:BuildProps   = Join-Path $SourceDir 'Directory.Build.props'
$script:BundleDir    = Join-Path $SourceDir '__artifacts\bundle'
$script:StagingRoot  = Join-Path $SourceDir '__artifacts\staging'
$script:PublishAot   = Join-Path $SourceDir 'scripts\publish-aot.ps1'

# must match SettingsStore.PORTABLE_MARKER (D19)
$script:PortableMarker = '.egportable'


# One <Tag>value</Tag> from Directory.Build.props.
function Get-BuildProp([string]$Tag) {
    $m = Select-String -Path $BuildProps -Pattern "<$Tag>(.*?)</$Tag>" | Select-Object -First 1
    if ($m) { return $m.Matches[0].Groups[1].Value.Trim() }
    return ''
}


# The release label every artifact and the GitHub tag carry: <version>[-<release type>], no "v".
function Get-ReleaseLabel {
    $version = Get-BuildProp 'ExifGlassVersion'
    if (-not $version) { throw "Could not read <ExifGlassVersion> from $BuildProps" }
    $type = Get-BuildProp 'ExifGlassReleaseType'
    if ($type) { return "$version-$type" }
    return $version
}


# The 4-part MSIX version, Major.Minor.<ExifGlassBundleBuild>.0: the Store reserves the 4th part.
function Get-PackageVersion {
    $short = Get-BuildProp 'ExifGlassBundleShortVersion'
    if (-not $short) { $short = Get-BuildProp 'ExifGlassVersion' }
    $build = Get-BuildProp 'ExifGlassBundleBuild'
    if (-not $build) { $build = '0' }

    $parts = $short.Split('.')
    $minor = if ($parts.Count -gt 1) { $parts[1] } else { '0' }
    return "$($parts[0]).$minor.$build.0"
}


# Publishes win-<arch> with scripts/publish-aot.ps1, or with -Reuse checks that the last publish carries this version:
# the version is compiled in, so an old publish would ship old code under a new number.
function Invoke-FreshPublish([string]$Arch, [switch]$Reuse) {
    $rid = "win-$Arch"
    $publishDir = Join-Path $SourceDir "__artifacts\publish\$rid"
    $exe = Join-Path $publishDir 'ExifGlass.exe'

    if ($Reuse -and (Test-Path $exe)) {
        Write-Host "    reusing $publishDir (local iteration only)"
    }
    else {
        & pwsh -NoProfile -ExecutionPolicy Bypass -File $PublishAot $rid | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "publish-aot failed for $rid (exit $LASTEXITCODE)" }
    }

    if (-not (Test-Path $exe)) { throw "No ExifGlass.exe in $publishDir" }
    if (-not (Test-Path (Join-Path $publishDir 'exiftool.exe'))) { throw "No exiftool.exe in $($publishDir): drop the Windows ExifTool into __assets\exiftool\win (its README)" }

    $expected = "$(Get-ReleaseLabel)-win-$Arch"
    $actual = (Get-Item $exe).VersionInfo.ProductVersion
    if ($actual -ne $expected) { throw "$exe is version '$actual', not '$expected': publish again (drop -SkipPublish)" }
    return $publishDir
}


# Copies a publish into a staging folder without symbols; the packers sign there, never in the shared publish folder.
function Copy-Payload([string]$PublishDir, [string]$Destination) {
    if (Test-Path $Destination) { Remove-Item $Destination -Recurse -Force }
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Copy-Item -Path (Join-Path $PublishDir '*') -Destination $Destination -Recurse -Force
    Get-ChildItem -Path $Destination -Recurse -Include '*.pdb' -File | Remove-Item -Force
}


# makeappx, makepri or signtool from the newest Windows SDK (x64 host build), else PATH.
function Find-SdkTool([string]$Name) {
    $roots = @("${env:ProgramFiles(x86)}\Windows Kits\10\bin", "${env:ProgramFiles}\Windows Kits\10\bin") | Where-Object { $_ -and (Test-Path $_) }
    foreach ($root in $roots) {
        $hit = Get-ChildItem -Path $root -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^10\.' } |
            Sort-Object { [version]$_.Name } -Descending |
            ForEach-Object { Join-Path $_.FullName "x64\$Name" } |
            Where-Object { Test-Path $_ } |
            Select-Object -First 1
        if ($hit) { return $hit }
    }

    $onPath = Get-Command $Name -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    throw "Could not find $($Name): install the Windows 10/11 SDK (makeappx, makepri, signtool)."
}


# The code-signing certificate: -CertFile, else the first one in CurrentUser\My or LocalMachine\My whose Subject holds $Subject.
# Returns @{ Subject; Machine; File; Password } or $null, which builds unsigned.
function Resolve-SigningCert([string]$Subject, [string]$CertFile, [string]$CertPassword) {
    if ($CertFile) {
        if (-not (Test-Path $CertFile)) { Write-Warning "Certificate file not found: $CertFile"; return $null }
        try {
            $cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($CertFile, $CertPassword)
            return @{ Subject = $cert.Subject; Machine = $false; File = $CertFile; Password = $CertPassword; Name = '' }
        }
        catch {
            Write-Warning "Could not load '$CertFile': $($_.Exception.Message)"
            return $null
        }
    }

    foreach ($store in @(@{ Path = 'Cert:\CurrentUser\My'; Machine = $false }, @{ Path = 'Cert:\LocalMachine\My'; Machine = $true })) {
        $cert = Get-ChildItem $store.Path -CodeSigningCert -ErrorAction SilentlyContinue |
            Where-Object { $_.Subject -like "*$Subject*" -and $_.HasPrivateKey } |
            Select-Object -First 1
        if ($cert) { return @{ Subject = $cert.Subject; Machine = $store.Machine; File = ''; Password = ''; Name = $Subject } }
    }
    return $null
}


# Authenticode, SHA-256, timestamped; $true when every file was signed.
function Invoke-SignTool($Cert, [string[]]$Files, [string]$TimestampUrl) {
    if ($Files.Count -eq 0) { return $true }

    $signtool = Find-SdkTool 'signtool.exe'
    $common = @('sign', '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256')
    if ($Cert.File) {
        $common += @('/f', $Cert.File)
        if ($Cert.Password) { $common += @('/p', $Cert.Password) }
    }
    else {
        $common += @('/n', $Cert.Name, '/a')
        if ($Cert.Machine) { $common += '/sm' }
    }

    # Out-Host: signtool's lines would otherwise join the return value, and a non-empty array reads as success (FewerPhotos G8)
    & $signtool @common @Files | Out-Host
    return ($LASTEXITCODE -eq 0)
}


# The binaries ExifGlass ships under its name; ExifTool and its Perl stay as exiftool.org signs them.
function Get-OwnBinaries([string]$PayloadDir) {
    return @(Get-ChildItem -Path $PayloadDir -Recurse -Include '*.exe', '*.dll' -File |
        Where-Object { $_.FullName -notmatch '\\exiftool_files\\' -and $_.Name -ne 'exiftool.exe' } |
        Select-Object -ExpandProperty FullName)
}
