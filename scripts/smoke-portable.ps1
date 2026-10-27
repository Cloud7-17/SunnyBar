[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Directory, [string]$ResultPath = '')
$ErrorActionPreference = 'Stop'
$packageRoot = (Resolve-Path -LiteralPath $Directory).Path
$exe = Join-Path $packageRoot 'SunnyBar.App.exe'
if (Get-Process SunnyBar.App -ErrorAction SilentlyContinue) { throw 'Exit the running Sunny Bar from its tray before testing the package.' }
$settingsPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'SunnyBar/settings.json'
$settingsBefore = if (Test-Path -LiteralPath $settingsPath) { (Get-FileHash -LiteralPath $settingsPath).Hash } else { '' }
$eventsPath = Join-Path (Split-Path -Parent $settingsPath) 'events.json'
$eventsBefore = if (Test-Path -LiteralPath $eventsPath) { (Get-FileHash -LiteralPath $eventsPath).Hash } else { '' }
$runPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$startupBefore = (Get-ItemProperty -LiteralPath $runPath -Name SunnyBar -ErrorAction SilentlyContinue).SunnyBar
if (-not ('SunnyBarPackageNative' -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class SunnyBarPackageNative {
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string className, string title);
    public static IntPtr FindOverlay() { return FindWindow(null, "Sunny Bar \u2014 \u60ac\u6d6e\u65f6\u95f4\u8f74"); }
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
"@
}
$info = New-Object Diagnostics.ProcessStartInfo
$info.FileName = $exe
$info.WorkingDirectory = $packageRoot
$info.UseShellExecute = $false
$info.CreateNoWindow = $true
$info.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$invalidRuntime = Join-Path $packageRoot 'no-shared-dotnet-runtime'
$info.EnvironmentVariables['DOTNET_ROOT'] = $invalidRuntime
$info.EnvironmentVariables['DOTNET_ROOT_X64'] = $invalidRuntime
$info.EnvironmentVariables['DOTNET_MULTILEVEL_LOOKUP'] = '0'
$info.EnvironmentVariables['PATH'] = Join-Path $env:SystemRoot 'System32'
$process = [Diagnostics.Process]::Start($info)
try {
    $barHandle = [IntPtr]::Zero
    [long]$required = 0x20 -bor 0x80 -bor 0x80000 -bor 0x08000000 -bor 0x8
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while ([DateTime]::UtcNow -lt $deadline -and -not $process.HasExited) {
        $candidate = [SunnyBarPackageNative]::FindOverlay()
        [uint32]$ownerProcess = 0
        if ($candidate -ne [IntPtr]::Zero) { [void][SunnyBarPackageNative]::GetWindowThreadProcessId($candidate, [ref]$ownerProcess) }
        if ($ownerProcess -eq $process.Id -and [SunnyBarPackageNative]::IsWindowVisible($candidate)) {
            $readyStyle = [SunnyBarPackageNative]::GetWindowLongPtr($candidate,-20).ToInt64()
            if (($readyStyle -band $required) -eq $required) { $barHandle = $candidate; break }
        }
        Start-Sleep -Milliseconds 100
    }
    if ($barHandle -eq [IntPtr]::Zero) { throw "Published overlay did not start within 45 seconds. Expected PID $($process.Id); last window $candidate, owner $ownerProcess; exited=$($process.HasExited)." }
    $process.Refresh()
    $coreclr = $process.Modules | Where-Object ModuleName -EQ 'coreclr.dll' | Select-Object -First 1
    if (-not $coreclr -or -not [string]::Equals($coreclr.FileName, (Join-Path $packageRoot 'coreclr.dll'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Process did not load the packaged .NET runtime.' }
    $style = [SunnyBarPackageNative]::GetWindowLongPtr($barHandle,-20).ToInt64()
    if (($style -band $required) -ne $required) { throw 'Overlay pass-through/no-activate/tool-window/topmost styles missing.' }
    $second = [Diagnostics.Process]::Start($info)
    try {
        if (-not $second.WaitForExit(10000) -or $second.ExitCode -ne 0 -or $process.HasExited) { throw 'Single-instance launch failed.' }
    }
    finally { if (-not $second.HasExited) { $second.Kill() }; $second.Dispose() }
    if (-not [SunnyBarPackageNative]::PostMessage($barHandle,0x0010,[IntPtr]::Zero,[IntPtr]::Zero)) { throw 'Cannot request application shutdown.' }
    if (-not $process.WaitForExit(10000) -or $process.ExitCode -ne 0) { throw 'Portable application did not exit cleanly.' }
    $settingsAfter = if (Test-Path -LiteralPath $settingsPath) { (Get-FileHash -LiteralPath $settingsPath).Hash } else { '' }
    $startupAfter = (Get-ItemProperty -LiteralPath $runPath -Name SunnyBar -ErrorAction SilentlyContinue).SunnyBar
    $eventsAfter = if (Test-Path -LiteralPath $eventsPath) { (Get-FileHash -LiteralPath $eventsPath).Hash } else { '' }
    if ($eventsBefore -ne $eventsAfter) { throw 'Startup smoke test changed user events.' }
    if ($settingsBefore -ne $settingsAfter -or $startupBefore -ne $startupAfter) { throw 'Startup smoke test changed user configuration or startup registration.' }
    $result = [ordered]@{ result='PASS'; executable=$exe; version=(Get-Item -LiteralPath $exe).VersionInfo.ProductVersion; packagedRuntime=$coreclr.FileName; dotnetRoot=$invalidRuntime; path=$info.EnvironmentVariables['PATH']; windowStyles=('0x{0:X}' -f $style); secondInstanceExitCode=0; shutdownExitCode=0; settingsPreserved=$true; eventsPreserved=$true; startupPreserved=$true }
    if ($ResultPath) { $result | ConvertTo-Json | Set-Content -LiteralPath $ResultPath -Encoding utf8 }
    $result | ConvertTo-Json
}
finally {
    if (-not $process.HasExited) { $process.Kill() }
    $process.Dispose()
}
