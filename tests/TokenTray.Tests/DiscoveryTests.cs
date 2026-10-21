using TokenTray.Core;

namespace TokenTray.Tests;

public class DiscoveryTests
{
    [Fact]
    public void Discovery_is_per_user_and_does_not_infer_paid_subscriptions()
    {
        var root = Path.Combine(Path.GetTempPath(), "pulse-discovery-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "AppData", "Roaming", "Trae"));
            Directory.CreateDirectory(Path.Combine(root, "AppData", "Local", "Programs", "Verdent"));
            var found = AppDiscovery.Found(root).Select(a => a.Name).ToArray();
            Assert.Contains("TRAE", found); Assert.Contains("Verdent", found); Assert.DoesNotContain("Windsurf", found);
            Assert.Empty(AppDiscovery.Found(Path.Combine(root, "another-user")));
            Assert.All(AppDiscovery.Catalog, app => Assert.StartsWith("https://", app.Url));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Unknown_Trae_quota_does_not_become_zero_or_a_paid_plan()
    {
        var snapshot = TraeProvider.Parse("{\"is_dollar_usage_billing\":true,\"user_entitlement_pack_list\":[]}", DateTimeOffset.Now);
        Assert.NotEqual(SnapshotStatus.Ok, snapshot.Status);
        Assert.Empty(snapshot.Windows); Assert.Null(snapshot.Plan); Assert.Null(snapshot.Binding);
    }
}
