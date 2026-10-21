using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TokenTray;

internal sealed partial class FloatingIsland
{
    int transitionGeneration;
    bool refreshAfterTransition;
    internal bool TransitionRunning { get; private set; }
    internal static readonly TimeSpan IslandTransitionDuration = TimeSpan.FromMilliseconds(350);

    void StopIslandTransition()
    {
        transitionGeneration++;
        TransitionRunning = false;
        refreshAfterTransition = false;
        // Keep the value currently on screen when replacing an animation clock.
        var width = Width; var height = Height;
        if (double.IsFinite(width)) Width = width;
        if (double.IsFinite(height)) Height = height;
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
    }

    void PresentIslandContent(FrameworkElement next, Size natural, Size target, bool animate)
    {
        var previous = new Size(double.IsFinite(Width) ? Width : ActualWidth, double.IsFinite(Height) ? Height : ActualHeight);
        var outgoing = surface.Child;
        StopIslandTransition();
        var run = animate && IsVisible && settings.Motion && canAnimate() && previous.Width > 0 && previous.Height > 0;
        // Measure content at its natural size throughout the reveal; never squeeze text into
        // the collapsed geometry. This also keeps provider hit targets upright on side docks.
        next.Width = natural.Width; next.Height = natural.Height;
        next.HorizontalAlignment = settings.IslandPosition.IsRightSide() ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        next.VerticalAlignment = VerticalAlignment.Top;
        if (!run)
        {
            surface.Child = next; Width = target.Width; Height = target.Height; Place(); return;
        }

        var generation = transitionGeneration;
        var host = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        surface.Child = null;
        if (outgoing is not null)
        {
            outgoing.IsHitTestVisible = false;
            host.Children.Add(outgoing);
            outgoing.BeginAnimation(OpacityProperty, new DoubleAnimation(outgoing.Opacity, 0, TimeSpan.FromMilliseconds(160)));
        }
        host.Children.Add(next); surface.Child = host;
        next.RenderTransformOrigin = new Point(settings.IslandPosition.IsRightSide() ? 1 : settings.IslandPosition.IsLeftSide() ? 0 : .5,
            settings.IslandPosition.IsSideBottom() ? 1 : settings.IslandPosition.IsSide() ? .5 : 0);
        var contentScale = new ScaleTransform(.94, .94); next.RenderTransform = contentScale;
        var ease = new IslandSpringEase();
        next.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250)));
        contentScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(.94, 1, IslandTransitionDuration) { EasingFunction = ease });
        contentScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(.94, 1, IslandTransitionDuration) { EasingFunction = ease });

        TransitionRunning = true;
        var widthAnimation = new DoubleAnimation(previous.Width, target.Width, IslandTransitionDuration) { EasingFunction = ease };
        var heightAnimation = new DoubleAnimation(previous.Height, target.Height, IslandTransitionDuration) { EasingFunction = ease };
        heightAnimation.Completed += (_, _) =>
        {
            if (generation != transitionGeneration) return;
            var refresh = refreshAfterTransition;
            StopIslandTransition();
            Width = target.Width; Height = target.Height;
            host.Children.Clear(); surface.Child = next;
            next.BeginAnimation(OpacityProperty, null); next.Opacity = 1;
            next.RenderTransform = Transform.Identity;
            Place();
            if (refresh && settings.Presentation == PresentationMode.DynamicIsland) BuildDynamic();
        };
        // Attach clocks before setting base values: there is never a final-size frame before
        // the first interpolation frame, including when hover reverses halfway through.
        BeginAnimation(WidthProperty, widthAnimation, HandoffBehavior.SnapshotAndReplace);
        BeginAnimation(HeightProperty, heightAnimation, HandoffBehavior.SnapshotAndReplace);
        Width = target.Width; Height = target.Height;
        Place();
    }
}

/// <summary>A finite, critically damped spring. It rests completely after 350 ms.</summary>
internal sealed class IslandSpringEase : EasingFunctionBase
{
    internal IslandSpringEase() { EasingMode = EasingMode.EaseIn; }
    protected override double EaseInCore(double t)
    {
        const double damping = 8;
        return (1 - (1 + damping * t) * Math.Exp(-damping * t)) / (1 - (1 + damping) * Math.Exp(-damping));
    }
    protected override Freezable CreateInstanceCore() => new IslandSpringEase();
}
