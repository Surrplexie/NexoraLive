namespace NL.Client.Core;

public interface INlClientSessionApi
{
    Task<IReadOnlyList<NlClientStreamerInfo>> ListStreamersAsync(CancellationToken cancellationToken = default);

    Task<NlClientManifest?> GetManifestAsync(CancellationToken cancellationToken = default);

    Task<NlClientAdmitResponse> AdmitAsync(NlClientJoinRequest request, CancellationToken cancellationToken = default);

    Task<bool> AcknowledgeAtOwnRiskAsync(string playerId, string gameId, CancellationToken cancellationToken = default);

    Task<NlClientOverlayState?> GetOverlayAsync(
        string playerId,
        string streamerId,
        CancellationToken cancellationToken = default);

    Task<NlClientMobileActionResult> MobileModerationAsync(
        NlClientMobileActionRequest request,
        CancellationToken cancellationToken = default);
}
