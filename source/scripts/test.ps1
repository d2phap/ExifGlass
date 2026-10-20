# Runs every test project and writes a report; -RuntimeAsync off rebuilds without Runtime Async (A/B run).
# Usage: scripts/test.ps1 [-RuntimeAsync on|off] [-Filter <test filter>]
param(
    [ValidateSet('on', 'off')][string]$RuntimeAsync = 'on',
    [string]$Filter = ''
)

. (Join-Path $PSScriptRoot '_common.ps1')

$report = New-ReportFile ("test" + $(if ($RuntimeAsync -eq 'off') { '-async-off' } else { '' }))
$asyncFlag = if ($RuntimeAsync -eq 'off') { 'false' } else { 'true' }
$platform = if ((Get-HostRid).EndsWith('-arm64')) { 'ARM64' } else { 'x64' }
$projects = Get-ChildItem (Join-Path $SourceDir 'tests') -Recurse -Filter '*.Tests.csproj' | Sort-Object FullName

Write-Report $report "RuntimeAsync=$RuntimeAsync, Platform=$platform, $($projects.Count) test project(s)"
$failed = 0

foreach ($project in $projects) {
    Write-Report $report "== $($project.BaseName)"
    $testArgs = @('test', $project.FullName, '-c', 'Debug', "-p:Platform=$platform", "-p:ExifGlassRuntimeAsync=$asyncFlag")
    if ($Filter) { $testArgs += @('--filter', $Filter) }

    $output = & dotnet @testArgs 2>&1
    $code = $LASTEXITCODE
    $output | Where-Object { $_ -match 'Passed!|Failed!|Skipped |\[FAIL\]|error |Error Message|Assert\.' } | ForEach-Object { Write-Report $report "  $($_.ToString().Trim())" }

    if ($code -ne 0) {
        $failed++
        Write-Report $report "  FAILED (exit $code)"
    }
}

Write-Report $report $(if ($failed -eq 0) { 'PASSED: all test projects' } else { "FAILED: $failed test project(s)" })
Write-Host "Report: $report"
exit $failed
