using NL.Core;
using NL.Core.Sp;
using NL.Identity;
using NL.Identity.Core;
using NL.Moderation;
using NL.Moderation.Core;
using NL.Server.Core.Integration;
using NL.Social;
using NL.Fork.Catalog;
using NL.Partnership;
using NL.Fork.Catalog.Core;

namespace NL.Server;

/// <summary>
/// Evaluates join eligibility at connect time (before <c>playerJoin</c> events) for networked
/// NL session servers. Phase L adds game ownership verification before Allow.
/// </summary>
public sealed class NlJoinAdmissionService
{
    private readonly ModerationService _moderation;
    private readonly JoinRequirements _requirements;
    private readonly string _streamerId;
    private readonly Func<DateTimeOffset> _clock;

    public NlJoinAdmissionService(
        ModerationService moderation,
        string streamerId,
        JoinRequirements requirements,
        Func<DateTimeOffset>? clock = null)
    {
        _moderation = moderation;
        _streamerId = streamerId;
        _requirements = requirements;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public static NlJoinAdmissionService CreateDefault(string streamerId, string? joinRequirementsPath = null)
    {
        NlPaths.EnsureRoot();
        var moderation = new ModerationService(
            new JsonlModerationStore(NlPaths.ModerationLog),
            new JsonFileSpProfileRepository(NlPaths.SpProfiles));
        var requirements = JoinRequirementsStore.LoadOrDefault(
            joinRequirementsPath ?? NlPaths.JoinRequirements);
        return new NlJoinAdmissionService(moderation, streamerId, requirements);
    }

    public NlJoinAdmissionResult Evaluate(string playerId, string? displayName = null) =>
        EvaluateAsync(
            new NlAdmitPlayerRequest { PlayerId = playerId, DisplayName = displayName },
            null,
            null).GetAwaiter().GetResult();

    public async Task<NlJoinAdmissionResult> EvaluateAsync(
        NlAdmitPlayerRequest request,
        SessionProfileFile? profile,
        NlIdentityHost? identity,
        NlSocialHost? social = null,
        NlForkCatalogHost? catalog = null,
        NlPartnershipHost? partnership = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerId))
        {
            throw new ArgumentException("playerId required.", nameof(request));
        }

        var playerId = request.PlayerId.Trim();
        var name = string.IsNullOrWhiteSpace(request.DisplayName) ? playerId : request.DisplayName.Trim();
        var spProfile = _moderation.GetOrCreateProfile(playerId, name);

        if (social?.Settings.Enabled == true && social.Settings.Mode != NlSocialMode.Off)
        {
            var linkInput = SocialLinkInput.FromAdmitRequest(
                request.TwitchUserId,
                request.YouTubeChannelId,
                request.KickUserId,
                request.DiscordUserId,
                request.SocialPlatform,
                request.SocialUserId);
            var links = social.Gate.ResolveLinks(playerId, linkInput);
            await social.Gate.RefreshRelationshipAsync(
                spProfile,
                _streamerId,
                links,
                _requirements.RequireFollow,
                _requirements.RequireSubscription,
                _requirements.RequireDiscordMember,
                cancellationToken);
            _moderation.SaveProfile(spProfile);
        }

        var join = JoinEligibilityEngine.Evaluate(spProfile, _streamerId, _requirements, _clock());
        var standing = spProfile.GetRelationship(_streamerId).Standing;

        if (join.Decision != JoinDecision.Allow)
        {
            return NlJoinAdmissionResult.FromJoinResult(join, playerId, standing);
        }

        if (profile?.RequireGameOwnership == true && identity is not null)
        {
            var ownershipContext = new OwnershipAdmissionContext(
                RequireGameOwnership: true,
                Mode: identity.Settings.Mode,
                Platform: request.Platform ?? profile.OwnershipPlatform,
                PlatformUserId: request.PlatformUserId,
                GameId: request.GameId ?? profile.GameId,
                AppId: request.AppId ?? profile.PlatformAppId,
                MajorVersion: request.MajorVersion ?? profile.GameMajorVersion,
                NlAccountId: request.NlAccountId,
                StrictUnknown: profile.StrictOwnershipUnknown);

            var ownershipDeny = await identity.OwnershipGate.EvaluateAsync(ownershipContext, cancellationToken);
            if (ownershipDeny is not null)
            {
                return NlJoinAdmissionResult.FromOwnershipDeny(
                    playerId,
                    ownershipDeny.Reason,
                    standing,
                    ownershipDeny.OwnershipStatus.ToString());
            }
        }

        var resolvedTier = PartnershipTier.AtOwnRisk;
        string? legalOverride = profile?.CatalogLegalNotice;
        ForkCatalogEntry? catalogEntry = null;

        if (profile?.CatalogEnforced == true && catalog?.Settings.Enabled == true)
        {
            var gameId = request.GameId ?? profile.GameId ?? profile.Game;
            var expectedMajor = profile.GameMajorVersion;
            if (string.IsNullOrWhiteSpace(expectedMajor))
            {
                return NlJoinAdmissionResult.FromCatalogDeny(
                    playerId,
                    "Session profile missing catalog major version.",
                    standing);
            }

            var catalogResult = catalog.Catalog.ValidateClientMajor(
                gameId,
                request.MajorVersion ?? expectedMajor,
                expectedMajor);
            if (!catalogResult.IsValid)
            {
                return NlJoinAdmissionResult.FromCatalogDeny(
                    playerId,
                    catalogResult.Error ?? "Catalog validation failed.",
                    standing,
                    catalogResult.Entry?.Tier.ToString());
            }

            catalogEntry = catalogResult.Entry;
            resolvedTier = catalogEntry?.Tier ?? resolvedTier;
            legalOverride = catalogEntry?.EffectiveLegalNotice ?? legalOverride;
        }
        else if (!string.IsNullOrWhiteSpace(profile?.PartnershipTier)
            && Enum.TryParse<PartnershipTier>(profile.PartnershipTier, true, out var profileTier))
        {
            resolvedTier = profileTier;
        }

        if (partnership?.Settings.Enabled == true
            && partnership.Settings.RequireGateAtAdmit
            && profile?.PartnershipGateEnabled != false)
        {
            var gateGameId = request.GameId ?? profile?.GameId ?? profile?.Game ?? "generic";
            var gate = partnership.Gate.EvaluateAdmit(
                playerId,
                gateGameId,
                resolvedTier,
                request.PlatformUserId,
                request.Platform ?? profile?.OwnershipPlatform,
                request.AppId ?? profile?.PlatformAppId,
                request.AtOwnRiskAcknowledged,
                legalOverride,
                _clock());

            if (!gate.Allowed)
            {
                var legalUrl = gate.Legal is not null
                    ? $"/api/v1/partnership/legal/{Uri.EscapeDataString(gateGameId)}"
                    : null;
                return NlJoinAdmissionResult.FromPartnershipDeny(
                    playerId,
                    gate.DenyReason ?? "Partnership gate denied.",
                    standing,
                    gate.Tier.ToString(),
                    gate.RequiresAcknowledgment,
                    legalUrl,
                    gate.Legal?.DisclaimerVersion);
            }

            resolvedTier = gate.Tier;
            var publisherId = partnership.Publishers.List()
                .FirstOrDefault(p => p.Titles.Any(t =>
                    string.Equals(t.GameId, gateGameId, StringComparison.OrdinalIgnoreCase)))
                ?.PublisherId;
            partnership.Metrics.RecordJoin(gateGameId, publisherId);
        }

        return NlJoinAdmissionResult.FromJoinResult(join, playerId, standing, resolvedTier.ToString());
    }

    public ModerationService Moderation => _moderation;
}

/// <summary>Builds public URLs and manifests for remote bridges.</summary>
public static class NlSessionServerHelper
{
    public static string ResolvePublicHttpBase(string bindHost, int httpPort)
    {
        var overrideUrl = Environment.GetEnvironmentVariable("NL_PUBLIC_HTTP");
        if (!string.IsNullOrWhiteSpace(overrideUrl))
        {
            return overrideUrl.Trim().TrimEnd('/');
        }

        var publicHost = Environment.GetEnvironmentVariable("NL_PUBLIC_HOST");
        if (!string.IsNullOrWhiteSpace(publicHost))
        {
            publicHost = publicHost.Trim().TrimEnd('/');
            if (publicHost.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || publicHost.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return publicHost;
            }

            return $"http://{publicHost}:{httpPort}";
        }

        var host = bindHost is "0.0.0.0" or "+" or "*" or "127.0.0.1" ? "127.0.0.1" : bindHost;
        return $"http://{host}:{httpPort}";
    }

    public static string ResolvePublicWebSocketBase(string bindHost, int wsPort)
    {
        var overrideUrl = Environment.GetEnvironmentVariable("NL_PUBLIC_WS");
        if (!string.IsNullOrWhiteSpace(overrideUrl))
        {
            return overrideUrl.Trim().TrimEnd('/');
        }

        var publicHost = Environment.GetEnvironmentVariable("NL_PUBLIC_HOST");
        if (!string.IsNullOrWhiteSpace(publicHost))
        {
            publicHost = publicHost.Trim().TrimEnd('/');
            if (publicHost.StartsWith("ws://", StringComparison.OrdinalIgnoreCase)
                || publicHost.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                return publicHost;
            }

            if (publicHost.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return "ws://" + publicHost["http://".Length..].TrimEnd('/') + NlIntegrationProtocol.WebSocketPath;
            }

            if (publicHost.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return "wss://" + publicHost["https://".Length..].TrimEnd('/') + NlIntegrationProtocol.WebSocketPath;
            }

            return $"ws://{publicHost}:{wsPort}{NlIntegrationProtocol.WebSocketPath}";
        }

        var host = bindHost is "0.0.0.0" or "+" or "*" or "127.0.0.1" ? "127.0.0.1" : bindHost;
        return $"ws://{host}:{wsPort}{NlIntegrationProtocol.WebSocketPath}";
    }

    public static string ResolvePublicModerationUrl(string bindHost, int modPort)
    {
        var overrideUrl = Environment.GetEnvironmentVariable("NL_PUBLIC_MOD_HTTP");
        if (!string.IsNullOrWhiteSpace(overrideUrl))
        {
            return overrideUrl.Trim().TrimEnd('/');
        }

        var publicHost = Environment.GetEnvironmentVariable("NL_PUBLIC_HOST");
        if (!string.IsNullOrWhiteSpace(publicHost))
        {
            publicHost = publicHost.Trim().TrimEnd('/');
            if (publicHost.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || publicHost.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(publicHost);
                return $"http://{uri.Host}:{NlSessionServerDefaults.ModerationPort}";
            }

            return $"http://{publicHost}:{NlSessionServerDefaults.ModerationPort}";
        }

        var host = bindHost is "0.0.0.0" or "+" or "*" or "127.0.0.1" ? "127.0.0.1" : bindHost;
        return $"http://{host}:{modPort}";
    }

    public static NlSessionManifest CreateManifest(
        NlSessionBusInfo bus,
        SessionProfileFile profile,
        string bindHost,
        int httpPort,
        int wsPort,
        int modPort,
        bool sessionRunning,
        ForkManifestConnectInfo? forkConnect = null,
        string? fleetRegionId = null,
        string? fleetTurnUri = null)
    {
        var httpBase = ResolvePublicHttpBase(bindHost, httpPort);
        var wsBase = ResolvePublicWebSocketBase(bindHost, wsPort);
        var bridgeUrl = $"{wsBase}?token={Uri.EscapeDataString(bus.Token)}";
        var moderationUrl = ResolvePublicModerationUrl(bindHost, modPort);

        return new NlSessionManifest
        {
            SessionId = bus.SessionId,
            StreamerId = string.IsNullOrWhiteSpace(profile.StreamerId)
                ? NlPaths.DefaultStreamerId
                : profile.StreamerId,
            HttpBaseUrl = httpBase,
            BridgeConnectUrl = bridgeUrl,
            AdmitUrl = $"{httpBase}/api/v1/session/admit",
            ManifestUrl = $"{httpBase}/api/v1/session/manifest",
            ModerationUrl = moderationUrl,
            JoinGateEnabled = profile.JoinGate,
            SessionRunning = sessionRunning,
            AntiCheatEnabled = profile.AntiCheat,
            OwnershipRequired = profile.RequireGameOwnership,
            GameId = profile.GameId,
            PlatformAppId = profile.PlatformAppId,
            CatalogMajorVersion = profile.GameMajorVersion,
            PartnershipTier = profile.PartnershipTier,
            NoProgressTransfer = profile.NoProgressTransfer,
            CatalogLegalNotice = profile.CatalogLegalNotice,
            RequiresAtOwnRiskAcknowledgment = IsAtOwnRiskTier(profile.PartnershipTier),
            PartnershipLegalUrl = !string.IsNullOrWhiteSpace(profile.GameId)
                ? $"{httpBase}/api/v1/partnership/legal/{Uri.EscapeDataString(profile.GameId)}"
                : null,
            PartnershipDisclaimerVersion = NL.Partnership.Core.PartnershipLegalTemplates.DisclaimerVersion,
            ForkOrchestratorEnabled = profile.ForkOrchestratorEnabled,
            ForkSessionId = forkConnect?.ForkSessionId ?? profile.ForkSessionId,
            ForkConnectEndpoint = forkConnect?.ForkConnectEndpoint,
            ForkProvisioner = forkConnect?.ForkProvisioner,
            ReservedPrivilegedSlots = forkConnect?.ReservedPrivilegedSlots
                ?? (profile.ForkReservedPrivilegedSlots > 0
                    ? profile.ForkReservedPrivilegedSlots
                    : 2),
            FleetRegionId = fleetRegionId,
            FleetTurnUri = fleetTurnUri,
        };
    }

    private static bool IsAtOwnRiskTier(string? tier) =>
        string.IsNullOrWhiteSpace(tier)
        || string.Equals(tier, "AtOwnRisk", StringComparison.OrdinalIgnoreCase);
}

public sealed class NlAdmitPlayerRequest
{
    public string? StreamerId { get; set; }
    public string PlayerId { get; set; } = "";
    public string? DisplayName { get; set; }

    /// <summary>Phase L — platform user id (e.g. Steam64).</summary>
    public string? PlatformUserId { get; set; }

    public string? Platform { get; set; }

    public string? GameId { get; set; }

    public string? AppId { get; set; }

    public string? MajorVersion { get; set; }

    public string? NlAccountId { get; set; }

    /// <summary>Phase M — linked platform ids for live follow/sub/discord checks.</summary>
    public string? TwitchUserId { get; set; }

    public string? YouTubeChannelId { get; set; }

    public string? KickUserId { get; set; }

    public string? DiscordUserId { get; set; }

    public string? SocialPlatform { get; set; }

    public string? SocialUserId { get; set; }

    /// <summary>Phase Q — SP confirmed at-own-risk disclaimer for this admit attempt.</summary>
    public bool AtOwnRiskAcknowledged { get; set; }
}
