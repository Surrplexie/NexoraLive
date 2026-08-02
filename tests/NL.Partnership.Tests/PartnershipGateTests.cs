using NL.Fork.Catalog.Core;
using NL.Partnership;
using NL.Partnership.Core;
using Xunit;

namespace NL.Partnership.Tests;

public class PartnershipGateTests
{
    private static (PartnershipGateService Gate, string Root) CreateGate()
    {
        var root = Path.Combine(Path.GetTempPath(), "nl-partnership-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("NL_PARTNERSHIP_ROOT", root);
        var gate = new PartnershipGateService(
            new JsonAtOwnRiskAcknowledgmentStore(Path.Combine(root, "acks.json")),
            new JsonPublisherBanStore(Path.Combine(root, "bans.json")),
            new JsonPlatformOptInStore(Path.Combine(root, "optin.json")),
            new JsonPublisherRegistry(Path.Combine(root, "pubs.json")));
        return (gate, root);
    }

    [Fact]
    public void OfficialTier_SkipsAcknowledgment()
    {
        var (gate, _) = CreateGate();
        var result = gate.EvaluateAdmit("player1", "hello-fork", PartnershipTier.Official);
        Assert.True(result.Allowed);
        Assert.False(result.RequiresAcknowledgment);
    }

    [Fact]
    public void AtOwnRisk_RequiresAckThenAllows()
    {
        var (gate, _) = CreateGate();
        var first = gate.EvaluateAdmit("player1", "gameA", PartnershipTier.AtOwnRisk);
        Assert.False(first.Allowed);
        Assert.True(first.RequiresAcknowledgment);

        gate.RecordAcknowledgment("player1", "gameA", PartnershipTier.AtOwnRisk);
        var second = gate.EvaluateAdmit("player1", "gameA", PartnershipTier.AtOwnRisk);
        Assert.True(second.Allowed);
    }

    [Fact]
    public void AtOwnRisk_AckOnAdmitFlag_RecordsAndAllows()
    {
        var (gate, _) = CreateGate();
        var result = gate.EvaluateAdmit("player2", "gameA", PartnershipTier.AtOwnRisk, atOwnRiskAcknowledged: true);
        Assert.True(result.Allowed);

        var again = gate.EvaluateAdmit("player2", "gameA", PartnershipTier.AtOwnRisk);
        Assert.True(again.Allowed);
    }

    [Fact]
    public void PublisherBan_BlocksAdmit()
    {
        var (gate, _) = CreateGate();
        var bans = new JsonPublisherBanStore(Path.Combine(Path.GetTempPath(), "nl-ban-" + Guid.NewGuid().ToString("N") + ".json"));
        var fullGate = new PartnershipGateService(
            new JsonAtOwnRiskAcknowledgmentStore(),
            bans,
            new JsonPlatformOptInStore(),
            new JsonPublisherRegistry());
        bans.Ban(new PublisherBanEntry("gameA", "steam-user-1", "test", DateTimeOffset.UtcNow));

        var result = fullGate.EvaluateAdmit("p1", "gameA", PartnershipTier.Official, "steam-user-1");
        Assert.False(result.Allowed);
        Assert.Contains("ban", result.DenyReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlatformOptIn_UpgradesTier()
    {
        var root = Path.Combine(Path.GetTempPath(), "nl-optin-" + Guid.NewGuid().ToString("N"));
        var optIn = new JsonPlatformOptInStore(Path.Combine(root, "optin.json"));
        optIn.Save(new PlatformOptInEntry("steam", "480", "gameA", PartnershipTier.Platform));
        var gate = new PartnershipGateService(
            new JsonAtOwnRiskAcknowledgmentStore(),
            new JsonPublisherBanStore(),
            optIn,
            new JsonPublisherRegistry());

        var tier = gate.ResolveTier("gameA", PartnershipTier.AtOwnRisk, "steam", "480");
        Assert.Equal(PartnershipTier.Platform, tier);
    }

    [Fact]
    public void LegalTemplates_AtOwnRiskRequiresAck()
    {
        var legal = PartnershipLegalTemplates.ForGame("gameA", PartnershipTier.AtOwnRisk);
        Assert.True(legal.RequiresAcknowledgment);
        Assert.Contains("own risk", legal.SessionDisclaimer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BanSync_AddAndRemove()
    {
        var root = Path.Combine(Path.GetTempPath(), "nl-sync-" + Guid.NewGuid().ToString("N"));
        var bans = new JsonPublisherBanStore(Path.Combine(root, "bans.json"));
        var audit = new JsonlPartnershipAuditStore(Path.Combine(root, "audit.jsonl"));
        var sync = new BanSyncWebhookService(bans, audit);

        sync.Apply(new BanSyncWebhookRequest("ban", "gameA", "u1", "reason"));
        Assert.True(bans.IsBanned("gameA", "u1", DateTimeOffset.UtcNow));

        sync.Apply(new BanSyncWebhookRequest("unban", "gameA", "u1"));
        Assert.False(bans.IsBanned("gameA", "u1", DateTimeOffset.UtcNow));
    }
}
