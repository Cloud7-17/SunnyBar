using System.Drawing;
using System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;
using SunnyBar.App.Interop;

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
        icon = CreateIcon();
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

    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            using var background = new SolidBrush(Color.FromArgb(40, 50, 75));
            using var sunlight = new SolidBrush(Color.FromArgb(255, 207, 112));
            using var ray = new Pen(Color.FromArgb(255, 207, 112), 2);
            graphics.FillEllipse(background, 0, 0, 32, 32);
            graphics.FillEllipse(sunlight, 11, 11, 10, 10);
            for (var index = 0; index < 8; index++)
            {
                var angle = index * Math.PI / 4;
                graphics.DrawLine(ray, 16 + (float)Math.Cos(angle) * 8, 16 + (float)Math.Sin(angle) * 8,
                    16 + (float)Math.Cos(angle) * 11, 16 + (float)Math.Sin(angle) * 11);
            }
        }
        var nativeIcon = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(nativeIcon);
            return (Icon)borrowed.Clone();
        }
        finally { NativeMethods.DestroyIcon(nativeIcon); }
    }

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
