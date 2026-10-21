using TokenTray.Core;

namespace TokenTray.Tests;

public class PulseAccuracyTests
{
    static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-20T22:40:00Z");

    [Fact]
    public void Antigravity_model_quota_is_not_replaced_with_empty_summary_zero()
    {
        // The same live account's summary said 0%, but fetchAvailableModels said 8.08%.
        var payload = """{"models":{"gemini-flash":{"displayName":"Gemini Flash","quotaInfo":{"remainingFraction":0.91919655,"resetTime":"2026-10-20T23:11:36Z"}},"claude-opus":{"displayName":"Claude Opus","quotaInfo":{"remainingFraction":0.66,"resetTime":"2026-10-21T02:00:00Z"}}}}""";
        var result = AntigravityProvider.Parse(payload, Now);
        Assert.Equal(SnapshotStatus.Ok, result.Status);
        Assert.Equal(2, result.Windows.Count);
        Assert.Contains(result.Windows, w => Math.Abs(w.UsedPercent - 8.080345) < 0.001);
        Assert.Contains(result.Windows, w => Math.Abs(w.UsedPercent - 34) < 0.001);
        Assert.All(result.Windows, w => Assert.Null(w.Length));
    }

    [Fact]
    public void Cached_seed_cannot_look_like_a_fresh_server_response()
    {
        using var poller = new Poller([new FakeProvider()], TimeSpan.FromMinutes(2));
        poller.Seed(new(ProviderId.Codex, SnapshotStatus.Ok,
            [new(WindowKind.Weekly, "Weekly", 33, Now.AddDays(1), TimeSpan.FromDays(7))], Now.AddDays(-1)));
        Assert.True(poller.Latest("Codex")!.Stale);
    }

    [Fact]
    public void Success_status_does_not_make_an_old_reading_current()
    {
        var s = new ProviderSnapshot(ProviderId.Codex, SnapshotStatus.Ok, [], Now.AddHours(-2));
        Assert.False(s.IsCurrent(Now, TimeSpan.FromMinutes(2)));
        Assert.True((s with { FetchedAt = Now }).IsCurrent(Now, TimeSpan.FromMinutes(2)));
        Assert.False((s with { FetchedAt = Now, Stale = true }).IsCurrent(Now, TimeSpan.FromMinutes(2)));
        Assert.False((s with { FetchedAt = Now, Status = SnapshotStatus.TokenExpired }).IsCurrent(Now, TimeSpan.FromMinutes(2)));
    }

    [Fact]
    public void Most_restricted_model_is_not_hidden_behind_an_unused_gemini_pool()
    {
        var s = new ProviderSnapshot(ProviderId.Antigravity, SnapshotStatus.Ok,
            [new(WindowKind.Model, "Gemini", 8, Now.AddHours(1), null), new(WindowKind.Model, "Claude", 94, Now.AddHours(2), null)], Now);
        Assert.Equal("Claude", s.MainWindows[0].Label);
        Assert.Equal("Claude", s.Binding!.Label);
    }

    [Fact]
    public void Shared_model_quotas_are_grouped_without_inventing_a_window_length()
    {
        var payload = """{"models":{"gemini-one":{"quotaInfo":{"remainingFraction":0.8,"resetTime":"2026-10-21T02:00:00Z"}},"gemini-two":{"quotaInfo":{"remainingFraction":0.8,"resetTime":"2026-10-21T02:00:00Z"}},"tab_hidden":{"quotaInfo":{"remainingFraction":0.1}}}}""";
        var result = AntigravityProvider.Parse(payload, Now);
        var window = Assert.Single(result.Windows);
        Assert.Equal("Gemini · 2 model", window.Label);
        Assert.Null(window.Length);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Invalid_remaining_fraction_does_not_become_a_real_zero_or_hundred(double fraction)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { models = new { gemini = new { quotaInfo = new { remainingFraction = fraction } } } });
        var result = AntigravityProvider.Parse(json, Now);
        Assert.Equal(SnapshotStatus.Error, result.Status);
        Assert.Empty(result.Windows);
    }

    sealed class FakeProvider : IUsageProvider
    {
        public ProviderId Id => ProviderId.Codex;
        public Task<ProviderSnapshot> FetchAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
