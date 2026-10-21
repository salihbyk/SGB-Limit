using TokenTray.Core;

namespace TokenTray.Tests;

public class PulseDesktopTests
{
    [Fact]
    public void Grok_bot_exhaustion_does_not_replace_cursors_own_allowance()
    {
        var now = DateTimeOffset.Now;
        var grok = GrokBotProvider.Parse("""{"usagePercent":100,"nextResetTimestampUtc":"2026-10-24T00:00:00Z"}""", now);
        var cursor = CursorProvider.Parse("""{"membershipType":"premium","billingCycleStart":"2026-10-01T00:00:00Z","billingCycleEnd":"2026-11-01T00:00:00Z","individualUsage":{"plan":{"totalPercentUsed":0}}}""", now);
        Assert.Equal(100, grok.Binding!.UsedPercent);
        Assert.Equal(0, cursor.Binding!.UsedPercent);
        Assert.Equal(WindowKind.Period, cursor.Binding.Kind);
        Assert.Equal(TimeSpan.FromDays(31), cursor.Binding.Length);
    }

    [Fact]
    public void Advice_projects_only_when_a_measured_period_is_known()
    {
        var now = DateTimeOffset.Parse("2026-10-21T12:00:00Z");
        var snapshot = new ProviderSnapshot(ProviderId.Claude, SnapshotStatus.Ok, [new(WindowKind.Weekly, "Weekly", 70, now.AddDays(5), TimeSpan.FromDays(7))], now);
        Assert.Contains("civarında biter", PulseAdvice.For(snapshot, now));
        snapshot = snapshot with { Windows = [new(WindowKind.Model, "Model", 70, now.AddDays(5), null)] };
        Assert.Contains("tahmin edilmiyor", PulseAdvice.For(snapshot, now));
    }
    [Fact]
    public void Argon_is_separate_from_other_gemini_models()
    {
        var result = AntigravityDesktop.Parse("""{"userStatus":{"email":"test@example.test","userTier":{"name":"Google AI Pro"},"cascadeModelConfigData":{"clientModelConfigs":[{"label":"Gemini 4 Argon (Medium)","quotaInfo":{"remainingFraction":0.00508,"resetTime":"2026-10-27T18:11:27Z"}},{"label":"Gemini 4 Argon (High)","quotaInfo":{"remainingFraction":0.00508,"resetTime":"2026-10-27T18:11:27Z"}},{"label":"Gemini 3.8 Flash (High)","quotaInfo":{"remainingFraction":0.919,"resetTime":"2026-10-21T03:00:00Z"}}]}}}""", DateTimeOffset.Now);
        Assert.Equal(SnapshotStatus.Ok, result.Status);
        Assert.Equal(2, result.Windows.Count);
        Assert.StartsWith("Gemini 4 Argon", result.Binding!.Label);
        Assert.Equal(99.492, result.Binding.UsedPercent, 3);
        Assert.Equal("Google AI Pro", result.Plan);
    }

    [Fact]
    public void Grok_bot_weekly_limit_is_not_grok_cli_credit_usage()
    {
        var result = GrokBotProvider.Parse("""{"usagePercent":100,"hasNonZeroIncludedLimit":true,"nextResetTimestampUtc":"2026-10-24T00:00:00Z","grokPlanLabel":"Premium"}""", DateTimeOffset.Now);
        Assert.Equal(SnapshotStatus.Ok, result.Status);
        Assert.Equal(100, result.Binding!.UsedPercent);
        Assert.Equal(WindowKind.Weekly, result.Binding.Kind);
        Assert.NotNull(result.Binding.ResetsAt);
    }

    [Fact]
    public void Missing_grok_quota_is_unknown_not_zero()
    {
        Assert.Equal(SnapshotStatus.Error, GrokBotProvider.Parse("{}", DateTimeOffset.Now).Status);
    }
}
