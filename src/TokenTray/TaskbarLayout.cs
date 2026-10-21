using System.Windows.Media.Imaging;

namespace TokenTray;

internal sealed record TaskbarLayoutResult(BitmapSource Bitmap, int VisibleCount, TaskbarDensity Density);

internal static class TaskbarLayout
{
    internal static TaskbarLayoutResult? Fit(IReadOnlyList<WidgetRenderer.Row> rows, WidgetOptions options, Palette palette,
        double scale, double heightDip, bool hover, int availablePixels)
    {
        if (rows.Count == 0 || availablePixels <= 0) return null;
        // Preserve all accounts before dropping any. A long name must not hide another provider.
        foreach (var density in new[] { TaskbarDensity.Full, TaskbarDensity.Small, TaskbarDensity.Icons })
        {
            var bitmap = WidgetRenderer.Render(rows, options, palette, scale, heightDip, hover, out _, density: density);
            if (bitmap.PixelWidth <= availablePixels) return new(bitmap, rows.Count, density);
        }
        for (var count = rows.Count - 1; count >= 1; count--)
        {
            var bitmap = WidgetRenderer.Render(rows.Take(count).ToList(), options, palette, scale, heightDip, hover, out _, rows.Count - count, TaskbarDensity.Icons);
            if (bitmap.PixelWidth <= availablePixels) return new(bitmap, count, TaskbarDensity.Icons);
        }
        return null;
    }
}
