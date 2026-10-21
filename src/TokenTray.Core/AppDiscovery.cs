namespace TokenTray.Core;

/// <summary>Known app locations, relative to this Windows user's profile. Installation is not subscription proof.</summary>
public sealed record DiscoveredApp(string Name, string Url, string[] Paths)
{
    public bool IsPresent(string root) => Paths.Any(p => Directory.Exists(Path.Combine(root, p)) || File.Exists(Path.Combine(root, p)));
}

public static class AppDiscovery
{
    public static string UserRoot => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public static IReadOnlyList<DiscoveredApp> Catalog { get; } = [
        new("TRAE", "https://www.trae.ai/", ["AppData/Roaming/Trae", "AppData/Roaming/TRAE", "AppData/Roaming/Trae CN", "AppData/Local/Programs/Trae", "AppData/Local/Programs/TRAE", ".trae"]),
        new("Windsurf", "https://windsurf.com/subscription", ["AppData/Roaming/Windsurf", "AppData/Local/Programs/Windsurf", ".codeium/windsurf"]),
        new("Verdent", "https://www.verdent.ai/", ["AppData/Roaming/Verdent", "AppData/Local/Programs/Verdent", ".verdent"]),
        new("Kiro", "https://kiro.dev/", ["AppData/Roaming/Kiro", "AppData/Local/Programs/Kiro", ".kiro"]),
        new("Gemini", "https://gemini.google.com/", ["AppData/Roaming/gemini", "AppData/Local/Google/Gemini", ".gemini"]),
        new("ChatGPT", "https://chatgpt.com/", ["AppData/Roaming/ChatGPT", "AppData/Local/Packages/OpenAI.ChatGPT-Desktop_2p2nqsd0c76g0"]),
        new("Perplexity", "https://www.perplexity.ai/", ["AppData/Roaming/Perplexity", "AppData/Local/Programs/Perplexity"]),
        new("GitHub Copilot", "https://github.com/settings/copilot", ["AppData/Local/github-copilot", ".config/github-copilot"]),
    ];

    public static IEnumerable<DiscoveredApp> Found(string? root = null) => Catalog.Where(a => a.IsPresent(root ?? UserRoot));
}

