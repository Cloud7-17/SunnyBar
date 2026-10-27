using System.Text;
using System.Text.Json;

namespace SunnyBar.Core;

public sealed record SettingsLoadResult(AppSettings Settings, bool IsFirstLaunch, string Message, bool CanSave = true);

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private bool writeBlocked;
    public string DirectoryPath { get; }
    public string FilePath => Path.Combine(DirectoryPath, "settings.json");

    public SettingsService(string? directory = null) => DirectoryPath = Path.GetFullPath(directory
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SunnyBar"));

    public SettingsLoadResult Load()
    {
        writeBlocked = false;
        if (!File.Exists(FilePath)) return new(new(), true, "首次使用：默认采用北京预设，可修改地点后保存。所有时间按电脑系统时区显示。");
        try
        {
            if (new FileInfo(FilePath).Length > 1024 * 1024) throw new JsonException("设置文件超过 1 MB。");
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? throw new JsonException("设置根对象为空。");
            if (settings.SchemaVersion > 1)
            {
                writeBlocked = true;
                return new(new(), false, "配置来自较新版本，已保留原文件；当前只使用默认预览，不能覆盖保存。", false);
            }
            if (settings.SchemaVersion == 0)
            {
                var migrated = settings with { SchemaVersion = 1 };
                EnsureValid(migrated);
                var backup = Backup("v0");
                Save(migrated);
                return new(migrated, false, $"旧版配置已升级，原文件备份于：{backup}");
            }
            EnsureValid(settings);
            return new(settings, false, "");
        }
        catch (Exception error) when (error is JsonException or SettingsValidationException)
        {
            try
            {
                var backup = Backup("corrupt");
                var defaults = new AppSettings();
                Save(defaults);
                return new(defaults, false, $"配置无效，已备份并恢复默认值。备份：{backup}\n原因：{error.Message}");
            }
            catch (Exception backupError) when (backupError is IOException or UnauthorizedAccessException)
            {
                writeBlocked = true;
                return new(new(), false, $"配置无法安全备份，保留原文件并禁止覆盖。{backupError.Message}", false);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            writeBlocked = true;
            return new(new(), false, $"无法读取设置，保留原文件并使用默认预览。{error.Message}", false);
        }
    }

    public void Save(AppSettings settings)
    {
        if (writeBlocked) throw new IOException("当前配置受保护，修复读取/备份问题或使用匹配版本后重启应用。");
        EnsureValid(settings);
        Directory.CreateDirectory(DirectoryPath);
        var temporary = FilePath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(settings, Options));
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public string ResolveAsset(string value) => Path.IsPathRooted(value) ? value : Path.Combine(DirectoryPath, value);

    public string ImportPng(string source)
    {
        using (var input = File.OpenRead(source))
        {
            if (input.Length > 5 * 1024 * 1024) throw new IOException("PNG 图片不能超过 5 MB。");
            Span<byte> signature = stackalloc byte[8];
            if (input.Read(signature) != 8 || !signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                throw new IOException("请选择有效的 PNG 文件。");
        }
        var relative = Path.Combine("Indicators", Guid.NewGuid().ToString("N") + ".png");
        Directory.CreateDirectory(Path.Combine(DirectoryPath, "Indicators"));
        File.Copy(source, ResolveAsset(relative));
        return relative;
    }

    private string Backup(string reason)
    {
        var path = Path.Combine(DirectoryPath, $"settings.{reason}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
        File.Copy(FilePath, path);
        return path;
    }

    private static void EnsureValid(AppSettings settings)
    {
        var errors = SettingsValidator.Validate(settings);
        if (errors.Count > 0) throw new SettingsValidationException(errors);
    }
}
