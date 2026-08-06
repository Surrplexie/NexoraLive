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

    public string? PublicBaseUrl { get; init; }

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

        var liveReady = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STEAM_WEB_API_KEY"))
            || PlatformOAuthEnv.HasPair("EPIC_CLIENT_ID", "EPIC_CLIENT_SECRET")
            || PlatformOAuthEnv.HasPair("XBOX_CLIENT_ID", "XBOX_CLIENT_SECRET")
            || PlatformOAuthEnv.HasPair("MICROSOFT_CLIENT_ID", "MICROSOFT_CLIENT_SECRET")
            || PlatformOAuthEnv.HasPair("PSN_CLIENT_ID", "PSN_CLIENT_SECRET");

        if (mode == NlOwnershipMode.Live && !liveReady)
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
        epicOAuthConfigured = PlatformOAuthEnv.HasPair("EPIC_CLIENT_ID", "EPIC_CLIENT_SECRET"),
        xboxOAuthConfigured = PlatformOAuthEnv.HasPair("XBOX_CLIENT_ID", "XBOX_CLIENT_SECRET")
            || PlatformOAuthEnv.HasPair("MICROSOFT_CLIENT_ID", "MICROSOFT_CLIENT_SECRET"),
        psnOAuthConfigured = PlatformOAuthEnv.HasPair("PSN_CLIENT_ID", "PSN_CLIENT_SECRET"),
        steamOpenIdEnabled = Enabled,
        publicBaseUrl = PublicBaseUrl,
        oauth = new
        {
            steamAuthorize = "/api/v1/identity/oauth/steam/authorize",
            steamCallback = "/api/v1/identity/oauth/steam/callback",
            epicAuthorize = "/api/v1/identity/oauth/epic/authorize",
            epicCallback = "/api/v1/identity/oauth/epic/callback",
            xboxAuthorize = "/api/v1/identity/oauth/xbox/authorize",
            xboxCallback = "/api/v1/identity/oauth/xbox/callback",
            playstationAuthorize = "/api/v1/identity/oauth/playstation/authorize",
            playstationCallback = "/api/v1/identity/oauth/playstation/callback",
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
        PlatformCredentials = new JsonPlatformOAuthCredentialStore();
        PlatformTokens = new PlatformOAuthTokenService(PlatformCredentials);
        var epic = new EpicOwnershipVerifier(_mockVerifier);
        var xbox = new XboxOwnershipVerifier(_mockVerifier, PlatformTokens);
        var playstation = new PlayStationOwnershipVerifier(_mockVerifier, PlatformTokens);

        OwnershipVerifier = settings.Mode switch
        {
            NlOwnershipMode.Off => new OffGameOwnershipVerifier(),
            NlOwnershipMode.Live => new CompositeGameOwnershipVerifier(
                steam,
                epic,
                xbox,
                playstation,
                _mockVerifier,
                new StubPlatformOwnershipVerifier(NlPlatform.Ubisoft),
                new StubPlatformOwnershipVerifier(NlPlatform.Ea),
                new StubPlatformOwnershipVerifier(NlPlatform.Riot),
                new StubPlatformOwnershipVerifier(NlPlatform.Itch)),
            _ => _mockVerifier,
        };

        BanChecker = new CompositePublisherBanChecker(steam, _mockVerifier);
        SubscriptionChecker = new CompositeMultiplayerSubscriptionChecker(xbox, playstation, _mockVerifier);
        OwnershipGate = new NlOwnershipAdmissionGate(
            OwnershipVerifier,
            BanChecker,
            SubscriptionChecker,
            Identity,
            settings,
            Audit);

        OAuthStates = new JsonOAuthStateStore();
        SteamOpenId = new SteamOpenIdService(OAuthStates, settings);
        EpicOAuth = new EpicOAuthService(OAuthStates, PlatformCredentials, Identity);
        XboxOAuth = new XboxOAuthService(OAuthStates, PlatformCredentials, Identity);
        PlayStationOAuth = new PlayStationOAuthService(OAuthStates, PlatformCredentials, Identity);
    }

    public NlIdentitySettings Settings { get; }

    public IIdentityStore Store { get; }

    public IIdentityAuditStore Audit { get; }

    public NlIdentityService Identity { get; }

    public JsonPlatformOAuthCredentialStore PlatformCredentials { get; }

    public PlatformOAuthTokenService PlatformTokens { get; }

    public IGameOwnershipVerifier OwnershipVerifier { get; }

    public IPublisherBanChecker BanChecker { get; }

    public IMultiplayerSubscriptionChecker SubscriptionChecker { get; }

    public NlOwnershipAdmissionGate OwnershipGate { get; }

    public JsonOAuthStateStore OAuthStates { get; }

    public SteamOpenIdService SteamOpenId { get; }

    public EpicOAuthService EpicOAuth { get; }

    public XboxOAuthService XboxOAuth { get; }

    public PlayStationOAuthService PlayStationOAuth { get; }

    public void ReloadMockOwnership() => _mockVerifier.Reload();
}

internal sealed class OffGameOwnershipVerifier : IGameOwnershipVerifier
{
    public Task<GameOwnershipResult> VerifyAsync(GameOwnershipRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new GameOwnershipResult(GameOwnershipStatus.Owned, "Ownership checks disabled."));
}
