using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TokenTray.Core;

namespace TokenTray;

internal sealed partial class FloatingIsland : Window
{
    readonly Settings settings;
    readonly Func<IReadOnlyList<ProviderSnapshot>> data;
    readonly Action<string?> open;
    readonly Action changed;
    readonly Func<bool> canAnimate;
    readonly Border surface;
    readonly DispatcherTimer motion;
    internal bool MotionRunning => motion.IsEnabled;

    internal FloatingIsland(Settings settings, Func<IReadOnlyList<ProviderSnapshot>> data, Action<string?> open, Action changed, Func<bool>? canAnimate = null)
    {
        this.settings = settings; this.data = data; this.open = open; this.changed = changed;
        this.canAnimate = canAnimate ?? (() => SystemParameters.ClientAreaAnimation);
        Title = "SGB Limit · Ada"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight; ShowActivated = false; UseLayoutRounding = true;
        surface = new Border { BorderThickness = new Thickness(1), BorderBrush = Palette.Brush(new Palette(true).Hairline), ClipToBounds = true };
        Content = surface;
        motion = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        motion.Tick += (_, _) => { if (orbitTrack is not null) { orbitTrack.Phase = (Environment.TickCount64 % 3000) / 3000.0; orbitTrack.InvalidateVisual(); } };
        collapseDelay.Tick += (_, _) => { collapseDelay.Stop(); if (!IsMouseOver && !IsKeyboardFocusWithin) SetExpanded(false); };
        MouseEnter += (_, _) =>
        {
            collapseDelay.Stop();
            if (settings.Presentation == PresentationMode.DynamicIsland) { if (settings.ExpandOnHover) SetExpanded(true); return; }
            if (!settings.Motion || !this.canAnimate()) return;
            surface.BeginAnimation(OpacityProperty, new DoubleAnimation(0.85, 1, TimeSpan.FromMilliseconds(180)));
            if (settings.WidgetStyle == WidgetStyle.Comet) motion.Start();
        };
        MouseLeave += (_, _) => { if (settings.Presentation == PresentationMode.DynamicIsland) collapseDelay.Start(); motion.Stop(); if (orbitTrack is not null) { orbitTrack.Phase = null; orbitTrack.InvalidateVisual(); } };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { SetExpanded(false); e.Handled = true; } };
        IsKeyboardFocusWithinChanged += (_, _) => { if (IsKeyboardFocusWithin && settings.Presentation == PresentationMode.DynamicIsland) SetExpanded(true); else if (!IsMouseOver) collapseDelay.Start(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { motion.Stop(); collapseDelay.Stop(); StopIslandTransition(); expanded = false; } };
        SizeChanged += (_, _) => Place();
        Loaded += (_, _) => Place();
        Closed += (_, _) => { motion.Stop(); collapseDelay.Stop(); StopIslandTransition(); };
    }

    internal Native.RECT? ScreenBounds => new WindowInteropHelper(this).Handle is var h && h != IntPtr.Zero && Native.GetWindowRect(h, out var rect) ? rect : null;

    internal void Refresh()
    {
        if (settings.Presentation == PresentationMode.DynamicIsland)
        {
            // Polling is independent of hover. A fresh reading must not tear down an in-flight morph.
            if (TransitionRunning) { refreshAfterTransition = true; return; }
            BuildDynamic(); return;
        }
        StopIslandTransition();
        BuildOrbital();
    }


    internal void Place()
    {
        var work = SystemParameters.WorkArea;
        var width = double.IsFinite(Width) ? Width : ActualWidth;
        var height = double.IsFinite(Height) ? Height : ActualHeight;
        var position = settings.IslandPosition;
        var edgeGap = settings.Presentation == PresentationMode.DynamicIsland ? 0 : 18;
        Left = position switch
        {
            IslandPosition.TopLeft or IslandPosition.BottomLeft or IslandPosition.SideLeftTop or IslandPosition.SideLeftCenter or IslandPosition.SideLeftBottom => work.Left + edgeGap,
            IslandPosition.TopRight or IslandPosition.BottomRight or IslandPosition.SideRightTop or IslandPosition.SideRightCenter or IslandPosition.SideRightBottom => work.Right - width - edgeGap,
            IslandPosition.Free when settings.IslandLeft is { } x && double.IsFinite(x) => Math.Clamp(x, work.Left, Math.Max(work.Left, work.Right - width)),
            _ => work.Left + (work.Width - width) / 2,
        };
        Top = position switch
        {
            IslandPosition.BottomLeft or IslandPosition.BottomCenter or IslandPosition.BottomRight => work.Bottom - height - (settings.Presentation == PresentationMode.DynamicIsland ? 0 : 18),
            IslandPosition.SideLeftTop or IslandPosition.SideRightTop => work.Top,
            IslandPosition.SideLeftCenter or IslandPosition.SideRightCenter => work.Top + (work.Height - height) / 2,
            IslandPosition.SideLeftBottom or IslandPosition.SideRightBottom => work.Bottom - height,
            IslandPosition.Free when settings.IslandTop is { } y && double.IsFinite(y) => Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - height)),
            _ => settings.Presentation == PresentationMode.DynamicIsland ? 0 : work.Top + 14,
        };
    }

}

