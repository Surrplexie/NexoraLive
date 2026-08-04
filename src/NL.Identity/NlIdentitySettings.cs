using NL.Identity.Core;

namespace NL.Identity;

public enum NlOwnershipMode
{
    Off,
    Mock,
    Live,
}

public sealed class NlIdentitySettings
{
    public const string EnabledVariable = "NL_IDENTITY_ENABLED";
    public const string ModeVariable = "NL_OWNERSHIP_MODE";

    public bool Enabled { get; init; }

    public NlOwnershipMode Mode { get; init; } = NlOwnershipMode.Mock;

    public bool StrictUnknown { get; init; } = true;

    public bool EnforceOneLinkPerPlatform { get; init; } = true;

    /// <summary>Public HTTP base for OAuth callbacks (e.g. http://127.0.0.1:27020).</summary>
    public string? PublicBaseUrl { get; init; }

    /// <summary>Steam OpenID realm; defaults to public base URL.</summary>
    public string? SteamRealm { get; init; }

    public static NlIdentitySettings LoadFromEnvironment()
    {
        var enabledRaw = Environment.GetEnvironmentVariable(EnabledVariable);
        var enabled = enabledRaw is null
            || string.Equals(enabledRaw, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(enabledRaw, "true", StringComparison.OrdinalIgnoreCase);

        var modeRaw = Environment.GetEnvironmentVariable(ModeVariable)?.Trim();
        var mode = modeRaw?.ToLowerInvariant() switch
        {
            "off" => NlOwnershipMode.Off,
            "live" => NlOwnershipMode.Live,
            _ => NlOwnershipMode.Mock,
        };

        if (mode == NlOwnershipMode.Live
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STEAM_WEB_API_KEY")))
        {
            mode = NlOwnershipMode.Mock;
        }

        var strictUnknown = !string.Equals(
            Environment.GetEnvironmentVariable("NL_OWNERSHIP_STRICT_UNKNOWN"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        return new NlIdentitySettings
        {
            Enabled = enabled,
            Mode = mode,
            StrictUnknown = strictUnknown,
            PublicBaseUrl = Environment.GetEnvironmentVariable("NL_PUBLIC_BASE_URL")
                ?? Environment.GetEnvironmentVariable("NL_SESSION_HTTP_URL"),
            SteamRealm = Environment.GetEnvironmentVariable("NL_STEAM_OPENID_REALM"),
        };
    }

    public object ToPublicInfo() => new
    {
        enabled = Enabled,
        mode = Mode.ToString(),
        strictUnknown = StrictUnknown,
        steamConfigured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STEAM_WEB_API_KEY")),
        steamOpenIdEnabled = Enabled,
        publicBaseUrl = PublicBaseUrl,
        oauth = new
        {
            steamAuthorize = "/api/v1/identity/oauth/steam/authorize",
            steamCallback = "/api/v1/identity/oauth/steam/callback",
            linkUi = "/identity-link.html",
        },
        storePath = NlIdentityPaths.Root,
    };
}

public sealed class NlIdentityHost
{
    private readonly MockGameOwnershipVerifier _mockVerifier;

    public NlIdentityHost(NlIdentitySettings settings)
    {
        Settings = settings;
        NlIdentityPaths.EnsureRoot();

        Store = new JsonFileIdentityStore();
        Audit = new JsonlIdentityAuditStore();
        Identity = new NlIdentityService(Store, Audit);

        _mockVerifier = new MockGameOwnershipVerifier();
        var steam = new SteamWebApiOwnershipVerifier(fallback: _mockVerifier);

        OwnershipVerifier = settings.Mode switch
        {
            NlOwnershipMode.Off => new OffGameOwnershipVerifier(),
            NlOwnershipMode.Live => new CompositeGameOwnershipVerifier(
                steam,
                _mockVerifier,
                new StubPlatformOwnershipVerifier(NlPlatform.Epic),
                new StubPlatformOwnershipVerifier(NlPlatform.Ubisoft),
                new StubPlatformOwnershipVerifier(NlPlatform.Ea),
                new StubPlatformOwnershipVerifier(NlPlatform.Xbox),
                new StubPlatformOwnershipVerifier(NlPlatform.PlayStation),
                new StubPlatformOwnershipVerifier(NlPlatform.Riot),
                new StubPlatformOwnershipVerifier(NlPlatform.Itch)),
            _ => _mockVerifier,
        };

        BanChecker = new CompositePublisherBanChecker(steam, _mockVerifier);
        SubscriptionChecker = _mockVerifier;
        OwnershipGate = new NlOwnershipAdmissionGate(
            OwnershipVerifier,
            BanChecker,
            SubscriptionChecker,
            Identity,
            settings,
            Audit);

        OAuthStates = new JsonOAuthStateStore();
        SteamOpenId = new SteamOpenIdService(OAuthStates, settings);
    }

    public NlIdentitySettings Settings { get; }

    public IIdentityStore Store { get; }

    public IIdentityAuditStore Audit { get; }

    public NlIdentityService Identity { get; }

    public IGameOwnershipVerifier OwnershipVerifier { get; }

    public IPublisherBanChecker BanChecker { get; }

    public IMultiplayerSubscriptionChecker SubscriptionChecker { get; }

    public NlOwnershipAdmissionGate OwnershipGate { get; }

    public JsonOAuthStateStore OAuthStates { get; }

    public SteamOpenIdService SteamOpenId { get; }

    /// <summary>Reload mock ownership matrix after the on-disk config file is created or updated.</summary>
    public void ReloadMockOwnership() => _mockVerifier.Reload();
}

internal sealed class OffGameOwnershipVerifier : IGameOwnershipVerifier
{
    public Task<GameOwnershipResult> VerifyAsync(GameOwnershipRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new GameOwnershipResult(GameOwnershipStatus.Owned, "Ownership checks disabled."));
}
