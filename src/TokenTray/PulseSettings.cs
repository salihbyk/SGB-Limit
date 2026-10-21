using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using TokenTray.Core;

namespace TokenTray;

internal partial class FlyoutWindow
{
    Settings? draft;
    bool draftStartup;
    int settingsTab, viewStep;

    internal static string IndicatorName(WidgetStyle style) => style switch
        { WidgetStyle.Orbit => "Noktalı", WidgetStyle.Comet => "Yörünge", _ => "Çevresel" };

    void BuildPulseSettings()
    {
        if (draft is null) { draft = settings.Copy(); draftStartup = Settings.StartWithWindows; viewStep = 0; }
        settingsPage = true; ApplyTheme();
        Frame.Background = Palette.Brush(palette.Surface); Frame.BorderBrush = Palette.Brush(palette.Hairline);
        Frame.BorderThickness = new Thickness(1); Frame.CornerRadius = new CornerRadius(10);
        Width = Math.Min(532, SystemParameters.WorkArea.Width - 24); Frame.Width = Width;
        Page.Children.Clear(); Page.Margin = new Thickness(16, 12, 16, 8);
        Footer.Visibility = SaveSettingsButton.Visibility = Visibility.Visible;
        SettingsButton.Content = ""; SettingsButton.ToolTip = "Kaydetmeden geri dön";
        UpdatedText.Text = "Değişiklikler kaydedilmedi"; UpdatedText.FontSize = 10.5;
        SaveSettingsButton.Foreground = Palette.Brush(palette.Accent); SaveSettingsButton.Content = "Kaydet";
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var close = new Button { Content = "", Style = (Style)FindResource("IconButton"), ToolTip = "Ayarları kapat" };
        close.Click += (_, _) => ShowUsage(); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(new TextBlock { Text = "SGB Limit", FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = Palette.Brush(palette.Text) });
        brand.Children.Add(new TextBlock { Text = "Ayarlar", FontSize = 11, Foreground = Palette.Brush(palette.TextMuted), Margin = new Thickness(10, 7, 0, 0) });
        header.Children.Add(brand); Page.Children.Add(header);
        var layout = new Grid(); layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var menu = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        foreach (var (title, index) in new[] { ("Görünüm", 0), ("Hesaplar", 1), ("Sistem", 2) })
        {
            var button = SettingsTab(title, settingsTab == index, () => { settingsTab = index; BuildPulseSettings(); });
            button.HorizontalAlignment = HorizontalAlignment.Stretch; button.Margin = new Thickness(0, 0, 0, 4); menu.Children.Add(button);
        }
        layout.Children.Add(menu);
        var body = new StackPanel { MinHeight = 270 }; Grid.SetColumn(body, 1); layout.Children.Add(body); Page.Children.Add(layout);
        void Title(string text, string? hint = null)
        {
            body.Children.Add(new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Palette.Brush(palette.Text), Margin = new Thickness(0, 8, 0, 6) });
            if (hint is not null) { var t = PulseText(hint, 10.5, palette.TextMuted); t.Margin = new Thickness(0, 0, 0, 6); body.Children.Add(t); }
        }
        void Toggle(string title, string? hint, bool value, Action<bool> set, Panel? target = null)
        {
            var text = new StackPanel(); text.Children.Add(PulseText(title, 12, palette.Text));
            var check = new CheckBox { Content = text, IsChecked = value, Style = (Style)FindResource("Switch"), Foreground = Palette.Brush(palette.Text), Margin = new Thickness(0, 5, 0, 5), MinHeight = 28 };
            if (!string.IsNullOrEmpty(hint)) check.ToolTip = hint;
            AutomationProperties.SetName(check, title);
            check.Click += (_, _) => { set(check.IsChecked == true); UpdatedText.Text = "Değişiklikler kaydedilmedi"; };
            if (target is not null) { check.Margin = new Thickness(0, 4, 8, 4); text.Margin = new Thickness(0); }
            (target ?? body).Children.Add(check);
        }
        void Choice<T>(string title, IEnumerable<(string Label, T Value)> choices, T current, Action<T> set)
        {
            Title(title); var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
            foreach (var (label, value) in choices)
            {
                var button = SettingsTab(label, EqualityComparer<T>.Default.Equals(current, value), () => { set(value); BuildPulseSettings(); });
                button.Margin = new Thickness(0, 0, 4, 4); row.Children.Add(button);
            }
            body.Children.Add(row);
        }
        if (settingsTab == 0)
        {
            SaveSettingsButton.Content = viewStep < 2 ? "İleri →" : "Kaydet";
            SettingsButton.ToolTip = viewStep > 0 ? "Önceki adım" : "Kaydetmeden kapat";
            var trail = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            if (viewStep > 0)
            {
                var back = SettingsTab("‹ Geri", false, () => { viewStep--; BuildPulseSettings(); });
                back.Margin = new Thickness(0, 0, 8, 0);
                DockPanel.SetDock(back, Dock.Left); trail.Children.Add(back);
            }
            trail.Children.Add(new TextBlock { Text = $"{viewStep + 1} / 3 · " + (viewStep == 0 ? "Gösterim biçimi" : viewStep == 1 ? "Gösterge" : "Yerleşim ve görünüm"),
                FontSize = 11, Foreground = Palette.Brush(palette.TextMuted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 0) });
            body.Children.Add(trail);
            if (viewStep == 0)
            {
                foreach (var (mode, name, hint) in new[] { (PresentationMode.DynamicIsland, "Dinamik Ada", "Üstte veya ekranın yan kenarında"), (PresentationMode.Taskbar, "Görev çubuğu", "Windows ikonlarının yanında"), (PresentationMode.Floating, "Yüzen gösterge", "Sürüklenebilen dairesel gösterge") })
                {
                    var content = new DockPanel { LastChildFill = true };
                    var preview = PresentationPreview(mode); preview.Width = 132; DockPanel.SetDock(preview, Dock.Right); content.Children.Add(preview);
                    var label = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    label.Children.Add(PulseText(name + "  ›", 12, palette.Text)); label.Children.Add(PulseText(hint, 10, palette.TextMuted)); content.Children.Add(label);
                    var card = SettingsCard(content, draft.Presentation == mode, () => { draft.Presentation = mode; draft.EmbedInTaskbar = mode == PresentationMode.Taskbar; viewStep = 1; BuildPulseSettings(); });
                    AutomationProperties.SetName(card, name + (draft.Presentation == mode ? ", seçili" : "")); body.Children.Add(card);
                }
                body.Children.Add(PulseText("Bir görünüm seçerek ayarlarına geç.", 10.5, palette.TextMuted));
            }
            else if (viewStep == 1)
            {
                Title(draft.Presentation == PresentationMode.Taskbar ? "Görev çubuğu göstergesi" : draft.Presentation == PresentationMode.DynamicIsland ? "Dinamik Ada" : "Yüzen gösterge", "Örnek veriler");
                if (draft.Presentation == PresentationMode.Taskbar)
                {
                    var cards = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new Thickness(0, 3, 0, 2) };
                    foreach (var (name, style) in new[] { ("İkon + ad", WidgetStyle.Identity), ("Halka", WidgetStyle.Rings), ("Şerit", WidgetStyle.Ribbon), ("Kadran", WidgetStyle.Dial), ("Sinyal", WidgetStyle.Signal) })
                    {
                        var content = new StackPanel(); content.Children.Add(PulseText(name, 11, palette.Text)); content.Children.Add(TaskbarPreview(style, 153));
                        var card = SettingsCard(content, draft.TaskbarStyle == style, () => { draft.TaskbarStyle = style; BuildPulseSettings(); });
                        card.Margin = new Thickness(0, 0, 5, 5); AutomationProperties.SetName(card, name); cards.Children.Add(card);
                    }
                    body.Children.Add(cards);
                    if (draft.TaskbarStyle == WidgetStyle.Signal)
                    {
                        Choice("Sinyal çubukları", new[] { ("3 çubuk", 3), ("5 çubuk", 5) }, draft.SignalBars, v => draft.SignalBars = v);
                        body.Children.Add(PulseText("Çubuklar gösterilen yüzdeyi izler. * işareti son okumayı belirtir.", 10, palette.TextMuted));
                        Toggle("Uygulama adları", "Yer daralınca ikon + yüzde görünür.", draft.WidgetName, v => draft.WidgetName = v);
                    }
                    else
                    {
                        var toggles = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new Thickness(0, 7, 0, 0) }; body.Children.Add(toggles);
                        Toggle("Uygulama adları", null, draft.WidgetName, v => draft.WidgetName = v, toggles);
                        Toggle("Kısa dönem", "Oturum veya model kotası", draft.WidgetSession, v => draft.WidgetSession = v, toggles);
                        Toggle("Uzun dönem", "Haftalık veya fatura dönemi", draft.WidgetWeekly, v => draft.WidgetWeekly = v, toggles);
                        Toggle("Sayaç", "Yenilenme süresini göster", draft.WidgetTime, v => draft.WidgetTime = v, toggles);
                    }
                }
                else if (draft.Presentation == PresentationMode.DynamicIsland)
                {
                    var preview = PresentationPreview(PresentationMode.DynamicIsland); preview.Width = 330; preview.Height = 62; body.Children.Add(preview);
                    Choice("Görünen hesaplar", new[] { ("3 hesap", 3), ("5 hesap", 5), ("8 hesap", 8) }, draft.IslandSlots, v => draft.IslandSlots = v);
                    Toggle("Üzerine gelince genişlet", "Ada akışkan bir geçişle açılır.", draft.ExpandOnHover, v => draft.ExpandOnHover = v);
                    Toggle("Yenilenme süreleri", null, draft.IslandReset, v => draft.IslandReset = v);
                }
                else
                {
                    foreach (var style in new[] { WidgetStyle.Halo, WidgetStyle.Orbit, WidgetStyle.Comet })
                    {
                        var content = new DockPanel(); var sample = draft.Copy(); sample.WidgetStyle = style;
                        var preview = new OrbitalTrack(MediaRenderer.Demo(DateTimeOffset.Now), sample) { Width = 52, Height = 52 }; DockPanel.SetDock(preview, Dock.Right); content.Children.Add(preview);
                        content.Children.Add(new TextBlock { Text = IndicatorName(style), FontSize = 12, Foreground = Palette.Brush(palette.Text), VerticalAlignment = VerticalAlignment.Center });
                        body.Children.Add(SettingsCard(content, draft.WidgetStyle == style, () => { draft.WidgetStyle = style; BuildPulseSettings(); }));
                    }
                }
            }
            else
            {
                if (draft.Presentation != PresentationMode.Taskbar)
                {
                    var position = draft.IslandPosition;
                    var edge = position is IslandPosition.SideLeftTop or IslandPosition.SideLeftCenter or IslandPosition.SideLeftBottom ? 2 :
                        position is IslandPosition.SideRightTop or IslandPosition.SideRightCenter or IslandPosition.SideRightBottom ? 3 :
                        position is IslandPosition.BottomLeft or IslandPosition.BottomCenter or IslandPosition.BottomRight ? 1 : 0;
                    var align = edge < 2 ? position is IslandPosition.TopLeft or IslandPosition.BottomLeft ? 0 : position is IslandPosition.TopRight or IslandPosition.BottomRight ? 2 : 1 :
                        position is IslandPosition.SideLeftTop or IslandPosition.SideRightTop ? 0 : position is IslandPosition.SideLeftBottom or IslandPosition.SideRightBottom ? 2 : 1;
                    void Position(int side, int alignment)
                    {
                        draft.IslandPosition = side switch
                        {
                            2 => alignment == 0 ? IslandPosition.SideLeftTop : alignment == 2 ? IslandPosition.SideLeftBottom : IslandPosition.SideLeftCenter,
                            3 => alignment == 0 ? IslandPosition.SideRightTop : alignment == 2 ? IslandPosition.SideRightBottom : IslandPosition.SideRightCenter,
                            1 => alignment == 0 ? IslandPosition.BottomLeft : alignment == 2 ? IslandPosition.BottomRight : IslandPosition.BottomCenter,
                            _ => alignment == 0 ? IslandPosition.TopLeft : alignment == 2 ? IslandPosition.TopRight : IslandPosition.TopCenter
                        };
                    }
                    Choice("Ekran kenarı", new[] { ("Üst", 0), ("Alt", 1), ("Sol", 2), ("Sağ", 3) }, edge, v => Position(v, align));
                    if (edge < 2) Choice("Hizalama", new[] { ("Sol", 0), ("Orta", 1), ("Sağ", 2) }, align, v => Position(edge, v));
                    else Choice("Dikey hizalama", new[] { ("Üst", 0), ("Orta", 1), ("Alt", 2) }, align, v => Position(edge, v));
                    Choice("Boyut", new[] { ("Küçük", 0.85), ("Standart", 1.0), ("Büyük", 1.15) }, draft.IslandScale, v => draft.IslandScale = v);
                    Toggle("Göstergeyi göster", null, draft.IslandEnabled, v => draft.IslandEnabled = v);
                }
                Choice("Sayılar", new[] { ("Kullanılan", false), ("Kalan", true) }, draft.ShowRemaining, v => draft.ShowRemaining = v);
                Choice("Tema", new[] { ("Koyu", AppTheme.Dark), ("Açık", AppTheme.Light), ("Windows", AppTheme.System) }, draft.Theme, v => draft.Theme = v);
                Toggle("Akışkan geçişler", "Windows'un azaltılmış hareket tercihini izler.", draft.Motion, v => draft.Motion = v);
            }
        }
        else if (settingsTab == 1)
        {
            Title("Hesaplar"); Toggle("Otomatik bul", "Yeni yerel oturumları beş dakikada bir kontrol eder.", draft.AutoDiscover, v => draft.AutoDiscover = v);
            foreach (var id in Enum.GetValues<ProviderId>())
            {
                var found = Settings.IsInstalled(id); var snapshot = data().FirstOrDefault(s => s.Provider == id);
                var hint = snapshot is null ? found ? "Uygulama bulundu" : "Yerel oturum bulunamadı" : Current(snapshot, DateTimeOffset.Now) ? "Kota güncel" : snapshot.Message ?? "Bağlantı doğrulanamadı";
                Toggle(S.Name(id), hint, draft.IsOn(id), v => draft.Set(id, v));
            }
            var scan = SettingsTab("Yeniden tara", false, () => { foreach (var id in Enum.GetValues<ProviderId>()) if (Settings.IsInstalled(id)) draft.Set(id, true); refresh(); BuildPulseSettings(); });
            body.Children.Add(scan); Toggle("E-postaları göster", null, draft.ShowEmail, v => draft.ShowEmail = v);
            var catalog = new StackPanel();
            foreach (var app in AppDiscovery.Catalog)
            {
                var link = SettingsTab(app.Name, false, () => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(app.Url) { UseShellExecute = true }); } catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { UpdatedText.Text = "Tarayıcı açılamadı"; } });
                link.ToolTip = "Resmi siteyi aç"; catalog.Children.Add(link);
            }
            body.Children.Add(new Expander { Header = "Diğer uygulamalar", Content = catalog, Foreground = Palette.Brush(palette.TextMuted), Margin = new Thickness(0, 10, 0, 0) });
        }
        else
        {
            Choice("Kontrol sıklığı", new[] { ("2 dk", 120), ("5 dk", 300), ("10 dk", 600), ("15 dk", 900) }, draft.IntervalSeconds, v => draft.IntervalSeconds = v);
            Toggle("Windows ile başlat", null, draftStartup, v => draftStartup = v);
            Toggle("Limit uyarıları", "%20 ve %5 kaldığında bildirir.", draft.Notifications, v => draft.Notifications = v);
            Title("Veri kullanımı", "Mevcut yerel oturumlar salt okunur kullanılır. Giriş bilgileri ayarlara kopyalanmaz.");
            body.Children.Add(PulseText("SGB Limit 2.4", 11, palette.TextMuted));
            var exit = SettingsTab("Uygulamadan çık", false, quit); exit.Foreground = Palette.Brush(palette.RedText); body.Children.Add(exit);
        }
    }

    Button SettingsTab(string label, bool selected, Action click)
    {
        var button = new Button { Content = label, Style = (Style)FindResource("TextButton"), Height = 31, FontSize = 11, Padding = new Thickness(7, 0, 7, 0),
            Background = Palette.Brush(selected ? palette.Raised : Colors.Transparent), Foreground = Palette.Brush(selected ? palette.Accent : palette.TextMuted), FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal };
        // Local template keeps selected tabs visible with native keyboard focus.
        var face = new FrameworkElementFactory(typeof(Border)); face.Name = "face"; face.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        face.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        face.SetValue(Border.PaddingProperty, new Thickness(7, 0, 7, 0));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center); face.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = face };
        foreach (var property in new[] { IsMouseOverProperty, IsKeyboardFocusedProperty }) { var trigger = new Trigger { Property = property, Value = true }; trigger.Setters.Add(new Setter(Border.BackgroundProperty, Palette.Brush(palette.Track), "face")); template.Triggers.Add(trigger); }
        button.Template = template; AutomationProperties.SetName(button, label); button.Click += (_, _) => click(); return button;
    }

    Button SettingsCard(FrameworkElement content, bool selected, Action click)
    {
        var button = new Button { Content = content, Style = (Style)FindResource("Tile"), Tag = selected ? "on" : null, Padding = new Thickness(9, 6, 9, 6),
            Margin = new Thickness(0, 0, 0, 6), HorizontalContentAlignment = HorizontalAlignment.Stretch, Foreground = Palette.Brush(palette.Text) };
        button.Click += (_, _) => click(); return button;
    }

    void SavePreferences(object sender, RoutedEventArgs e)
    {
        if (draft is null) return;
        if (settingsTab == 0 && viewStep < 2) { viewStep++; BuildPulseSettings(); return; }
        if (!draft.TrySave(out var error, verificationSettingsPath)) { UpdatedText.Text = "Kaydedilemedi: " + error; return; }
        settings.CopyFrom(draft);
        try { if (!demoCapture) Settings.StartWithWindows = draftStartup; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException) { UpdatedText.Text = "Başlangıç izni alınamadı"; return; }
        settingsChanged(); ShowUsage(); UpdatedText.Text = "Ayarlar kaydedildi";
    }
}
