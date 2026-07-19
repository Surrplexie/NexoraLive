using NL.Core.Sp;
using Xunit;

namespace NL.Moderation.Tests;

public class JsonFileSpProfileRepositoryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"nl-sp-store-test-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    [Fact]
    public void GetOrCreate_ThenFind_RoundTripsWithinSameInstance()
    {
        var repo = new JsonFileSpProfileRepository(_path);

        var created = repo.GetOrCreate("sp-1", "Alice");
        var found = repo.Find("sp-1");

        Assert.NotNull(found);
        Assert.Equal("Alice", found!.DisplayName);
        Assert.Same(created, found);
    }

    [Fact]
    public void Save_PersistsOffensesAndRelationships_AcrossRepositoryInstances()
    {
        var first = new JsonFileSpProfileRepository(_path);
        var profile = first.GetOrCreate("sp-1", "Alice");
        profile.Offenses.Add(new SpOffense("streamer-zed", DateTimeOffset.UtcNow, "mod-erin", "cheating", "minecraft"));
        profile.SetRelationship(new SpStreamerRelationship(
            "streamer-zed", SpStanding.Banned, IsFollowing: true, IsSubscribed: false,
            Roles: new HashSet<SpRole> { SpRole.Vip }));
        first.Save(profile);

        var second = new JsonFileSpProfileRepository(_path);
        var reloaded = second.Find("sp-1");

        Assert.NotNull(reloaded);
        Assert.Single(reloaded!.Offenses);
        Assert.Equal("cheating", reloaded.Offenses[0].Reason);
        Assert.Equal("minecraft", reloaded.Offenses[0].Game);

        var relationship = reloaded.GetRelationship("streamer-zed");
        Assert.Equal(SpStanding.Banned, relationship.Standing);
        Assert.True(relationship.IsFollowing);
        Assert.True(relationship.HasRole(SpRole.Vip));
    }

    [Fact]
    public void All_ReturnsEveryPersistedProfile()
    {
        var repo = new JsonFileSpProfileRepository(_path);
        repo.GetOrCreate("sp-1", "Alice");
        repo.GetOrCreate("sp-2", "Bob");

        var all = new JsonFileSpProfileRepository(_path).All();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Find_UnknownPlayer_ReturnsNull()
    {
        var repo = new JsonFileSpProfileRepository(_path);

        Assert.Null(repo.Find("sp-ghost"));
    }
}
