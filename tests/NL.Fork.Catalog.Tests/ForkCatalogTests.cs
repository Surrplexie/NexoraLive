using NL.Fork.Catalog;
using NL.Fork.Catalog.Core;
using NL.Server;
using Xunit;

namespace NL.Fork.Catalog.Tests;

public class ForkMajorVersionTests
{
    [Theory]
    [InlineData("1.0", "1.0")]
    [InlineData("2.0", "2.0")]
    [InlineData("2", "2.0")]
    [InlineData("v3.0", "3.0")]
    public void TryNormalize_AcceptsMajorOnly(string input, string expected)
    {
        Assert.True(ForkMajorVersion.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("1.2")]
    [InlineData("1.4")]
    [InlineData("2.1")]
    [InlineData("")]
    public void TryNormalize_RejectsPatchMinors(string input)
    {
        Assert.False(ForkMajorVersion.TryNormalize(input, out _));
    }
}

public class ForkCatalogValidatorTests
{
    private static string TempManifestPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nl-catalog-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "catalog.json");
    }

    [Fact]
    public void ValidateSelection_RejectsUnknownMajor()
    {
        var path = TempManifestPath();
        var repo = new JsonForkCatalogRepository(path);
        repo.Save(new ForkCatalogManifest
        {
            Entries =
            [
                new ForkCatalogEntry("gameA", "Game A", "1.0", "sha256:abc", PartnershipTier.AtOwnRisk),
            ],
        });

        var validator = new ForkCatalogValidator(repo);
        var result = validator.ValidateSelection(new ForkCatalogSelection("gameA", "2.0", []));

        Assert.False(result.IsValid);
        Assert.Contains("Unknown", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateSelection_RejectsDeprecatedMajor()
    {
        var path = TempManifestPath();
        var repo = new JsonForkCatalogRepository(path);
        repo.Save(new ForkCatalogManifest
        {
            Entries =
            [
                new ForkCatalogEntry(
                    "gameA", "Game A", "1.0", "sha256:old",
                    Status: ForkCatalogEntryStatus.Deprecated),
            ],
        });

        var validator = new ForkCatalogValidator(repo);
        var result = validator.ValidateSelection(new ForkCatalogSelection("gameA", "1.0", []));

        Assert.False(result.IsValid);
        Assert.Contains("deprecated", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateSelection_RejectsPatchVersionRow()
    {
        var path = TempManifestPath();
        var validator = new ForkCatalogValidator(new JsonForkCatalogRepository(path));

        var reg = validator.ValidateEntryForRegistration(new ForkCatalogEntry(
            "gameA", "Game A", "1.2", "sha256:bad"));

        Assert.False(reg.IsValid);
        Assert.Contains("X.0", reg.Error);
    }
}

public class ForkCatalogGovernanceTests
{
    [Fact]
    public void Register_DeprecatesOldestMajor_WhenOverQuota()
    {
        var path = Path.Combine(Path.GetTempPath(), "nl-catalog-gov-" + Guid.NewGuid().ToString("N"), "catalog.json");
        var repo = new JsonForkCatalogRepository(path);
        repo.Save(new ForkCatalogManifest { MaxMajorsPerGame = 2 });

        var governance = new ForkCatalogGovernance(repo, () => new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
        governance.Register(new ForkCatalogEntry(
            "gameA", "Game A", "1.0", "sha256:a",
            RegisteredAtUtc: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        governance.Register(new ForkCatalogEntry(
            "gameA", "Game A", "2.0", "sha256:b",
            RegisteredAtUtc: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)));
        governance.Register(new ForkCatalogEntry(
            "gameA", "Game A", "3.0", "sha256:c",
            RegisteredAtUtc: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)));

        var manifest = repo.Load();
        var v1 = manifest.Entries.Single(e => e.MajorVersion == "1.0");
        var v3 = manifest.Entries.Single(e => e.MajorVersion == "3.0");

        Assert.Equal(ForkCatalogEntryStatus.Deprecated, v1.Status);
        Assert.Equal(ForkCatalogEntryStatus.Active, v3.Status);
    }
}

public class ForkModSlotResolverTests
{
    [Fact]
    public void Resolve_BuildsManifest_FromHubIds()
    {
        var path = Path.Combine(Path.GetTempPath(), "nl-catalog-mods-" + Guid.NewGuid().ToString("N"), "catalog.json");
        var repo = new JsonForkCatalogRepository(path);
        repo.Save(new ForkCatalogManifest
        {
            ModHub =
            [
                new ModHubEntry("demo-boost", "ABC", "test", new Dictionary<string, double> { ["x"] = 2 }),
            ],
        });

        var resolver = new ForkModSlotResolver(repo);
        var manifest = resolver.Resolve(["demo-boost"]);

        Assert.Single(manifest.Mods);
        Assert.Equal("demo-boost", manifest.Mods[0].Id);
        Assert.Equal("ABC", manifest.Mods[0].Sha256);
    }
}

public class CatalogAdmissionTests
{
    [Fact]
    public async Task Admit_DeniesWrongClientMajor_WhenCatalogEnforced()
    {
        var root = Path.Combine(Path.GetTempPath(), "nl-catalog-admit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("NL_DATA_ROOT", root);

        var catalogPath = Path.Combine(root, "fork-catalog", "catalog.json");
        Directory.CreateDirectory(Path.GetDirectoryName(catalogPath)!);
        File.Copy(
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "samples", "fork", "catalog.json")),
            catalogPath);

        var host = new NlForkCatalogHost(new NlForkCatalogSettings { Enabled = true }, catalogPath);
        var moderation = new NL.Moderation.Core.ModerationService(
            new NL.Moderation.JsonlModerationStore(Path.Combine(root, "mod.jsonl")),
            new NL.Moderation.JsonFileSpProfileRepository(Path.Combine(root, "sp.json")));

        var admission = new NlJoinAdmissionService(
            moderation,
            NL.Core.NlPaths.DefaultStreamerId,
            NL.Core.Sp.JoinRequirements.None);

        var profile = new SessionProfileFile
        {
            GameId = "gameA",
            GameMajorVersion = "1.0",
            CatalogEnforced = true,
        };

        var deny = await admission.EvaluateAsync(
            new NlAdmitPlayerRequest { PlayerId = "sp-1", MajorVersion = "2.0" },
            profile,
            identity: null,
            social: null,
            catalog: host);

        Assert.Equal(NL.Core.Sp.JoinDecision.Deny, deny.Decision);
        Assert.Contains("does not match", deny.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
