using NL.Client.Core;
using NL.Core;
using NL.Core.Sp;
using NL.Server;
using NL.Server.Core.Integration;
using NL.Social;
using NL.Social.Core;

namespace NL.Client;

/// <summary>Session Host–local adapter for NL Client join flow (Phase R).</summary>
public sealed class NlClientHost
{
    public NlClientHost(
        Func<SessionProfileFile> getProfile,
        Func<bool> isSessionRunning,
        Func<NlSessionManifestDto> getManifest,
        Func<NlAdmitPlayerRequest, Task<NlJoinAdmissionResult>> admitAsync,
        Func<string, string, Task<bool>> acknowledgeAsync,
        Func<string, string, Task<NlClientOverlayState?>> getOverlayAsync,
        NlSocialHost? social = null)
    {
        GetProfile = getProfile;
        IsSessionRunning = isSessionRunning;
        GetManifest = getManifest;
        AdmitAsync = admitAsync;
        AcknowledgeAsync = acknowledgeAsync;
        GetOverlayAsync = getOverlayAsync;
        Social = social;
        JoinFlow = new NlClientJoinFlowService(new InProcessNlClientSessionApi(this));
    }

    public Func<SessionProfileFile> GetProfile { get; }

    public Func<bool> IsSessionRunning { get; }

    public Func<NlSessionManifestDto> GetManifest { get; }

    public Func<NlAdmitPlayerRequest, Task<NlJoinAdmissionResult>> AdmitAsync { get; }

    public Func<string, string, Task<bool>> AcknowledgeAsync { get; }

    public Func<string, string, Task<NlClientOverlayState?>> GetOverlayAsync { get; }

    public NlSocialHost? Social { get; }

    public NlClientJoinFlowService JoinFlow { get; }

    public async Task<IReadOnlyList<NlClientStreamerInfo>> ListStreamersAsync(CancellationToken cancellationToken = default)
    {
        var profile = GetProfile();
        var streamerId = string.IsNullOrWhiteSpace(profile.StreamerId)
            ? NlPaths.DefaultStreamerId
            : profile.StreamerId;

        var list = new List<NlClientStreamerInfo> { await BuildStreamerInfoAsync(streamerId, profile, cancellationToken) };

        if (Social?.Settings.Enabled == true)
        {
            var cfg = Social.StreamerStore.GetOrDefault(streamerId);
            if (!list.Any(s => string.Equals(s.StreamerId, cfg.StreamerId, StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(await BuildStreamerInfoAsync(cfg.StreamerId, profile, cancellationToken));
            }
        }

        return list;
    }

    private async Task<NlClientStreamerInfo> BuildStreamerInfoAsync(
        string streamerId,
        SessionProfileFile profile,
        CancellationToken cancellationToken)
    {
        var isLive = IsSessionRunning();
        string? title = null;
        string? platform = null;
        if (Social?.Settings.Enabled == true)
        {
            var cfg = Social.Gate.GetStreamerConfig(streamerId);
            var live = await Social.LiveMonitor.GetStatusAsync(cfg, cancellationToken);
            isLive = live.IsLive || isLive;
            title = live.Title;
            platform = live.Platform?.ToString();
        }

        return new NlClientStreamerInfo(
            streamerId,
            isLive,
            title,
            platform,
            profile.GameId ?? profile.Game);
    }

    public object ToPublicSettings() => new
    {
        deepLinkScheme = NlClientDeepLink.Scheme,
        modes = Enum.GetNames(typeof(NlClientMode)),
        inviteBlocker = true,
        overlayEnabled = true,
        mobileCompanion = true,
    };
}

public sealed record NlSessionManifestDto(
    string SessionId,
    string StreamerId,
    string HttpBaseUrl,
    string BridgeConnectUrl,
    string AdmitUrl,
    string? ForkConnectEndpoint,
    string? PartnershipTier,
    bool RequiresAtOwnRiskAcknowledgment,
    bool SessionRunning,
    string? GameId,
    string? CatalogMajorVersion);

internal sealed class InProcessNlClientSessionApi : INlClientSessionApi
{
    private readonly NlClientHost _host;

    public InProcessNlClientSessionApi(NlClientHost host) => _host = host;

    public Task<IReadOnlyList<NlClientStreamerInfo>> ListStreamersAsync(CancellationToken cancellationToken = default) =>
        _host.ListStreamersAsync(cancellationToken);

    public Task<NlClientManifest?> GetManifestAsync(CancellationToken cancellationToken = default)
    {
        var m = _host.GetManifest();
        return Task.FromResult<NlClientManifest?>(new NlClientManifest(
            m.SessionId,
            m.StreamerId,
            m.HttpBaseUrl,
            m.BridgeConnectUrl,
            m.AdmitUrl,
            m.ForkConnectEndpoint,
            m.PartnershipTier,
            m.RequiresAtOwnRiskAcknowledgment,
            m.SessionRunning,
            m.GameId,
            m.CatalogMajorVersion));
    }

    public async Task<NlClientAdmitResponse> AdmitAsync(NlClientJoinRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _host.AdmitAsync(new NlAdmitPlayerRequest
        {
            PlayerId = request.PlayerId,
            DisplayName = request.DisplayName,
            StreamerId = request.StreamerId,
            GameId = request.GameId,
            MajorVersion = request.MajorVersion,
            PlatformUserId = request.PlatformUserId,
            Platform = request.Platform,
            AppId = request.AppId,
            AtOwnRiskAcknowledged = request.AtOwnRiskAcknowledged,
        });

        return new NlClientAdmitResponse(
            result.Admit,
            result.Reason,
            result.Decision.ToString(),
            result.RequiresAtOwnRiskAcknowledgment,
            result.PartnershipTier,
            result.PartnershipLegalUrl);
    }

    public Task<bool> AcknowledgeAtOwnRiskAsync(string playerId, string gameId, CancellationToken cancellationToken = default) =>
        _host.AcknowledgeAsync(playerId, gameId);

    public Task<NlClientOverlayState?> GetOverlayAsync(string playerId, string streamerId, CancellationToken cancellationToken = default) =>
        _host.GetOverlayAsync(playerId, streamerId);

    public Task<NlClientMobileActionResult> MobileModerationAsync(
        NlClientMobileActionRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new NlClientMobileActionResult(false, "Use /api/v1/client/mobile/action on session host."));
}
