using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TokenTray.Core;

namespace TokenTray;

internal partial class FlyoutWindow
{
    string? selectedSource;

    internal System.Windows.Media.Imaging.BitmapSource RenderProviderForMedia(string source, double scale)
    {
        selectedSource = source;
        return RenderForMedia(scale);
    }

    internal void OpenSource(string? source, Native.RECT? anchor)
    {
        selectedSource = source;
        ShowAt(anchor);
    }

    internal void OpenProvider(ProviderId? provider, Native.RECT? anchor)
    {
        selectedSource = provider is { } id ? data().FirstOrDefault(s => s.Provider == id)?.Source : null;
        ShowAt(anchor);
    }

    void BuildCompact()
    {
        Page.Children.Clear();
        Page.Margin = new Thickness(18, 14, 18, 4);
        Width = Math.Min(410, SystemParameters.WorkArea.Width - 24);
        Frame.Width = Width;
        SaveSettingsButton.Visibility = Visibility.Collapsed;
        Footer.Visibility = Visibility.Visible;
        SettingsButton.Content = ""; SettingsButton.ToolTip = S.Settings;
        var snaps = data(); var now = DateTimeOffset.Now;
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 3) };
        var pin = new Button { Content = new PulsePin { Width = 14, Height = 14, Ink = palette.Text, Pinned = pinned }, Style = (Style)FindResource("IconButton"), ToolTip = pinned ? "Sabitlemeyi kaldır" : "Paneli açık tut" };
        pin.Click += (_, _) => { pinned = !pinned; BuildCompact(); };
        var close = new Button { Content = "", Style = (Style)FindResource("IconButton"), ToolTip = "Paneli kapat" };
        close.Click += (_, _) => HideFlyout();
        DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
        DockPanel.SetDock(pin, Dock.Right); header.Children.Add(pin);
        header.Children.Add(new TextBlock { Text = "SGB Limit", FontSize = 19, FontWeight = FontWeights.SemiBold, Foreground = Palette.Brush(palette.Text), VerticalAlignment = VerticalAlignment.Center });
        Page.Children.Add(header);
        var toolbar = new DockPanel { Margin = new Thickness(0, 1, 0, 8) };
        var mode = new Button { Content = settings.ShowRemaining ? "Kalan" : "Kullanılan", Style = (Style)FindResource("TextButton"), Foreground = Palette.Brush(palette.Accent), ToolTip = "Kullanılan / kalan hakkı değiştir", Height = 27 };
        mode.Click += (_, _) => { settings.ShowRemaining = !settings.ShowRemaining; settingsChanged(); BuildCompact(); };
        DockPanel.SetDock(mode, Dock.Right); toolbar.Children.Add(mode);
        toolbar.Children.Add(PulseText(demoCapture ? "Örnek veriler · gerçek hesap değil" : $"{snaps.Count(CurrentNow)} / {snaps.Count} hesap güncel", 11, palette.TextMuted));
        Page.Children.Add(toolbar);
        if (snaps.Count == 0)
        {
            Page.Children.Add(PulseText("Desteklenen hesapları bulmak için Ayarlar → Bağlantılar → Bilgisayarı tara seçeneğini kullan.", 13, palette.TextMuted));
        }
        foreach (var snap in snaps)
        {
            var selected = selectedSource == snap.Source;
            var current = Current(snap, now);
            var window = snap.Binding;
            var remembered = snap.Provider == ProviderId.Antigravity && snap.Stale && window is not null;
            var value = (current || remembered) && window is not null && (remembered || window.ResetsAt is null || window.ResetsAt > now)
                ? S.Percent(settings.ShowRemaining ? window.RemainingPercent : window.UsedPercent) : "—";
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2, 0, 2) };
            var number = new TextBlock { Text = value, FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = Palette.Brush(remembered ? palette.AmberText : current && window?.UsedPercent >= 95 ? palette.RedText : palette.Text), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            DockPanel.SetDock(number, Dock.Right); row.Children.Add(number);
            var mark = ProviderMark.Element(snap.Provider, palette.Hue(snap.Provider), 18); mark.Margin = new Thickness(0, 0, 10, 0); mark.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(mark, Dock.Left); row.Children.Add(mark);
            var names = new StackPanel();
            var identity = new DockPanel { LastChildFill = true };
            var plan = new TextBlock { Text = S.PlanLabel(snap), FontSize = 10, Foreground = Palette.Brush(snap.Plan is null ? palette.TextMuted : palette.Accent), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 135 };
            var badge = new Border { Background = Palette.Brush(palette.Raised), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(8, 0, 0, 0), Child = plan, ToolTip = (current ? "Sağlayıcının bildirdiği plan: " : "Son bilinen plan: ") + (snap.Plan ?? "bildirilmedi") };
            DockPanel.SetDock(badge, Dock.Right); identity.Children.Add(badge);
            var fullName = PulseText(S.FullLabel(snap), 13, palette.Text); fullName.TextWrapping = TextWrapping.NoWrap; fullName.TextTrimming = TextTrimming.CharacterEllipsis; fullName.ToolTip = S.FullLabel(snap); identity.Children.Add(fullName);
            names.Children.Add(identity);
            names.Children.Add(PulseText(remembered ? $"Son okuma · {snap.FetchedAt.ToLocalTime():HH:mm} · güncel değil" : current && window is not null ? S.Window(window) : snap.Status == SnapshotStatus.Loading ? "Okunuyor" : "Bağlantı doğrulanamadı", 10.5, remembered ? palette.AmberText : palette.TextMuted));
            row.Children.Add(names);
            var button = new Button { Content = row, Style = (Style)FindResource("Tile"), HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 0, 8, 0), MinHeight = 0, Height = 42, Tag = selected ? "on" : null, Margin = new Thickness(-8, 0, -8, 0), ToolTip = "Limitleri ve kullanım önerisini aç" };
            button.Click += (_, _) => { selectedSource = selected ? null : snap.Source; BuildCompact(); };
            Page.Children.Add(button);
            if (!selected) continue;
            var detail = new StackPanel { Margin = new Thickness(0, 8, 0, 14) };
            if (current)
            {
                foreach (var quota in snap.Windows)
                {
                    var expired = quota.ResetsAt is { } reset && reset <= now;
                    var q = new DockPanel { Margin = new Thickness(0, 9, 0, 0) };
                    var amount = PulseText(expired ? "Yenileniyor" : S.Percent(settings.ShowRemaining ? quota.RemainingPercent : quota.UsedPercent), 12, palette.Text);
                    DockPanel.SetDock(amount, Dock.Right); q.Children.Add(amount); q.Children.Add(PulseText(S.Window(quota), 11, palette.TextMuted));
                    detail.Children.Add(q);
                    if (quota.Detail is { } amountDetail) detail.Children.Add(PulseText(amountDetail, 10, palette.TextMuted));
                    if (quota.ResetsAt is { } at && !expired) detail.Children.Add(PulseText($"{at.ToLocalTime():dd MMM HH:mm} · {S.ShortDuration(at - now)} sonra", 10, palette.TextMuted));
                }
                var advice = new StackPanel { Margin = new Thickness(0, 13, 0, 0) };
                advice.Children.Add(new TextBlock { Text = "Kullanım önerisi", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Palette.Brush(palette.Accent) });
                var copy = PulseText(PulseAdvice.For(snap, now), 11.5, palette.Text); copy.Margin = new Thickness(0, 5, 0, 0); advice.Children.Add(copy);
                detail.Children.Add(advice);
            }
            else if (remembered)
            {
                detail.Children.Add(PulseText($"Son başarılı okuma: {snap.FetchedAt.ToLocalTime():dd MMM HH:mm}. Güncel kota için Antigravity'yi açıp yenile.", 11.5, palette.AmberText));
                foreach (var quota in snap.MainWindows)
                    detail.Children.Add(PulseText(S.Window(quota) + " · " + S.Percent(settings.ShowRemaining ? quota.RemainingPercent : quota.UsedPercent) + " · son okuma", 11, palette.TextMuted));
            }
            else
            {
                detail.Children.Add(PulseText(snap.Message ?? S.Status(snap), 11.5, palette.TextMuted));
                var retry = new Button { Content = "Yeniden kontrol et", Style = (Style)FindResource("TextButton"), Foreground = Palette.Brush(palette.Accent), HorizontalAlignment = HorizontalAlignment.Left };
                retry.Click += (_, _) => refresh(); detail.Children.Add(retry);
            }
            if (settings.ShowEmail && snap.Email is { } email) { var address = PulseText(email, 10, palette.TextMuted); address.Margin = new Thickness(0, 9, 0, 0); detail.Children.Add(address); }
            Page.Children.Add(detail);
        }
        var last = snaps.Where(s => s.Status != SnapshotStatus.Loading).Select(s => s.FetchedAt).DefaultIfEmpty().Max();
        UpdatedText.Text = last == default ? "İlk okuma bekleniyor" : $"Son kontrol {last.ToLocalTime():HH:mm}";
        bool CurrentNow(ProviderSnapshot s) => Current(s, now);
    }
}
