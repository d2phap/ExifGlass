# Downloads the public C2PA test files listed in c2pa-public-files.txt into source/tests/fixtures/c2pa-public (git-ignored).
# Usage: tools/get-c2pa-public-files.ps1 [-Destination <folder>] [-Force]
param(
    [string]$Destination = [IO.Path]::Combine($PSScriptRoot, '..', 'tests', 'fixtures', 'c2pa-public'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$list = Join-Path $PSScriptRoot 'c2pa-public-files.txt'
$count = 0
$bytes = 0

foreach ($line in Get-Content $list) {
    if ($line -match '^\s*(#|$)') { continue }
    $folder, $repo, $commit, $path = $line -split '\s+', 4
    $target = [IO.Path]::Combine($Destination, $folder, $path)

    if ($Force -or -not (Test-Path $target)) {
        New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
        Invoke-WebRequest -UseBasicParsing -Uri "https://raw.githubusercontent.com/$repo/$commit/$path" -OutFile $target
    }

    $count++
    $bytes += (Get-Item $target).Length
}

Write-Host ("{0} files, {1:N1} MB in {2}" -f $count, ($bytes / 1MB), (Resolve-Path $Destination))
