[CmdletBinding()]
param([string]$Dotnet = '')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $Dotnet) {
    $localDotnet = Join-Path $repoRoot '.tools/dotnet/dotnet.exe'
    $Dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
}
[xml]$buildProps = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
$version = [string]$buildProps.Project.PropertyGroup.Version
if ($version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.]+)?$') { throw 'Unsafe or missing version in Directory.Build.props.' }
$releaseName = "SunnyBar-$version-win-x64"
$releaseRoot = Join-Path $repoRoot 'artifacts/releases'
$stageParent = Join-Path $repoRoot ('artifacts/staging/' + [Guid]::NewGuid().ToString('N'))
$stage = Join-Path $stageParent $releaseName
New-Item -ItemType Directory -Path $stage, $releaseRoot -Force | Out-Null
Push-Location $repoRoot
try {
    & $Dotnet publish src/SunnyBar.App/SunnyBar.App.csproj -c Release -r win-x64 --self-contained true '-p:PublishSingleFile=false' '-p:PublishTrimmed=false' '-p:DebugType=None' '-p:DebugSymbols=false' -m:1 -nr:false -o $stage
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
    $packageInfo = & $Dotnet nuget locals global-packages --list --force-english-output
    if ($LASTEXITCODE -ne 0) { throw 'Cannot locate runtime license files.' }
    $packageRoot = ($packageInfo | Where-Object { $_ -match '^global-packages:' }) -replace '^global-packages:\s*', ''
    [xml]$appProject = Get-Content -LiteralPath 'src/SunnyBar.App/SunnyBar.App.csproj' -Raw
    $runtimeVersion = $appProject.SelectSingleNode('/Project/PropertyGroup/RuntimeFrameworkVersion').InnerText
    foreach ($pack in @('microsoft.netcore.app.runtime.win-x64', 'microsoft.windowsdesktop.app.runtime.win-x64')) {
        $packRoot = Join-Path $packageRoot "$pack/$runtimeVersion"
        $notices = @(Get-ChildItem -LiteralPath $packRoot -File | Where-Object { $_.Name -match '^(LICENSE|THIRD-PARTY)' })
        if (-not ($notices | Where-Object Name -Match '^LICENSE')) { throw "Missing license: $pack" }
        $licenseTarget = Join-Path $stage "licenses/$pack"
        New-Item -ItemType Directory -Path $licenseTarget -Force | Out-Null
        $notices | Copy-Item -Destination $licenseTarget
    }
    Copy-Item -LiteralPath LICENSE, THIRD_PARTY_NOTICES.md -Destination $stage
    foreach ($doc in @('SETTINGS.md','SCHEDULES.md','PRIVACY.md','SOLAR_CALCULATION.md','RELEASE_CHECKLIST.md')) {
        Copy-Item -LiteralPath (Join-Path 'docs' $doc) -Destination $stage
    }
    Copy-Item -LiteralPath docs/PORTABLE_GUIDE.md -Destination (Join-Path $stage 'README.md')
    if (Test-Path -LiteralPath docs/images) { Copy-Item -LiteralPath docs/images -Destination (Join-Path $stage 'images') -Recurse }
    [ordered]@{ product='Sunny Bar'; version=$version; runtime=$runtimeVersion; rid='win-x64'; selfContained=$true; signed=$false; buildUtc=[DateTime]::UtcNow.ToString('o') } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stage 'release.json') -Encoding utf8
    $hashes = Get-ChildItem -LiteralPath $stage -Recurse -File | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($stage.Length + 1).Replace('\','/')
        '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $relative
    }
    $hashes | Set-Content -LiteralPath (Join-Path $stage 'SHA256SUMS.txt') -Encoding ascii
    & (Join-Path $PSScriptRoot 'verify-portable.ps1') -Directory $stage
    $zip = Join-Path $releaseRoot "$releaseName.zip"
    Compress-Archive -LiteralPath $stage -DestinationPath $zip -CompressionLevel Optimal -Force
    '{0}  {1}' -f (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path -Leaf $zip) |
        Set-Content -LiteralPath "$zip.sha256" -Encoding ascii
    Write-Output "Package: $zip"
    Write-Output "Unpacked: $stage"
}
finally { Pop-Location }
