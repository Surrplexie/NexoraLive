namespace NL.Client.Core;

public static class NlClientDeepLink
{
    public const string Scheme = "nlclient";

    public static bool TryParse(string url, out NlClientDeepLinkRequest request)
    {
        request = new NlClientDeepLinkRequest("", "", "");
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var query = ParseQuery(uri.Query);
        var streamer = Get(query, "streamer");
        var game = Get(query, "game");
        var major = Get(query, "major") ?? Get(query, "majorVersion") ?? "1.0";
        if (string.IsNullOrWhiteSpace(streamer) || string.IsNullOrWhiteSpace(game))
        {
            return false;
        }

        request = new NlClientDeepLinkRequest(
            streamer,
            game,
            major,
            Get(query, "player") ?? Get(query, "playerId"));
        return true;
    }

    public static string Build(NlClientDeepLinkRequest request)
    {
        var q = new Dictionary<string, string>
        {
            ["streamer"] = request.StreamerId,
            ["game"] = request.GameId,
            ["major"] = request.MajorVersion,
        };
        if (!string.IsNullOrWhiteSpace(request.PlayerId))
        {
            q["player"] = request.PlayerId;
        }

        return $"{Scheme}://join?{string.Join("&", q.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"))}";
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var trimmed = query.TrimStart('?');
        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = part.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(part[..idx]);
            var val = Uri.UnescapeDataString(part[(idx + 1)..]);
            map[key] = val;
        }

        return map;
    }

    private static string? Get(Dictionary<string, string> query, string key) =>
        query.TryGetValue(key, out var val) ? val : null;
}

public static class NlInviteBlocker
{
    private static readonly string[] NlHostMarkers =
    [
        "/api/v1/session/admit",
        "/nl/v1",
        "bridgeConnectUrl",
        "forkConnectEndpoint",
        "nl-session-server",
    ];

    public static NlClientInviteBlockResult Evaluate(string inviteUrl, string? expectedSessionHost = null)
    {
        if (string.IsNullOrWhiteSpace(inviteUrl))
        {
            return new NlClientInviteBlockResult(false, "Empty invite URL.");
        }

        var looksNl = NlHostMarkers.Any(m =>
            inviteUrl.Contains(m, StringComparison.OrdinalIgnoreCase));
        if (!looksNl && !inviteUrl.Contains("27020", StringComparison.Ordinal)
            && !inviteUrl.Contains("27021", StringComparison.Ordinal))
        {
            return new NlClientInviteBlockResult(false, "Not an NL session endpoint.");
        }

        if (!string.IsNullOrWhiteSpace(expectedSessionHost)
            && inviteUrl.Contains(expectedSessionHost, StringComparison.OrdinalIgnoreCase))
        {
            return new NlClientInviteBlockResult(
                true,
                "Native multiplayer invite to NL session host blocked — use NL Client join flow.",
                RedirectUrl: "/nl-client.html");
        }

        return new NlClientInviteBlockResult(
            true,
            "Stray invite to NL infrastructure blocked. Connect through NL Client admit flow only.");
    }
}

public static class NlClientLaunchBuilder
{
    public static NlClientLaunchParams Build(NlClientManifest manifest)
    {
        var fork = string.IsNullOrWhiteSpace(manifest.ForkConnectEndpoint)
            ? "direct-bridge"
            : manifest.ForkConnectEndpoint;
        var cmd =
            $"--nl-bridge \"{manifest.BridgeConnectUrl}\" --nl-fork \"{fork}\" --nl-streamer \"{manifest.StreamerId}\"";
        return new NlClientLaunchParams(
            "nl-fork-launch",
            cmd,
            fork,
            manifest.BridgeConnectUrl,
            NlClientDeepLink.Build(new NlClientDeepLinkRequest(
                manifest.StreamerId,
                manifest.GameId ?? "generic",
                manifest.CatalogMajorVersion ?? "1.0")));
    }
}
