using System.Net.Sockets;
using System.Text;
using NL.Fork.Core;
using NL.Kenshi.Bridge;
using Xunit;

namespace NL.Fork.Core.Tests;

public class KenshiBridgeTests
{
    [Fact]
    public void ActionParser_RoundTripsBlockKick()
    {
        var line = NlKenshiActionParser.BuildEventLine(
            "steal",
            "Beep",
            new Dictionary<string, double> { ["steal.value"] = 1200 });
        Assert.Contains("\"nl\":1", line);
        Assert.Contains("steal", line);

        var parsed = NlKenshiActionParser.TryParse(
            """{"nl":1,"action":"kick","player":"Beep","event":"steal","decision":"Block","message":"theft"}""");
        Assert.NotNull(parsed);
        var decision = NlKenshiActionParser.ToDecision(parsed);
        Assert.False(decision.Allowed);
        Assert.Equal("kick", decision.Action);
    }

    [Fact]
    public void ModHost_CapsChat_WarnsWithoutKick()
    {
        var host = new NlKenshiModHost(NlKenshiRuleBus.FromOutpostDefaults());
        Assert.True(host.TryJoin("Beep").Allowed);

        var chat = host.TryChat("Beep", "HELLO SQUAD!!!");
        Assert.False(chat.Allowed);
        Assert.Equal("warn", chat.Action);
        Assert.Contains("Beep", host.ConnectedPawns);
    }

    [Fact]
    public void ModHost_HighValueSteal_Kicks()
    {
        var host = new NlKenshiModHost(NlKenshiRuleBus.FromOutpostDefaults());
        Assert.True(host.TryJoin("Beep").Allowed);

        var theft = host.TrySteal("Beep", 1200);
        Assert.False(theft.Allowed);
        Assert.Equal("kick", theft.Action);
        Assert.DoesNotContain("Beep", host.ConnectedPawns);
    }

    [Fact]
    public void ModHost_TradeAndRaidRules()
    {
        var host = new NlKenshiModHost(NlKenshiRuleBus.FromOutpostDefaults());
        host.TryJoin("Beep");

        Assert.True(host.TryTrade("Beep", 10).Allowed);
        Assert.True(host.TrySquadOrder("Beep", true).Allowed);
        Assert.False(host.TryTrade("Beep", 9000).Allowed);
        Assert.Contains("Beep", host.ConnectedPawns);
        Assert.False(host.TryRaid("Beep", 12).Allowed);
        Assert.DoesNotContain("Beep", host.ConnectedPawns);
    }

    [Fact]
    public void HarmonyHooks_ResolveCommunityMpAndVanilla()
    {
        Assert.True(NlKenshiHarmonyHooks.TryResolveHook("KenshiMP.Network.OnPlayerJoin", out var join));
        Assert.Equal("playerJoin", join);
        Assert.True(NlKenshiHarmonyHooks.TryResolveHook("Kenshi.Combat.Hit", out var dmg));
        Assert.Equal("entityDamage", dmg);
        Assert.Contains("playerChat", NlKenshiHarmonyHooks.RequiredEvents);
        Assert.Contains("kick", NlKenshiHarmonyHooks.RequiredActions);
        Assert.Equal("233860", NlKenshiHarmonyHooks.SteamAppId);
        Assert.Equal(23386, NlKenshiHarmonyHooks.DedicatedPort);
    }

    [Fact]
    public void HarmonyHooks_DispatchSteal_Blocks()
    {
        var host = new NlKenshiModHost(NlKenshiRuleBus.FromOutpostDefaults());
        host.TryJoin("Beep");
        var decision = NlKenshiHarmonyHooks.Dispatch(
            host,
            "Kenshi.Inventory.Steal",
            "Beep",
            new Dictionary<string, double> { ["steal.value"] = 1200 });
        Assert.False(decision.Allowed);
    }

    [Fact]
    public async Task ConnectListener_WritesHandshakeBanner()
    {
        await using var listener = new KenshiConnectListener(0);
        listener.Start();
        Assert.True(listener.IsListening);
        Assert.True(listener.BoundPort > 0);

        using var client = new TcpClient();
        await client.ConnectAsync(System.Net.IPAddress.Loopback, listener.BoundPort);
        var buffer = new byte[64];
        var read = await client.GetStream().ReadAsync(buffer);
        var banner = Encoding.ASCII.GetString(buffer, 0, read);
        Assert.StartsWith("NL-KENSHI/1", banner);
    }
}

public class KenshiAdapterChecklistTests
{
    [Fact]
    public void Kenshi_PassesChecklistIncludingNativePlugin()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? root = null;
        for (var i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "integrations", "kenshi")))
            {
                root = dir.FullName;
                break;
            }
        }

        Assert.NotNull(root);
        var adapter = GameForkAdapterManifest.LoadFromFile(
            Path.Combine(root!, "integrations", "kenshi", "adapter.manifest.json"));
        var report = GameForkAdapterChecklist.Evaluate(root!, adapter);
        Assert.True(report.Passed, string.Join("; ", report.Items.Where(i => !i.Passed).Select(i => $"{i.Id}: {i.Detail}")));
        Assert.Contains(report.Items, i => i.Id == "native-plugin" && i.Passed);
        Assert.Equal("integrations/kenshi/mod", adapter.Dto.NativePlugin);
    }
}
