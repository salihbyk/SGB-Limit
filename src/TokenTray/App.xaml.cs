using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using TokenTray.Core;

namespace TokenTray;

public partial class App : Application
{
    Mutex? single;
    EventWaitHandle? showSignal;
    RegisteredWaitHandle? showWait;
    Settings settings = new();
    Poller? poller;
    TaskbarWidget? widget;
    TrayIcon? tray;
    FlyoutWindow? flyout;
    Notifier? notifier;
    DispatcherTimer? tick;
    TaskbarOccupancy? occupancy;
    HoverCard? card;
    FloatingIsland? island;
    DateTimeOffset lastDiscovery;
    Native.RECT? CurrentAnchor => island?.IsVisible == true ? island.ScreenBounds : widget?.ScreenBounds;
    DispatcherTimer? hoverDelay;
    readonly SnapshotCache cache = new(System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SGBPulse", "last.json"));

    protected override void OnStartup(StartupEventArgs e)
    {
        // Small, mostly static native surfaces do not need a persistent GPU rendering context.
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        base.OnStartup(e);
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("tr-TR");
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
        if (e.Args.Length > 2 && e.Args[0] == "--capture-presentation")
        {
            Dispatcher.BeginInvoke(async () =>
            {
                try { await PresentationCapture.Run(e.Args[1], e.Args[2]); Shutdown(0); }
                catch (Exception ex) { Log(ex); Shutdown(1); }
            });
            return;
        }
        if (e.Args.Length > 1 && e.Args[0] == "--verify-alignment")
        {
            Dispatcher.BeginInvoke(async () =>
            {
                try { await IslandAlignmentProbe.Run(e.Args[1]); Shutdown(0); }
                catch (Exception ex) { Log(ex); Shutdown(1); }
            });
            return;
        }
        if (e.Args.Length > 1 && e.Args[0] == "--verify-settings-window")
        {
            Dispatcher.BeginInvoke(async () =>
            {
                try { await FlyoutWindow.ShowSettingsProbe(e.Args[1]); Shutdown(0); }
                catch (Exception ex) { Log(ex); Shutdown(1); }
            });
            return;
        }
        if (e.Args.Length > 1 && e.Args[0] == "--verify-taskbar")
        {
            Dispatcher.BeginInvoke(async () =>
            {
                try { await TaskbarLayoutProbe.VerifyNative(e.Args[1]); Shutdown(0); }
                catch (Exception ex) { Log(ex); Shutdown(1); }
            });
            return;
        }
        if (e.Args.Length > 1 && e.Args[0] == "--verify-window")
        {
            Dispatcher.BeginInvoke(async () =>
            {
                try { await IslandWindowProbe.Run(e.Args[1]); Shutdown(0); }
                catch (Exception ex) { Log(ex); Shutdown(1); }
            });
            return;
        }
        if (e.Args.Length > 1 && e.Args[0] == "--verify")
        {
            try
            {
                Palette.UiOverride = true;
                var demo = MediaRenderer.Demo(DateTimeOffset.Now);
                var now = DateTimeOffset.Now;
                var obsolete = RowsFor([
                    new(ProviderId.Codex, SnapshotStatus.Ok, [new(WindowKind.Weekly, "Weekly", 93, now.AddMinutes(-1), TimeSpan.FromDays(7))], now),
                    new(ProviderId.Cursor, SnapshotStatus.Ok, [new(WindowKind.Period, "This month", 82, now.AddDays(1), null)], now.AddHours(-1)),
                    new(ProviderId.Claude, SnapshotStatus.TokenExpired, [], now)
                ], now);
                if (obsolete.Count != 3 || obsolete.Any(r => r.Session != null || r.Weekly != null))
                    throw new InvalidOperationException("Expired/stale readings must have visible provider rows and no taskbar percentages.");
                demo.Add(new ProviderSnapshot(ProviderId.Grok, SnapshotStatus.Offline, [], DateTimeOffset.Now));
                var proof = new FlyoutWindow(() => demo, () => RowsFor(demo, DateTimeOffset.Now), new Settings(), () => { }, () => { }, () => { });
                proof.VerifyAndCapture(e.Args[1]);
                proof.Close();
                Shutdown(0);
            }
            catch (Exception ex) { Log(ex); Shutdown(1); }
            return;
        }
        if (e.Args.Length > 1 && e.Args[0] == "--render-media")
        {
            // README images from sample data. Language first: strings read the culture once.
            var lang = Array.IndexOf(e.Args, "--lang") is var li and >= 0 && li + 1 < e.Args.Length ? e.Args[li + 1] : "en";
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo(lang);
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(lang);
            try
            {
                MediaRenderer.Run(e.Args[1], (snaps, _) => RowsFor(snaps, DateTimeOffset.Now),
                    (data, s) => { settings = s; return new FlyoutWindow(data, () => [], s, () => { }, () => { }, () => { }); });
            }
            catch (Exception ex) { Log(ex); }
            Shutdown();
            return;
        }
        single = new Mutex(true, @"Local\SGBPulse.SingleInstance", out var first);
        if (!first)
        {
            try { using var existing = EventWaitHandle.OpenExisting(@"Local\SGBPulse.Show"); existing.Set(); }
            catch (WaitHandleCannotBeOpenedException) { }
            Shutdown(); return;
        }

        DispatcherUnhandledException += (_, ex) => { Log(ex.Exception); ex.Handled = true; };
        settings = Settings.Load();
        Palette.UiOverride = settings.UiDark;

        tray = new TrayIcon();
        notifier = new Notifier(tray);
        flyout = new FlyoutWindow(Snapshots, WidgetRows, settings, OnSettingsChanged, () => poller?.RefreshNow(), Quit);
        widget = new TaskbarWidget { Offset = () => settings.TaskbarOffset, Options = () => settings.Widget, Occupied = () => occupancy?.Ready == true ? occupancy.Occupied : null };
        island = new FloatingIsland(settings, Snapshots, source => { card?.Hide(); flyout.OpenSource(source, CurrentAnchor); }, () => flyout.ShowSettingsAt(CurrentAnchor));
        showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\SGBPulse.Show");
        showWait = ThreadPool.RegisterWaitForSingleObject(showSignal, (_, _) => Dispatcher.BeginInvoke(() => flyout.ShowAt(CurrentAnchor)), null, Timeout.Infinite, false);

        // Hovering the widget shows the glance card after a short dwell.
        card = new HoverCard();
        hoverDelay = new DispatcherTimer(TimeSpan.FromMilliseconds(380), DispatcherPriority.Normal, (_, _) =>
        {
            hoverDelay!.Stop();
            if (flyout?.IsVisible == true || widget.ScreenBounds is not { } bounds) return;
            card.PollingInterval = TimeSpan.FromSeconds(Math.Max(120, settings.IntervalSeconds));
            card.ShowFor(Snapshots(), settings.ShowRemaining, bounds);
        }, Dispatcher);
        hoverDelay.Stop();
        widget.HoverChanged += over =>
        {
            if (over) hoverDelay.Start();
            else { hoverDelay.Stop(); card.Hide(); }
        };

        tray.Clicked += () => { card?.Hide(); flyout.Toggle(CurrentAnchor); };
        tray.RightClicked += ShowMenu;
        widget.Clicked += () => { card?.Hide(); flyout.Toggle(widget.ScreenBounds); };
        widget.RightClicked += ShowMenu;

        StartPolling();
        ApplyWidget();

        // Countdowns move every minute even without new data.
        tick = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, (_, _) => Render(), Dispatcher);
        tick.Start();

        SystemEvents.UserPreferenceChanged += (_, _) => Dispatcher.BeginInvoke(Render);
        SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.BeginInvoke(() => { widget?.Redraw(); island?.Place(); });
        SystemEvents.PowerModeChanged += (_, pe) => { if (pe.Mode == PowerModes.Resume) poller?.RefreshNow(); };

        if (e.Args.Contains("--open")) Dispatcher.BeginInvoke(() => flyout.ShowAt(CurrentAnchor), DispatcherPriority.ApplicationIdle);
        if (!settings.Welcomed) Dispatcher.BeginInvoke(Welcome, DispatcherPriority.ApplicationIdle);
        if (e.Args.Contains("--settings")) Dispatcher.BeginInvoke(() => flyout.ShowSettingsAt(CurrentAnchor), DispatcherPriority.ApplicationIdle);
        var captureIndex = Array.IndexOf(e.Args, "--capture-live");
        if (captureIndex >= 0 && captureIndex + 1 < e.Args.Length)
        {
            var captureTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(25) };
            captureTimer.Tick += (_, _) =>
            {
                captureTimer.Stop();
                try
                {
                    var dir = e.Args[captureIndex + 1];
                    System.IO.Directory.CreateDirectory(dir);
                    var options = settings.Copy(); options.ShowEmail = false;
                    var capturePanel = new FlyoutWindow(Snapshots, WidgetRows, options, () => { }, () => { }, () => { });
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(capturePanel.RenderForMedia(1)));
                    using (var image = System.IO.File.Create(System.IO.Path.Combine(dir, "panel-canli.png"))) encoder.Save(image);
                    capturePanel.Close();
                    double? widgetWidth = null;
                    if (island?.IsVisible == true)
                    {
                        var bitmap = MediaRenderer.Capture((FrameworkElement)island.Content, 1);
                        widgetWidth = bitmap.Width;
                        MediaRenderer.Save(bitmap, System.IO.Path.Combine(dir, "secili-ada-canli.png"));
                    }
                    else if (settings.Presentation == PresentationMode.Taskbar && widget?.LastRendered is { } taskbarBitmap)
                        MediaRenderer.Save(taskbarBitmap, System.IO.Path.Combine(dir, "gorev-cubugu-canli.png"));
                    // Render both dynamic states with real readings without replacing the user's selected mode.
                    options.Presentation = PresentationMode.DynamicIsland;
                    var proofIsland = new FloatingIsland(options, Snapshots, _ => { }, () => { });
                    foreach (var full in new[] { false, true })
                    {
                        proofIsland.Refresh(); proofIsland.SetExpanded(full, false);
                        var visual = (FrameworkElement)proofIsland.Content;
                        visual.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); visual.Arrange(new Rect(visual.DesiredSize)); visual.UpdateLayout();
                        MediaRenderer.Save(MediaRenderer.Capture(visual, 1), System.IO.Path.Combine(dir, full ? "dinamik-gercek-veri-acik.png" : "dinamik-gercek-veri-kapali.png"));
                    }
                    proofIsland.Close();
                    if (Snapshots().Any(s => s.Provider == ProviderId.Trae))
                    {
                        var traePanel = new FlyoutWindow(Snapshots, WidgetRows, options, () => { }, () => { }, () => { });
                        MediaRenderer.Save(traePanel.RenderProviderForMedia(ProviderId.Trae.ToString(), 1), System.IO.Path.Combine(dir, "trae-panel-gercek-veri.png"));
                        traePanel.Close();
                    }
                    System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "ada-placement.json"), System.Text.Json.JsonSerializer.Serialize(new { width = widgetWidth, presentation = settings.Presentation.ToString(), position = settings.IslandPosition.ToString(), dynamicEvidence = "Offscreen native production component with live provider readings; selected mode is not changed", bounds = CurrentAnchor is { } b ? new { left = b.Left, top = b.Top, right = b.Right, bottom = b.Bottom } : null }));
                    var report = Snapshots().Select(s => new { provider = s.Provider.ToString(), status = s.Status.ToString(), stale = s.Stale, fetchedAt = s.FetchedAt, windows = s.Windows.Select(w => new { kind = w.Kind.ToString(), used = w.UsedPercent, resetsAt = w.ResetsAt }) });
                    System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "canli-durum.json"), System.Text.Json.JsonSerializer.Serialize(report));
                    using var process = System.Diagnostics.Process.GetCurrentProcess();
                    System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "kaynak-kullanimi.json"), System.Text.Json.JsonSerializer.Serialize(new { workingSetMB = process.WorkingSet64 / 1048576.0, privateMB = process.PrivateMemorySize64 / 1048576.0, managedMB = GC.GetTotalMemory(false) / 1048576.0, managedHeapMB = GC.GetGCMemoryInfo().HeapSizeBytes / 1048576.0 }));
                }
                catch (Exception ex) { Log(ex); }
            };
            captureTimer.Start();
        }
    }

    /// <summary>
    /// First run: open the flyout so people see what they got, and say where the tray icon went,
    /// since Windows 11 tucks new ones behind the ^ overflow.
    /// </summary>
    void Welcome()
    {
        settings.Welcomed = true;
        settings.Save();
        flyout?.ShowAt(CurrentAnchor);
        tray?.Notify(S.WelcomeTitle, "Desteklenen hesapların bu bilgisayardan bulunur. Adadaki hesaba tıkla; limitleri ve kullanım önerisini gör. SGB yazısından tutarak adayı taşı.");
    }

    void StartPolling()
    {
        poller?.Dispose();
        poller = new Poller(settings.BuildProviders(), TimeSpan.FromSeconds(Math.Max(120, settings.IntervalSeconds)));
        foreach (var snap in cache.Load()) poller.Seed(snap);
        foreach (var (key, until) in cache.LoadHolds()) poller.Hold(key, until);
        poller.Updated += snap => Dispatcher.BeginInvoke(() =>
        {
            if (settings.Notifications) notifier?.Observe(snap);
            if (snap.Status == SnapshotStatus.Ok && !snap.Stale) { cache.Save([snap]); cache.SaveHold(snap.Source, null); }
            if (snap.Status == SnapshotStatus.RateLimited) cache.SaveHold(snap.Source, snap.RetryAt);
            Render();
        });
        poller.Start();
        Render();
    }

    IReadOnlyList<ProviderSnapshot> Snapshots()
    {
        if (poller is null) return [];
        return poller.All
            .OrderBy(p => p.Id).ThenBy(p => p.Key)
            .Select(p => poller.Latest(p.Key) ?? new ProviderSnapshot(p.Id, SnapshotStatus.Loading, [], DateTimeOffset.Now,
                Key: p.Key == p.Id.ToString() ? null : p.Key,
                Name: settings.Accounts.FirstOrDefault(a => $"{a.Provider}:{a.Path}" == p.Key)?.Name))
            .ToList();
    }

    IReadOnlyList<WidgetRenderer.Row> WidgetRows() => RowsFor(Snapshots(), DateTimeOffset.Now);

    IReadOnlyList<WidgetRenderer.Row> RowsFor(IReadOnlyList<ProviderSnapshot> snapshots, DateTimeOffset now) =>
        TaskbarReadings.FromSnapshots(snapshots, settings.ShowRemaining, now, TimeSpan.FromSeconds(settings.IntervalSeconds));
    /// <summary>A reading that failed to refresh, or is older than two polls, is shown as old.</summary>
    bool IsOld(ProviderSnapshot s, DateTimeOffset now) =>
        s.Windows.Count > 0 && !s.IsCurrent(now, TimeSpan.FromSeconds(Math.Max(120, settings.IntervalSeconds)));

    void Render()
    {
        var now = DateTimeOffset.Now;
        if (settings.AutoDiscover && now - lastDiscovery > TimeSpan.FromMinutes(5))
        {
            lastDiscovery = now;
            var wanted = settings.BuildProviders().Select(p => p.Key).OrderBy(k => k).ToList();
            if (poller is not null && !wanted.SequenceEqual(poller.Sources.OrderBy(k => k))) { OnSettingsChanged(); return; }
        }
        var snaps = Snapshots();

        // Tray icon follows whichever provider is closest to running out.
        var tightest = snaps
            .Where(s => !IsOld(s, now) && s.Binding is { } b && (b.ResetsAt is null || b.ResetsAt > now))
            .Select(s => (s, r: Reading.Of(s.Binding!, remaining: true, now)))
            .OrderBy(x => x.r.Percent)
            .FirstOrDefault();
        var tip = string.Join("\n", snaps.Select(s => s.Binding is { ResetsAt: { } reset } && reset <= now
            ? $"{S.Label(s)}: Yenileniyor; güncel ölçüm bekleniyor"
            : s.Binding is { } b
            ? $"{S.Label(s)}: {S.Percent(settings.ShowRemaining ? b.RemainingPercent : b.UsedPercent)} {(settings.ShowRemaining ? S.Left : S.Used)} · {S.Window(b)}" +
              (b.ResetsAt is { } r && r > now ? $" · {S.ShortDuration(r - now)}" : "") +
              (IsOld(s, now) ? (S.Tr ? $" (son okuma {S.ShortDuration(now - s.FetchedAt)} önce)" : $" (read {S.ShortDuration(now - s.FetchedAt)} ago)") : "")
            : $"{S.Label(s)}: {S.Status(s)}"));
        if (tip.Length == 0) tip = "SGB Limit";
        var shown = tightest.s is null ? (Reading?)null : Reading.Of(tightest.s.Binding!, settings.ShowRemaining, now);
        tray?.Update(tightest.s?.Provider, shown, snaps.Any(s => s.Windows.Count == 0 && s.Status != SnapshotStatus.Loading) ? "!" : null, tip);

        if (settings.Presentation == PresentationMode.Taskbar) widget?.Redraw();
        if (settings.Presentation != PresentationMode.Taskbar && settings.IslandEnabled) island?.Refresh();
        flyout?.Refresh();
    }

    void ApplyWidget()
    {
        if (widget is null) return;
        if (settings.Presentation == PresentationMode.Taskbar)
        {
            if (occupancy is null) { occupancy = new TaskbarOccupancy(); occupancy.Changed += () => Dispatcher.BeginInvoke(() => widget?.Redraw()); }
            widget.Show(WidgetRows);
        }
        else { widget.Hide(); occupancy?.Dispose(); occupancy = null; }
        if (settings.Presentation != PresentationMode.Taskbar && settings.IslandEnabled && island is not null) { island.Refresh(); island.Show(); island.Place(); }
        else island?.Hide();
    }

    void OnSettingsChanged()
    {
        var wanted = settings.BuildProviders().Select(p => p.Key).OrderBy(k => k).ToList();
        var providersChanged = poller is null || !wanted.SequenceEqual(poller.Sources.OrderBy(k => k));
        settings.Save();
        Palette.UiOverride = settings.UiDark;
        if (providersChanged) StartPolling();
        else if (poller != null) poller.Interval = TimeSpan.FromSeconds(Math.Max(120, settings.IntervalSeconds));
        ApplyWidget();
        Render();
    }

    void ShowMenu()
    {
        flyout?.HideFlyout();
        var p = new Palette(Palette.UiDark);
        var menu = new ContextMenu { Style = (Style)Resources["TrayMenu"] };
        menu.Resources["MenuSurface"] = Palette.Brush(p.Surface);
        menu.Resources["MenuHairline"] = Palette.Brush(p.Hairline);
        menu.Resources["MenuText"] = Palette.Brush(p.Text);
        menu.Resources["MenuHover"] = Palette.Brush(p.Track);
        void Add(string text, Action act)
        {
            var mi = new MenuItem { Header = text, Style = (Style)Resources["TrayMenuItem"] };
            mi.Click += (_, _) => act();
            menu.Items.Add(mi);
        }
        Add(S.Refresh, () => poller?.RefreshNow());
        Add(S.Settings, () => flyout?.ShowSettingsAt(CurrentAnchor));
        menu.Items.Add(new Separator { Style = (Style)Resources["TrayMenuSeparator"] });
        Add(S.Quit, Quit);
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
        // A popup without an active owner doesn't close on outside clicks; give it focus.
        if (PresentationSource.FromVisual(menu) is System.Windows.Interop.HwndSource src) Native.SetForegroundWindow(src.Handle);
    }

    void Quit()
    {
        tick?.Stop();
        occupancy?.Dispose();
        card?.Close();
        island?.Close();
        poller?.Dispose();
        widget?.Dispose();
        tray?.Dispose();
        flyout?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        widget?.Dispose();
        tray?.Dispose();
        single?.Dispose();
        showWait?.Unregister(null);
        showSignal?.Dispose();
        base.OnExit(e);
    }

    internal static void Log(Exception ex)
    {
        try
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SGBPulse.log");
            System.IO.File.AppendAllText(path, $"{DateTimeOffset.Now:u} {ex}\n\n");
        }
        catch { }
    }
}
