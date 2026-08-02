using System.Net.Http.Headers;
using NL.Identity.Core;

namespace NL.Identity;

public sealed record SteamOpenIdCallbackResult(
    bool Success,
    string? SteamId = null,
    string? AccountId = null,
    string? ReturnUrl = null,
    string? Error = null);

/// <summary>Steam OpenID 2.0 sign-in for linking platform accounts (Phase L).</summary>
public sealed class SteamOpenIdService
{
    private const string SteamOpenIdEndpoint = "https://steamcommunity.com/openid/login";
    private const string ClaimedIdPrefix = "https://steamcommunity.com/openid/id/";

    private readonly HttpClient _http;
    private readonly JsonOAuthStateStore _stateStore;
    private readonly NlIdentitySettings _settings;

    public SteamOpenIdService(
        JsonOAuthStateStore stateStore,
        NlIdentitySettings settings,
        HttpClient? http = null)
    {
        _stateStore = stateStore;
        _settings = settings;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public string BuildAuthorizeRedirect(string accountId, string? returnUrl, string publicBaseUrl)
    {
        var realm = _settings.SteamRealm ?? publicBaseUrl.TrimEnd('/');
        var callback = $"{publicBaseUrl.TrimEnd('/')}/api/v1/identity/oauth/steam/callback";
        var state = _stateStore.Create(accountId, returnUrl, TimeSpan.FromMinutes(10));

        var query = new Dictionary<string, string>
        {
            ["openid.ns"] = "http://specs.openid.net/auth/2.0",
            ["openid.mode"] = "checkid_setup",
            ["openid.return_to"] = $"{callback}?state={Uri.EscapeDataString(state)}",
            ["openid.realm"] = realm,
            ["openid.identity"] = "http://specs.openid.net/auth/2.0/identifier_select",
            ["openid.claimed_id"] = "http://specs.openid.net/auth/2.0/identifier_select",
        };

        return SteamOpenIdEndpoint + "?" + string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    public async Task<SteamOpenIdCallbackResult> HandleCallbackAsync(
        IReadOnlyDictionary<string, string> query,
        NlIdentityService identity,
        CancellationToken cancellationToken = default)
    {
        if (!query.TryGetValue("state", out var state) || string.IsNullOrWhiteSpace(state))
        {
            return new SteamOpenIdCallbackResult(false, Error: "Missing OAuth state.");
        }

        var pending = _stateStore.Consume(state);
        if (pending is null)
        {
            return new SteamOpenIdCallbackResult(false, Error: "Invalid or expired OAuth state.");
        }

        var mode = GetOpenIdValue(query, "openid.mode");
        if (!string.Equals(mode, "id_res", StringComparison.OrdinalIgnoreCase))
        {
            return new SteamOpenIdCallbackResult(false, Error: $"Unexpected OpenID mode: {mode ?? "(null)"}");
        }

        if (!await VerifyWithSteamAsync(query, cancellationToken))
        {
            return new SteamOpenIdCallbackResult(false, Error: "Steam OpenID verification failed.");
        }

        var claimedId = GetOpenIdValue(query, "openid.claimed_id");
        var steamId = ExtractSteamId(claimedId);
        if (steamId is null)
        {
            return new SteamOpenIdCallbackResult(false, Error: "Could not parse Steam ID from OpenID response.");
        }

        try
        {
            identity.LinkPlatform(pending.AccountId, NlPlatform.Steam, steamId);
        }
        catch (PlatformLinkConflictException ex)
        {
            return new SteamOpenIdCallbackResult(
                false,
                SteamId: steamId,
                AccountId: pending.AccountId,
                ReturnUrl: pending.ReturnUrl,
                Error: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return new SteamOpenIdCallbackResult(false, Error: ex.Message);
        }

        return new SteamOpenIdCallbackResult(
            true,
            SteamId: steamId,
            AccountId: pending.AccountId,
            ReturnUrl: pending.ReturnUrl);
    }

    public static string? ExtractSteamId(string? claimedId)
    {
        if (string.IsNullOrWhiteSpace(claimedId))
        {
            return null;
        }

        if (claimedId.StartsWith(ClaimedIdPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var id = claimedId[ClaimedIdPrefix.Length..].Trim('/');
            return id.Length > 0 && id.All(char.IsDigit) ? id : null;
        }

        return null;
    }

    private async Task<bool> VerifyWithSteamAsync(
        IReadOnlyDictionary<string, string> query,
        CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>();
        foreach (var (key, value) in query)
        {
            if (key.StartsWith("openid.", StringComparison.OrdinalIgnoreCase))
            {
                form[key] = value;
            }
        }

        form["openid.mode"] = "check_authentication";

        using var content = new FormUrlEncodedContent(form);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        using var response = await _http.PostAsync(SteamOpenIdEndpoint, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return body.Contains("is_valid:true", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetOpenIdValue(IReadOnlyDictionary<string, string> query, string key)
    {
        if (query.TryGetValue(key, out var direct))
        {
            return direct;
        }

        foreach (var kv in query)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return kv.Value;
            }
        }

        return null;
    }
}
