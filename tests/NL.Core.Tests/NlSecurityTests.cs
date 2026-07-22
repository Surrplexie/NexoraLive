using NL.Core.Security;
using Xunit;

namespace NL.Core.Tests;

public class NlSecuritySettingsTests
{
    [Fact]
    public void PublicMode_RequiresBusTokenAndOperatorKey()
    {
        using var scope = EnvScope.Create(
            (NlSecuritySettings.PublicModeVariable, "true"),
            (NlSecuritySettings.BusTokenVariable, null),
            (NlSecuritySettings.OperatorKeyVariable, "op-key"));

        var ex = Assert.Throws<InvalidOperationException>(() => NlSecuritySettings.LoadFromEnvironment());
        Assert.Contains("NL_BUS_TOKEN", ex.Message);
    }

    [Fact]
    public void PublicMode_RequiresOperatorKey()
    {
        using var scope = EnvScope.Create(
            (NlSecuritySettings.PublicModeVariable, "true"),
            (NlSecuritySettings.BusTokenVariable, "bus-token"),
            (NlSecuritySettings.OperatorKeyVariable, null));

        var ex = Assert.Throws<InvalidOperationException>(() => NlSecuritySettings.LoadFromEnvironment());
        Assert.Contains("NL_OPERATOR_KEY", ex.Message);
    }

    [Fact]
    public void LocalDev_AllowsEphemeralBusToken()
    {
        using var scope = EnvScope.Create(
            (NlSecuritySettings.PublicModeVariable, null),
            (NlSecuritySettings.BusTokenVariable, null),
            (NlSecuritySettings.OperatorKeyVariable, null));

        var settings = NlSecuritySettings.LoadFromEnvironment();
        Assert.False(settings.PublicMode);
        Assert.False(settings.RequireOperatorAuth);
        Assert.Matches("^[a-f0-9]{32}$", NlSecuritySettings.ResolveBusToken(settings));
    }

    [Fact]
    public void OperatorKeyAlone_EnablesAuthWithoutPublicMode()
    {
        using var scope = EnvScope.Create(
            (NlSecuritySettings.OperatorKeyVariable, "secret-op"),
            (NlSecuritySettings.PublicModeVariable, null));

        var settings = NlSecuritySettings.LoadFromEnvironment();
        Assert.False(settings.PublicMode);
        Assert.True(settings.RequireOperatorAuth);
    }

    [Fact]
    public void PublicMode_UsesPublicHttpForDefaultCors()
    {
        using var scope = EnvScope.Create(
            (NlSecuritySettings.PublicModeVariable, "true"),
            (NlSecuritySettings.BusTokenVariable, "bus"),
            (NlSecuritySettings.OperatorKeyVariable, "op"),
            ("NL_PUBLIC_HTTP", "https://demo.example.com"));

        var settings = NlSecuritySettings.LoadFromEnvironment();
        Assert.Equal(new[] { "https://demo.example.com" }, settings.CorsOrigins);
    }

    private sealed class EnvScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previous = new();

        private EnvScope(IReadOnlyList<(string Key, string? Value)> values)
        {
            foreach (var (key, value) in values)
            {
                _previous[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        public static EnvScope Create(params (string Key, string? Value)[] values) => new(values);

        public void Dispose()
        {
            foreach (var (key, value) in _previous)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}

public class NlOperatorAuthTests
{
    [Fact]
    public void LocalDev_SkipsAuthWhenOperatorKeyUnset()
    {
        var settings = new NlSecuritySettings { PublicMode = false, OperatorKey = null };
        Assert.True(NlOperatorAuth.IsAuthorized(settings, (string?)null));
    }

    [Fact]
    public void RejectsWrongKey()
    {
        var settings = new NlSecuritySettings { PublicMode = true, OperatorKey = "expected" };
        Assert.False(NlOperatorAuth.IsAuthorized(settings, "wrong"));
        Assert.True(NlOperatorAuth.IsAuthorized(settings, "expected"));
    }

    [Fact]
    public void AcceptsBearerHeader()
    {
        var settings = new NlSecuritySettings { PublicMode = true, OperatorKey = "expected" };
        Assert.True(NlOperatorAuth.IsAuthorized(settings, Array.Empty<string>(), "Bearer expected"));
    }
}

public class NlSecurityPathsTests
{
    [Theory]
    [InlineData("POST", "/api/v1/moderation/ban", true)]
    [InlineData("POST", "/api/v1/session/admit", false)]
    [InlineData("GET", "/api/v1/session/manifest", false)]
    [InlineData("PUT", "/api/v1/session/profile/", true)]
    public void RequiresOperatorAuth_MatchesWriteEndpoints(string method, string path, bool expected)
    {
        Assert.Equal(expected, NlSecurityPaths.RequiresOperatorAuth(method, path));
    }
}
