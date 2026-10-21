using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TokenTray.Core;

public static class AntigravityDesktop
{
    static string Profile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Antigravity");
    public static bool IsInstalled => File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Antigravity", "resources", "app.asar"));

    /// <summary>
    /// Identity only: ask SQLite for the email, never load the auth record or copy its database.
    /// Rechecked after a failed desktop request so sign-outs/account switches cannot inherit old quotas.
    /// </summary>
    public static string? ReadAccount(string? statePath = null)
    {
        var path = statePath ?? Path.Combine(Profile, "User", "globalStorage", "state.vscdb");
        if (!File.Exists(path)) return null;
        var email = WinSqlite.QueryText(path,
            "SELECT CASE WHEN json_type(value, '$.email') = 'text' THEN json_extract(value, '$.email') END FROM ItemTable WHERE key = 'antigravityAuthStatus'",
            allowTemporaryCopy: false);
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    public static async Task<ProviderSnapshot> FetchAsync(CancellationToken ct)
    {
        // This client is restricted to the app's loopback HTTPS server with its self-signed certificate.
        using var handler = new HttpClientHandler { AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (request, _, _, _) => request.RequestUri is { IsLoopback: true, Scheme: "https" } };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12) };
        try
        {
            var active = Path.Combine(Profile, "DevToolsActivePort");
            if (!File.Exists(active)) return Closed();
            var first = (await File.ReadAllLinesAsync(active, ct)).FirstOrDefault();
            if (!int.TryParse(first, out var port) || port is < 1024 or > 65535) return Closed();
            using var targets = JsonDocument.Parse(await client.GetStringAsync($"http://127.0.0.1:{port}/json/list", ct));
            var url = targets.RootElement.EnumerateArray().Select(t => Http.Str(t, "url"))
                .FirstOrDefault(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && uri.IsLoopback && uri.Host == "127.0.0.1" && uri.Scheme == "https");
            if (url is null) return Closed();
            var origin = new Uri(url).GetLeftPart(UriPartial.Authority);
            var html = await client.GetStringAsync(origin + "/", ct);
            var match = Regex.Match(html, "[\"']csrfToken[\"']\\s*:\\s*[\"']([^\"']+)[\"']");
            if (!match.Success) return ProviderSnapshot.Failed(ProviderId.Antigravity, SnapshotStatus.Error, "Antigravity bağlantı biçimi değişti; kota doğrulanamadı.");
            using var request = new HttpRequestMessage(HttpMethod.Post, origin + "/exa.language_server_pb.LanguageServerService/GetUserStatus")
            { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
            request.Headers.Add("x-codeium-csrf-token", match.Groups[1].Value);
            using var response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            return Parse(await response.Content.ReadAsStringAsync(ct), DateTimeOffset.Now);
        }
        catch (Exception e) when (e is IOException or HttpRequestException or JsonException or UnauthorizedAccessException or TaskCanceledException or InvalidOperationException)
        {
            if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);
            return Closed();
        }
    }

    static ProviderSnapshot Closed() => ProviderSnapshot.Failed(ProviderId.Antigravity, SnapshotStatus.Offline,
        "Antigravity kapalı veya yerel kota bağlantısına ulaşılamıyor. Güncel kota için uygulamayı açıp yeniden tara.");

    public static ProviderSnapshot Parse(string json, DateTimeOffset now)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.GetProperty("userStatus");
        var windows = new List<UsageWindow>();
        if (root.TryGetProperty("cascadeModelConfigData", out var config) && config.TryGetProperty("clientModelConfigs", out var models))
        {
            foreach (var m in models.EnumerateArray())
            {
                if (!m.TryGetProperty("quotaInfo", out var q) || Http.Number(q, "remainingFraction") is not { } f || !double.IsFinite(f) || f < 0 || f > 1) continue;
                var label = Regex.Replace(Http.Str(m, "label") ?? "Model", @"\s*\((Low|Medium|High)\)$", "");
                var window = new UsageWindow(WindowKind.Model, label, (1 - f) * 100, Http.Time(q, "resetTime"), null);
                if (!windows.Contains(window)) windows.Add(window);
            }
        }
        var email = Http.Str(root, "email");
        var plan = root.TryGetProperty("userTier", out var tier) ? Http.Str(tier, "name") : null;
        return windows.Count == 0 ? ProviderSnapshot.Failed(ProviderId.Antigravity, SnapshotStatus.Error, "Antigravity model kotası döndürmedi.")
            : new(ProviderId.Antigravity, SnapshotStatus.Ok, windows.OrderByDescending(w => w.UsedPercent).ToList(), now, plan, Email: email, Account: email);
    }
}

public sealed class GrokBotProvider : IUsageProvider
{
    internal const string MissingQuota = "Bu hesap için doğrulanabilir ortak haftalık yüzde bildirilmedi.";
    public ProviderId Id => ProviderId.Grok;
    public string? LastAccount { get; private set; }
    static string Profile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Grok Bot");
    public static bool IsInstalled() => Directory.Exists(Profile) || File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Grok Bot", "Grok Bot.exe"));

    public async Task<ProviderSnapshot> FetchAsync(CancellationToken ct)
    {
        try
        {
            using var secrets = JsonDocument.Parse(await Http.ReadShared(Path.Combine(Profile, "sand-secrets.json"), ct));
            using var accounts = JsonDocument.Parse(secrets.RootElement.GetProperty("cursor-accounts").GetString()!);
            var active = accounts.RootElement.GetProperty("active").GetString()!;
            var account = accounts.RootElement.GetProperty("accounts").GetProperty(active);
            var localState = Path.Combine(Profile, "Local State");
            var token = ClaudeDesktop.ReadEncrypted(account.GetProperty("cursor-access-token").GetString()!, localState);
            if (account.TryGetProperty("cursor-account-profile", out var profile))
            {
                var text = ClaudeDesktop.ReadEncrypted(profile.GetString()!, localState);
                if (text is not null) { using var p = JsonDocument.Parse(text); LastAccount = Http.Str(p.RootElement, "email"); }
            }
            if (string.IsNullOrEmpty(token)) return Missing();
            string? selectedTeam = null;
            if (account.TryGetProperty("cursor-selected-team-id", out var team))
            {
                var teamId = ClaudeDesktop.ReadEncrypted(team.GetString()!, localState);
                if (long.TryParse(teamId, out var id)) selectedTeam = id.ToString();
            }
            return (await FetchQuota(token, ct, selectedTeam)) with { Email = LastAccount, Account = LastAccount };
        }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException or System.Security.Cryptography.CryptographicException or UnauthorizedAccessException)
        { return Missing(); }
    }

    ProviderSnapshot Missing() => ProviderSnapshot.Failed(Id, SnapshotStatus.NotSignedIn, "Grok Bot hesabı okunamadı. Grok Bot'ta hesabını kontrol et ve yeniden tara.");

    internal static async Task<ProviderSnapshot> FetchQuota(string token, CancellationToken ct, string? team = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api2.cursor.sh/aiserver.v1.DashboardService/GetSandUsageStatus")
        { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Connect-Protocol-Version", "1");
        request.Headers.Add("x-cursor-client-type", "sand");
        request.Headers.Add("x-cursor-client-version", "sand-desktop");
        request.Headers.Add("x-ghost-mode", "true");
        if (team is not null) request.Headers.Add("x-cursor-team-id", team);
        var (outcome, status, body) = await Http.Send(request, ct);
        if (outcome == Http.Outcome.Auth) return ProviderSnapshot.Failed(ProviderId.Grok, SnapshotStatus.TokenExpired, "Hesap servisi oturumu kabul etmedi. Uygulamada hesabını kontrol et ve yeniden tara.");
        if (outcome != Http.Outcome.Ok) return ProviderSnapshot.Failed(ProviderId.Grok, SnapshotStatus.Error, $"Kota servisi: HTTP {status}.");
        return Parse(body, DateTimeOffset.Now);
    }

    public static ProviderSnapshot Parse(string json, DateTimeOffset now)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (Http.Number(root, "usagePercent") is not { } percent || !double.IsFinite(percent) ||
            root.TryGetProperty("usesPooledEnterpriseAllowance", out var pooled) && pooled.ValueKind == JsonValueKind.True)
            return ProviderSnapshot.Failed(ProviderId.Grok, SnapshotStatus.Error, MissingQuota);
        return new(ProviderId.Grok, SnapshotStatus.Ok,
            [new(WindowKind.Weekly, "Grok Bot haftalık", Math.Max(0, percent), Http.Time(root, "nextResetTimestampUtc"), TimeSpan.FromDays(7))],
            now, Http.Str(root, "grokPlanLabel") ?? "The X Subscription");
    }
}
