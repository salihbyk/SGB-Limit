using System.Windows;
using System.Windows.Media;

namespace TokenTray;

internal sealed class PulsePin : FrameworkElement
{
    public Color Ink { get; set; }
    public bool Pinned { get; set; }
    protected override void OnRender(DrawingContext dc)
    {
        var pen = new Pen(Palette.Brush(Ink), 1.3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        if (!Pinned) dc.PushTransform(new RotateTransform(35, 8, 8));
        var pin = new StreamGeometry();
        using (var g = pin.Open())
        {
            g.BeginFigure(new Point(5, 1.5), false, true);
            g.PolyLineTo([new(11, 1.5), new(10.5, 7), new(13, 10), new(3, 10), new(5.5, 7)], true, false);
        }
        dc.DrawGeometry(null, pen, pin);
        dc.DrawLine(pen, new Point(8, 10), new Point(8, 15));
        if (!Pinned) dc.Pop();
    }
}
