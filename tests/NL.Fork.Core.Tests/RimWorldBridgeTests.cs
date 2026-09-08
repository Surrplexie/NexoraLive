using System.Net.Sockets;
using System.Text;
using NL.Fork.Core;
using NL.RimWorld.Bridge;
using Xunit;

namespace NL.Fork.Core.Tests;

public class RimWorldBridgeTests
{
    [Fact]
    public void ActionParser_RoundTripsBlockKick()
    {
        var line = NlRimWorldActionParser.BuildEventLine(
            "buildingDestroy",
            "Randy",
            new Dictionary<string, double> { ["building.value"] = 2500 });
        Assert.Contains("\"nl\":1", line);
        Assert.Contains("buildingDestroy", line);

        var parsed = NlRimWorldActionParser.TryParse(
            """{"nl":1,"action":"kick","player":"Randy","event":"buildingDestroy","decision":"Block","message":"grief"}""");
        Assert.NotNull(parsed);
        var decision = NlRimWorldActionParser.ToDecision(parsed);
        Assert.False(decision.Allowed);
        Assert.Equal("kick", decision.Action);
    }

    [Fact]
    public void ModHost_CapsChat_WarnsWithoutKick()
    {
        var host = new NlRimWorldModHost(NlRimWorldRuleBus.FromColonyDefaults());
        Assert.True(host.TryJoin("Randy").Allowed);

        var chat = host.TryChat("Randy", "HELLO COLONY!!!");
        Assert.False(chat.Allowed);
        Assert.Equal("warn", chat.Action);
        Assert.Contains("Randy", host.ConnectedPawns);
    }

    [Fact]
    public void ModHost_HighValueBuilding_Kicks()
    {
        var host = new NlRimWorldModHost(NlRimWorldRuleBus.FromColonyDefaults());
        Assert.True(host.TryJoin("Randy").Allowed);

        var grief = host.TryBuildingDestroy("Randy", 2500);
        Assert.False(grief.Allowed);
        Assert.Equal("kick", grief.Action);
        Assert.DoesNotContain("Randy", host.ConnectedPawns);
    }

    [Fact]
    public void ModHost_TradeAndItemRules()
    {
        var host = new NlRimWorldModHost(NlRimWorldRuleBus.FromColonyDefaults());
        host.TryJoin("Randy");

        Assert.True(host.TryTrade("Randy", 10).Allowed);
        Assert.True(host.TryDraft("Randy", true).Allowed);
        Assert.False(host.TryTrade("Randy", 9000).Allowed);
        Assert.Contains("Randy", host.ConnectedPawns);
        Assert.False(host.TryItemDestroy("Randy", 1200).Allowed);
        Assert.DoesNotContain("Randy", host.ConnectedPawns);
    }

    [Fact]
    public void HarmonyHooks_ResolveTogetherAndVanilla()
    {
        Assert.True(NlRimWorldHarmonyHooks.TryResolveHook("RimWorldTogether.Network.OnPlayerJoin", out var join));
        Assert.Equal("playerJoin", join);
        Assert.True(NlRimWorldHarmonyHooks.TryResolveHook("Verse.DamageWorker.Apply", out var dmg));
        Assert.Equal("entityDamage", dmg);
        Assert.Contains("playerChat", NlRimWorldHarmonyHooks.RequiredEvents);
        Assert.Contains("kick", NlRimWorldHarmonyHooks.RequiredActions);
        Assert.Equal("294100", NlRimWorldHarmonyHooks.SteamAppId);
        Assert.Equal(25555, NlRimWorldHarmonyHooks.DedicatedPort);
    }

    [Fact]
    public void HarmonyHooks_DispatchBuildingDestroy_Blocks()
    {
        var host = new NlRimWorldModHost(NlRimWorldRuleBus.FromColonyDefaults());
        host.TryJoin("Randy");
        var decision = NlRimWorldHarmonyHooks.Dispatch(
            host,
            "RimWorld.Building.Destroy",
            "Randy",
            new Dictionary<string, double> { ["building.value"] = 2500 });
        Assert.False(decision.Allowed);
    }

    [Fact]
    public async Task ConnectListener_WritesHandshakeBanner()
    {
        await using var listener = new RimWorldConnectListener(0);
        listener.Start();
        Assert.True(listener.IsListening);
        Assert.True(listener.BoundPort > 0);

        using var client = new TcpClient();
        await client.ConnectAsync(System.Net.IPAddress.Loopback, listener.BoundPort);
        var buffer = new byte[64];
        var read = await client.GetStream().ReadAsync(buffer);
        var banner = Encoding.ASCII.GetString(buffer, 0, read);
        Assert.StartsWith("NL-RIMWORLD/1", banner);
    }
}

public class RimWorldAdapterChecklistTests
{
    [Fact]
    public void RimWorld_PassesChecklistIncludingNativePlugin()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? root = null;
        for (var i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "integrations", "rimworld")))
            {
                root = dir.FullName;
                break;
            }
        }

        Assert.NotNull(root);
        var adapter = GameForkAdapterManifest.LoadFromFile(
            Path.Combine(root!, "integrations", "rimworld", "adapter.manifest.json"));
        var report = GameForkAdapterChecklist.Evaluate(root!, adapter);
        Assert.True(report.Passed, string.Join("; ", report.Items.Where(i => !i.Passed).Select(i => $"{i.Id}: {i.Detail}")));
        Assert.Contains(report.Items, i => i.Id == "native-plugin" && i.Passed);
        Assert.Equal("integrations/rimworld/mod", adapter.Dto.NativePlugin);
    }
}
