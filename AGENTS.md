# Sunny Bar — Coding Agent Instructions

This repository builds **Sunny Bar**, a lightweight Windows desktop timeline overlay positioned above the Windows taskbar.

## Primary source of truth

Read `docs/PRODUCT_PLAN.md` before coding. Follow the milestones in order. Complete and validate a milestone before starting the next one. If the file is not yet placed there, ask the repository owner to copy the provided `SunnyBar_项目计划与需求说明.md` to `docs/PRODUCT_PLAN.md`.

## Technology

- C#, .NET 10, WPF (Windows desktop), XAML.
- xUnit for pure time and data logic.
- `System.Text.Json` for local settings.
- Minimize dependencies. No backend, no required login, no telemetry, no admin permissions.

## Required behaviors

- Operational day: **04:00 local time → 04:00 next local calendar day**.
- Before 04:00 the current timeline belongs to the previous calendar date.
- All positions (now, sunrise, sunset, custom markers, future event intervals) must be computed through the same time-axis mapping service.
- Use offset-aware instants for DST correctness, and injectable clocks for deterministic tests.
- Native Windows overlay is a borderless transparent WPF window, not an injected taskbar component.
- Default mouse pass-through; tray icon provides settings, hide/show, and exit.
- Only primary monitor, bottom non-auto-hidden taskbar guaranteed for MVP.
- Sunrise/sunset computation is offline. No automatic geolocation or network requirement.
- Config at `%LocalAppData%\SunnyBar\settings.json`. Default to the Beijing preset as explicitly requested by the user on 2026-10-27; show it clearly, never imply automatic geolocation, and preserve saved locations (including an explicit null location).
- The user explicitly authorized schedule work on 2026-10-27 after the public 0.2.0 preview and Windows CI passed. Implement single local events first; keep unresolved M5 desktop checks documented, defer recurrence, and retain default mouse pass-through and concise hover information.

## Working method

1. Before changes: explain goal, touched files, and acceptance criteria for this milestone.
2. Implement testable services separately from WPF views; avoid giant code-behind files.
3. Add and run relevant tests; report what was actually executed.
4. Never claim graphical behavior was tested without a Windows GUI environment.
5. Keep a short changelog and note known limitations (DPI, autohide taskbar, fullscreen, multi-monitor).
6. Preserve user settings across migrations and handle corrupted config gracefully.
7. After changes: summarize files, commands, results, unresolved issues, and next milestone.

## First assignment

Implement **M0 and M1 only**: repository structure, WPF build, xUnit suite, time-axis service and a solid-color timeline preview that shows current position. Verify 03:59, 04:00, 16:00, next-day 00:00, next-day 04:00 and DST boundaries before implementing sunrise/sunset.
