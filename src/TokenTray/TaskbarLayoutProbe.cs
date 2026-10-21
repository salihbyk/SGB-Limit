using System.IO;
using TokenTray.Core;

namespace TokenTray;

internal static class TaskbarLayoutProbe
{
    internal static void Verify(string folder)
    {
        var now = DateTimeOffset.Now;
        var rows = new[] { ProviderId.Claude, ProviderId.Codex, ProviderId.Antigravity, ProviderId.Grok, ProviderId.Cursor }
            .Select((id, i) => new WidgetRenderer.Row(id,
                Reading.Of(new UsageWindow(WindowKind.Session, "5h", i == 0 ? 100 : 12.5 + i, now.AddHours(3), TimeSpan.FromHours(5)), false, now),
                Reading.Of(new UsageWindow(WindowKind.Weekly, "Weekly", 40 + i, now.AddDays(3), TimeSpan.FromDays(7)), false, now), null, null)).ToList();
        var options = new WidgetOptions(WidgetStyle.Identity, true, true, true, true);
        var palette = new Palette(true);
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        Directory.CreateDirectory(folder);
        foreach (var scale in new[] { 1.0, 1.25, 1.5, 2.0 })
        {
            var narrow = TaskbarLayout.Fit(rows, options, palette, scale, 48, false, (int)(310 * scale));
            Check(narrow is { VisibleCount: 5 }, "Narrow taskbar must keep all five accounts as icons and percentages before hiding any account.");
            Check(narrow!.Bitmap.PixelWidth <= 310 * scale, "Layout must stay within its physical pixel budget at every DPI.");
            var wide = TaskbarLayout.Fit(rows, options, palette, scale, 48, false, (int)(1100 * scale));
            Check(wide is { VisibleCount: 5 } && wide.Bitmap.PixelWidth > narrow.Bitmap.PixelWidth, "Wide layout restores account names and details.");
            var medium = TaskbarLayout.Fit(rows, options, palette, scale, 48, false, (int)(wide!.Bitmap.PixelWidth * 0.92));
            Check(medium is { VisibleCount: 5, Density: TaskbarDensity.Small }, "Moderate crowding first shrinks the named layout.");
            Check(narrow.Density == TaskbarDensity.Icons, "Tighter budgets switch to icons and one binding percentage.");
            var tiny = TaskbarLayout.Fit(rows, options, palette, scale, 48, false, (int)(140 * scale));
            Check(tiny is { VisibleCount: > 0 and < 5 } && tiny.Bitmap.PixelWidth <= 140 * scale, "Very narrow layout keeps a fitting overflow entry.");
            Check(TaskbarLayout.Fit(rows, options, palette, scale, 48, false, 1) is null, "No room means no overlay on Windows buttons.");
            Check(TaskbarLayout.Fit([], options, palette, scale, 48, false, 310) is null, "Empty provider list must not leave a ghost widget.");
            if (scale == 1)
            {
                MediaRenderer.Save(wide!.Bitmap, Path.Combine(folder, "taskbar-wide.png"));
                MediaRenderer.Save(medium!.Bitmap, Path.Combine(folder, "taskbar-small.png"));
                MediaRenderer.Save(narrow.Bitmap, Path.Combine(folder, "taskbar-icons.png"));
                MediaRenderer.Save(tiny!.Bitmap, Path.Combine(folder, "taskbar-overflow.png"));
            }
        }
        var centered = TaskbarOccupancy.CenteredBounds(new() { Left = 0, Top = 1032, Right = 1920, Bottom = 1080 },
            new() { Left = 627, Top = 1032, Right = 672, Bottom = 1080 });
        Check(centered is { Left: 627, Right: 1293 }, "Protect the full centered group including search, beyond the legacy task-list rectangle.");
        var offsetMonitor = TaskbarOccupancy.CenteredBounds(new() { Left = -1920, Top = 1032, Right = 0, Bottom = 1080 },
            new() { Left = -1293, Top = 1032, Right = -1248, Bottom = 1080 });
        Check(offsetMonitor is { Left: -1293, Right: -627 }, "Centered bounds must respect nonzero monitor origins.");
        Check(TaskbarOccupancy.CenteredBounds(new() { Left = 0, Top = 0, Right = 1920, Bottom = 48 },
            new() { Left = 0, Top = 0, Right = 48, Bottom = 48 }) is null, "Never mirror a left-aligned Start button into a false centered group.");
        var partial = TaskbarOccupancy.ProtectSample(new() { Left = 0, Top = 0, Right = 1920, Bottom = 48 },
            new() { Left = 1747, Top = 0, Right = 1920, Bottom = 48 },
            [new() { Left = 0, Top = 0, Right = 48, Bottom = 48 }], [], false);
        var budgetAfterPartialSample = 1741 - partial.Max(r => r.Right) - 8;
        Check(TaskbarLayout.Fit(rows, options, palette, 1, 48, false, budgetAfterPartialSample) is null,
            "A partial Start-only UIA sample must never place the widget over unknown application buttons.");
        File.WriteAllText(Path.Combine(folder, "taskbar-result.json"), "{\"passed\":true,\"accounts\":5,\"dpi\":[100,125,150,200],\"checks\":[\"wide\",\"small\",\"icons\",\"overflow\",\"no room\",\"empty\",\"centered bounds\",\"monitor origin\"]}");
    }

    internal static async Task VerifyNative(string folder)
    {
        Directory.CreateDirectory(folder);
        var now = DateTimeOffset.Now;
        var rows = MediaRenderer.Demo(now).Select(s => new WidgetRenderer.Row(s.Provider,
            s.Get(WindowKind.Session) is { } q ? Reading.Of(q, false, now) : null,
            s.Long is { } w ? Reading.Of(w, false, now) : null, null, null)).ToList();
        using var occupancy = new TaskbarOccupancy();
        for (var i = 0; i < 50 && !occupancy.Ready; i++) await Task.Delay(100);
        if (!occupancy.Ready) throw new InvalidOperationException("No reliable live taskbar sample.");
        var taskbar = Native.FindWindow("Shell_TrayWnd", null);
        var tray = Native.FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        Native.GetWindowRect(taskbar, out var tb); Native.GetWindowRect(tray, out var tr);
        var scale = Native.GetDpiForWindow(taskbar) / 96.0;
        var budget = int.MaxValue;
        using var widget = new TaskbarWidget { Options = () => new(WidgetStyle.Identity, true, true, true, true), Occupied = () =>
            occupancy.Occupied.Concat(budget == int.MaxValue ? [] : new[] { new Native.RECT { Left = tb.Left, Top = tb.Top, Right = tr.Left - (int)(6 * scale) - budget - (int)(8 * scale), Bottom = tb.Bottom } }).ToArray() };
        widget.Show(() => rows);
        var results = new List<object>();
        foreach (var (label, dip) in new[] { ("natural", int.MaxValue), ("icons", 310), ("crowded", 1), ("restored", int.MaxValue) })
        {
            budget = dip == int.MaxValue ? dip : (int)(dip * scale);
            widget.Redraw(); await Task.Delay(350);
            var bounds = widget.ScreenBounds;
            if (dip == 1 && bounds is not null) throw new InvalidOperationException("Crowded widget must step aside.");
            if (dip != 1 && bounds is null) throw new InvalidOperationException("Expected a visible taskbar widget.");
            if (bounds is { } b && (b.Left < tb.Left || b.Right >= tr.Left || TaskbarOccupancy.Overlaps(b, occupancy.Occupied, (int)(4 * scale))))
                throw new InvalidOperationException("Widget overlaps protected taskbar space.");
            using var image = new System.Drawing.Bitmap(tb.Width, tb.Height);
            using (var graphics = System.Drawing.Graphics.FromImage(image)) graphics.CopyFromScreen(tb.Left, tb.Top, 0, 0, image.Size);
            image.Save(Path.Combine(folder, "native-taskbar-" + label + ".png"));
            results.Add(new { label, visible = bounds is not null, left = bounds?.Left, right = bounds?.Right });
        }
        File.WriteAllText(Path.Combine(folder, "native-taskbar-result.json"), System.Text.Json.JsonSerializer.Serialize(new { passed = true, data = "synthetic", results }));
    }
}
