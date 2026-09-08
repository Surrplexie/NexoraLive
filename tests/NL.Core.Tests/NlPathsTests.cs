using NL.Core;
using Xunit;

namespace NL.Core.Tests;

public class NlPathsTests
{
    [Fact]
    public void Root_UsesNlDataRootOverride()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"nl-paths-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        var previous = Environment.GetEnvironmentVariable("NL_DATA_ROOT");

        try
        {
            Environment.SetEnvironmentVariable("NL_DATA_ROOT", temp);
            Assert.Equal(Path.GetFullPath(temp), NlPaths.Root);
            Assert.Equal(Path.Combine(temp, "moderation.jsonl"), NlPaths.ModerationLog);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NL_DATA_ROOT", previous);
            Directory.Delete(temp, recursive: true);
        }
    }
}
