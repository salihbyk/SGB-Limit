using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace TokenTray;

internal static class IslandWindowProbe
{
    internal static async Task Run(string folder)
    {
        Directory.CreateDirectory(folder);
        var options = new Settings { Presentation = PresentationMode.DynamicIsland, IslandPosition = IslandPosition.TopCenter, ExpandOnHover = true, Motion = true };
        var island = new FloatingIsland(options, () => MediaRenderer.Demo(DateTimeOffset.Now), _ => { }, () => { }, () => true);
        island.Title = "SGB Limit · Hover kontrolü";
        island.ShowInTaskbar = true;
        // The probe accepts hover only; a test drag must never save over the user's settings.
        island.PreviewMouseLeftButtonDown += (_, e) => e.Handled = true;
        var entered = new TaskCompletionSource();
        var enters = 0; island.MouseEnter += (_, _) => { enters++; entered.TrySetResult(); };
        var leaves = 0; island.MouseLeave += (_, _) => leaves++;
        try
        {
            island.Refresh(); var initialHeight = island.Height; island.Show(); await Task.Delay(100);
            var handle = new WindowInteropHelper(island).Handle;
            var nativeVisible = Native.IsWindowVisible(handle);
            File.WriteAllText(Path.Combine(folder, "window-start.json"), JsonSerializer.Serialize(new { nativeVisible, wpfVisible = island.IsVisible, title = island.Title, workArea = SystemParameters.WorkArea.ToString(), top = island.Top }));
            if (!nativeVisible) throw new InvalidOperationException("WPF IsVisible is true but the actual HWND is hidden.");
            if (Math.Abs(island.Top) > 1) throw new InvalidOperationException("Top-docked island must touch the physical screen edge on its first visible frame.");
            await Task.WhenAny(entered.Task, Task.Delay(TimeSpan.FromSeconds(50)));
            await Task.Delay(400);
            if (enters == 0) throw new InvalidOperationException("Native mouse message did not reach WPF MouseEnter.");
            if (!island.Expanded || island.ActualHeight <= initialHeight + 20) throw new InvalidOperationException("Hover did not visibly expand the shown native window.");
            var expandedHeight = island.ActualHeight;
            MediaRenderer.Save(MediaRenderer.Capture((FrameworkElement)island.Content, 1), Path.Combine(folder, "hover-native-expanded.png"));
            if (((Border)island.Content).CornerRadius != new CornerRadius(0, 0, 14, 14)) throw new InvalidOperationException("Docked upper corners must be square.");
            island.Refresh(); await Task.Delay(350);
            if (island.ActualHeight < expandedHeight - 1) throw new InvalidOperationException("A data refresh collapsed the visible island.");
            Keyboard.ClearFocus(); options.IslandPosition = IslandPosition.Free; options.IslandLeft = SystemParameters.WorkArea.Right - island.ActualWidth - 20; options.IslandTop = SystemParameters.WorkArea.Bottom - island.ActualHeight - 30;
            // 240 ms leave grace plus the complete 350 ms spring, with dispatcher headroom.
            island.Place(); Mouse.Synchronize(); await Task.Delay(750);
            if (leaves == 0 || island.Expanded || island.ActualHeight > initialHeight + 1) throw new InvalidOperationException("A real pointer departure did not collapse the native window.");
            var collapsedHeight = island.ActualHeight;
            MediaRenderer.Save(MediaRenderer.Capture((FrameworkElement)island.Content, 1), Path.Combine(folder, "hover-native-collapsed.png"));
            File.WriteAllText(Path.Combine(folder, "window-hover.json"), JsonSerializer.Serialize(new { passed = true, nativeEnterEvents = enters, nativeLeaveEvents = leaves, initialHeight, expandedHeight, collapsedHeight, topDockedAt = 0, testAnimationAllowed = true, dataRefreshStayedExpanded = true }));
        }
        finally { island.Close(); }
        foreach (var style in new[] { WidgetStyle.Halo, WidgetStyle.Orbit, WidgetStyle.Comet })
        {
            var sample = new Settings { Presentation = PresentationMode.Floating, WidgetStyle = style, IslandPosition = IslandPosition.TopLeft };
            var orb = new FloatingIsland(sample, () => MediaRenderer.Demo(DateTimeOffset.Now), _ => { }, () => { });
            orb.PreviewMouseLeftButtonDown += (_, e) => e.Handled = true;
            try
            {
                orb.Refresh(); orb.Show(); await Task.Delay(60);
                if (!Native.IsWindowVisible(new WindowInteropHelper(orb).Handle) || Math.Abs(orb.ActualWidth - orb.ActualHeight) > 1) throw new InvalidOperationException("Every floating style must be a visible circle, not an oval.");
                MediaRenderer.Save(MediaRenderer.Capture((FrameworkElement)orb.Content, 1), Path.Combine(folder, "floating-native-" + style.ToString().ToLowerInvariant() + ".png"));
            }
            finally { orb.Close(); }
        }
        File.WriteAllText(Path.Combine(folder, "native-result.json"), JsonSerializer.Serialize(new { passed = true, shownWindowHover = true, nativePointerDeparture = true, dataRefreshStayedExpanded = true, animationPathTested = true, allFloatingStylesAreCircles = true, data = "synthetic sample, no account data" }));
    }
}
