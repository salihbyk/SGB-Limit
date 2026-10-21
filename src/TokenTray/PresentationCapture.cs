using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TokenTray;

/// <summary>Native presentation scenes. Uses synthetic data before normal account discovery starts.</summary>
internal static class PresentationCapture
{
    static void ReadOnly(Window window)
    {
        window.IsHitTestVisible = false;
        window.Focusable = false;
        window.ShowActivated = false;
        window.PreviewMouseDown += (_, e) => e.Handled = true;
        window.PreviewKeyDown += (_, e) => e.Handled = true;
    }
    internal static async Task Run(string folder, string wallpaper)
    {
        Directory.CreateDirectory(folder);
        Palette.UiOverride = true;
        var area = SystemParameters.WorkArea;
        var picture = new BitmapImage(new Uri(Path.GetFullPath(wallpaper)));
        // A temporary presentation surface covers desktop icons and other windows. It never
        // changes wallpaper, desktop settings, user windows, credentials or saved preferences.
        var stage = new Window {
            Title = "SGB Limit · Sunum çekimi", WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true,
            Left = area.Left, Top = area.Top, Width = area.Width, Height = area.Height,
            Content = new Image { Source = picture, Stretch = Stretch.UniformToFill },
            ShowActivated = true, Background = Brushes.Black
        };
        var now = DateTimeOffset.Now;
        var samples = MediaRenderer.Demo(now);
        var shots = new List<object>();
        void Capture(string name, FloatingIsland? island = null)
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
            var width = screen.Width;
            var height = screen.Height;
            using var bitmap = new System.Drawing.Bitmap(width, height);
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                graphics.CopyFromScreen(screen.Left, screen.Top, 0, 0, bitmap.Size);
            bitmap.Save(Path.Combine(folder, name + "-desktop.png"));
            if (island is not null) MediaRenderer.Save(MediaRenderer.Capture((FrameworkElement)island.Content, 2), Path.Combine(folder, name + "-ui.png"));
            shots.Add(new { name, actualWindowsCapture = true, data = "synthetic", width, height });
        }
        try
        {
            stage.Show(); await Task.Delay(250);
            foreach (var (name, position, expanded) in new[] {
                ("top-compact", IslandPosition.TopCenter, false),
                ("top-expanded", IslandPosition.TopCenter, true),
                ("left-vertical", IslandPosition.SideLeftCenter, false),
                ("right-expanded", IslandPosition.SideRightCenter, true) })
            {
                var settings = new Settings { Motion = false, ExpandOnHover = false, IslandPosition = position, ShowEmail = false };
                var island = new FloatingIsland(settings, () => samples, _ => { }, () => { });
                ReadOnly(island);
                try { island.Refresh(); island.Show(); island.SetExpanded(expanded, false); await Task.Delay(180); Capture(name, island); }
                finally { island.Close(); }
            }
            foreach (var style in new[] { WidgetStyle.Halo, WidgetStyle.Orbit, WidgetStyle.Comet })
            {
                var settings = new Settings { Presentation = PresentationMode.Floating, WidgetStyle = style, Motion = false,
                    IslandPosition = IslandPosition.Free, IslandLeft = area.Width * .61, IslandTop = area.Height * .37, ShowEmail = false };
                var island = new FloatingIsland(settings, () => samples, _ => { }, () => { });
                ReadOnly(island);
                try { island.Refresh(); island.Show(); await Task.Delay(160); Capture("floating-" + style.ToString().ToLowerInvariant(), island); }
                finally { island.Close(); }
            }
            var rowData = TaskbarReadings.FromSnapshots(samples, false, now, TimeSpan.FromMinutes(2));
            foreach (var style in new[] { WidgetStyle.Identity, WidgetStyle.Signal })
            {
                using var occupancy = new TaskbarOccupancy();
                for (var i = 0; i < 40 && !occupancy.Ready; i++) await Task.Delay(100);
                if (!occupancy.Ready) throw new InvalidOperationException("Taskbar occupancy unavailable; presentation cannot claim safe placement.");
                var options = new WidgetOptions(style, true, true, true, true, SignalBars: 5);
                using var widget = new TaskbarWidget { Options = () => options, Occupied = () => occupancy.Occupied };
                widget.Show(() => rowData); await Task.Delay(250);
                if (widget.ScreenBounds is null) throw new InvalidOperationException("Taskbar has insufficient room for the capture.");
                var name = "taskbar-" + style.ToString().ToLowerInvariant();
                Capture(name);
                // Print close-ups use the same native vector renderer at 4x density, rather
                // than enlarging the screen's 1x taskbar pixels into a blurry marketing image.
                var rendered = WidgetRenderer.Render(rowData, options, new Palette(true), 4, 48, false, out _);
                MediaRenderer.Save(rendered, Path.Combine(folder, name + "-ui.png"));
            }
            foreach (var step in new[] { -1, 0, 2 })
            {
                var settings = new Settings { ShowEmail = false, IslandPosition = IslandPosition.SideLeftCenter };
                var panel = FlyoutWindow.CreatePresentation(samples, settings, step, Path.Combine(folder, "ignored-capture-settings.json"));
                ReadOnly(panel); panel.Show();
                try {
                    await Task.Delay(100); panel.Left = area.Width * .56; panel.Top = Math.Max(30, (area.Height - panel.ActualHeight) / 2);
                    await Task.Delay(100);
                    var name = step < 0 ? "panel" : "settings-" + step;
                    Capture(name);
                    MediaRenderer.Save(MediaRenderer.Capture((FrameworkElement)panel.Content, 2), Path.Combine(folder, name + "-ui.png"));
                } finally { panel.Close(); }
            }
            File.WriteAllText(Path.Combine(folder, "provenance.json"), JsonSerializer.Serialize(new {
                application = "SGB Limit 2.4", data = "synthetic example quotas; no account discovery or network calls",
                capture = "Actual native WPF windows and embedded taskbar widget, captured using CopyFromScreen on a temporary wallpaper presentation surface",
                desktop = "Desktop icons and other windows covered during capture; original wallpaper, icon preferences and other windows never changed",
                wallpaper = "Original image generated with OpenAI ImageGen", shots
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { stage.Close(); }
    }
}

internal partial class FlyoutWindow
{
    internal static FlyoutWindow CreatePresentation(IReadOnlyList<TokenTray.Core.ProviderSnapshot> samples, Settings settings, int step, string isolatedSettingsPath)
    {
        var panel = new FlyoutWindow(() => samples, () => [], settings, () => { }, () => { }, () => { })
            { demoCapture = true, pinned = true, verificationSettingsPath = isolatedSettingsPath };
        if (step < 0) panel.BuildCompact();
        else { panel.draft = settings.Copy(); panel.viewStep = step; panel.settingsTab = 0; panel.BuildPulseSettings(); }
        return panel;
    }
}
