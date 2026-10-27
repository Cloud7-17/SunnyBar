[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Directory)
$ErrorActionPreference = 'Stop'
$packageRoot = (Resolve-Path -LiteralPath $Directory).Path.TrimEnd('\','/')
foreach ($required in @('SunnyBar.App.exe','SunnyBar.App.dll','SunnyBar.App.runtimeconfig.json','coreclr.dll','hostfxr.dll','hostpolicy.dll','PresentationFramework.dll','System.Windows.Forms.dll','LICENSE','THIRD_PARTY_NOTICES.md','README.md','PRIVACY.md','release.json','SHA256SUMS.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $packageRoot $required) -PathType Leaf)) { throw "Missing portable file: $required" }
}
$release = Get-Content -LiteralPath (Join-Path $packageRoot 'release.json') -Raw | ConvertFrom-Json
$config = Get-Content -LiteralPath (Join-Path $packageRoot 'SunnyBar.App.runtimeconfig.json') -Raw | ConvertFrom-Json
if (-not $release.selfContained -or $release.rid -ne 'win-x64' -or $config.runtimeOptions.frameworks -or $config.runtimeOptions.framework) { throw 'Package is not self-contained win-x64.' }
if (-not ($config.runtimeOptions.includedFrameworks | Where-Object name -EQ 'Microsoft.WindowsDesktop.App')) { throw 'WPF runtime not declared in package.' }
$expected = @{}
foreach ($line in Get-Content -LiteralPath (Join-Path $packageRoot 'SHA256SUMS.txt')) {
    if ($line -notmatch '^([0-9a-f]{64})  (.+)$') { throw 'Invalid SHA256 manifest.' }
    $hash = $Matches[1]; $relative = $Matches[2]
    $target = [IO.Path]::GetFullPath((Join-Path $packageRoot $relative))
    if (-not $target.StartsWith($packageRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest path escapes package.' }
    if ($expected.ContainsKey($relative)) { throw "Duplicate manifest entry: $relative" }
    $expected[$relative] = $true
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $hash) { throw "SHA256 mismatch: $relative" }
}
$actual = @(Get-ChildItem -LiteralPath $packageRoot -File -Recurse | Where-Object FullName -NE (Join-Path $packageRoot 'SHA256SUMS.txt'))
if ($actual.Count -ne $expected.Count) { throw 'Unexpected or unlisted files in package.' }
Write-Output "Verified $($release.version): $($expected.Count) files; self-contained win-x64, runtime $($release.runtime)."
