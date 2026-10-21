using System.Text.Json.Nodes;
using TokenTray.Core;

namespace TokenTray.Tests;

public class TraeProviderTests
{
    static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-21T00:00:00Z");
    const string Payload = """
        {"is_dollar_usage_billing":true,"user_entitlement_pack_list":[{"status":1,"entitlement_base_info":{"product_type":1,"start_time":"2026-10-01T00:00:00Z","end_time":"2026-11-01T00:00:00Z","quota":{"basic_usage_limit":20,"bonus_usage_limit":0,"no_bonus_quota":true,"premium_model_fast_request_limit":600}},"usage":{"basic_usage_amount":0,"bonus_usage_amount":0}}]}
        """;
    static JsonObject Document() => JsonNode.Parse(Payload)!.AsObject();
    static JsonNode Pack(JsonObject j) => j["user_entitlement_pack_list"]![0]!;

    [Fact]
    public void Current_dollar_quota_does_not_use_the_obsolete_600_request_counter()
    {
        var snapshot = TraeProvider.Parse(Payload, Now);
        Assert.Equal(SnapshotStatus.Ok, snapshot.Status); Assert.Equal("TRAE Pro", snapshot.Plan);
        Assert.Equal(0, snapshot.Binding!.UsedPercent); Assert.Equal(TimeSpan.FromDays(31), snapshot.Binding.Length);
        Assert.Equal("0 / 20 USD", snapshot.Binding.Detail); Assert.Single(snapshot.Windows);
    }

    [Fact]
    public void Basic_exhaustion_with_bonus_left_is_not_total_plan_exhaustion()
    {
        var j = Document(); var pack = Pack(j);
        pack["entitlement_base_info"]!["quota"]!["no_bonus_quota"] = false;
        pack["entitlement_base_info"]!["quota"]!["bonus_usage_limit"] = 10;
        pack["usage"]!["basic_usage_amount"] = 20;
        var snapshot = TraeProvider.Parse(j.ToJsonString(), Now);
        Assert.Equal(20.0 / 30 * 100, snapshot.Binding!.UsedPercent, 8);
        Assert.Equal(3, snapshot.Windows.Count);
        Assert.Contains(snapshot.Windows, w => w.UsedPercent == 100 && w.Group == "Ayrıntı");
    }

    [Fact]
    public void Missing_usage_or_bonus_information_is_unknown_instead_of_zero()
    {
        var j = Document(); Pack(j)["usage"]!.AsObject().Remove("basic_usage_amount");
        Assert.Empty(TraeProvider.Parse(j.ToJsonString(), Now).Windows);
        j = Document(); var quota = Pack(j)["entitlement_base_info"]!["quota"]!.AsObject();
        quota["no_bonus_quota"] = false; quota.Remove("bonus_usage_limit");
        Assert.Equal(SnapshotStatus.Error, TraeProvider.Parse(j.ToJsonString(), Now).Status);
        quota.Remove("basic_usage_limit");
        Assert.Empty(TraeProvider.Parse(j.ToJsonString(), Now).Windows);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Ended_plans_are_not_current_subscriptions(int status)
    {
        var j = Document(); Pack(j)["status"] = status;
        Assert.Null(TraeProvider.Parse(j.ToJsonString(), Now).Plan);
        j = Document(); Pack(j)["entitlement_base_info"]!["end_time"] = "2026-10-20T00:00:00Z";
        Assert.Empty(TraeProvider.Parse(j.ToJsonString(), Now).Windows);
        j = Document(); Pack(j)["entitlement_base_info"]!["start_time"] = "2026-10-22T00:00:00Z";
        Assert.Empty(TraeProvider.Parse(j.ToJsonString(), Now).Windows);
    }

    [Fact]
    public void Private_storage_is_decoded_and_integrity_checked_without_app_code_execution()
    {
        const string fixture = "dGMFEAAAAAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh/f1RoY4kK/pWUeZt0J9ZIfYvnVkU+F2IkmmwGoYB8Uf8lsLr0npAz+4JKFtWRe5ZgvhXCSmfGg+hkqXCT1/e5m63FNaaMGSG/4Dm8iNSaFhA==";
        Assert.Equal("{\"sample\":true}", TraeProvider.DecodeStored(fixture));
        var changed = Convert.FromBase64String(fixture); changed[45] ^= 1;
        Assert.Null(TraeProvider.DecodeStored(Convert.ToBase64String(changed)));
        changed = Convert.FromBase64String(fixture); changed[2] = 99;
        Assert.Null(TraeProvider.DecodeStored(Convert.ToBase64String(changed)));
        Assert.Null(TraeProvider.DecodeStored("bad format"));
    }

    [Fact]
    public void Session_hosts_are_restricted_before_transmitting_credentials()
    {
        Assert.Equal("https://growsg-normal.trae.ai", TraeProvider.UsageHost("https://growsg-normal.trae.ai", "SG"));
        Assert.Equal("https://growva-normal.trae.ai", TraeProvider.UsageHost("https://growsg-normal.trae.ai", "US"));
        foreach (var value in new[] { "https://growsg-normal.trae.ai.evil.test", "https://other.test", "http://growsg-normal.trae.ai", "https://growsg-normal.trae.ai:444", "https://growsg-normal.trae.ai/path", "https://user@growsg-normal.trae.ai" })
            Assert.Null(TraeProvider.UsageHost(value, "SG"));
    }
}
