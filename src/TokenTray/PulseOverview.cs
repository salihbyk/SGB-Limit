using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using TokenTray.Core;

namespace TokenTray;

internal partial class FlyoutWindow
{
    bool demoCapture;
    readonly HashSet<string> expandedSources = [];

    static TextBlock PulseText(string text, double size, Color color) => new()
    {
        Text = text, FontSize = size, Foreground = Palette.Brush(color), TextWrapping = TextWrapping.Wrap,
    };

    bool Current(ProviderSnapshot snap, DateTimeOffset now) => snap.IsCurrent(now, TimeSpan.FromSeconds(Math.Max(120, settings.IntervalSeconds)));

    FrameworkElement PulseHeader(IReadOnlyList<ProviderSnapshot> snaps, DateTimeOffset now)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        var heading = new DockPanel { LastChildFill = true };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        var pin = new Button { Content = new PulsePin { Width = 16, Height = 16, Ink = palette.Text, Pinned = pinned }, Style = (Style)FindResource("IconButton"), ToolTip = pinned ? "Sabitlemeyi kaldır" : "Paneli açık tut" };
        AutomationProperties.SetName(pin, (string)pin.ToolTip);
        pin.Click += (_, _) => { pinned = !pinned; Build(false); };
        var close = new Button { Content = "", Style = (Style)FindResource("IconButton"), ToolTip = "Paneli kapat" };
        AutomationProperties.SetName(close, "Paneli kapat");
        close.Click += (_, _) => HideFlyout();
        actions.Children.Add(pin);
        actions.Children.Add(close);
        DockPanel.SetDock(actions, Dock.Right);
        heading.Children.Add(actions);
        var title = new StackPanel { Orientation = Orientation.Horizontal };
        title.Children.Add(new TextBlock { Text = "SGB", FontSize = 28, FontWeight = FontWeights.Bold, Foreground = Palette.Brush(palette.Accent), Margin = new Thickness(0, 0, 9, 0) });
        title.Children.Add(new TextBlock { Text = "SGB Limit", FontSize = 28, FontWeight = FontWeights.Light, Foreground = Palette.Brush(palette.Text) });
        heading.Children.Add(title);
        panel.Children.Add(heading);
        var intro = PulseText(demoCapture ? "Örnek veriler • Hesaplarından okunmadı" : "Hesapların ve kullanım hakların, aynı yerde.", 13, palette.TextMuted);
        intro.Margin = new Thickness(0, 6, 0, 20);
        panel.Children.Add(intro);
        var toolbar = new DockPanel { LastChildFill = true };
        var segments = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var remaining in new[] { false, true })
        {
            var active = settings.ShowRemaining == remaining;
            var button = new Button { Content = remaining ? "Kalan" : "Kullanılan", Style = (Style)FindResource("TextButton"), Height = 30, Foreground = Palette.Brush(active ? palette.Text : palette.TextMuted), FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal };
            AutomationProperties.SetName(button, remaining ? "Kalan hakkı göster" : "Kullanılan hakkı göster");
            button.Click += (_, _) => { settings.ShowRemaining = remaining; settingsChanged(); Build(false); };
            segments.Children.Add(new Border { Background = active ? Palette.Brush(palette.Track) : Brushes.Transparent, CornerRadius = new CornerRadius(6), Child = button });
        }
        var mode = new Border { Background = Palette.Brush(palette.Raised), CornerRadius = new CornerRadius(8), Padding = new Thickness(3), Child = segments };
        DockPanel.SetDock(mode, Dock.Right);
        toolbar.Children.Add(mode);
        var live = snaps.Count(s => Current(s, now));
        var summary = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var headline = PulseText($"{live} / {snaps.Count} bağlantı güncel", 13, palette.Text);
        headline.FontWeight = FontWeights.SemiBold;
        summary.Children.Add(headline);
        var last = snaps.Where(s => s.Status != SnapshotStatus.Loading).Select(s => s.FetchedAt).DefaultIfEmpty().Max();
        summary.Children.Add(PulseText(last == default ? "İlk ölçüm bekleniyor" : $"Son kontrol  {last.ToLocalTime():HH:mm:ss}", 11, palette.TextMuted));
        toolbar.Children.Add(summary);
        panel.Children.Add(toolbar);
        return panel;
    }

    FrameworkElement PulseRow(ProviderSnapshot snap, DateTimeOffset now, bool animate)
    {
        var content = new StackPanel();
        var current = Current(snap, now);
        var expired = snap.Windows.Count > 0 && snap.Windows.All(w => w.ResetsAt is { } r && r <= now);
        var hue = palette.Hue(snap.Provider);
        var top = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 18) };
        var status = snap.Status == SnapshotStatus.Loading ? "Bağlanıyor" : expired ? "Yenileniyor" : current ? "Güncel" : snap.Windows.Count > 0 ? "Son okuma" : snap.Status == SnapshotStatus.TokenExpired ? "Oturum doldu" : "Bağlantı yok";
        var state = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8, 3, 0, 0) };
        state.Children.Add(new System.Windows.Shapes.Ellipse { Width = 5, Height = 5, Fill = Palette.Brush(current && !expired ? palette.Accent : snap.Status == SnapshotStatus.Loading ? palette.TextMuted : palette.Amber), Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
        state.Children.Add(PulseText(status, 10.5, palette.TextMuted));
        DockPanel.SetDock(state, Dock.Right);
        top.Children.Add(state);
        var identity = new DockPanel { LastChildFill = true };
        var mark = ProviderMark.Element(snap.Provider, hue, 20);
        mark.Margin = new Thickness(0, 2, 9, 0);
        mark.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(mark, Dock.Left);
        identity.Children.Add(mark);
        var names = new StackPanel();
        names.Children.Add(new TextBlock { Text = S.Label(snap), FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Palette.Brush(palette.Text), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = S.FullLabel(snap) });
        names.Children.Add(PulseText(snap.Plan ?? "Hesap bağlantısı", 11, palette.TextMuted));
        identity.Children.Add(names);
        top.Children.Add(identity);
        content.Children.Add(top);
        var primary = snap.MainWindows;
        var quotaGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = Math.Max(1, primary.Count), Margin = new Thickness(0, 0, -16, 0) };
        content.Children.Add(quotaGrid);
        FrameworkElement Quota(UsageWindow window)
        {
            var reading = Reading.Of(window, settings.ShowRemaining, now);
            var stack = new StackPanel { Margin = new Thickness(0, 0, 16, 8) };
            var label = PulseText(S.Window(window), 11.5, palette.TextMuted);
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            label.TextWrapping = TextWrapping.NoWrap;
            label.ToolTip = S.Window(window);
            stack.Children.Add(label);
            var number = PulseText(reading.PastReset ? "—" : S.Percent(reading.Percent), 32, current && !reading.PastReset ? palette.Text : palette.TextMuted);
            number.FontWeight = FontWeights.SemiBold;
            number.Margin = new Thickness(0, 3, 0, 0);
            stack.Children.Add(number);
            stack.Children.Add(PulseText(reading.PastReset ? "Güncel ölçüm bekleniyor" : !current ? "son ölçüm · güncel değil" : settings.ShowRemaining ? "kalan hak" : "kullanılan hak", 10.5, !current ? palette.AmberText : palette.TextMuted));
            var bar = new PaceBar { Height = 6, Margin = new Thickness(0, 12, 0, 9), Animate = animate, TrackColor = palette.Track, FillColor = current && !reading.PastReset ? palette.Fill(snap.Provider, window, reading.Pace) : palette.TextMuted, NotchColor = palette.Tick, Notch = reading.Notch, Value = reading.Fraction, ToolTip = window.Length is null ? "Servisin bildirdiği gerçek kullanım oranı" : S.NotchHint(settings.ShowRemaining) };
            AutomationProperties.SetName(bar, $"{S.Label(snap)}, {S.Window(window)}, {(reading.PastReset ? S.Refilling : S.Percent(reading.Percent))}");
            stack.Children.Add(bar);
            stack.Children.Add(PulseText(window.ResetsAt is { } at ? reading.PastReset ? "Yeni veri alınıyor" : $"{S.ShortDuration(at - now)} sonra yenilenir" : "Yenilenme zamanı bildirilmedi", 11, palette.TextMuted));
            if (window.ResetsAt is { } reset && !reading.PastReset) stack.Children.Add(PulseText(reset.ToLocalTime().ToString("ddd HH:mm"), 10.5, palette.TextMuted));
            return stack;
        }
        foreach (var window in primary) quotaGrid.Children.Add(Quota(window));
        var extra = snap.Windows.Except(primary).ToList();
        if (extra.Count > 0)
        {
            var details = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            foreach (var window in extra) details.Children.Add(Quota(window));
            var expand = new Expander { Header = $"Diğer {extra.Count} limit", Content = details, Foreground = Palette.Brush(palette.TextMuted), FontSize = 11, Margin = new Thickness(0, 8, 0, 0), IsExpanded = expandedSources.Contains(snap.Source) };
            AutomationProperties.SetName(expand, $"{S.Label(snap)} diğer limitleri göster");
            expand.Expanded += (_, _) => expandedSources.Add(snap.Source);
            expand.Collapsed += (_, _) => expandedSources.Remove(snap.Source);
            content.Children.Add(expand);
        }
        if (snap.Windows.Count == 0 || !current)
        {
            var message = S.Status(snap);
            if (snap.Status == SnapshotStatus.Offline && snap.Windows.Count == 0) message = "Kullanım servisine ulaşılamıyor. Yeniden denenecek.";
            if (string.IsNullOrWhiteSpace(message)) message = "Güncel ölçüm bekleniyor. Son okuma gösteriliyor.";
            var note = PulseText(message, 12, palette.TextMuted);
            note.Margin = new Thickness(0, primary.Count == 0 ? 3 : 12, 0, 12);
            content.Children.Add(note);
            if (snap.Windows.Count == 0)
            {
                var retry = new Button { Content = "Yeniden kontrol et", Style = (Style)FindResource("TextButton"), HorizontalAlignment = HorizontalAlignment.Left, Foreground = Palette.Brush(palette.Accent), Margin = new Thickness(-10, 4, 0, 0), ToolTip = "Servisin bekleme süresine uyarak kullanımı tekrar kontrol et" };
                retry.Click += (_, _) => refresh();
                content.Children.Add(retry);
            }
        }
        if (settings.ShowEmail && snap.Email is { } email)
        {
            var address = PulseText(email, 10.5, palette.TextMuted);
            address.Margin = new Thickness(0, 12, 0, 0);
            content.Children.Add(address);
        }
        return new Border { Child = content, Background = Palette.Brush(palette.Raised), CornerRadius = new CornerRadius(12), Padding = new Thickness(18), Margin = new Thickness(6), MinHeight = snap.Windows.Count > 0 ? 228 : 0, VerticalAlignment = VerticalAlignment.Top };
    }
}

internal sealed class PulseMark : FrameworkElement
{
    public Color Ink { get; set; }
    protected override void OnRender(DrawingContext dc)
    {
        var scale = Math.Min(ActualWidth / 34, ActualHeight / 32);
        dc.PushTransform(new ScaleTransform(scale, scale));
        var pen = new Pen(Palette.Brush(Ink), 2.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(1, 17), false, false);
            g.PolyLineTo([new(8, 17), new(12, 6), new(18, 27), new(23, 13), new(27, 17), new(33, 17)], true, false);
        }
        dc.DrawGeometry(null, pen, geometry);
        dc.Pop();
    }
}
