using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TokenTray.Core;

/// <summary>TRAE 3.6 international dollar usage. Existing login is decoded in memory and never refreshed or rewritten.</summary>
public sealed class TraeProvider : IUsageProvider
{
    public ProviderId Id => ProviderId.Trae;
    public string? LastAccount { get; private set; }
    static string Store => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Trae", "User", "globalStorage", "storage.json");
    static readonly HttpClient Client = new(new SocketsHttpHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.All, PooledConnectionLifetime = TimeSpan.FromMinutes(5) }) { Timeout = TimeSpan.FromSeconds(20) };
    // Public format salt from TRAE's shipped byteCrypto codec, not an account key.
    const string Salt = "4dd4c2e6b83162090e52b3c7a6733ba41cb2462b829ab58a196b39db57177524f49baf7f08e8d68d26a72e37c1a95a2f1f05a51892aef2949732b62a38aadd58";
    public static bool IsInstalled() => AppDiscovery.Catalog[0].IsPresent(AppDiscovery.UserRoot);

    public async Task<ProviderSnapshot> FetchAsync(CancellationToken ct)
    {
        LastAccount = null;
        if (!File.Exists(Store)) return Fail(SnapshotStatus.NotSignedIn, "TRAE yerel oturumu bulunamadı. TRAE uygulamasını açıp hesabınla giriş yap.");
        string token; string host; string? email;
        try
        {
            using var store = JsonDocument.Parse(await Http.ReadShared(Store, ct));
            var encoded = Http.Str(store.RootElement, "iCubeAuthInfo://icube.cloudide");
            if (encoded is null) return Fail(SnapshotStatus.NotSignedIn, "TRAE hesabı bulunamadı. TRAE uygulamasında giriş yap.");
            using var auth = JsonDocument.Parse(DecodeStored(encoded) ?? throw new FormatException());
            var root = auth.RootElement;
            LastAccount = Http.Str(root, "userId");
            var account = root.TryGetProperty("account", out var a) ? a : default;
            email = Http.Str(account, "email");
            token = Http.Str(root, "token") ?? throw new FormatException();
            if (token.Length == 0 || token.Contains('\r') || token.Contains('\n')) throw new FormatException();
            if (Http.Time(root, "expiredAt") is { } expires && expires <= DateTimeOffset.UtcNow)
                return Fail(SnapshotStatus.TokenExpired, "TRAE oturumu yenilenmeli. TRAE uygulamasını bir kez aç.");
            var region = Http.Str(account, "storeRegion");
            if (region is null && root.TryGetProperty("userRegion", out var r)) region = Http.Str(r, "region");
            var usageHost = UsageHost(Http.Str(root, "host"), region);
            if (usageHost is null) return Fail(SnapshotStatus.Error, "Bu TRAE bölge/hesap bağlantısı henüz desteklenmiyor.");
            host = usageHost;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or CryptographicException or InvalidOperationException)
        { return Fail(SnapshotStatus.NotSignedIn, "TRAE oturum biçimi okunamadı. TRAE uygulamasında girişini kontrol et."); }

        using var req = new HttpRequestMessage(HttpMethod.Post, host + "/trae/api/v1/pay/ide_user_ent_usage") { Content = new StringContent("{\"require_usage\":true}", Encoding.UTF8, "application/json") };
        req.Headers.Authorization = new AuthenticationHeaderValue("Cloud-IDE-JWT", token);
        using var res = await Client.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.TooManyRequests) throw new RateLimitedException(Http.RetryAfter(res));
        if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return Fail(SnapshotStatus.TokenExpired, "TRAE oturumu kabul edilmedi. TRAE uygulamasını açıp hesabını kontrol et.");
        if (!res.IsSuccessStatusCode) return Fail(SnapshotStatus.Error, $"TRAE kullanım servisi yanıt vermedi (HTTP {(int)res.StatusCode}).");
        try { return Parse(await res.Content.ReadAsStringAsync(ct), DateTimeOffset.Now) with { Account = LastAccount, Email = email }; }
        catch (JsonException) { return Fail(SnapshotStatus.Error, "TRAE kullanım yanıtı okunamadı; sayı gösterilmiyor."); }
    }

    ProviderSnapshot Fail(SnapshotStatus status, string message) => ProviderSnapshot.Failed(Id, status, message) with { Account = LastAccount };

    public static string? UsageHost(string? authHost, string? region)
    {
        if (!Uri.TryCreate(authHost, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0) return null;
        if (uri.Host is not ("grow-normal.trae.ai" or "growsg-normal.trae.ai" or "growva-normal.trae.ai" or "grow-normal.traeapi.us")) return null;
        return region switch { "SG" => "https://growsg-normal.trae.ai", "US" => "https://growva-normal.trae.ai", "USTTP" => "https://grow-normal.traeapi.us", _ => uri.GetLeftPart(UriPartial.Authority) };
    }

    public static string? DecodeStored(string value)
    {
        try
        {
            var blob = Convert.FromBase64String(value);
            if (blob.Length < 118 || (blob.Length - 38) % 16 != 0 || !blob.AsSpan(0, 6).SequenceEqual(new byte[] { 116, 99, 5, 16, 0, 0 })) return null;
            var material = SHA512.HashData(blob.AsSpan(6, 32)).Concat(Convert.FromHexString(Salt)).ToArray();
            var derived = SHA512.HashData(material);
            using var aes = Aes.Create(); aes.Key = derived[..16];
            var plain = aes.DecryptCbc(blob.AsSpan(38), derived.AsSpan(16, 16));
            if (plain.Length < 64 || !CryptographicOperations.FixedTimeEquals(plain.AsSpan(0, 64), SHA512.HashData(plain.AsSpan(64)))) return null;
            return Encoding.UTF8.GetString(plain.AsSpan(64));
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException) { return null; }
    }

    public static ProviderSnapshot Parse(string json, DateTimeOffset now)
    {
        using var doc = JsonDocument.Parse(json); var root = doc.RootElement;
        ProviderSnapshot Unknown(string message) => ProviderSnapshot.Failed(ProviderId.Trae, SnapshotStatus.Error, message);
        if (Http.Number(root, "code") is { } code && code != 0) return Unknown("TRAE kullanım servisi isteği kabul etmedi.");
        if (!root.TryGetProperty("is_dollar_usage_billing", out var dollar) || dollar.ValueKind != JsonValueKind.True)
            return Unknown("Bu TRAE hesabının faturalama biçimi henüz desteklenmiyor; eski istek sayacı bakiye yerine gösterilmez.");
        if (!root.TryGetProperty("user_entitlement_pack_list", out var packs) || packs.ValueKind != JsonValueKind.Array) return Unknown("TRAE dönem bakiyesi bildirmedi.");
        var candidates = packs.EnumerateArray().Where(p => Http.Number(p, "status") is 1 or 3 && p.TryGetProperty("entitlement_base_info", out var info) && (Http.Time(info, "start_time") is not { } start || start <= now) && ((Http.Time(info, "end_time") ?? Http.Time(p, "expire_time")) is not { } end || end > now)).ToList();
        foreach (var type in new[] { 6, 4, 1, 8, 0 })
        {
            var pack = candidates.FirstOrDefault(p => p.TryGetProperty("entitlement_base_info", out var b) && Http.Number(b, "product_type") == type);
            if (pack.ValueKind != JsonValueKind.Object) continue;
            var info = pack.GetProperty("entitlement_base_info");
            if (!info.TryGetProperty("quota", out var quota) || !pack.TryGetProperty("usage", out var usage)) return Unknown("TRAE bakiye ayrıntılarını bildirmedi.");
            var basicLimit = Http.Number(quota, "basic_usage_limit"); var basicUsed = Http.Number(usage, "basic_usage_amount");
            if (basicLimit is not > 0 || basicUsed is not >= 0) return Unknown("TRAE güncel bakiye ve kullanım tutarını bildirmedi.");
            var noBonus = quota.TryGetProperty("no_bonus_quota", out var no) && no.ValueKind == JsonValueKind.True;
            var bonusLimit = noBonus ? 0 : Http.Number(quota, "bonus_usage_limit");
            var bonusUsed = noBonus || bonusLimit == 0 ? 0 : Http.Number(usage, "bonus_usage_amount");
            if (bonusLimit is not >= 0 || bonusUsed is not >= 0) return Unknown("TRAE bonus bakiyesi doğrulanamadı.");
            var end = Http.Time(info, "end_time"); var start = Http.Time(info, "start_time");
            var length = end is { } e && start is { } s && e > s ? e - s : (TimeSpan?)null;
            UsageWindow Window(string label, double used, double limit, string? group = null) => new(WindowKind.Period, label, Math.Clamp(used / limit * 100, 0, 100), end, length, group, $"{used:0.##} / {limit:0.##} USD");
            List<UsageWindow> windows = [Window("TRAE dönem bakiyesi", basicUsed.Value + bonusUsed!.Value, basicLimit.Value + bonusLimit.Value)];
            if (bonusLimit > 0) { windows.Add(Window("TRAE temel bakiye", basicUsed.Value, basicLimit.Value, "Ayrıntı")); windows.Add(Window("TRAE bonus bakiye", bonusUsed.Value, bonusLimit.Value, "Ayrıntı")); }
            return new ProviderSnapshot(ProviderId.Trae, SnapshotStatus.Ok, windows, now, type switch { 6 => "TRAE Ultra", 4 => "TRAE Pro+", 1 => "TRAE Pro", 8 => "TRAE Lite", _ => "TRAE Free" });
        }
        return Unknown("TRAE geçerli bir dönem planı bildirmedi.");
    }
}
