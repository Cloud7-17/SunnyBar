using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using SunnyBar.Core;

namespace SunnyBar.App.ViewModels;

public sealed record ScheduleRow(ScheduleEvent Event, TimeZoneInfo Zone)
{
    public string Title => Event.Title;
    public string StartText => TimeZoneInfo.ConvertTime(Event.StartAt, Zone).ToString("yyyy-MM-dd HH:mm zzz");
    public string EndText => TimeZoneInfo.ConvertTime(Event.EndAt, Zone).ToString("yyyy-MM-dd HH:mm zzz");
    public string Color => Event.Color;
}

public sealed class ScheduleEditorViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly TimelineViewModel timeline;
    private readonly ScheduleStore store;
    private readonly TimeZoneInfo zone;
    private ScheduleDocument document;
    private ScheduleRow? selected;
    public ObservableCollection<ScheduleRow> Events { get; } = [];
    public bool CanSave { get; }
    public bool HasSelection => selected is not null && CanSave;
    public bool CanClearAll => CanSave && Events.Count > 0;
    public string TimeZoneText => "时区：" + zone.DisplayName + "。单次日程按日期保存；重复小时默认第一次，编辑已有日程保留原偏移。";
    public string CurrentText => timeline.CurrentScheduleText;
    public string NextText => timeline.NextScheduleText;
    public string FilePath => store.FilePath;
    public ScheduleRow? Selected
    {
        get => selected;
        set { if (selected == value) return; selected = value; if (value is not null) Fill(value.Event); Notify(); }
    }
    public string Title { get; set; } = "";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string StartHour { get; set; } = "09";
    public string StartMinute { get; set; } = "00";
    public string EndHour { get; set; } = "10";
    public string EndMinute { get; set; } = "00";
    public string Notes { get; set; } = "";
    private string color = "#5AA9E6";
    public string[] ColorChoices => new[] { "#5AA9E6", "#44AA88", "#EE8844", "#AF8FDD", "#E86677", "#F2B544", "#5577CC" }.Append(color).Distinct().ToArray();
    public string Color { get => color; set { if (string.IsNullOrWhiteSpace(value) || color == value) return; color = value; Notify(); } }
    public string Status { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;

    public ScheduleEditorViewModel(TimelineViewModel timeline, ScheduleStore store, string initialMessage = "", TimeZoneInfo? zone = null)
    {
        this.timeline = timeline; this.store = store; this.zone = zone ?? TimeZoneInfo.Local;
        var loaded = store.Load(); document = loaded.Document; CanSave = loaded.CanSave;
        Status = initialMessage + loaded.Message;
        timeline.ApplySchedule(document);
        ReloadRows(); Selected = Events.FirstOrDefault();
        timeline.PropertyChanged += OnTimelineChanged;
    }
    public void New()
    {
        try
        {
            if (!CanSave) throw new IOException("日程文件受保护，不能新建。");
            var now = TimeZoneInfo.ConvertTime(timeline.Snapshot!.Now, zone);
            // Round the instant forward, so a DST jump never creates a nonexistent wall time.
            var start = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMinute)).AddMinutes(30 - now.Minute % 30);
            var item = new ScheduleEvent { Title = "新日程", StartAt = TimeZoneInfo.ConvertTime(start, zone), EndAt = TimeZoneInfo.ConvertTime(start.AddHours(1), zone) };
            var candidate = document with { Events = document.Events.Append(item).ToList() };
            store.Save(candidate); document = candidate; timeline.ApplySchedule(document);
            ReloadRows(); Selected = Events.First(row => row.Event.Id == item.Id);
            Status = "已新建日程。可在下方编辑，点击“保存修改”更新。"; Notify();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SettingsValidationException or ArgumentException)
        { Status = "未新建：" + error.Message; Notify(); }
    }
    private void Fill(ScheduleEvent item)
    {
        Title = item.Title; Notes = item.Notes ?? ""; color = item.Color;
        SetDates(TimeZoneInfo.ConvertTime(item.StartAt, zone).DateTime, TimeZoneInfo.ConvertTime(item.EndAt, zone).DateTime);
    }
    private void SetDates(DateTime start, DateTime end)
    {
        StartDate = start.Date; EndDate = end.Date;
        StartHour = start.Hour.ToString("00", CultureInfo.InvariantCulture); StartMinute = start.Minute.ToString("00", CultureInfo.InvariantCulture);
        EndHour = end.Hour.ToString("00", CultureInfo.InvariantCulture); EndMinute = end.Minute.ToString("00", CultureInfo.InvariantCulture);
    }
    public bool Save()
    {
        try
        {
            if (!CanSave) throw new IOException("日程文件受保护，不能覆盖保存。");
            if (StartDate is not { } startDate || EndDate is not { } endDate) throw new FormatException("请选择开始日期和结束日期。");
            var original = selected?.Event ?? throw new FormatException("请先新建或选择日程，再保存修改。");
            var item = original with
            {
                Title = Title.Trim(), Notes = Notes.Trim(), Color = Color,
                StartAt = ScheduleTimeResolver.Resolve(startDate, StartHour, StartMinute, zone,
                    original is null ? null : TimeZoneInfo.ConvertTime(original.StartAt, zone).Offset),
                EndAt = ScheduleTimeResolver.Resolve(endDate, EndHour, EndMinute, zone,
                    original is null ? null : TimeZoneInfo.ConvertTime(original.EndAt, zone).Offset)
            };
            // Preserve offset and sub-minute precision when the user has not changed the corresponding time field.
            if (original is not null)
            {
                if (SameMinute(item.StartAt, original.StartAt)) item = item with { StartAt = original.StartAt };
                if (SameMinute(item.EndAt, original.EndAt)) item = item with { EndAt = original.EndAt };
            }
            var candidate = document with { Events = document.Events.Where(prior => prior.Id != item.Id).Append(item).OrderBy(prior => prior.StartAt).ToList() };
            store.Save(candidate); document = candidate; timeline.ApplySchedule(document);
            ReloadRows(); selected = Events.First(row => row.Event.Id == item.Id); Fill(item);
            Status = "已保存并立即更新。相交日程会分层显示。"; Notify(); return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SettingsValidationException or FormatException or ArgumentException)
        { Status = "未保存：" + error.Message; Notify(); return false; }
    }
    private static bool SameMinute(DateTimeOffset left, DateTimeOffset right) => left.UtcTicks / TimeSpan.TicksPerMinute == right.UtcTicks / TimeSpan.TicksPerMinute;
    public bool Delete()
    {
        if (!HasSelection) return false;
        try
        {
            var candidate = document with { Events = document.Events.Where(item => item.Id != selected!.Event.Id).ToList() };
            store.Save(candidate); document = candidate; timeline.ApplySchedule(document); ReloadRows();
            Title = ""; Notes = ""; Selected = Events.FirstOrDefault();
            Status = "已删除选中日程。"; Notify(); return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SettingsValidationException)
        { Status = "未删除：" + error.Message; Notify(); return false; }
    }
    private void ReloadRows() { selected = null; Events.Clear(); foreach (var item in document.Events.OrderBy(item => item.StartAt)) Events.Add(new(item, zone)); }
    public bool ClearAll()
    {
        if (!CanClearAll) return false;
        try
        {
            var candidate = document with { Events = [] };
            store.Save(candidate); document = candidate; timeline.ApplySchedule(document);
            ReloadRows(); Title = ""; Notes = ""; StartDate = EndDate = null;
            Status = "已清除全部日程并立即保存。"; Notify(); return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SettingsValidationException)
        { Status = "未清除：" + error.Message; Notify(); return false; }
    }
    private void OnTimelineChanged(object? sender, PropertyChangedEventArgs e) { Notify(nameof(CurrentText)); Notify(nameof(NextText)); }
    private void Notify(string property = "") => PropertyChanged?.Invoke(this, new(property));
    public void Dispose() => timeline.PropertyChanged -= OnTimelineChanged;
}
