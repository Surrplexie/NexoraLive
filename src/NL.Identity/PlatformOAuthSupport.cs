using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using NL.Identity.Core;

namespace NL.Identity;

internal static class PlatformOAuthEnv
{
    public static string? Get(string name) => Environment.GetEnvironmentVariable(name);

    public static bool HasPair(string idName, string secretName) =>
        !string.IsNullOrWhiteSpace(Get(idName)) && !string.IsNullOrWhiteSpace(Get(secretName));
}

internal static class PlatformOAuthHttp
{
    public static async Task<(string? AccessToken, string? RefreshToken, int ExpiresIn)> ExchangeCodeAsync(
        HttpClient http,
        string tokenEndpoint,
        Dictionary<string, string> form,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(form);
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}")));

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return (null, null, 0);
        }

        var body = await response.Content.ReadFromJsonAsync<GenericTokenResponse>(cancellationToken);
        return (body?.AccessToken, body?.RefreshToken, body?.ExpiresIn ?? 0);
    }

    public static async Task<(string? AccessToken, string? RefreshToken, int ExpiresIn)> ExchangeCodePublicAsync(
        HttpClient http,
        string tokenEndpoint,
        Dictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await http.PostAsync(tokenEndpoint, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return (null, null, 0);
        }

        var body = await response.Content.ReadFromJsonAsync<GenericTokenResponse>(cancellationToken);
        return (body?.AccessToken, body?.RefreshToken, body?.ExpiresIn ?? 0);
    }

    public static async Task<string?> ClientCredentialsTokenAsync(
        HttpClient http,
        string tokenEndpoint,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}")));

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadFromJsonAsync<GenericTokenResponse>(cancellationToken);
        return body?.AccessToken;
    }

    private sealed class GenericTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}

internal static class PlatformOAuthPersistence
{
    public static PlatformOAuthCallbackResult SaveLink(
        NlIdentityService identity,
        JsonPlatformOAuthCredentialStore credentials,
        NlTokenProtector protector,
        string accountId,
        NlPlatform platform,
        string externalUserId,
        string? displayName,
        string refreshToken,
        string accessToken,
        int expiresIn,
        string? metadataJson,
        string? returnUrl)
    {
        try
        {
            identity.LinkPlatform(accountId, platform, externalUserId);
            credentials.Save(new PlatformOAuthCredential(
                accountId,
                platform,
                externalUserId,
                displayName,
                protector.Protect(refreshToken),
                protector.Protect(accessToken),
                DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expiresIn - 60)),
                metadataJson));

            return new PlatformOAuthCallbackResult(
                true,
                platform,
                accountId,
                externalUserId,
                displayName,
                returnUrl);
        }
        catch (PlatformLinkConflictException ex)
        {
            return new PlatformOAuthCallbackResult(
                false,
                platform,
                accountId,
                externalUserId,
                ReturnUrl: returnUrl,
                Error: ex.Message);
        }
        catch (Exception ex)
        {
            return new PlatformOAuthCallbackResult(false, platform, accountId, ReturnUrl: returnUrl, Error: ex.Message);
        }
    }
}
