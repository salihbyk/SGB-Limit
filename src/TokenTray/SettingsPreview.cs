using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TokenTray.Core;

namespace TokenTray;

internal partial class FlyoutWindow
{
    static IReadOnlyList<WidgetRenderer.Row> MakePreviewRows()
    {
        var now = DateTimeOffset.Now;
        return new[] { ProviderId.Codex, ProviderId.Claude }.Select((id, i) => new WidgetRenderer.Row(id,
            Reading.Of(new UsageWindow(WindowKind.Session, "5h", 36 + i * 21, now.AddHours(3), TimeSpan.FromHours(5)), false, now),
            null, null, null)).ToList();
    }

    Image TaskbarPreview(WidgetStyle style, double width = 158)
    {
        var bitmap = WidgetRenderer.Render(MakePreviewRows(), new(style, true, true, false, false, SignalBars: draft?.SignalBars ?? 5), new Palette(true), 1.5, 48, false, out _);
        return new Image { Source = bitmap, Width = width, Height = 34, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left, ToolTip = "Örnek veriler" };
    }

    Image PresentationPreview(PresentationMode mode)
    {
        if (mode == PresentationMode.Taskbar) return TaskbarPreview(WidgetStyle.Identity, 152);
        var sample = new Settings { Presentation = mode, WidgetStyle = WidgetStyle.Orbit, IslandSlots = 3 };
        var island = new FloatingIsland(sample, () => MediaRenderer.Demo(DateTimeOffset.Now), _ => { }, () => { });
        try
        {
            island.Refresh();
            var visual = (FrameworkElement)island.Content;
            visual.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); visual.Arrange(new Rect(visual.DesiredSize)); visual.UpdateLayout();
            return new Image { Source = MediaRenderer.Capture(visual, 1.5), Width = 152, Height = mode == PresentationMode.Floating ? 56 : 42, Stretch = Stretch.Uniform, ToolTip = "Örnek veriler" };
        }
        finally { island.Close(); }
    }
}
