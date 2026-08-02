using System.Net.Http.Json;
using System.Text.Json;
using NL.Client.Core;

namespace NL.Client;

public sealed class HttpNlClientSessionApi : INlClientSessionApi, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public HttpNlClientSessionApi(NlClientSettings settings, HttpClient? http = null)
    {
        _baseUrl = settings.SessionBaseUrl.Trim().TrimEnd('/');
        _http = http ?? new HttpClient();
        if (!string.IsNullOrWhiteSpace(settings.OperatorKey))
        {
            _http.DefaultRequestHeaders.Add("X-NL-Operator-Key", settings.OperatorKey);
        }
    }

    public async Task<IReadOnlyList<NlClientStreamerInfo>> ListStreamersAsync(CancellationToken cancellationToken = default)
    {
        var list = await GetJsonAsync<List<NlClientStreamerInfo>>("/api/v1/client/streamers", cancellationToken);
        return list ?? [];
    }

    public async Task<NlClientManifest?> GetManifestAsync(CancellationToken cancellationToken = default)
    {
        var doc = await GetJsonAsync<JsonElement>("/api/v1/session/manifest", cancellationToken);
        if (doc.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        return MapManifest(doc);
    }

    public async Task<NlClientAdmitResponse> AdmitAsync(NlClientJoinRequest request, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            playerId = request.PlayerId,
            displayName = request.DisplayName ?? request.PlayerId,
            streamerId = request.StreamerId,
            gameId = request.GameId,
            majorVersion = request.MajorVersion,
            platformUserId = request.PlatformUserId,
            platform = request.Platform ?? "steam",
            appId = request.AppId,
            atOwnRiskAcknowledged = request.AtOwnRiskAcknowledged,
        };

        using var res = await _http.PostAsJsonAsync($"{_baseUrl}/api/v1/session/admit", body, cancellationToken);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
        return MapAdmit(json);
    }

    public async Task<bool> AcknowledgeAtOwnRiskAsync(string playerId, string gameId, CancellationToken cancellationToken = default)
    {
        using var res = await _http.PostAsJsonAsync(
            $"{_baseUrl}/api/v1/partnership/acknowledge",
            new { playerId, gameId },
            cancellationToken);
        return res.IsSuccessStatusCode;
    }

    public async Task<NlClientOverlayState?> GetOverlayAsync(
        string playerId,
        string streamerId,
        CancellationToken cancellationToken = default)
    {
        var path = $"/api/v1/client/overlay/{Uri.EscapeDataString(playerId)}?streamer={Uri.EscapeDataString(streamerId)}";
        return await GetJsonAsync<NlClientOverlayState>(path, cancellationToken);
    }

    public async Task<NlClientMobileActionResult> MobileModerationAsync(
        NlClientMobileActionRequest request,
        CancellationToken cancellationToken = default)
    {
        using var res = await _http.PostAsJsonAsync($"{_baseUrl}/api/v1/client/mobile/action", request, cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
            var msg = err.ValueKind != JsonValueKind.Undefined && err.TryGetProperty("error", out var e)
                ? e.GetString()
                : res.ReasonPhrase;
            return new NlClientMobileActionResult(false, msg);
        }

        return new NlClientMobileActionResult(true);
    }

    public void Dispose() => _http.Dispose();

    private async Task<T?> GetJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var res = await _http.GetAsync($"{_baseUrl}{path}", cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            return default;
        }

        return await res.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private static NlClientManifest MapManifest(JsonElement json) => new(
        json.GetProperty("sessionId").GetString() ?? "",
        json.GetProperty("streamerId").GetString() ?? "",
        json.GetProperty("httpBaseUrl").GetString() ?? "",
        json.GetProperty("bridgeConnectUrl").GetString() ?? "",
        json.GetProperty("admitUrl").GetString() ?? "",
        json.TryGetProperty("forkConnectEndpoint", out var fork) ? fork.GetString() : null,
        json.TryGetProperty("partnershipTier", out var tier) ? tier.GetString() : null,
        json.TryGetProperty("requiresAtOwnRiskAcknowledgment", out var ack) && ack.GetBoolean(),
        json.TryGetProperty("sessionRunning", out var run) && run.GetBoolean(),
        json.TryGetProperty("gameId", out var game) ? game.GetString() : null,
        json.TryGetProperty("catalogMajorVersion", out var major) ? major.GetString() : null);

    private static NlClientAdmitResponse MapAdmit(JsonElement json) => new(
        json.TryGetProperty("admit", out var admit) && admit.GetBoolean(),
        json.TryGetProperty("reason", out var reason) ? reason.GetString() : null,
        json.TryGetProperty("decision", out var decision) ? decision.GetString() : null,
        json.TryGetProperty("requiresAtOwnRiskAcknowledgment", out var req) && req.GetBoolean(),
        json.TryGetProperty("partnershipTier", out var tier) ? tier.GetString() : null,
        json.TryGetProperty("partnershipLegalUrl", out var legal) ? legal.GetString() : null);
}
