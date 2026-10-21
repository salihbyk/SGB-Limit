using System.Text;
using System.Text.Json;
using TokenTray.Core;

namespace TokenTray.Tests;

public class AntigravityFallbackTests
{
    static ProviderSnapshot Desktop(SnapshotStatus status = SnapshotStatus.Offline) =>
        ProviderSnapshot.Failed(ProviderId.Antigravity, status, "Antigravity yerel bağlantısına ulaşılamıyor.");

    static string Credential(string? email = "same@example.test", int expiryMinutes = 10) => JsonSerializer.Serialize(new {
        token = new { access_token = "synthetic-test-token", expiry = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes) },
        id_token = "test." + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { email }))).TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".test"
    });

    [Fact]
    public async Task Offline_desktop_uses_valid_same_account_credential_for_fresh_model_quota()
    {
        var requests = new List<string>();
        var provider = new AntigravityProvider(
            readCredential: () => Credential(), fetchDesktop: _ => Task.FromResult(Desktop()),
            readDesktopAccount: () => "same@example.test",
            send: (request, _) => {
                Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
                Assert.Equal("synthetic-test-token", request.Headers.Authorization.Parameter);
                requests.Add(request.RequestUri!.AbsolutePath);
                return Task.FromResult((Http.Outcome.Ok, 200, request.RequestUri.AbsolutePath.EndsWith(":loadCodeAssist")
                    ? """{"cloudaicompanionProject":"fixture-project","paidTier":{"name":"Pro"}}"""
                    : """{"models":{"gemini":{"displayName":"Gemini","quotaInfo":{"remainingFraction":0.2,"resetTime":"2026-10-22T12:00:00Z"}}}}"""));
            });
        var result = await provider.FetchAsync(CancellationToken.None);
        Assert.Equal(SnapshotStatus.Ok, result.Status);
        Assert.False(result.Stale);
        Assert.Equal("same@example.test", result.Account);
        Assert.Equal("same@example.test", provider.LastAccount);
        Assert.Equal("Pro", result.Plan);
        Assert.Equal(80, Assert.Single(result.Windows).UsedPercent);
        Assert.Equal(new[] { "/v1internal:loadCodeAssist", "/v1internal:fetchAvailableModels" }, requests);
    }

    [Theory]
    [InlineData("other@example.test", "same@example.test", 10)]
    [InlineData(null, "same@example.test", 10)]
    [InlineData("same@example.test", null, 10)]
    [InlineData("same@example.test", "same@example.test", -1)]
    [InlineData("same@example.test", "same@example.test", 0)]
    [InlineData("same@example.test", "same@example.test", 1)]
    public async Task Offline_fallback_never_calls_remote_for_expired_unknown_or_another_account(string? credentialAccount, string? desktopAccount, int expiryMinutes)
    {
        var provider = new AntigravityProvider(
            readCredential: () => Credential(credentialAccount, expiryMinutes),
            fetchDesktop: _ => Task.FromResult(Desktop()), readDesktopAccount: () => desktopAccount,
            send: (_, _) => throw new InvalidOperationException("Unverified credentials must not reach a remote endpoint."));
        var result = await provider.FetchAsync(CancellationToken.None);
        Assert.Equal(SnapshotStatus.Offline, result.Status);
        Assert.Equal(desktopAccount, result.Account);
        Assert.Equal(desktopAccount, provider.LastAccount);
        Assert.Empty(result.Windows);
    }

    [Fact]
    public async Task Unknown_token_expiry_does_not_use_cloud_fallback()
    {
        var raw = Credential();
        using var doc = JsonDocument.Parse(raw);
        raw = JsonSerializer.Serialize(new { token = new { access_token = "synthetic-test-token" }, id_token = doc.RootElement.GetProperty("id_token").GetString() });
        var provider = new AntigravityProvider(readCredential: () => raw,
            fetchDesktop: _ => Task.FromResult(Desktop()), readDesktopAccount: () => "same@example.test",
            send: (_, _) => throw new InvalidOperationException("Unknown expiry must not make a request."));
        Assert.Equal(SnapshotStatus.Offline, (await provider.FetchAsync(CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Cloud_timeout_returns_offline_with_local_identity_for_cached_reading()
    {
        var provider = new AntigravityProvider(readCredential: () => Credential(),
            fetchDesktop: _ => Task.FromResult(Desktop()), readDesktopAccount: () => "same@example.test",
            send: async (_, ct) => {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                throw new InvalidOperationException("Unreachable");
            });
        var result = await provider.FetchAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(SnapshotStatus.Offline, result.Status);
        Assert.Equal("same@example.test", result.Account);
        Assert.Equal("same@example.test", provider.LastAccount);
        Assert.Empty(result.Windows);
    }

    [Fact]
    public async Task Rejected_cloud_session_remains_offline_with_session_renewal_explanation()
    {
        var provider = new AntigravityProvider(readCredential: () => Credential(),
            fetchDesktop: _ => Task.FromResult(Desktop()), readDesktopAccount: () => "same@example.test",
            send: (_, _) => Task.FromResult((Http.Outcome.Auth, 401, "{}")));
        var result = await provider.FetchAsync(CancellationToken.None);
        Assert.Equal(SnapshotStatus.Offline, result.Status);
        Assert.Equal("same@example.test", result.Account);
        Assert.Contains("oturum yenilenmeli", result.Message);
    }

    [Fact]
    public async Task Cloud_rate_limit_keeps_retry_after_and_local_account_for_poller()
    {
        var provider = new AntigravityProvider(readCredential: () => Credential(),
            fetchDesktop: _ => Task.FromResult(Desktop()), readDesktopAccount: () => "same@example.test",
            send: (_, _) => throw new RateLimitedException(TimeSpan.FromMinutes(5)));
        var error = await Assert.ThrowsAsync<RateLimitedException>(() => provider.FetchAsync(CancellationToken.None));
        Assert.Equal(TimeSpan.FromMinutes(5), error.RetryAfter);
        Assert.Equal("same@example.test", provider.LastAccount);
    }

    [Theory]
    [InlineData(SnapshotStatus.Ok)]
    [InlineData(SnapshotStatus.Error)]
    public async Task Available_or_changed_desktop_protocol_does_not_use_credentials(SnapshotStatus status)
    {
        var provider = new AntigravityProvider(
            readCredential: () => throw new InvalidOperationException("Fallback is only for an offline desktop."),
            fetchDesktop: _ => Task.FromResult(Desktop(status)), readDesktopAccount: () => "same@example.test");
        Assert.Equal(status, (await provider.FetchAsync(CancellationToken.None)).Status);
    }
}
