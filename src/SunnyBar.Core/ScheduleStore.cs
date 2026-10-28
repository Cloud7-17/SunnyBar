using System.Text;
using System.Text.Json;

namespace SunnyBar.Core;

public sealed record ScheduleLoadResult(ScheduleDocument Document, string Message, bool CanSave = true);

public sealed class ScheduleStore(string? directory = null)
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private readonly string root = Path.GetFullPath(directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SunnyBar"));
    private bool writeBlocked;
    public string FilePath => Path.Combine(root, "events.json");

    public ScheduleLoadResult Load()
    {
        writeBlocked = false;
        if (!File.Exists(FilePath)) return new(new(), "");
        try
        {
            if (new FileInfo(FilePath).Length > 4 * 1024 * 1024) throw new JsonException("日程文件超过 4 MB。");
            var document = JsonSerializer.Deserialize<ScheduleDocument>(File.ReadAllText(FilePath), Options) ?? throw new JsonException("日程根对象为空。");
            if (document.SchemaVersion > 2)
            {
                writeBlocked = true;
                return new(new(), "日程来自更新版本，保留原文件并禁止覆盖。请使用匹配版本。", false);
            }
            EnsureValid(document);
            return new(document, "");
        }
        catch (Exception error) when (error is JsonException or SettingsValidationException)
        {
            try
            {
                var backup = Path.Combine(root, $"events.corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
                File.Copy(FilePath, backup);
                var defaults = new ScheduleDocument(); Save(defaults);
                return new(defaults, $"日程文件无效，已原样备份并恢复空列表：{backup}\n原因：{error.Message}");
            }
            catch (Exception backupError) when (backupError is IOException or UnauthorizedAccessException)
            {
                writeBlocked = true;
                return new(new(), "无法安全备份日程，保留原文件并禁止覆盖。" + backupError.Message, false);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            writeBlocked = true;
            return new(new(), "无法读取日程，保留原文件并禁止覆盖。" + error.Message, false);
        }
    }

    public void Save(ScheduleDocument document)
    {
        if (writeBlocked) throw new IOException("日程文件受保护，修复后请重新打开日程窗口。");
        EnsureValid(document);
        Directory.CreateDirectory(root);
        var temporary = FilePath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(document, Options));
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(flushToDisk: true); }
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void EnsureValid(ScheduleDocument document)
    {
        var errors = ScheduleValidator.Validate(document);
        if (errors.Count > 0) throw new SettingsValidationException(errors);
    }
}
