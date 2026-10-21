using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using TokenTray.Core;

namespace TokenTray;

internal partial class FlyoutWindow
{
    FrameworkElement DeskRow(ProviderSnapshot snap, DateTimeOffset now)
    {
        var current = Current(snap, now);
        var row = new Grid { Margin = new Thickness(0, 0, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        var identity = new StackPanel { Margin = new Thickness(0, 3, 16, 0) };
        var heading = new StackPanel { Orientation = Orientation.Horizontal };
        var mark = ProviderMark.Element(snap.Provider, palette.Hue(snap.Provider), 18);
        mark.Margin = new Thickness(0, 1, 9, 0); heading.Children.Add(mark);
        heading.Children.Add(new TextBlock { Text = S.Label(snap), FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Palette.Brush(palette.Text) });
        identity.Children.Add(heading);
        var plan = PulseText(snap.Plan ?? (snap.Status == SnapshotStatus.Loading ? "Hesap okunuyor" : "Hesap bağlantısı"), 11, palette.TextMuted);
        plan.Margin = new Thickness(27, 6, 0, 0); identity.Children.Add(plan);
        var used = snap.Binding?.UsedPercent;
        var status = !current ? snap.Status == SnapshotStatus.Loading ? "Bağlanıyor" : "Doğrulanamadı" : used >= 100 ? "Limit doldu" : used >= 99 ? "Sınırda" : used >= 80 ? "Azalıyor" : "Güncel";
        var tone = !current ? palette.TextMuted : used >= 99 ? palette.RedText : used >= 80 ? palette.AmberText : palette.Accent;
        var statusText = PulseText(status, 11, tone); statusText.Margin = new Thickness(27, 10, 0, 0); identity.Children.Add(statusText);
        row.Children.Add(identity);
        var detail = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };
        Grid.SetColumn(detail, 1); row.Children.Add(detail);
        FrameworkElement Quota(UsageWindow window)
        {
            var rd = Reading.Of(window, settings.ShowRemaining, now);
            var stack = new StackPanel { Margin = new Thickness(0, 0, 20, 0) };
            var label = PulseText(S.Window(window), 11, palette.TextMuted);
            label.TextTrimming = TextTrimming.CharacterEllipsis; label.TextWrapping = TextWrapping.NoWrap; label.ToolTip = S.Window(window);
            stack.Children.Add(label);
            var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 4) };
            var color = current && !rd.PastReset ? window.UsedPercent >= 99 ? palette.RedText : palette.Text : palette.TextMuted;
            line.Children.Add(new TextBlock { Text = rd.PastReset ? "—" : S.Percent(rd.Percent), FontSize = 28, FontWeight = FontWeights.SemiBold, Foreground = Palette.Brush(color) });
            line.Children.Add(new TextBlock { Text = rd.PastReset ? "yenileniyor" : !current ? "son okuma" : settings.ShowRemaining ? "kalan" : "kullanılan", FontSize = 10.5, Foreground = Palette.Brush(palette.TextMuted), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(7, 0, 0, 5) });
            stack.Children.Add(line);
            var rail = new QuotaRail { Height = 7, Fraction = rd.PastReset ? 0 : rd.Fraction, Ink = current && !rd.PastReset ? palette.Fill(snap.Provider, window, rd.Pace) : palette.TextMuted, Track = palette.Track, Margin = new Thickness(0, 0, 0, 7) };
            AutomationProperties.SetName(rail, $"{S.Label(snap)} {S.Window(window)} {S.Percent(rd.Percent)}"); stack.Children.Add(rail);
            stack.Children.Add(PulseText(window.ResetsAt is { } reset ? rd.PastReset ? "Yeni ölçüm bekleniyor" : $"{reset.ToLocalTime():dd MMM HH:mm} · {S.ShortDuration(reset - now)}" : "Yenilenme bildirilmedi", 10.5, palette.TextMuted));
            return stack;
        }
        var main = snap.MainWindows;
        var meters = new System.Windows.Controls.Primitives.UniformGrid { Columns = Math.Max(1, main.Count) };
        foreach (var window in main) meters.Children.Add(Quota(window));
        detail.Children.Add(meters);
        if (current && used is >= 99 and < 100)
        {
            var caution = PulseText("Küçük bakiye yeni bir isteğe yetmeyebilir.", 10.5, palette.RedText); caution.Margin = new Thickness(0, 7, 0, 0); detail.Children.Add(caution);
        }
        var extra = snap.Windows.Except(main).ToList();
        if (extra.Count > 0)
        {
            var contents = new StackPanel { Margin = new Thickness(0, 12, 0, 4) };
            foreach (var window in extra) { var item = Quota(window); item.Margin = new Thickness(0, 0, 0, 14); contents.Children.Add(item); }
            var expander = new Expander { Header = $"Diğer {extra.Count} limit", Content = contents, Foreground = Palette.Brush(palette.TextMuted), FontSize = 11, Margin = new Thickness(0, 10, 0, 0), IsExpanded = expandedSources.Contains(snap.Source) };
            expander.Expanded += (_, _) => expandedSources.Add(snap.Source);
            expander.Collapsed += (_, _) => expandedSources.Remove(snap.Source);
            detail.Children.Add(expander);
        }
        if (!current || snap.Windows.Count == 0)
        {
            var message = snap.Message ?? S.Status(snap);
            if (string.IsNullOrWhiteSpace(message)) message = "Güncel veri alınamadı; son okuma gösteriliyor.";
            detail.Children.Add(PulseText(message, 12, palette.TextMuted));
            var retry = new Button { Content = "Yeniden kontrol et", Style = (Style)FindResource("TextButton"), Foreground = Palette.Brush(palette.Accent), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(-10, 4, 0, 0) };
            retry.Click += (_, _) => refresh(); detail.Children.Add(retry);
        }
        if (settings.ShowEmail && snap.Email is { } email) { var e = PulseText(email, 10.5, palette.TextMuted); e.Margin = new Thickness(0, 9, 0, 0); detail.Children.Add(e); }
        return new Border { Child = row, BorderBrush = Palette.Brush(palette.Hairline), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 19, 0, 19) };
    }
}

internal sealed class QuotaRail : FrameworkElement
{
    public double Fraction { get; set; }
    public Color Ink { get; set; }
    public Color Track { get; set; }
    protected override void OnRender(DrawingContext dc)
    {
        const int count = 24;
        var width = Math.Max(0.5, (ActualWidth - (count - 1) * 2) / count);
        for (var i = 0; i < count; i++)
        {
            var rect = new Rect(i * (width + 2), 0, width, ActualHeight);
            dc.DrawRoundedRectangle(Palette.Brush(Track), null, rect, 1, 1);
            var fraction = Math.Clamp(Fraction * count - i, 0, 1);
            if (fraction > 0) dc.DrawRoundedRectangle(Palette.Brush(Ink), null, new Rect(rect.X, 0, width * fraction, ActualHeight), 1, 1);
        }
    }
}
