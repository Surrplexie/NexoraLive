using NL.Fork.Orchestrator;
using NL.Fork.Orchestrator.Core;
using Xunit;

namespace NL.Fork.Orchestrator.Tests;

public class KubernetesProvisionerTests
{
    [Fact]
    public void JobManifest_ContainsSessionAndImage()
    {
        var request = new ForkProvisionerStartRequest(
            "sessabc123",
            "/tmp/ws",
            "ws://127.0.0.1:27021/nl/v1?token=t",
            "http://127.0.0.1:27020/api/v1/session/admit",
            "/tmp/mods.json",
            "/tmp/rules.nle",
            DockerImage: "nl-fork-hello:latest",
            GameId: "hello-fork");

        var yaml = KubernetesForkJobManifestBuilder.BuildJobYaml(request, "nl-fork-hello:latest", "nl-fork", "hello");
        Assert.Contains("nl-fork-sessabc123", yaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nl-fork-hello:latest", yaml);
        Assert.Contains("NL_FORK_WS_URL", yaml);
        Assert.Contains("sessabc123", yaml);
    }

    [Fact]
    public void ConfigMapManifest_ContainsNleAndMods()
    {
        var yaml = KubernetesForkJobManifestBuilder.BuildConfigMapYaml(
            "sess1", "nl-fork", "on player_join\n  allow", "{\"mods\":[]}");
        Assert.Contains("rules.nle", yaml);
        Assert.Contains("mods.json", yaml);
        Assert.Contains("player_join", yaml);
    }
}
