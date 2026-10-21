using TokenTray.Core;
using System.Runtime.InteropServices;
using System.Text;

namespace TokenTray.Tests;

public class AntigravityClosedTests
{
    static readonly DateTimeOffset ReadAt = DateTimeOffset.Parse("2026-10-21T10:00:00Z");

    static ProviderSnapshot Remembered(string? account) => new(ProviderId.Antigravity, SnapshotStatus.Ok,
        [new(WindowKind.Model, "Gemini Argon", 72, ReadAt.AddDays(1), null)], ReadAt,
        Plan: "Pro", Account: account, Email: account);

    [Fact]
    public async Task Closed_desktop_keeps_same_accounts_numbers_explicitly_stale_with_original_read_time()
    {
        var closed = ProviderSnapshot.Failed(ProviderId.Antigravity, SnapshotStatus.Offline, "Antigravity kapalı; güncel kota okunamıyor.")
            with { Account = "same@example.test" };
        var result = await Poll(Remembered("same@example.test"), closed);
        Assert.Equal(SnapshotStatus.Offline, result.Status);
        Assert.True(result.Stale);
        Assert.Equal(ReadAt, result.FetchedAt);
        Assert.Equal(72, Assert.Single(result.Windows).UsedPercent);
        Assert.Equal(closed.Message, result.Message);
        Assert.False(result.IsCurrent(DateTimeOffset.Now, TimeSpan.FromMinutes(2)));
    }

    [Theory]
    [InlineData("other@example.test")]
    [InlineData(null)]
    public async Task Closed_desktop_does_not_inherit_numbers_for_another_or_unidentified_account(string? account)
    {
        var closed = ProviderSnapshot.Failed(ProviderId.Antigravity, SnapshotStatus.Offline, "Antigravity kapalı.") with { Account = account };
        var result = await Poll(Remembered("same@example.test"), closed);
        Assert.Equal(SnapshotStatus.Offline, result.Status);
        Assert.Empty(result.Windows);
    }

    [Fact]
    public async Task Reopened_desktop_replaces_stale_numbers_with_a_new_current_reading()
    {
        var fresh = Remembered("same@example.test") with { FetchedAt = DateTimeOffset.Now, Windows = [new(WindowKind.Model, "Gemini Argon", 75, DateTimeOffset.Now.AddDays(1), null)] };
        var result = await Poll(Remembered("same@example.test") with { Status = SnapshotStatus.Offline, Stale = true }, fresh);
        Assert.Equal(SnapshotStatus.Ok, result.Status);
        Assert.False(result.Stale);
        Assert.Equal(fresh.FetchedAt, result.FetchedAt);
        Assert.Equal(75, Assert.Single(result.Windows).UsedPercent);
    }

    [Fact]
    public async Task Closed_provider_identifies_account_from_local_profile_when_no_cloud_credential_exists()
    {
        var provider = new AntigravityProvider(
            readCredential: () => null,
            fetchDesktop: _ => Task.FromResult(ProviderSnapshot.Failed(ProviderId.Antigravity, SnapshotStatus.Offline, "closed")),
            readDesktopAccount: () => "same@example.test");
        var closed = await provider.FetchAsync(CancellationToken.None);
        Assert.Equal(SnapshotStatus.Offline, closed.Status);
        Assert.Equal("same@example.test", closed.Account);
        Assert.Equal("same@example.test", provider.LastAccount);
        Assert.Empty(closed.Windows);
    }

    [Theory]
    [InlineData("other@example.test")]
    [InlineData(null)]
    public async Task Closed_provider_rechecks_profile_identity_instead_of_reusing_previous_account(string? currentAccount)
    {
        var calls = 0;
        var provider = new AntigravityProvider(
            readCredential: () => null,
            fetchDesktop: _ => Task.FromResult(calls++ == 0 ? Remembered("same@example.test")
                : ProviderSnapshot.Failed(ProviderId.Antigravity, SnapshotStatus.Offline, "closed")),
            readDesktopAccount: () => currentAccount);
        var open = await provider.FetchAsync(CancellationToken.None);
        var closed = await provider.FetchAsync(CancellationToken.None);
        Assert.Equal(currentAccount, closed.Account);
        Assert.Equal(currentAccount, provider.LastAccount);
        Assert.Empty((await Poll(open, closed)).Windows);
    }

    [Theory]
    [InlineData("{\"email\":\"same@example.test\",\"apiKey\":\"test-fixture-only\"}", "same@example.test")]
    [InlineData("{\"email\":\"  \"}", null)]
    [InlineData("{\"email\":12}", null)]
    [InlineData("{}", null)]
    [InlineData("malformed", null)]
    public void Local_profile_identity_reads_email_only_and_treats_unreadable_identity_as_unknown(string json, string? expected)
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Path.Combine(Path.GetTempPath(), $"sgb-identity-test-{Guid.NewGuid():N}.db");
        try
        {
            Assert.Equal(0, sqlite3_open_v2(Encoding.UTF8.GetBytes(path + '\0'), out var db, 6, IntPtr.Zero));
            try
            {
                var sql = "CREATE TABLE ItemTable (key TEXT, value TEXT); INSERT INTO ItemTable VALUES ('antigravityAuthStatus', '" + json.Replace("'", "''") + "');";
                Assert.Equal(0, sqlite3_exec(db, Encoding.UTF8.GetBytes(sql + '\0'), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero));
            }
            finally { sqlite3_close(db); }
            Assert.Equal(expected, AntigravityDesktop.ReadAccount(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Missing_local_profile_identity_is_unknown()
    {
        Assert.Null(AntigravityDesktop.ReadAccount(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".missing")));
    }

    [DllImport("winsqlite3", CallingConvention = CallingConvention.StdCall)]
    static extern int sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr vfs);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.StdCall)]
    static extern int sqlite3_exec(IntPtr db, byte[] sql, IntPtr callback, IntPtr state, IntPtr error);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.StdCall)]
    static extern int sqlite3_close(IntPtr db);

    static async Task<ProviderSnapshot> Poll(ProviderSnapshot seed, ProviderSnapshot next)
    {
        using var poller = new Poller([new FixedProvider(next)], TimeSpan.FromHours(1));
        poller.Seed(seed);
        var updated = new TaskCompletionSource<ProviderSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        poller.Updated += snapshot => updated.TrySetResult(snapshot);
        poller.Start();
        return await updated.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    sealed class FixedProvider(ProviderSnapshot snapshot) : IUsageProvider
    {
        public ProviderId Id => ProviderId.Antigravity;
        public Task<ProviderSnapshot> FetchAsync(CancellationToken ct) => Task.FromResult(snapshot);
    }
}
