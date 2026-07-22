using NL.Core;
using NL.Moderation;
using NL.Moderation.Core;
using Xunit;

namespace NL.Moderation.Tests;

public class ModerationHostStateTests
{
    [Fact]
    public async Task WebHostState_IssuesWarningAndRecordsAudit()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"nl-mod-web-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "moderation.jsonl");
        var profiles = Path.Combine(dir, "sp-profiles.json");
        var previous = Environment.GetEnvironmentVariable("NL_DATA_ROOT");

        try
        {
            Environment.SetEnvironmentVariable("NL_DATA_ROOT", dir);
            var host = new ModerationHostState(log, profiles);
            host.Moderation.GetOrCreateProfile("eve", "Eve");
            await host.Moderation.IssueWarningAsync(NlPaths.DefaultStreamerId, "eve", "mod-test", "test warn");

            var recent = await host.Moderation.GetRecentActionsAsync(NlPaths.DefaultStreamerId, 10);
            Assert.Contains(recent, r => r.Kind == ModerationActionKind.Warning);

            var history = host.Moderation.GetOffenseHistory(NlPaths.DefaultStreamerId, "eve");
            Assert.NotNull(history);
            Assert.Single(history!.Offenses);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NL_DATA_ROOT", previous);
            Directory.Delete(dir, recursive: true);
        }
    }
}
