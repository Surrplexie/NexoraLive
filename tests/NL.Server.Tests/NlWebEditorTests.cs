using NL.Core;
using NL.Core.Security;
using NL.NleEditor;
using NL.NleEditor.Model;
using NL.Server;
using Xunit;

namespace NL.Server.Tests;

[CollectionDefinition("NlWebEditor", DisableParallelization = true)]
public sealed class NlWebEditorCollection;

[Collection("NlWebEditor")]
public class NlWebEditorTests
{
    [Fact]
    public void RoundTrip_ModelToNleAndBack()
    {
        var model = new ConfigModel
        {
            Events =
            [
                new EventEntry
                {
                    Name = "shoot",
                    Statements = [new StatementEntry { Type = StatementType.Block }],
                },
                new EventEntry
                {
                    Name = "respawn",
                    Statements =
                    [
                        new StatementEntry
                        {
                            Type = StatementType.If,
                            Condition = new ConditionEntry
                            {
                                Parts = [new SimpleConditionEntry { Left = "player.health", Op = ">", Right = "0" }],
                                Joins = [],
                            },
                            ThenBody = [new StatementEntry { Type = StatementType.Block }],
                            ElseBody = [new StatementEntry { Type = StatementType.Allow }],
                        },
                    ],
                },
            ],
        };

        var nle = NleWriter.Write(model);
        var loaded = NleLoader.Load(nle);

        Assert.Equal(2, loaded.Events.Count);
        Assert.Equal("shoot", loaded.Events[0].Name);
        Assert.Equal(StatementType.Block, loaded.Events[0].Statements[0].Type);
        Assert.Equal(StatementType.If, loaded.Events[1].Statements[0].Type);
    }

    [Fact]
    public void Evaluate_BlocksShootInDemoRules()
    {
        var path = NlSampleConfigPaths.Resolve("demo.nle");
        if (!File.Exists(path))
        {
            return;
        }

        var nle = File.ReadAllText(path);
        var result = NleEditorEvaluate.Evaluate(new NleEvaluateRequest(
            "shoot",
            new Dictionary<string, double>(),
            NleText: nle));

        Assert.True(result.ParseOk);
        Assert.Equal("Block", result.Decision);
    }

    [Fact]
    public void WebEditorStore_SaveAndLoadSandbox()
    {
        var root = Path.Combine(Path.GetTempPath(), "nl-editor-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("NL_DATA_ROOT", root);
        try
        {
            var store = new NlWebEditorStore();
            var model = new ConfigModel
            {
                Events =
                [
                    new EventEntry
                    {
                        Name = "useItem",
                        Statements = [new StatementEntry { Type = StatementType.Allow }],
                    },
                ],
            };

            store.Save(model);
            Assert.True(store.SandboxExists());

            var snap = store.Load(null);
            Assert.True(snap.IsSandbox);
            Assert.Single(snap.Model.Events);
            Assert.Equal("useItem", snap.Model.Events[0].Name);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NL_DATA_ROOT", null);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void DemoReset_RestoresWebEditorSandbox()
    {
        var root = Path.Combine(Path.GetTempPath(), "nl-editor-reset-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("NL_DATA_ROOT", root);
        try
        {
            var store = new NlWebEditorStore();
            store.Save(new ConfigModel
            {
                Events = [new EventEntry { Name = "custom", Statements = [new StatementEntry { Type = StatementType.Allow }] }],
            });

            NlDemoReset.ResetWebEditorSandbox("demo.nle");

            var template = NlSampleConfigPaths.Resolve("demo.nle");
            if (!File.Exists(template))
            {
                Assert.False(store.SandboxExists());
                return;
            }

            var snap = store.Load(null);
            Assert.Contains("shoot", snap.NleText, StringComparison.Ordinal);
            Assert.DoesNotContain("event custom:", snap.NleText, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NL_DATA_ROOT", null);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void SecurityPaths_ProtectEditorWrites()
    {
        Assert.True(NlSecurityPaths.RequiresOperatorAuth("PUT", "/api/v1/editor/config"));
        Assert.True(NlSecurityPaths.RequiresOperatorAuth("POST", "/api/v1/editor/apply"));
        Assert.True(NlSecurityPaths.RequiresOperatorAuth("POST", "/api/v1/editor/reset"));
        Assert.False(NlSecurityPaths.RequiresOperatorAuth("GET", "/api/v1/editor/config"));
        Assert.False(NlSecurityPaths.RequiresOperatorAuth("POST", "/api/v1/editor/evaluate"));
    }
}
