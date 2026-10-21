using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TokenTray;

internal partial class FlyoutWindow
{
    void VerifyLimitSettings(string folder)
    {
        viewStep = 0; settingsTab = 0; OnSettings(this, new RoutedEventArgs());
        void Settle() { Frame.Measure(new Size(Width, double.PositiveInfinity)); Frame.Arrange(new Rect(Frame.DesiredSize)); Frame.UpdateLayout(); }
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        Settle();
        var before = settings.Copy();
        Check(Descendants(Frame).OfType<TextBlock>().Any(t => t.Text == "SGB Limit"), "Settings brand missing");
        Check(Descendants(Frame).OfType<Image>().Count() >= 2, "Presentation previews missing");
        Check(!Descendants(Frame).OfType<Button>().Any(b => b.Content as string == "Gösterge"), "Appearance must use progression rather than tabs");
        var choose = Descendants(Frame).OfType<Button>().First(b => System.Windows.Automation.AutomationProperties.GetName(b).StartsWith("Görev çubuğu"));
        choose.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Settle();
        Check(viewStep == 1 && draft!.Presentation == PresentationMode.Taskbar, "Selecting a presentation must enter its options immediately");
        Descendants(Frame).OfType<Button>().First(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Sinyal").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Settle();
        Check(draft!.TaskbarStyle == WidgetStyle.Signal && settings.TaskbarStyle == before.TaskbarStyle, "Indicator choices must remain a draft");
        Directory.CreateDirectory(folder);
        MediaRenderer.Save(CaptureFullPage(1), Path.Combine(folder, "settings-indicators.png"));
        MediaRenderer.Save(CaptureFullPage(1.5), Path.Combine(folder, "settings-indicators-150.png"));
        SavePreferences(this, new RoutedEventArgs()); Settle();
        Check(viewStep == 2 && settings.Presentation == before.Presentation, "Next must advance without applying the draft");
        OnSettings(this, new RoutedEventArgs()); Settle();
        Check(viewStep == 1 && draft.TaskbarStyle == WidgetStyle.Signal, "Back must preserve draft choices");
        var oldFlow = new Settings { Version = 6, TaskbarStyle = WidgetStyle.Flow, Claude = false, WidgetName = false, Presentation = PresentationMode.Taskbar };
        oldFlow.Upgrade();
        Check(oldFlow.TaskbarStyle == WidgetStyle.Signal && oldFlow.Claude == false && !oldFlow.WidgetName && oldFlow.Presentation == PresentationMode.Taskbar, "Migration must preserve appearance/account preferences");
        var style = new Settings { Version = 6, TaskbarStyle = WidgetStyle.Dial }; style.Upgrade();
        Check(style.TaskbarStyle == WidgetStyle.Dial, "Existing indicator must survive migration");
        draft = null; settingsTab = 0; viewStep = 0; ShowUsage();
    }
    internal static async Task ShowSettingsProbe(string folder)
    {
        Directory.CreateDirectory(folder);
        var options = new Settings { Presentation = PresentationMode.Taskbar, TaskbarStyle = WidgetStyle.Signal };
        var panel = new FlyoutWindow(() => MediaRenderer.Demo(DateTimeOffset.Now), () => [], options, () => { }, () => { }, () => { })
        {
            demoCapture = true, pinned = true, verificationSettingsPath = Path.Combine(folder, "settings-clicks.json"),
            ShowInTaskbar = true, Title = "SGB Limit · Ayar kontrolü"
        };
        var closed = new TaskCompletionSource(); panel.Closed += (_, _) => closed.TrySetResult();
        panel.ShowSettingsAt(null);
        await Task.WhenAny(closed.Task, Task.Delay(TimeSpan.FromMinutes(5)));
        panel.Close();
    }
}
