using System.Windows.Automation;
using static TokenTray.Native;

namespace TokenTray;

/// <summary>
/// Where the taskbar's own buttons are, so the widget can step aside instead of covering them.
/// UI Automation calls into Explorer can be slow, so they run on a dedicated MTA thread and the
/// UI thread only ever reads the last sample.
/// </summary>
internal sealed class TaskbarOccupancy : IDisposable
{
    readonly Thread thread;
    readonly CancellationTokenSource stop = new();
    volatile RECT[] occupied = [];
    volatile bool ready;
    public bool Ready => ready;
    public event Action? Changed;

    public TaskbarOccupancy()
    {
        thread = new Thread(Run) { IsBackground = true, Name = "SGB Limit taskbar sampling", Priority = ThreadPriority.BelowNormal };
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
    }

    /// <summary>Screen rectangles of taskbar buttons outside the tray area.</summary>
    public RECT[] Occupied => occupied;

    public static bool Overlaps(RECT a, IEnumerable<RECT> others, int margin) =>
        others.Any(b => a.Left - margin < b.Right && b.Left < a.Right + margin && a.Top < b.Bottom && b.Top < a.Bottom);

    void Run()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                var next = Sample();
                var changed = !Ready || !Same(next, occupied);
                occupied = next; ready = true;
                if (changed) Changed?.Invoke();
            }
            catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException or TimeoutException)
            {
                if (Ready) { ready = false; Changed?.Invoke(); }
            }
            stop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(1));
        }
    }

    static RECT[] Sample()
    {
        var taskbar = FindWindow("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero) throw new InvalidOperationException("Taskbar unavailable.");
        GetWindowRect(taskbar, out var taskbarRect);
        var trayHwnd = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        RECT tray = default;
        if (trayHwnd != IntPtr.Zero) GetWindowRect(trayHwnd, out tray);

        var root = AutomationElement.FromHandle(taskbar);
        var buttons = root.FindAll(TreeScope.Descendants, new OrCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)));
        var list = new List<RECT>();
        var appButtonsFound = false;
        foreach (AutomationElement b in buttons)
        {
            System.Windows.Rect r;
            try { if (b.Current.IsOffscreen) continue; r = b.Current.BoundingRectangle; } catch (ElementNotAvailableException) { continue; }
            if (r.IsEmpty || r.Width < 4 || r.Height < 4) continue;
            var rect = new RECT { Left = (int)r.Left, Top = (int)r.Top, Right = (int)r.Right, Bottom = (int)r.Bottom };
            // Tray icons, the clock and our own widget live in the tray area; they don't count.
            if (tray.Width > 0 && rect.Left >= tray.Left - 1) continue;
            list.Add(rect);
            var current = b.Current;
            appButtonsFound |= current.ClassName.Contains("TaskList", StringComparison.OrdinalIgnoreCase) ||
                current.AutomationId.StartsWith("Appid:", StringComparison.OrdinalIgnoreCase) ||
                current.ControlType == ControlType.TabItem || current.ControlType == ControlType.ListItem;
        }
        // Some Windows 11 builds expose only tray buttons through UIA. The native Start
        // rectangle still follows the centered group's left edge, including the search box.
        // Legacy MSTaskListWClass bounds omit search and are not a safe occupied boundary.
        var native = LiveNativeBounds();
        return ProtectSample(taskbarRect, tray, list, native, appButtonsFound);
    }

    internal static RECT[] ProtectSample(RECT taskbar, RECT tray, List<RECT> buttons, RECT[] native, bool appButtonsFound)
    {
        buttons.AddRange(native);
        if (!appButtonsFound && native.Length == 0)
        {
            // Start/Search alone do not prove that the application area is empty.
            buttons.Add(new RECT { Left = taskbar.Left, Top = taskbar.Top,
                Right = tray.Width > 0 ? tray.Left : taskbar.Right, Bottom = taskbar.Bottom });
        }
        return [.. buttons];
    }

    internal static RECT? CenteredBounds(RECT taskbar, RECT start)
    {
        if (start.Width <= 0 || start.Height <= 0 || taskbar.Width < taskbar.Height * 2 ||
            start.Top >= taskbar.Bottom || start.Bottom <= taskbar.Top ||
            start.Left < taskbar.Left + taskbar.Width / 8 || start.Left >= taskbar.Left + taskbar.Width / 2)
            return null;
        return new RECT { Left = start.Left, Top = taskbar.Top,
            Right = taskbar.Right - (start.Left - taskbar.Left), Bottom = taskbar.Bottom };
    }

    internal static RECT[] LiveNativeBounds()
    {
        if (!Native.IsWindows11) return [];
        var taskbar = FindWindow("Shell_TrayWnd", null);
        var start = taskbar == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(taskbar, IntPtr.Zero, "Start", null);
        return start != IntPtr.Zero && GetWindowRect(taskbar, out var tb) && GetWindowRect(start, out var st) && CenteredBounds(tb, st) is { } bounds
            ? [bounds] : [];
    }

    static bool Same(RECT[] a, RECT[] b) => a.Length == b.Length && a.Zip(b).All(p => p.First.Equals(p.Second));

    public void Dispose() => stop.Cancel();
}
