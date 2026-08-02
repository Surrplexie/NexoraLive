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

    public static NlIdentitySettings LoadFromEnvironment()
    {
        var enabled = string.Equals(
            Environment.GetEnvironmentVariable(EnabledVariable),
            "1",
            StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                Environment.GetEnvironmentVariable(EnabledVariable),
                "true",
                StringComparison.OrdinalIgnoreCase);

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
        };
    }

    public object ToPublicInfo() => new
    {
        enabled = Enabled,
        mode = Mode.ToString(),
        strictUnknown = StrictUnknown,
        steamConfigured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STEAM_WEB_API_KEY")),
        storePath = NlIdentityPaths.Root,
    };
}

public sealed class NlIdentityHost
{
    public NlIdentityHost(NlIdentitySettings settings)
    {
        Settings = settings;
        NlIdentityPaths.EnsureRoot();

        Store = new JsonFileIdentityStore();
        Audit = new JsonlIdentityAuditStore();
        Identity = new NlIdentityService(Store, Audit);

        var mock = new MockGameOwnershipVerifier();
        var steam = new SteamWebApiOwnershipVerifier(fallback: mock);

        OwnershipVerifier = settings.Mode switch
        {
            NlOwnershipMode.Off => new OffGameOwnershipVerifier(),
            NlOwnershipMode.Live => new CompositeGameOwnershipVerifier(
                steam,
                mock,
                new StubPlatformOwnershipVerifier(NlPlatform.Epic),
                new StubPlatformOwnershipVerifier(NlPlatform.Ubisoft),
                new StubPlatformOwnershipVerifier(NlPlatform.Ea),
                new StubPlatformOwnershipVerifier(NlPlatform.Xbox),
                new StubPlatformOwnershipVerifier(NlPlatform.PlayStation),
                new StubPlatformOwnershipVerifier(NlPlatform.Riot),
                new StubPlatformOwnershipVerifier(NlPlatform.Itch)),
            _ => mock,
        };

        BanChecker = new CompositePublisherBanChecker(steam, mock);
        SubscriptionChecker = mock;
        OwnershipGate = new NlOwnershipAdmissionGate(
            OwnershipVerifier,
            BanChecker,
            SubscriptionChecker,
            Identity,
            settings);
    }

    public NlIdentitySettings Settings { get; }

    public IIdentityStore Store { get; }

    public IIdentityAuditStore Audit { get; }

    public NlIdentityService Identity { get; }

    public IGameOwnershipVerifier OwnershipVerifier { get; }

    public IPublisherBanChecker BanChecker { get; }

    public IMultiplayerSubscriptionChecker SubscriptionChecker { get; }

    public NlOwnershipAdmissionGate OwnershipGate { get; }
}

internal sealed class OffGameOwnershipVerifier : IGameOwnershipVerifier
{
    public Task<GameOwnershipResult> VerifyAsync(GameOwnershipRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new GameOwnershipResult(GameOwnershipStatus.Owned, "Ownership checks disabled."));
}
