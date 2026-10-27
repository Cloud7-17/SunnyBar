param([switch]$Preview)

$ErrorActionPreference = 'Stop'
$sunnyRoot = Split-Path -Parent $PSScriptRoot
$sunnyLocalDotnet = Join-Path $sunnyRoot '.tools/dotnet/dotnet.exe'
if (Test-Path -LiteralPath $sunnyLocalDotnet) {
    $sunnyDotnet = $sunnyLocalDotnet
    $env:DOTNET_ROOT = Split-Path -Parent $sunnyLocalDotnet
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
    $env:DOTNET_CLI_HOME = Join-Path $sunnyRoot '.tools/cli-home'
    $env:NUGET_PACKAGES = Join-Path $sunnyRoot '.tools/nuget'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
} else {
    $sunnyDotnet = (Get-Command dotnet -ErrorAction Stop).Source
}

$sunnyExe = Join-Path $sunnyRoot 'src/SunnyBar.App/bin/Debug/net10.0-windows/SunnyBar.App.exe'
if (-not (Test-Path -LiteralPath $sunnyExe)) {
    & $sunnyDotnet build (Join-Path $sunnyRoot 'SunnyBar.slnx')
    if ($LASTEXITCODE -ne 0) { throw 'Sunny Bar build failed.' }
}
$sunnyArguments = @()
if ($Preview) { $sunnyArguments += '--preview' }
& $sunnyExe @sunnyArguments
