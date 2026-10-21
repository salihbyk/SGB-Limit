using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TokenTray.Core;

namespace TokenTray;

internal sealed partial class FloatingIsland
{
    bool expanded;
    double sideCollapsedHeight;
    readonly DispatcherTimer collapseDelay = new() { Interval = TimeSpan.FromMilliseconds(240) };
    internal bool Expanded => expanded;
    internal bool CollapsePending => collapseDelay.IsEnabled;

    internal void SetExpanded(bool value, bool animate = true)
    {
        if (settings.Presentation != PresentationMode.DynamicIsland || expanded == value) return;
        expanded = value;
        BuildDynamic(animate);
    }

    void BuildDynamic(bool animate = false)
    {
        motion.Stop();
        SizeToContent = SizeToContent.Manual;
        var p = new Palette(true);
        surface.Background = Palette.Brush(Color.FromRgb(8, 10, 13));
        surface.BorderBrush = Palette.Brush(Color.FromRgb(43, 47, 52));
        surface.Padding = expanded ? new Thickness(17, 13, 17, 14) : new Thickness(13, 8, 13, 8);
        var dockTop = settings.IslandPosition is IslandPosition.TopLeft or IslandPosition.TopCenter or IslandPosition.TopRight;
        var dockBottom = settings.IslandPosition is IslandPosition.BottomLeft or IslandPosition.BottomCenter or IslandPosition.BottomRight;
        var side = settings.IslandPosition.IsSide();
        var vertical = side && !expanded;
        surface.CornerRadius = settings.IslandPosition.IsLeftSide() ? new CornerRadius(0, 16, 16, 0) : settings.IslandPosition.IsRightSide() ? new CornerRadius(16, 0, 0, 16) : dockTop ? new CornerRadius(0, 0, 14, 14) : dockBottom ? new CornerRadius(14, 14, 0, 0) : new CornerRadius(18);
        surface.BorderThickness = settings.IslandPosition.IsLeftSide() ? new Thickness(0, 1, 1, 1) : settings.IslandPosition.IsRightSide() ? new Thickness(1, 1, 0, 1) : dockTop ? new Thickness(1, 0, 1, 1) : dockBottom ? new Thickness(1, 1, 1, 0) : new Thickness(1);
        if (vertical) surface.Padding = new Thickness(10, 12, 10, 12);
        var scale = Math.Clamp(settings.IslandScale, 0.85, 1.15);
        surface.LayoutTransform = new ScaleTransform(scale, scale);
        var stack = new StackPanel();
        var snaps = data();
        var slots = Math.Clamp(settings.IslandSlots, 3, 8);
        // Fit the real work area, including Windows DPI, rather than clipping a large account list.
        var fit = Math.Max(1, (int)(((vertical ? SystemParameters.WorkArea.Height : SystemParameters.WorkArea.Width) / scale - 90) / (expanded ? 114 : vertical ? 42 : 57)));
        var shown = snaps.Take(Math.Min(slots, fit)).ToList();
        var line = new StackPanel { Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (expanded)
        {
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            var all = ActionButton("Tümü aç", p.TextMuted, () => open(null)); actions.Children.Add(all);
            actions.Children.Add(ActionButton("Ayarlar", p.TextMuted, changed));
            DockPanel.SetDock(actions, Dock.Right); header.Children.Add(actions);
            var brand = Grip(p, true); header.Children.Add(brand);
            stack.Children.Add(header);
            var mode = Text(settings.ShowRemaining ? "Kalan limitler" : "Kullanılan limitler", 10, p.TextMuted);
            mode.Margin = new Thickness(0, 0, 0, 7); stack.Children.Add(mode);
        }
        else
        {
            var grip = Grip(p, false);
            if (vertical) { grip.Margin = new Thickness(0, 0, 0, 13); grip.HorizontalAlignment = HorizontalAlignment.Center; }
            line.Children.Add(grip);
        }

        foreach (var snap in shown)
        {
            var current = snap.IsCurrent(DateTimeOffset.Now, TimeSpan.FromSeconds(Math.Max(120, settings.IntervalSeconds)));
            var quota = snap.Binding;
            var valid = current && quota is not null && (quota.ResetsAt is null || quota.ResetsAt > DateTimeOffset.Now);
            var historical = HistoricalIslandReading(snap, DateTimeOffset.Now);
            var historyLabel = $"Son okuma {snap.FetchedAt.ToLocalTime():HH:mm} · güncel değil";
            var hue = valid ? p.Fill(snap.Provider, quota!, Pace.Of(quota!, DateTimeOffset.Now)) : historical ? p.AmberText : p.TextMuted;
            var box = new StackPanel { Width = expanded ? 102 : vertical ? 39 : historical ? 57 : 49, Margin = vertical ? new Thickness(0, 0, 0, 13) : new Thickness(0, 0, expanded ? 12 : 8, 0) };
            var identity = new StackPanel { Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = vertical ? HorizontalAlignment.Center : HorizontalAlignment.Stretch };
            identity.Children.Add(ProviderMark.Element(snap.Provider, p.Hue(snap.Provider), expanded ? 20 : 15));
            if (expanded)
            {
                var name = Text(S.FullLabel(snap), 11.5, p.Text);
                name.Margin = new Thickness(7, 0, 0, 0); name.Width = 75; name.TextTrimming = TextTrimming.CharacterEllipsis;
                identity.Children.Add(name); box.Children.Add(identity);
                var windows = ExpandedWindows(snap);
                if (windows.Count == 0) box.Children.Add(Text(snap.Status == SnapshotStatus.Loading ? "Okunuyor…" : "Kota okunamadı", 10.5, p.TextMuted));
                foreach (var w in windows)
                {
                    var live = current && (w.ResetsAt is null || w.ResetsAt > DateTimeOffset.Now);
                    var row = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
                    var value = Text(live || historical ? S.Percent(settings.ShowRemaining ? w.RemainingPercent : w.UsedPercent) + (historical ? "*" : "") : "—", 17, historical ? p.AmberText : live ? p.Fill(snap.Provider, w, Pace.Of(w, DateTimeOffset.Now)) : p.TextMuted);
                    DockPanel.SetDock(value, Dock.Right); row.Children.Add(value);
                    var label = Text(snap.Provider == ProviderId.Trae ? "Dönem" : S.Window(w).Replace("Oturum (", "").TrimEnd(')'), 10, p.TextMuted);
                    label.ToolTip = S.Window(w);
                    label.Width = 59; label.TextTrimming = TextTrimming.CharacterEllipsis; label.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(label);
                    box.Children.Add(row);
                    if (w.Detail is { } amount) box.Children.Add(Text(amount, 9.5, p.TextMuted));
                }
                if (valid)
                {
                    box.Children.Add(new Border { Height = 2, Background = Palette.Brush(p.Track), Margin = new Thickness(0, 8, 0, 0), Child = new Border { Background = Palette.Brush(hue), Width = 102 * Math.Clamp((settings.ShowRemaining ? quota!.RemainingPercent : quota!.UsedPercent) / 100, 0, 1), HorizontalAlignment = HorizontalAlignment.Left } });
                    if (settings.IslandReset) { var reset = Text(quota!.ResetsAt is { } r ? S.ShortDuration(r - DateTimeOffset.Now) + " · yenilenir" : "Süre bildirilmedi", 9.5, p.TextMuted); reset.Margin = new Thickness(0, 8, 0, 0); box.Children.Add(reset); }
                }
                if (historical)
                {
                    var readAt = Text(historyLabel, 9, p.AmberText);
                    readAt.Width = 102; readAt.TextWrapping = TextWrapping.Wrap; readAt.Margin = new Thickness(0, 8, 0, 0);
                    box.Children.Add(readAt);
                }
            }
            else
            {
                var value = Text(valid || historical ? S.Percent(settings.ShowRemaining ? quota!.RemainingPercent : quota!.UsedPercent) + (historical ? "*" : "") : "—", 11, hue);
                value.Margin = vertical ? new Thickness(0, 4, 0, 0) : new Thickness(5, 0, 0, 0); value.HorizontalAlignment = HorizontalAlignment.Center;
                identity.Children.Add(value); box.Children.Add(identity);
            }
            var button = SurfaceButton(box, () => open(snap.Source));
            button.ToolTip = S.FullLabel(snap) + " · " + (settings.ShowRemaining ? "kalan" : "kullanılan") + "\n" + (historical ? historyLabel + "\n* Önceki ölçüm" : valid ? S.Window(quota!) : snap.Message ?? S.Status(snap));
            AutomationProperties.SetName(button, S.FullLabel(snap) + ", " + (valid || historical ? S.Percent(settings.ShowRemaining ? quota!.RemainingPercent : quota!.UsedPercent) + (historical ? ", " + historyLabel : "") : "kota okunamadı") + ", limitleri aç");
            line.Children.Add(button);
        }
        if (snaps.Count == 0) line.Children.Add(ActionButton("Hesapları bul", p.Text, changed));
        if (snaps.Count > shown.Count) line.Children.Add(ActionButton("+" + (snaps.Count - shown.Count), p.Text, () => open(null)));
        stack.Children.Add(line);
        stack.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        // Resizing Width remeasures the tree synchronously; snapshot both dimensions first.
        var natural = stack.DesiredSize;
        var targetWidth = Math.Ceiling((natural.Width + surface.Padding.Left + surface.Padding.Right + surface.BorderThickness.Left + surface.BorderThickness.Right) * scale);
        var targetHeight = Math.Ceiling((natural.Height + surface.Padding.Top + surface.Padding.Bottom + surface.BorderThickness.Top + surface.BorderThickness.Bottom) * scale);
        // Keep the entire original hover region inside the expanded side surface. Otherwise
        // hovering a provider near an end can trigger leave/enter repeatedly as it unfolds.
        if (side && !expanded) sideCollapsedHeight = targetHeight;
        if (side && expanded) targetHeight = Math.Max(targetHeight, sideCollapsedHeight);
        PresentIslandContent(stack, natural, new Size(targetWidth, targetHeight), animate);
    }

    FrameworkElement Grip(Palette p, bool full)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Cursor = Cursors.SizeAll, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, full ? 15 : 13, 0), ToolTip = "Tut ve sürükle · çift tıkla tümünü aç" };
        row.Children.Add(new PulseMark { Width = 23, Height = 23, Ink = p.Accent });
        if (full) { var brand = Text("SGB Limit", 13, p.Text); brand.FontWeight = FontWeights.SemiBold; brand.Margin = new Thickness(8, 0, 0, 0); brand.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(brand); }
        row.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) { open(null); return; }
            StopIslandTransition(); collapseDelay.Stop();
            try { DragMove(); settings.IslandPosition = IslandPosition.Free; settings.IslandLeft = Left; settings.IslandTop = Top; settings.Save(); Refresh(); }
            catch (InvalidOperationException) { }
        };
        return row;
    }

    static TextBlock Text(string text, double size, Color color) => new() { Text = text, FontSize = size, Foreground = Palette.Brush(color), TextWrapping = TextWrapping.NoWrap };
    internal static bool HistoricalIslandReading(ProviderSnapshot snap, DateTimeOffset now) =>
        snap.Provider == ProviderId.Antigravity && snap.Stale && snap.Binding is not null &&
        !string.IsNullOrWhiteSpace(snap.Account) && snap.FetchedAt <= now &&
        snap.Status is not (SnapshotStatus.NotSignedIn or SnapshotStatus.TokenExpired);
    internal static IReadOnlyList<UsageWindow> ExpandedWindows(ProviderSnapshot snapshot)
    {
        var windows = snapshot.MainWindows.ToList();
        if (snapshot.Binding is { } binding && !windows.Contains(binding)) windows.Insert(0, binding);
        return windows;
    }
    static Button ActionButton(string title, Color color, Action action) => SurfaceButton(new TextBlock { Text = title, FontSize = 10.5, Foreground = Palette.Brush(color), Margin = new Thickness(6, 3, 6, 3) }, action);
    static Button SurfaceButton(FrameworkElement content, Action action)
    {
        var button = new Button { Content = content, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), Cursor = Cursors.Hand, FocusVisualStyle = null };
        var face = new FrameworkElementFactory(typeof(Border)); face.Name = "face";
        face.SetValue(Border.BackgroundProperty, Brushes.Transparent); face.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
        face.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        var template = new ControlTemplate(typeof(Button)) { VisualTree = face };
        foreach (var property in new[] { IsMouseOverProperty, IsKeyboardFocusedProperty })
        {
            var trigger = new Trigger { Property = property, Value = true };
            trigger.Setters.Add(new Setter(Border.BackgroundProperty, Palette.Brush(Color.FromRgb(30, 34, 38)), "face")); template.Triggers.Add(trigger);
        }
        button.Template = template; button.Click += (_, _) => action(); return button;
    }
}
