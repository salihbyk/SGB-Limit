using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TokenTray.Core;

namespace TokenTray;

internal partial class FlyoutWindow
{
    string? verificationSettingsPath;
    internal void VerifyAndCapture(string folder)
    {
        Directory.CreateDirectory(folder); demoCapture = true;
        TaskbarLayoutProbe.Verify(folder);
        SignalReadingProbe.Verify(folder);
        VerifyLimitSettings(folder);
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        var upgraded = new Settings { Version = 1, WidgetStyle = WidgetStyle.Rings, ShowRemaining = true, Claude = false };
        upgraded.Upgrade();
        Check(upgraded.Version == 7 && upgraded.Presentation == PresentationMode.DynamicIsland && upgraded.WidgetStyle == WidgetStyle.Orbit && upgraded.TaskbarStyle == WidgetStyle.Identity && upgraded.WidgetName && upgraded.ShowRemaining && upgraded.Claude == false && !upgraded.EmbedInTaskbar, "Migration keeps account choices and restores taskbar icons and names");
        var oldTaskbar = new Settings { Version = 4, EmbedInTaskbar = true, Claude = false, IslandScale = 0.85 };
        oldTaskbar.Upgrade();
        Check(oldTaskbar.Presentation == PresentationMode.Taskbar && oldTaskbar.Claude == false && oldTaskbar.IslandScale == 0.85, "Migration preserves taskbar preference and account opt-out");
        settings.ShowRemaining = false;
        Save(RenderForMedia(1), "panel-ornek-100.png");
        Check(Width <= 410, "The collapsed panel is compact");
        var remembered = data().First(s => s.Provider == ProviderId.Antigravity) with { Status = SnapshotStatus.Offline, Stale = true, FetchedAt = DateTimeOffset.Now.AddMinutes(-8) };
        var rememberedPanel = new FlyoutWindow(() => [remembered], () => [], new Settings(), () => { }, () => { }, () => { }) { demoCapture = true };
        Save(rememberedPanel.RenderForMedia(1), "antigravity-last-reading.png");
        Check(Descendants(rememberedPanel.Frame).OfType<TextBlock>().Any(t => t.Text.StartsWith("Son okuma ·")), "An unavailable Antigravity service exposes a labeled previous reading.");
        rememberedPanel.Close();
        Check(Descendants(Frame).OfType<TextBlock>().Any(t => t.Text == "Max 20x"), "Subscription plan is visible before expanding an account");
        var mode = Descendants(Frame).OfType<Button>().First(b => b.Content as string == "Kullanılan");
        mode.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Frame.Measure(new Size(Width, double.PositiveInfinity)); Frame.Arrange(new Rect(Frame.DesiredSize)); Frame.UpdateLayout();
        Check(settings.ShowRemaining, "Used/remaining action");
        Check(Descendants(Frame).OfType<TextBlock>().Any(t => t.Text == S.Percent(29)), "71 used becomes29 remaining");
        selectedSource = data().First(s => s.Provider == ProviderId.Claude).Source;
        BuildCompact();
        Check(Descendants(Frame).OfType<TextBlock>().Any(t => t.Text == "Kullanım önerisi"), "Selected provider has advice");
        Save(RenderForMedia(1), "panel-detay-ornek.png");
        Save(RenderForMedia(1.5), "panel-ornek-150.png");
        OnSettings(this, new RoutedEventArgs());
        Check(settingsPage && SaveSettingsButton.Visibility == Visibility.Visible, "Persistent Save action");
        var original = settings.WidgetStyle;
        draft!.WidgetStyle = WidgetStyle.Comet;
        draft.Presentation = PresentationMode.DynamicIsland;
        draft.IslandScale = 0.85; draft.IslandSlots = 3; draft.IslandReset = false;
        draft.IslandPosition = IslandPosition.BottomRight;
        BuildPulseSettings();
        Check(settings.WidgetStyle == original, "A settings draft does not mutate live preferences");
        CapturePage("ayarlar-ornek.png");
        draft.Presentation = PresentationMode.Taskbar; BuildPulseSettings(); CapturePage("ayarlar-gorev-cubugu.png");
        draft.Presentation = PresentationMode.Floating; BuildPulseSettings(); CapturePage("ayarlar-yuzen.png");
        draft.Presentation = PresentationMode.DynamicIsland; BuildPulseSettings();
        settingsTab = 1; BuildPulseSettings(); CapturePage("baglantilar-ornek.png");
        settingsTab = 2; BuildPulseSettings(); CapturePage("sistem-ornek.png");
        var preferences = Path.Combine(folder, "settings-roundtrip.json");
        verificationSettingsPath = preferences;
        Check(draft.TrySave(out _, preferences), "Atomic settings write");
        verificationSettingsPath = Path.Combine(preferences, "blocked.json");
        viewStep = 2;
        SavePreferences(this, new RoutedEventArgs());
        Check(settings.WidgetStyle == original && settingsPage && UpdatedText.Text.StartsWith("Kaydedilemedi"), "Save failure is visible and leaves live settings intact");
        verificationSettingsPath = preferences;
        SavePreferences(this, new RoutedEventArgs());
        Check(!settingsPage && settings.WidgetStyle == WidgetStyle.Comet && settings.IslandPosition == IslandPosition.BottomRight, "Save applies indicator and placement");
        var reloaded = System.Text.Json.JsonSerializer.Deserialize<Settings>(File.ReadAllText(preferences), new System.Text.Json.JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } })!;
        Check(reloaded.WidgetStyle == settings.WidgetStyle && reloaded.IslandPosition == settings.IslandPosition && reloaded.ShowRemaining == settings.ShowRemaining && reloaded.Presentation == PresentationMode.DynamicIsland && reloaded.IslandSlots == 3 && reloaded.IslandScale == 0.85 && !reloaded.IslandReset, "Preferences survive serialization and reload");
        var disabled = new Settings { Claude = false, Codex = false, Grok = false };
        Check(!disabled.IsOn(ProviderId.Claude) && !disabled.Copy().IsOn(ProviderId.Grok), "Automatic discovery preserves explicit opt-outs");
        foreach (var style in new[] { WidgetStyle.Halo, WidgetStyle.Orbit, WidgetStyle.Comet })
        {
            var sample = settings.Copy(); sample.Presentation = PresentationMode.Floating; sample.WidgetStyle = style; sample.ShowRemaining = false;
            var island = new FloatingIsland(sample, data, _ => { }, () => { });
            island.Refresh();
            var visual = (FrameworkElement)island.Content;
            visual.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); visual.Arrange(new Rect(visual.DesiredSize));
            var caption = new StackPanel();
            caption.Children.Add(PulseText("Örnek veriler · gerçek hesap değil", 11, new Palette(true).TextMuted));
            // Capture the production island surface; the caption lives outside its rounded shell.
            var surfaceImage = MediaRenderer.Capture(visual, 2);
            caption.Children.Add(new Image { Source = surfaceImage, Width = surfaceImage.Width, Height = surfaceImage.Height, HorizontalAlignment = HorizontalAlignment.Left });
            var proof = new Border { Background = Palette.Brush(new Palette(true).Surface), Padding = new Thickness(8), Child = caption };
            proof.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); proof.Arrange(new Rect(proof.DesiredSize));
            Save(MediaRenderer.Capture(proof, 2), $"ada-{style.ToString().ToLowerInvariant()}-ornek.png");
            if (style == WidgetStyle.Comet)
            {
                var gauge = Descendants(visual).OfType<OrbitalTrack>().First();
                gauge.Phase = 0.2; gauge.InvalidateVisual(); var a = MediaRenderer.Capture(visual, 1);
                gauge.Phase = 0.7; gauge.InvalidateVisual(); var b = MediaRenderer.Capture(visual, 1);
                var pixelsA = new byte[a.PixelWidth * a.PixelHeight * 4]; var pixelsB = new byte[pixelsA.Length];
                a.CopyPixels(pixelsA, a.PixelWidth * 4, 0); b.CopyPixels(pixelsB, b.PixelWidth * 4, 0);
                Check(!pixelsA.SequenceEqual(pixelsB), "Motion changes decorative position without altering quota");
                island.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
                Check(island.MotionRunning == SystemParameters.ClientAreaAnimation, "Hover starts motion only when Windows allows it");
                island.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
                Check(!island.MotionRunning, "Idle has no animation timer");
            }
            island.Close();
        }
        var dynamicSettings = new Settings { Presentation = PresentationMode.DynamicIsland, IslandSlots = 5 };
        var clicked = "";
        var dynamicIsland = new FloatingIsland(dynamicSettings, data, source => clicked = source ?? "all", () => { });
        dynamicIsland.Refresh();
        var dynamicSurface = (FrameworkElement)dynamicIsland.Content;
        void DynamicCapture(string file, double scale = 1)
        {
            dynamicSurface.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); dynamicSurface.Arrange(new Rect(dynamicSurface.DesiredSize)); dynamicSurface.UpdateLayout();
            var group = new StackPanel();
            group.Children.Add(PulseText("Örnek veriler · gerçek hesap değil", 11, new Palette(true).TextMuted));
            var image = MediaRenderer.Capture(dynamicSurface, scale);
            group.Children.Add(new Image { Source = image, Width = image.Width, Height = image.Height, HorizontalAlignment = HorizontalAlignment.Left });
            var proof = new Border { Background = Palette.Brush(new Palette(true).Surface), Padding = new Thickness(12), Child = group };
            proof.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); proof.Arrange(new Rect(proof.DesiredSize));
            Save(MediaRenderer.Capture(proof, scale), file);
        }
        DynamicCapture("dinamik-kapali-ornek.png");
        var smallHeight = dynamicIsland.Height;
        dynamicIsland.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
        Check(dynamicIsland.Expanded && dynamicIsland.Height > smallHeight, "Hover expands the actual production island");
        DynamicCapture("dinamik-acik-ornek.png");
        DynamicCapture("dinamik-acik-150.png", 1.5);
        Check(Descendants(dynamicSurface).OfType<TextBlock>().Any(t => t.Text == "Antigravity"), "Expanded island displays full provider names");
        var bindingSample = new ProviderSnapshot(ProviderId.Claude, SnapshotStatus.Ok, [new(WindowKind.Session, "5h", 0, DateTimeOffset.Now.AddHours(4), TimeSpan.FromHours(5)), new(WindowKind.Weekly, "Weekly", 33, DateTimeOffset.Now.AddDays(3), TimeSpan.FromDays(7)), new(WindowKind.WeeklyModel, "Weekly · Fable", 44, DateTimeOffset.Now.AddDays(3), TimeSpan.FromDays(7))], DateTimeOffset.Now);
        Check(FloatingIsland.ExpandedWindows(bindingSample).Contains(bindingSample.Binding!) && FloatingIsland.ExpandedWindows(bindingSample).Count == 3, "Expanded limits include the same binding quota shown when collapsed");
        var providerButton = Descendants(dynamicSurface).OfType<Button>().First(b => System.Windows.Automation.AutomationProperties.GetName(b).StartsWith("Claude,"));
        providerButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(clicked == data().First(s => s.Provider == ProviderId.Claude).Source, "Island selects the exact account source");
        dynamicIsland.SetExpanded(false, false);
        Check(!dynamicIsland.Expanded && !dynamicIsland.MotionRunning, "Collapsed island has no continuous animation timer");
        dynamicSettings.ExpandOnHover = false;
        dynamicIsland.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
        Check(!dynamicIsland.Expanded, "Hover expansion can be disabled");
        dynamicSettings.Motion = false; dynamicIsland.SetExpanded(true);
        Check(dynamicIsland.Expanded && !dynamicIsland.HasAnimatedProperties, "Reduced motion changes state without animation");
        dynamicSettings.IslandSlots = 3; dynamicSettings.IslandScale = 0.85; dynamicIsland.Refresh();
        DynamicCapture("dinamik-kucuk-3-ornek.png");
        Check(Descendants(dynamicSurface).OfType<Button>().Any(b => Descendants(b).OfType<TextBlock>().Any(t => t.Text == "+2")), "Overflow accounts remain accessible");
        dynamicIsland.Close();
        var traeSample = new ProviderSnapshot(ProviderId.Trae, SnapshotStatus.Ok, [new(WindowKind.Period, "TRAE dönem bakiyesi", 0, DateTimeOffset.Now.AddDays(20), TimeSpan.FromDays(30), Detail: "0 / 20 USD")], DateTimeOffset.Now, "TRAE Pro");
        var traePanel = new FlyoutWindow(() => [traeSample], () => [], new Settings(), () => { }, () => { }, () => { }) { demoCapture = true };
        Save(traePanel.RenderProviderForMedia(ProviderId.Trae.ToString(), 1), "trae-panel-ornek.png");
        Check(Descendants(traePanel.Frame).OfType<TextBlock>().Any(t => t.Text == "0 / 20 USD"), "TRAE's actual currency unit accompanies its percentage");
        traePanel.Close();
        var empty = new FloatingIsland(new Settings(), () => [], _ => { }, () => { }); empty.Refresh();
        var emptyVisual = (FrameworkElement)empty.Content; emptyVisual.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); emptyVisual.Arrange(new Rect(emptyVisual.DesiredSize));
        Save(MediaRenderer.Capture(emptyVisual, 1), "dinamik-bos.png"); empty.Close();
        foreach (var style in new[] { WidgetStyle.Identity, WidgetStyle.Rings, WidgetStyle.Ribbon, WidgetStyle.Dial, WidgetStyle.Flow })
        {
            var rows = data().Select(s => new WidgetRenderer.Row(s.Provider, s.Get(WindowKind.Session) is { } q ? Reading.Of(q, false, DateTimeOffset.Now) : null, s.Long is { } w ? Reading.Of(w, false, DateTimeOffset.Now) : null, null, null)).ToList();
            Save(WidgetRenderer.Render(rows, new WidgetOptions(style, true, true, true, true), new Palette(true), 1, 48, false, out _), "gorev-cubugu-" + style.ToString().ToLowerInvariant() + ".png");
            if (style == WidgetStyle.Identity)
            {
                var narrow = WidgetRenderer.Render(rows.Take(2).ToList(), new WidgetOptions(style, true, true, true, true), new Palette(true), 1, 48, false, out var narrowWidth, rows.Count - 2);
                Save(narrow, "gorev-cubugu-dar-ornek.png");
                Check(narrowWidth > 0 && narrowWidth < WidgetRenderer.Render(rows, new WidgetOptions(style, true, true, true, true), new Palette(true), 1, 48, false, out _).Width, "Taskbar overflow reduces width while preserving names and marks");
            }
        }
        File.WriteAllText(Path.Combine(folder, "smoke-result.json"), "{\"passed\":true,\"data\":\"synthetic demo, not real usage\",\"checks\":[\"compact panel with visible plan badges\",\"provider details and advice\",\"used/remaining\",\"Save and failed Save\",\"presentation, size, slots, placement and style reload\",\"v6 migration preserves presentation and restores named taskbar items\",\"discovery preserves explicit opt-outs\",\"three circular floating styles\",\"dynamic collapsed, expanded, 150 percent, overflow and empty\",\"dynamic hover state and exact account source\",\"hover expansion can be disabled\",\"five taskbar styles and named overflow\",\"motion changes pixels\",\"hover-only timer and reduced motion\"]}");
        void CapturePage(string name) { Save(CaptureFullPage(1), name); }
        void Save(BitmapSource image, string name)
        {
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var output = File.Create(Path.Combine(folder, name)); encoder.Save(output);
        }
    }

    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
