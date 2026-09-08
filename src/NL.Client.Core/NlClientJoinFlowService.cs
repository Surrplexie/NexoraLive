namespace NL.Client.Core;

public sealed class NlClientJoinFlowService
{
    private readonly INlClientSessionApi _api;

    public NlClientJoinFlowService(INlClientSessionApi api) => _api = api;

    public async Task<NlClientJoinFlowResult> ExecuteAsync(
        NlClientJoinRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerId))
        {
            return Fail(NlClientJoinStep.Error, "playerId required.");
        }

        if (string.IsNullOrWhiteSpace(request.StreamerId))
        {
            return Fail(NlClientJoinStep.Error, "streamerId required.");
        }

        var manifest = await _api.GetManifestAsync(cancellationToken);
        if (manifest is null)
        {
            return Fail(NlClientJoinStep.Error, "Could not load session manifest.");
        }

        if (!manifest.SessionRunning)
        {
            return new NlClientJoinFlowResult(
                false,
                NlClientJoinStep.SessionOffline,
                "Streamer session is not live.",
                manifest);
        }

        var gameId = request.GameId ?? manifest.GameId ?? "generic";
        var major = request.MajorVersion ?? manifest.CatalogMajorVersion ?? "1.0";

        if (manifest.RequiresAtOwnRiskAcknowledgment && !request.AtOwnRiskAcknowledged)
        {
            var probe = await _api.AdmitAsync(request with { GameId = gameId, MajorVersion = major }, cancellationToken);
            if (probe.RequiresAtOwnRiskAcknowledgment)
            {
                return new NlClientJoinFlowResult(
                    false,
                    NlClientJoinStep.RequiresAtOwnRiskAck,
                    probe.Reason ?? "At-own-risk acknowledgment required.",
                    manifest,
                    Admit: probe);
            }
        }

        if (request.AtOwnRiskAcknowledged)
        {
            await _api.AcknowledgeAtOwnRiskAsync(request.PlayerId, gameId, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(request.PlatformUserId))
        {
            return new NlClientJoinFlowResult(
                false,
                NlClientJoinStep.RequiresOwnership,
                "Platform ownership proof required (platformUserId).",
                manifest);
        }

        var admit = await _api.AdmitAsync(
            request with
            {
                GameId = gameId,
                MajorVersion = major,
                AtOwnRiskAcknowledged = request.AtOwnRiskAcknowledged || !manifest.RequiresAtOwnRiskAcknowledgment,
            },
            cancellationToken);

        if (!admit.Admit)
        {
            if (admit.RequiresAtOwnRiskAcknowledgment)
            {
                return new NlClientJoinFlowResult(
                    false,
                    NlClientJoinStep.RequiresAtOwnRiskAck,
                    admit.Reason,
                    manifest,
                    Admit: admit);
            }

            return new NlClientJoinFlowResult(
                false,
                NlClientJoinStep.AdmitDenied,
                admit.Reason ?? "Admit denied.",
                manifest,
                Admit: admit);
        }

        var launch = NlClientLaunchBuilder.Build(manifest);
        return new NlClientJoinFlowResult(
            true,
            NlClientJoinStep.Completed,
            "Join flow complete — launch game with NL connect params.",
            manifest,
            launch,
            admit);
    }

    public Task<NlClientJoinFlowResult> ExecuteDeepLinkAsync(
        string deepLinkUrl,
        string playerId,
        string? platformUserId = null,
        bool atOwnRiskAcknowledged = false,
        CancellationToken cancellationToken = default)
    {
        if (!NlClientDeepLink.TryParse(deepLinkUrl, out var link))
        {
            return Task.FromResult(Fail(NlClientJoinStep.Error, "Invalid nlclient deep link."));
        }

        return ExecuteAsync(new NlClientJoinRequest(
            playerId,
            link.StreamerId,
            GameId: link.GameId,
            MajorVersion: link.MajorVersion,
            PlatformUserId: platformUserId,
            AtOwnRiskAcknowledged: atOwnRiskAcknowledged), cancellationToken);
    }

    private static NlClientJoinFlowResult Fail(NlClientJoinStep step, string message) =>
        new(false, step, message);
}
