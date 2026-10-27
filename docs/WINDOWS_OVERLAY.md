# M3 Windows 悬浮条

## 结构

- `BarWindow` 是普通进程中的透明、无边框、非激活、置顶 WPF 窗口；不注入 Explorer、不注册 AppBar、不改变系统工作区。
- `TimelineControl.IsCompact` 使用 32 DIP 总高（图标 16 DIP + 色条 16 DIP）的紧凑绘制，取消下方刻度，共用预览的 TimelineSnapshot 与时间映射器。
- `WindowPlacementService` 使用主屏 `MONITORINFO.rcWork` 和目标 HWND 的 DPI。Core 中的 OverlayLayoutCalculator 负责纯几何计算。
- `DesktopRuntime` 管理共享 ViewModel、预览、悬浮窗和托盘。`TimelineRefreshService` 管理 30 秒计时器、系统时间变化与恢复唤醒事件。
- `TrayService` 使用 .NET WinForms NotifyIcon 和自绘太阳图标。图标、原生临时 HICON、菜单、消息钩子、计时器和静态系统事件订阅均有对应释放路径。
- `SingleInstanceGuard` 使用会话内命名 Mutex；重复进程正常退出，通过注册窗口消息请求已有实例重新显示，带 `--preview` 时同时打开设置预览。

## 坐标与输入

宽度取完整工作区物理像素宽度，高度 32 DIP 转为物理像素，底部无间距。色条绘制到窗口底部，方形端点没有额外左右内边距。最后使用 SetWindowPos 直接定位，避免把不同屏幕的全局像素坐标误当作 DIP。收到 WM_DISPLAYCHANGE、WM_SETTINGCHANGE、WM_DPICHANGED 后，等待 WPF 更新再合并重定位请求。

`OverlayHoverService` 在条可见时每 120ms 读取一次系统光标位置，停留至少 450ms 后显示 `HoverInfoWindow`；提示显示时每秒刷新当前信息，移出或隐藏条时收起。浮层同样采用分层、穿透、非激活样式。未注册全局鼠标钩子，不依赖会拦截下层点击的 WPF MouseEnter。隐藏条和退出时会停止观察计时器，关闭提示并释放订阅。

鼠标穿透由 WS_EX_LAYERED + WS_EX_TRANSPARENT 实现；WS_EX_TOOLWINDOW 和 WS_EX_NOACTIVATE 避免切换列表干扰和抢焦点，ShowInTaskbar=false。WM_NCHITTEST/WM_MOUSEACTIVATE 也有防御性处理。仅设置 WPF IsHitTestVisible=false 不足以保证跨进程穿透。

这些行为参考微软的 [分层窗口命中规则](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows)、[扩展样式](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles)、[GetDpiForWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdpiforwindow) 和 [SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos)。

WPF 通过 app.manifest 声明 PerMonitorV2；使用 EXE/apphost 启动才能应用该 manifest，因此新增 `scripts/run.ps1` 作为本地启动入口。WinForms 在本项目只提供托盘，未使用其 Application 启动模型。WFO0003 的特定抑制带有代码注释，参见 [WPF/WinForms manifest 诊断冲突](https://github.com/dotnet/wpf/issues/6811)；其他编译警告仍作为错误处理。

## 实测与限制

M3 初始验证为 88% 宽度、56 DIP 高度、8 DIP 间距。按用户截图调整后，本机主屏工作区仍为 `(0,0)–(2560,1392)`、窗口 DPI=96；新 HWND 矩形为 `(0,1360)–(2560,1392)`，可见色条为 `(0,1376)–(2560,1392)`，正好铺满并贴住任务栏上沿。分层、穿透、工具窗、非激活和置顶样式均已读取核实。

悬停验证时光标位于 `(1090,1380)`，当前信息浮层显示在 `(1102,1232)–(1392,1352)`，位于条上方、未抢焦点且保留穿透；实际截图中时间、进度、地点和太阳事件文本完整可见。移出条后检查浮层隐藏。自动化坐标最初落到条外，读取真实光标位置并校正后才计入成功结果。

使用 computer-use 技能的 SendInput 点击了实体时间条中间。下方单独进程的测试按钮收到点击，WindowFromPoint 同样定位到下层进程。测试按钮随后已关闭；没有通过仅检查样式来推断点击成功。

托盘显示/隐藏、设置重开和退出处理程序通过进程内实际 ToolStripMenuItem.PerformClick 检查，退出后检查图标不可见、菜单销毁、计时器停止。系统托盘图标的人工右键点击未完成验证；桌面工具未列出可定位的任务栏窗口，因此没有使用猜测坐标操作系统托盘。

100% 缩放做了真实位置和点击验证；150%/200% 是纯定位计算测试。WM_SETTINGCHANGE 的重新锚定用真实窗口消息验证，但没有改变操作系统分辨率或缩放。自动隐藏任务栏、其他任务栏边缘、多屏、全屏自动隐藏、真实唤醒和长时间跨夜仍在待验范围。
