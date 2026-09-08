namespace NL.Social.Core;

public enum NlSocialPlatform
{
    Twitch,
    YouTube,
    Kick,
    Discord,
}

public static class NlSocialPlatformNames
{
    public static bool TryParse(string? raw, out NlSocialPlatform platform)
    {
        platform = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "twitch" => Assign(NlSocialPlatform.Twitch, out platform),
            "youtube" => Assign(NlSocialPlatform.YouTube, out platform),
            "kick" => Assign(NlSocialPlatform.Kick, out platform),
            "discord" => Assign(NlSocialPlatform.Discord, out platform),
            _ => false,
        };
    }

    private static bool Assign(NlSocialPlatform value, out NlSocialPlatform platform)
    {
        platform = value;
        return true;
    }
}
