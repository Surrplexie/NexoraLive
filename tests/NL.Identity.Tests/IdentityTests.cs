using NL.Core;
using NL.Core.Sp;
using NL.Identity;
using NL.Identity.Core;
using NL.Server;
using Xunit;

namespace NL.Identity.Tests;

public class NlIdentityServiceTests
{
    [Fact]
    public void LinkPlatform_SecondAccountRejected_AntiAlt()
    {
            var dir = IdentityTestHelpers.NewTempDir();
        try
        {
            Environment.SetEnvironmentVariable("NL_IDENTITY_ROOT", dir);
            var audit = new JsonlIdentityAuditStore();
            var service = new NlIdentityService(new JsonFileIdentityStore(dir), audit);

            var a1 = service.CreateAccount("Alice");
            var a2 = service.CreateAccount("Bob");
            service.LinkPlatform(a1.Id, NlPlatform.Steam, "76561198000000001");

            var ex = Assert.Throws<PlatformLinkConflictException>(() =>
                service.LinkPlatform(a2.Id, NlPlatform.Steam, "76561198000000001"));
            Assert.Equal(a1.Id, ex.ExistingAccountId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NL_IDENTITY_ROOT", null);
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class MockOwnershipVerifierTests
{
    [Fact]
    public async Task Verify_OwnedApp_ReturnsOwned()
    {
            var dir = IdentityTestHelpers.NewTempDir();
        try
        {
            var configPath = Path.Combine(dir, "mock-ownership.json");
            File.WriteAllText(configPath, """
                {
                  "ownership": {
                    "steam:76561198000000001": { "440": "Owned", "730": "NotOwned" }
                  }
                }
                """);

            var verifier = new MockGameOwnershipVerifier(configPath);
            var owned = await verifier.VerifyAsync(new GameOwnershipRequest(
                NlPlatform.Steam, "76561198000000001", "tf2", "440"));
            var notOwned = await verifier.VerifyAsync(new GameOwnershipRequest(
                NlPlatform.Steam, "76561198000000001", "cs2", "730"));

            Assert.Equal(GameOwnershipStatus.Owned, owned.Status);
            Assert.Equal(GameOwnershipStatus.NotOwned, notOwned.Status);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Verify_BannedUser_ReturnsBanned()
    {
            var dir = IdentityTestHelpers.NewTempDir();
        try
        {
            var configPath = Path.Combine(dir, "mock-ownership.json");
            File.WriteAllText(configPath, """
                { "banned": { "steam:76561198999999999": true } }
                """);

            var verifier = new MockGameOwnershipVerifier(configPath);
            var result = await verifier.VerifyAsync(new GameOwnershipRequest(
                NlPlatform.Steam, "76561198999999999", "any", "440"));

            Assert.Equal(GameOwnershipStatus.Banned, result.Status);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class OwnershipAdmissionIntegrationTests
{
    [Fact]
    public async Task Admit_WithoutOwnership_DeniedWhenRequired()
    {
            var dir = IdentityTestHelpers.NewTempDir();
        var previous = Environment.GetEnvironmentVariable("NL_DATA_ROOT");
        var previousIdentity = Environment.GetEnvironmentVariable("NL_IDENTITY_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("NL_DATA_ROOT", dir);
            Environment.SetEnvironmentVariable("NL_IDENTITY_ROOT", Path.Combine(dir, "identity"));
            Directory.CreateDirectory(Path.Combine(dir, "identity"));
            File.Copy(
                Path.Combine(IdentityTestHelpers.FindRepoRoot(), "samples", "identity", "mock-ownership.json"),
                Path.Combine(dir, "identity", "mock-ownership.json"));

            var identity = new NlIdentityHost(new NlIdentitySettings
            {
                Enabled = true,
                Mode = NlOwnershipMode.Mock,
            });

            var admission = NlJoinAdmissionService.CreateDefault(NlPaths.DefaultStreamerId);
            var profile = new SessionProfileFile
            {
                RequireGameOwnership = true,
                GameId = "hello-fork",
                PlatformAppId = "440",
                OwnershipPlatform = "steam",
            };

            var deny = await admission.EvaluateAsync(
                new NlAdmitPlayerRequest
                {
                    PlayerId = "pirate",
                    Platform = "steam",
                    PlatformUserId = "76561198000000001",
                    AppId = "730",
                },
                profile,
                identity);

            Assert.False(deny.Admit);
            Assert.Equal("NotOwned", deny.OwnershipStatus);

            var allow = await admission.EvaluateAsync(
                new NlAdmitPlayerRequest
                {
                    PlayerId = "owner",
                    Platform = "steam",
                    PlatformUserId = "76561198000000001",
                    AppId = "440",
                },
                profile,
                identity);

            Assert.True(allow.Admit);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NL_DATA_ROOT", previous);
            Environment.SetEnvironmentVariable("NL_IDENTITY_ROOT", previousIdentity);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Admit_MissingPlatformUserId_Denied()
    {
        var identity = new NlIdentityHost(new NlIdentitySettings { Enabled = true, Mode = NlOwnershipMode.Mock });
            var dir = IdentityTestHelpers.NewTempDir();
        var previous = Environment.GetEnvironmentVariable("NL_DATA_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("NL_DATA_ROOT", dir);
            var admission = NlJoinAdmissionService.CreateDefault(NlPaths.DefaultStreamerId);
            var profile = new SessionProfileFile { RequireGameOwnership = true, PlatformAppId = "440" };

            var result = await admission.EvaluateAsync(
                new NlAdmitPlayerRequest { PlayerId = "alice", Platform = "steam" },
                profile,
                identity);

            Assert.False(result.Admit);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NL_DATA_ROOT", previous);
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class NlTokenProtectorTests
{
    [Fact]
    public void ProtectUnprotect_RoundTrip_WithAesKey()
    {
        var key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var previous = Environment.GetEnvironmentVariable("NL_IDENTITY_ENCRYPTION_KEY");
        try
        {
            Environment.SetEnvironmentVariable("NL_IDENTITY_ENCRYPTION_KEY", key);
            var protector = new NlTokenProtector();
            var blob = protector.Protect("refresh-secret");
            var plain = protector.Unprotect(blob);
            Assert.Equal("refresh-secret", plain);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NL_IDENTITY_ENCRYPTION_KEY", previous);
        }
    }
}

static class IdentityTestHelpers
{
    public static string NewTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nl-id-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    public static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir, "src", "NL.Core")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}
