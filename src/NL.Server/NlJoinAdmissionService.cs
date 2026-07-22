using NL.Core;
using NL.Core.Sp;
using NL.Moderation;
using NL.Moderation.Core;
using NL.Server.Core.Integration;

namespace NL.Server;

/// <summary>
/// Evaluates join eligibility at connect time (before <c>playerJoin</c> events) for networked
/// NL session servers. Bridges call the REST admit endpoint; game servers gate on <see cref="NlJoinAdmissionResult.Admit"/>.
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

    public NlJoinAdmissionResult Evaluate(string playerId, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            throw new ArgumentException("playerId required.", nameof(playerId));
        }

        var name = string.IsNullOrWhiteSpace(displayName) ? playerId.Trim() : displayName.Trim();
        var profile = _moderation.GetOrCreateProfile(playerId.Trim(), name);
        var join = JoinEligibilityEngine.Evaluate(profile, _streamerId, _requirements, _clock());
        var standing = profile.GetRelationship(_streamerId).Standing;
        return NlJoinAdmissionResult.FromJoinResult(join, playerId.Trim(), standing);
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
        bool sessionRunning)
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
        };
    }
}

public sealed class NlAdmitPlayerRequest
{
    public string? StreamerId { get; set; }
    public string PlayerId { get; set; } = "";
    public string? DisplayName { get; set; }
}
