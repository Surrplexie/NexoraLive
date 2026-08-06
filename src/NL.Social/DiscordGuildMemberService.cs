using System.Net.Http.Headers;
using NL.Social.Core;

namespace NL.Social;

/// <summary>Live Discord guild membership check via player OAuth token (Phase M.2).</summary>
public sealed class DiscordGuildMemberService : IDiscordGuildMembershipChecker
{
    private readonly DiscordOAuthTokenService _tokenService;
    private readonly HttpClient _http;

    public DiscordGuildMemberService(
        DiscordOAuthTokenService tokenService,
        HttpClient? http = null)
    {
        _tokenService = tokenService;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISCORD_CLIENT_ID"))
        && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISCORD_CLIENT_SECRET"));

    /// <summary>Returns membership when live check ran; null when check could not run.</summary>
    public async Task<bool?> TryGetMembershipAsync(
        SocialGateContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.RequireDiscordMember)
        {
            return null;
        }

        var guildId = context.StreamerConfig.DiscordGuildId;
        if (string.IsNullOrWhiteSpace(guildId)
            || string.IsNullOrWhiteSpace(context.Links.DiscordUserId))
        {
            return null;
        }

        var accessToken = await _tokenService.GetValidAccessTokenAsync(context.PlayerId, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://discord.com/api/users/@me/guilds/{Uri.EscapeDataString(guildId.Trim())}/member");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _http.SendAsync(request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Merges Twitch Helix checks with live Discord guild membership.</summary>
public sealed class LiveSocialRelationshipProvider : ISocialRelationshipProvider
{
    private readonly ISocialRelationshipProvider _twitch;
    private readonly IDiscordGuildMembershipChecker _discord;

    public LiveSocialRelationshipProvider(
        ISocialRelationshipProvider twitch,
        IDiscordGuildMembershipChecker discord)
    {
        _twitch = twitch;
        _discord = discord;
    }

    public async Task<SocialRelationshipStatus> GetStatusAsync(
        SocialGateContext context,
        CancellationToken cancellationToken = default)
    {
        var status = await _twitch.GetStatusAsync(context, cancellationToken);
        var discordMember = await _discord.TryGetMembershipAsync(context, cancellationToken);
        if (discordMember is not bool isMember)
        {
            return status;
        }

        var source = status.Source.Contains("discord", StringComparison.OrdinalIgnoreCase)
            ? status.Source
            : string.IsNullOrWhiteSpace(status.Source) || status.Source == "unknown"
                ? "discord-oauth"
                : status.Source + "+discord-oauth";

        return status with { IsDiscordMember = isMember, Source = source };
    }
}
