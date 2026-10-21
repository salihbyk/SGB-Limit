using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;
using TokenTray.Core;

namespace TokenTray;

internal static class IslandAlignmentProbe
{
    internal static async Task Run(string folder)
    {
        Directory.CreateDirectory(folder); var results = new List<object>();
        foreach (var name in new[] { "SideLeftTop", "SideLeftCenter", "SideLeftBottom", "SideRightTop", "SideRightCenter", "SideRightBottom" })
            if (!Enum.TryParse<IslandPosition>(name, out _)) throw new InvalidOperationException("Missing vertical edge position: " + name);
        var legacy = new[] { IslandPosition.TopCenter, IslandPosition.TopLeft, IslandPosition.TopRight, IslandPosition.BottomCenter, IslandPosition.BottomLeft, IslandPosition.BottomRight, IslandPosition.Free };
        if (legacy.Where((position, index) => (int)position != index).Any()) throw new InvalidOperationException("Legacy position IDs must remain stable.");
        await VerifyTransition(folder);
        VerifyHistoricalReading(folder);
        var settings = new Settings { Presentation = PresentationMode.DynamicIsland, Motion = false, ExpandOnHover = false };
        var island = new FloatingIsland(settings, () => MediaRenderer.Demo(DateTimeOffset.Now), _ => { }, () => { });
        try
        {
            foreach (var position in Enum.GetValues<IslandPosition>().Where(p => p != IslandPosition.Free))
            {
                settings.IslandPosition = position; island.Refresh(); island.Show();
                foreach (var expanded in new[] { false, true })
                {
                    island.SetExpanded(expanded, false); island.Place(); await Task.Delay(100);
                    var area = SystemParameters.WorkArea;
                    var expectedLeft = position is IslandPosition.TopLeft or IslandPosition.BottomLeft || position.IsLeftSide() ? area.Left
                        : position is IslandPosition.TopRight or IslandPosition.BottomRight || position.IsRightSide() ? area.Right - island.ActualWidth
                        : area.Left + (area.Width - island.ActualWidth) / 2;
                    var expectedTop = position is IslandPosition.BottomLeft or IslandPosition.BottomCenter or IslandPosition.BottomRight || position.IsSideBottom() ? area.Bottom - island.ActualHeight
                        : position.IsSideTop() ? area.Top : position.IsSide() ? area.Top + (area.Height - island.ActualHeight) / 2 : 0;
                    var dpi = VisualTreeHelper.GetDpi(island);
                    var native = island.ScreenBounds ?? throw new InvalidOperationException("No native island bounds.");
                    if (!Native.IsWindowVisible(new WindowInteropHelper(island).Handle) || Math.Abs(island.Left - expectedLeft) > 1 || Math.Abs(island.Top - expectedTop) > 1
                        || Math.Abs(native.Left / dpi.DpiScaleX - expectedLeft) > 2 || Math.Abs(native.Top / dpi.DpiScaleY - expectedTop) > 2)
                        throw new InvalidOperationException("Shown island lost its selected edge alignment: " + position);
                    if (position.IsSide() && !expanded && island.ActualHeight <= island.ActualWidth)
                        throw new InvalidOperationException("Side dock must stack its collapsed providers vertically: " + position);
                    if (position.IsSide() && expanded && island.ActualWidth <= island.ActualHeight)
                        throw new InvalidOperationException("Expanded side panel must remain upright and readable: " + position);
                    var corners = ((Border)island.Content).CornerRadius;
                    if (position.IsLeftSide() && (corners.TopLeft != 0 || corners.BottomLeft != 0)
                        || position.IsRightSide() && (corners.TopRight != 0 || corners.BottomRight != 0))
                        throw new InvalidOperationException("Side dock edge corners must be square.");
                    results.Add(new { position = position.ToString(), expanded, left = island.Left, top = island.Top, width = island.ActualWidth, height = island.ActualHeight, dpi = dpi.PixelsPerInchX, nativeLeft = native.Left, nativeTop = native.Top });
                    MediaRenderer.Save(MediaRenderer.Capture((FrameworkElement)island.Content, 1), Path.Combine(folder, position + (expanded ? "-expanded.png" : "-collapsed.png")));
                }
            }
        }
        finally { island.Close(); }
        File.WriteAllText(Path.Combine(folder, "alignment-result.json"), JsonSerializer.Serialize(new { passed = true, data = "synthetic", results }));
    }

    static async Task VerifyTransition(string folder)
    {
        var options = new Settings { Presentation = PresentationMode.DynamicIsland, Motion = true, ExpandOnHover = false };
        var island = new FloatingIsland(options, () => MediaRenderer.Demo(DateTimeOffset.Now), _ => { }, () => { }, () => true);
        try
        {
            var samples = new List<object>();
            foreach (var position in new[] { IslandPosition.TopCenter, IslandPosition.SideLeftTop, IslandPosition.SideLeftCenter, IslandPosition.SideLeftBottom, IslandPosition.SideRightTop, IslandPosition.SideRightCenter, IslandPosition.SideRightBottom })
            {
                options.IslandPosition = position; island.SetExpanded(false, false); island.Refresh(); island.Show(); await Task.Delay(80);
                var collapsed = new Size(island.ActualWidth, island.ActualHeight);
                island.SetExpanded(true);
                var immediate = new Size(island.Width, island.Height);
                if (Math.Abs(immediate.Width - collapsed.Width) > 2 || Math.Abs(immediate.Height - collapsed.Height) > 2)
                    throw new InvalidOperationException("Expansion exposed its final geometry before animation started.");
                await Task.Delay(55);
                var mid = new Size(island.Width, island.Height);
                island.Refresh();
                var refreshed = new Size(island.Width, island.Height);
                if (Math.Abs(refreshed.Width - mid.Width) > 2 || Math.Abs(refreshed.Height - mid.Height) > 2)
                    throw new InvalidOperationException("A data refresh snapped the in-flight island transition to its final dimensions.");
                island.SetExpanded(false);
                var reversed = new Size(island.Width, island.Height);
                if (Math.Abs(reversed.Width - mid.Width) > 2 || Math.Abs(reversed.Height - mid.Height) > 2)
                    throw new InvalidOperationException("Mid-flight reversal jumped instead of continuing from the visible geometry.");
                await Task.Delay(55);
                var reverseMid = new Size(island.Width, island.Height);
                island.SetExpanded(true); await Task.Delay(420);
                var full = new Size(island.ActualWidth, island.ActualHeight);
                if (Math.Abs(mid.Width - collapsed.Width) < 1 || Math.Abs(mid.Width - full.Width) < 1)
                    throw new InvalidOperationException("Hover geometry must have a genuine intermediate width.");
                if (!position.IsSide() && (Math.Abs(mid.Height - collapsed.Height) < 1 || Math.Abs(mid.Height - full.Height) < 1))
                    throw new InvalidOperationException("Top hover must animate both dimensions.");
                if (position.IsSide() && full.Height < collapsed.Height - 1)
                    throw new InvalidOperationException("Side expansion must keep its original hover footprint, or a pointer near the end oscillates between states.");
                if (island.TransitionRunning || island.MotionRunning) throw new InvalidOperationException("An idle island must have no animation timer or transition running.");
                samples.Add(new { position = position.ToString(), collapsed = collapsed.ToString(), immediate = immediate.ToString(), mid = mid.ToString(), refreshed = refreshed.ToString(), reversed = reversed.ToString(), reverseMid = reverseMid.ToString(), full = full.ToString() });
            }
            options.Motion = false; island.SetExpanded(false);
            if (island.TransitionRunning) throw new InvalidOperationException("Motion off must resize immediately.");
            File.WriteAllText(Path.Combine(folder, "transition-result.json"), JsonSerializer.Serialize(new { passed = true, durationMs = FloatingIsland.IslandTransitionDuration.TotalMilliseconds, samples }));
        }
        finally { island.Close(); }
    }

    static void VerifyHistoricalReading(string folder)
    {
        var now = DateTimeOffset.Now;
        var snapshot = new ProviderSnapshot(ProviderId.Antigravity, SnapshotStatus.Offline,
            [new(WindowKind.Model, "Claude", 44, now.AddMinutes(-2), null)], now.AddMinutes(-8), Stale: true, Account: "sample-account", Key: "Antigravity:history-source");
        var options = new Settings { Presentation = PresentationMode.DynamicIsland, Motion = false, IslandPosition = IslandPosition.SideLeftCenter };
        var opened = new List<string?>();
        var island = new FloatingIsland(options, () => [snapshot], source => opened.Add(source), () => { });
        IEnumerable<DependencyObject> Tree(DependencyObject root)
        {
            yield return root;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in Tree(VisualTreeHelper.GetChild(root, i))) yield return child;
        }
        try
        {
            foreach (var expanded in new[] { false, true })
            {
                island.Refresh(); island.SetExpanded(expanded, false); island.UpdateLayout();
                var visual = (FrameworkElement)island.Content;
                visual.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); visual.Arrange(new Rect(visual.DesiredSize)); visual.UpdateLayout();
                var texts = Tree(visual).OfType<TextBlock>().Select(t => t.Text).ToList();
                if (!texts.Contains(S.Percent(44) + "*")) throw new InvalidOperationException("Identified Antigravity history must retain a marked percentage in both island states.");
                var tips = Tree(visual).OfType<Button>().Select(b => b.ToolTip?.ToString() ?? "");
                if (!tips.Any(t => t.Contains("Son okuma ") && t.Contains("güncel değil"))) throw new InvalidOperationException("Historical island reading must disclose its read time and stale status.");
                var accountButton = Tree(visual).OfType<Button>().Single(b => AutomationProperties.GetName(b).Contains("limitleri aç"));
                accountButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (opened.LastOrDefault() != snapshot.Source) throw new InvalidOperationException("An island account click lost its exact source identity.");
                MediaRenderer.Save(MediaRenderer.Capture(visual, 1), Path.Combine(folder, expanded ? "history-expanded.png" : "history-collapsed.png"));
            }
            foreach (var invalid in new[] { snapshot with { Account = null }, snapshot with { Status = SnapshotStatus.NotSignedIn }, snapshot with { Status = SnapshotStatus.TokenExpired }, snapshot with { Provider = ProviderId.Codex } })
            {
                snapshot = invalid; island.Refresh();
                var visual = (FrameworkElement)island.Content;
                visual.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); visual.Arrange(new Rect(visual.DesiredSize)); visual.UpdateLayout();
                if (Tree(visual).OfType<TextBlock>().Any(t => t.Text == S.Percent(44) + "*")) throw new InvalidOperationException("Unknown or unauthenticated island readings must not inherit historical percentages.");
            }
        }
        finally { island.Close(); }
    }
}
