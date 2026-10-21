using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TokenTray.Core;

namespace TokenTray;

internal sealed partial class FloatingIsland
{
    OrbitalTrack? orbitTrack;

    void BuildOrbital()
    {
        motion.Stop();
        BeginAnimation(WidthProperty, null); BeginAnimation(HeightProperty, null);
        SizeToContent = SizeToContent.WidthAndHeight; Width = double.NaN; Height = double.NaN;
        var p = new Palette(true);
        surface.Background = Palette.Brush(p.Surface); surface.BorderBrush = Palette.Brush(p.Hairline);
        surface.BorderThickness = new Thickness(1); surface.CornerRadius = new CornerRadius(107); surface.Padding = new Thickness(6);
        var scale = Math.Clamp(settings.IslandScale, 0.85, 1.15); surface.LayoutTransform = new ScaleTransform(scale, scale);
        var snaps = data(); var shown = snaps.Take(6).ToList();
        var canvas = new Canvas { Width = 200, Height = 200 };
        orbitTrack = new OrbitalTrack(shown, settings) { Width = 200, Height = 200, IsHitTestVisible = false };
        canvas.Children.Add(orbitTrack);
        for (var i = 0; i < shown.Count; i++)
        {
            var snap = shown[i];
            var angle = -Math.PI / 2 + i * Math.PI * 2 / shown.Count;
            var box = new StackPanel { Width = 54, HorizontalAlignment = HorizontalAlignment.Center };
            var mark = ProviderMark.Element(snap.Provider, p.Hue(snap.Provider), 21); mark.HorizontalAlignment = HorizontalAlignment.Center; box.Children.Add(mark);
            var valid = snap.IsCurrent(DateTimeOffset.Now, TimeSpan.FromSeconds(Math.Max(120, settings.IntervalSeconds))) && snap.Binding is { } w && (w.ResetsAt is null || w.ResetsAt > DateTimeOffset.Now);
            var number = Text(valid ? S.Percent(settings.ShowRemaining ? snap.Binding!.RemainingPercent : snap.Binding!.UsedPercent) : "—", 12, valid ? p.Fill(snap.Provider, snap.Binding!, Pace.Of(snap.Binding!, DateTimeOffset.Now)) : p.TextMuted);
            number.FontWeight = FontWeights.SemiBold; number.HorizontalAlignment = HorizontalAlignment.Center; number.Margin = new Thickness(0, 4, 0, 0); box.Children.Add(number);
            var button = SurfaceButton(box, () => open(snap.Source));
            button.ToolTip = $"{S.FullLabel(snap)} · {snap.Plan ?? "Plan bildirilmedi"}\n{(snap.Binding is { } quota ? S.Window(quota) : S.Status(snap))}";
            AutomationProperties.SetName(button, S.FullLabel(snap) + ", " + number.Text + ", limitleri aç");
            Canvas.SetLeft(button, 100 + 59 * Math.Cos(angle) - 27); Canvas.SetTop(button, 100 + 59 * Math.Sin(angle) - 21); canvas.Children.Add(button);
        }
        var grip = new StackPanel { Width = 42, Cursor = Cursors.SizeAll, ToolTip = "Tut ve taşı · çift tıkla aç" };
        grip.Children.Add(new PulseMark { Width = 30, Height = 28, Ink = p.Accent, HorizontalAlignment = HorizontalAlignment.Center });
        var caption = Text(settings.ShowRemaining ? "kalan" : "kullanılan", 8.5, p.TextMuted); caption.HorizontalAlignment = HorizontalAlignment.Center; grip.Children.Add(caption);
        grip.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) { open(null); return; }
            try { DragMove(); settings.IslandPosition = IslandPosition.Free; settings.IslandLeft = Left; settings.IslandTop = Top; settings.Save(); Refresh(); }
            catch (InvalidOperationException) { }
        };
        Canvas.SetLeft(grip, 79); Canvas.SetTop(grip, 79); canvas.Children.Add(grip);
        if (snaps.Count > shown.Count || shown.Count == 0)
        {
            var more = ActionButton(shown.Count == 0 ? "Hesap bul" : "+" + (snaps.Count - shown.Count), p.Text, shown.Count == 0 ? changed : () => open(null));
            Canvas.SetLeft(more, 78); Canvas.SetTop(more, 118); canvas.Children.Add(more);
        }
        surface.Child = canvas;
        if (IsMouseOver && settings.Motion && settings.WidgetStyle == WidgetStyle.Comet && canAnimate()) motion.Start();
        Place();
    }
}

internal sealed class OrbitalTrack(IReadOnlyList<ProviderSnapshot> snapshots, Settings settings) : FrameworkElement
{
    internal double? Phase { get; set; }
    protected override void OnRender(DrawingContext dc)
    {
        var p = new Palette(true); var center = new Point(ActualWidth / 2, ActualHeight / 2); var radius = Math.Min(ActualWidth, ActualHeight) / 2 - 11;
        if (snapshots.Count == 0) { dc.DrawEllipse(null, new Pen(Palette.Brush(p.Track), 2), center, radius, radius); return; }
        var span = 360.0 / snapshots.Count; var sweep = span - 12;
        for (var i = 0; i < snapshots.Count; i++)
        {
            var snap = snapshots[i]; var quota = snap.Binding;
            var current = snap.IsCurrent(DateTimeOffset.Now, TimeSpan.FromSeconds(Math.Max(120, settings.IntervalSeconds))) && quota is not null && (quota.ResetsAt is null || quota.ResetsAt > DateTimeOffset.Now);
            var start = -90 + i * span - sweep / 2;
            Arc(dc, center, radius, start, sweep, p.Track, settings.WidgetStyle == WidgetStyle.Orbit);
            if (current) Arc(dc, center, radius, start, sweep * Math.Clamp((settings.ShowRemaining ? quota!.RemainingPercent : quota!.UsedPercent) / 100, 0, 1), p.Fill(snap.Provider, quota!, Pace.Of(quota!, DateTimeOffset.Now)), settings.WidgetStyle == WidgetStyle.Orbit);
        }
        if (settings.WidgetStyle == WidgetStyle.Comet && Phase is { } phase)
        {
            var angle = phase * 2 * Math.PI;
            dc.DrawEllipse(Palette.Brush(p.Text), null, new Point(center.X + (radius + 5) * Math.Sin(angle), center.Y - (radius + 5) * Math.Cos(angle)), 2, 2);
        }
    }

    static void Arc(DrawingContext dc, Point center, double radius, double start, double sweep, Color color, bool dotted)
    {
        if (sweep <= 0) return;
        Point At(double angle) => new(center.X + radius * Math.Cos(angle * Math.PI / 180), center.Y + radius * Math.Sin(angle * Math.PI / 180));
        var path = new StreamGeometry(); using (var g = path.Open()) { g.BeginFigure(At(start), false, false); g.ArcTo(At(start + sweep), new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise, true, false); }
        var pen = new Pen(Palette.Brush(color), 2.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (dotted) { pen.DashStyle = new DashStyle([0.1, 2.1], 0); pen.DashCap = PenLineCap.Round; }
        dc.DrawGeometry(null, pen, path);
    }
}
