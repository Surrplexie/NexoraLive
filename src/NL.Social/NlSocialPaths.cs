namespace NL.Social;

public static class NlSocialPaths
{
    public static string Root
    {
        get
        {
            var overrideRoot = Environment.GetEnvironmentVariable("NL_SOCIAL_ROOT");
            if (!string.IsNullOrWhiteSpace(overrideRoot))
            {
                return Path.GetFullPath(overrideRoot);
            }

            return Path.Combine(NL.Core.NlPaths.Root, "social");
        }
    }

    public static string StreamerConfig => Path.Combine(Root, "streamer-social.json");

    public static string SpLinks => Path.Combine(Root, "sp-social-links.json");

    public static string MockData =>
        Environment.GetEnvironmentVariable("NL_SOCIAL_MOCK_DATA")
        ?? Path.Combine(Root, "mock-social.json");

    public static string OAuthState => Path.Combine(Root, "oauth-state.json");

    public static string TwitchCredentials => Path.Combine(Root, "twitch-oauth-credentials.json");

    public static string DiscordCredentials => Path.Combine(Root, "discord-oauth-credentials.json");

    public static void EnsureRoot() => Directory.CreateDirectory(Root);
}
