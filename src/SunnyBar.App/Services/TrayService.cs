using System.Drawing;
using Forms = System.Windows.Forms;

namespace SunnyBar.App.Services;

public sealed class TrayService : IDisposable
{
    private readonly Icon icon;
    private readonly Forms.NotifyIcon notification;
    private readonly Forms.ContextMenuStrip menu;
    private readonly Forms.ToolStripMenuItem visibility;
    private readonly Forms.ToolStripMenuItem currentSchedule = new("当前日程：暂无") { Enabled = false };
    private readonly Forms.ToolStripMenuItem nextSchedule = new("下一日程：暂无") { Enabled = false };
    private readonly Action openPreview;
    private bool disposed;

    public TrayService(Action toggle, Action openPreview, Action exit, Action? openSchedule = null)
    {
        this.openPreview = openPreview;
        icon = AppIcon.CreateTrayIcon();
        menu = new Forms.ContextMenuStrip();
        visibility = new Forms.ToolStripMenuItem("隐藏时间条", null, (_, _) => toggle());
        menu.Items.Add(visibility);
        menu.Items.Add("标记与设置…", null, (_, _) => openPreview());
        if (openSchedule is not null)
        {
            menu.Items.Add("日程…", null, (_, _) => openSchedule());
            menu.Items.Add(currentSchedule); menu.Items.Add(nextSchedule);
        }
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出 Sunny Bar", null, (_, _) => exit());
        notification = new Forms.NotifyIcon
        {
            Icon = icon,
            Text = "Sunny Bar — 时间轴（右键菜单）",
            ContextMenuStrip = menu,
            Visible = true
        };
        notification.DoubleClick += OnDoubleClick;
    }

    public void UpdateVisibility(bool visible) => visibility.Text = visible ? "隐藏时间条" : "显示时间条";
    public void UpdateSchedule(string current, string next)
    {
        currentSchedule.Text = current.Length > 70 ? current[..67] + "…" : current;
        nextSchedule.Text = next.Length > 70 ? next[..67] + "…" : next;
    }

    public void ShowPlacementError(string message)
    {
        notification.ShowBalloonTip(6000, "Sunny Bar 暂时隐藏时间条", message + " 可从托盘重新显示。", Forms.ToolTipIcon.Warning);
    }

    private void OnDoubleClick(object? sender, EventArgs e) => openPreview();

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        notification.Visible = false;
        notification.DoubleClick -= OnDoubleClick;
        notification.Dispose();
        menu.Dispose();
        icon.Dispose();
    }
}
