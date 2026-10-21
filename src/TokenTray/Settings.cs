using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using TokenTray.Core;

namespace TokenTray;

public sealed class Settings
{
    public int Version { get; set; } = 7;
    /// <summary>Off by default, matching how Claude and Codex report usage themselves.</summary>
    public bool ShowRemaining { get; set; }
    public bool? Claude { get; set; }
    public bool? Codex { get; set; }
    public bool AutoDiscover { get; set; } = true;
    // Null means "on when the tool is found on this PC", so new users see what they have.
    public bool? Antigravity { get; set; }
    public bool? Grok { get; set; }
    public bool? Cursor { get; set; }
    public bool? OpenCode { get; set; }
    public bool? Trae { get; set; }
    /// <summary>Additional Claude Code or Codex sign-ins, each in its own config folder.</summary>
    public List<ExtraAccount> Accounts { get; set; } = [];
    public bool Welcomed { get; set; }

    public bool IsOn(ProviderId id) => id switch
    {
        ProviderId.Claude => Claude ?? ClaudeProvider.IsInstalled(),
        ProviderId.Codex => Codex ?? CodexProvider.IsInstalled(),
        ProviderId.Antigravity => Antigravity ?? AntigravityProvider.IsInstalled(),
        ProviderId.Grok => Grok ?? GrokBotProvider.IsInstalled(),
        ProviderId.Cursor => Cursor ?? CursorProvider.IsInstalled(),
        ProviderId.OpenCode => OpenCode ?? OpenCodeProvider.IsInstalled(),
        ProviderId.Trae => Trae ?? TraeProvider.IsInstalled(),
        _ => false,
    };

    public void Set(ProviderId id, bool on)
    {
        switch (id)
        {
            case ProviderId.Claude: Claude = on; break;
            case ProviderId.Codex: Codex = on; break;
            case ProviderId.Antigravity: Antigravity = on; break;
            case ProviderId.Grok: Grok = on; break;
            case ProviderId.Cursor: Cursor = on; break;
            case ProviderId.OpenCode: OpenCode = on; break;
            case ProviderId.Trae: Trae = on; break;
        }
    }

    public static bool IsInstalled(ProviderId id) => id switch
    {
        ProviderId.Claude => ClaudeProvider.IsInstalled(),
        ProviderId.Codex => CodexProvider.IsInstalled(),
        ProviderId.Antigravity => AntigravityProvider.IsInstalled(),
        ProviderId.Grok => GrokBotProvider.IsInstalled(),
        ProviderId.Cursor => CursorProvider.IsInstalled(),
        ProviderId.OpenCode => OpenCodeProvider.IsInstalled(),
        ProviderId.Trae => TraeProvider.IsInstalled(),
        _ => false,
    };

    /// <summary>Every source to poll: enabled providers, then extra accounts.</summary>
    public IEnumerable<IUsageProvider> BuildProviders()
    {
        if (IsOn(ProviderId.Claude)) yield return new ClaudeProvider();
        if (IsOn(ProviderId.Codex)) yield return new CodexProvider();
        if (IsOn(ProviderId.Antigravity)) yield return new AntigravityProvider();
        if (IsOn(ProviderId.Grok)) yield return new GrokBotProvider();
        if (IsOn(ProviderId.Cursor)) yield return new CursorProvider();
        if (IsOn(ProviderId.OpenCode)) yield return new OpenCodeProvider();
        if (IsOn(ProviderId.Trae)) yield return new TraeProvider();
        foreach (var a in Accounts)
        {
            var key = $"{a.Provider}:{a.Path}";
            if (a.Provider == ProviderId.Claude) yield return new ClaudeProvider(a.Path, key, a.Name);
            else if (a.Provider == ProviderId.Codex) yield return new CodexProvider(a.Path, key, a.Name);
        }
    }
    public int IntervalSeconds { get; set; } = 120;
    public bool EmbedInTaskbar { get; set; }
    public bool IslandEnabled { get; set; } = true;
    public PresentationMode Presentation { get; set; } = PresentationMode.DynamicIsland;
    public double IslandScale { get; set; } = 1;
    public int IslandSlots { get; set; } = 5;
    public bool ExpandOnHover { get; set; } = true;
    public bool IslandReset { get; set; } = true;
    public WidgetStyle TaskbarStyle { get; set; } = WidgetStyle.Identity;
    public int SignalBars { get; set; } = 5;
    public bool Motion { get; set; } = true;
    public IslandPosition IslandPosition { get; set; } = IslandPosition.TopCenter;
    public double? IslandLeft { get; set; }
    public double? IslandTop { get; set; }
    /// <summary>Extra gap, in pixels at 100% scale, between the widget and the tray icons.</summary>
    public int TaskbarOffset { get; set; }
    public bool Notifications { get; set; } = true;
    public AppTheme Theme { get; set; } = AppTheme.Dark;
    /// <summary>Show each account's email under the tool's name in the flyout.</summary>
    public bool ShowEmail { get; set; } = false;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool? UiDark => Theme switch { AppTheme.Light => false, AppTheme.Dark => true, _ => null };

    // Taskbar widget look
    public WidgetStyle WidgetStyle { get; set; } = WidgetStyle.Orbit;
    public bool WidgetName { get; set; } = true;
    public bool WidgetSession { get; set; } = true;
    public bool WidgetWeekly { get; set; } = true;
    public bool WidgetTime { get; set; } = true;

    [System.Text.Json.Serialization.JsonIgnore]
    public WidgetOptions Widget => new(TaskbarStyle, WidgetName, WidgetSession, WidgetWeekly, WidgetTime, ShowRemaining, Motion, SignalBars);

    static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SGBPulse");
    static string FilePath => Path.Combine(Dir, "settings.json");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    public Settings Copy() => JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(this, Json), Json)!;
    public void CopyFrom(Settings other)
    {
        foreach (var property in typeof(Settings).GetProperties().Where(p => p.CanWrite && p.SetMethod?.IsStatic == false))
            property.SetValue(this, property.GetValue(other));
    }

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new();
                if (loaded.Version < 7) { loaded.Upgrade(); loaded.Save(); }
                return loaded;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
        return new();
    }

    public void Save()
    {
        TrySave(out _);
    }

    public bool TrySave(out string? error, string? destination = null)
    {
        error = null;
        try
        {
            var path = destination ?? FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { error = ex.Message; return false; }
    }

    internal void Upgrade()
    {
        if (Version >= 7) return;
        if (Version < 4)
        {
            WidgetStyle = WidgetStyle.Orbit;
            IslandEnabled = true;
            if (GrokBotProvider.IsInstalled()) Grok = true;
        }
        if (Version < 5)
        {
            Presentation = EmbedInTaskbar ? PresentationMode.Taskbar : PresentationMode.DynamicIsland;
            EmbedInTaskbar = Presentation == PresentationMode.Taskbar;
        }
        if (Version < 6) { TaskbarStyle = WidgetStyle.Identity; WidgetName = true; }
        if (TaskbarStyle == WidgetStyle.Flow) TaskbarStyle = WidgetStyle.Signal;
        Version = 7;
    }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool StartWithWindows
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue("SGBPulse") is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value && Environment.ProcessPath is { } exe) key.SetValue("SGBPulse", $"\"{exe}\"");
            else key.DeleteValue("SGBPulse", throwOnMissingValue: false);
        }
    }
}

public sealed record ExtraAccount(ProviderId Provider, string Path, string Name);

public enum AppTheme { System, Light, Dark }
public enum IslandPosition { TopCenter, TopLeft, TopRight, BottomCenter, BottomLeft, BottomRight, Free, SideLeftTop, SideLeftCenter, SideLeftBottom, SideRightTop, SideRightCenter, SideRightBottom }

public static class IslandPositions
{
    public static bool IsSide(this IslandPosition position) => position.IsLeftSide() || position.IsRightSide();
    public static bool IsLeftSide(this IslandPosition position) => position is IslandPosition.SideLeftTop or IslandPosition.SideLeftCenter or IslandPosition.SideLeftBottom;
    public static bool IsRightSide(this IslandPosition position) => position is IslandPosition.SideRightTop or IslandPosition.SideRightCenter or IslandPosition.SideRightBottom;
    public static bool IsSideTop(this IslandPosition position) => position is IslandPosition.SideLeftTop or IslandPosition.SideRightTop;
    public static bool IsSideBottom(this IslandPosition position) => position is IslandPosition.SideLeftBottom or IslandPosition.SideRightBottom;
}
public enum PresentationMode { DynamicIsland, Taskbar, Floating }
