using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SunnyBar.Core;

namespace SunnyBar.App.Services;

public interface IStartupRegistration
{
    void Apply(bool enabled, Action persistSettings);
}

public sealed class StartupRegistrationService(SettingsService store) : IStartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public void Apply(bool enabled, Action persistSettings)
    {
        using var existing = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        var oldCommand = existing?.GetValue("SunnyBar") as string;
        if (!enabled && oldCommand is null) { persistSettings(); return; }
        var launcher = Path.Combine(store.DirectoryPath, "startup.cmd");
        var oldLauncher = File.Exists(launcher) ? File.ReadAllText(launcher) : null;
        try
        {
            if (enabled)
            {
                var exe = Path.Combine(AppContext.BaseDirectory, "SunnyBar.App.exe");
                if (!File.Exists(exe)) throw new IOException("找不到 Sunny Bar EXE，无法设置开机启动。");
                var command = $"\"{exe}\"";
                // Self-contained releases can start their own EXE directly, without a console launcher.
                if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "coreclr.dll")))
                {
                    var runtime = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory()).Parent?.Parent?.Parent?.FullName;
                    var runtimeLines = runtime is not null && File.Exists(Path.Combine(runtime, "dotnet.exe"))
                        ? $"set \"DOTNET_ROOT={EscapeBatch(runtime)}\"\r\nset \"DOTNET_ROOT_X64={EscapeBatch(runtime)}\"\r\n" : "";
                    Directory.CreateDirectory(store.DirectoryPath);
                    File.WriteAllText(launcher, "@echo off\r\nchcp 65001 >nul\r\nsetlocal DisableDelayedExpansion\r\n" + runtimeLines + $"start \"\" \"{EscapeBatch(exe)}\"\r\n");
                    command = $"\"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe")}\" /c \"\"{launcher}\"\"";
                }
                using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
                key.SetValue("SunnyBar", command, RegistryValueKind.String);
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                key?.DeleteValue("SunnyBar", throwOnMissingValue: false);
            }
            persistSettings();
        }
        catch
        {
            // Restore previous registration if either registry update or JSON persistence fails.
            using var rollback = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (oldCommand is null) rollback.DeleteValue("SunnyBar", throwOnMissingValue: false);
            else rollback.SetValue("SunnyBar", oldCommand, RegistryValueKind.String);
            if (oldLauncher is not null) File.WriteAllText(launcher, oldLauncher);
            throw;
        }
    }

    private static string EscapeBatch(string value) => value.Replace("%", "%%");
}
