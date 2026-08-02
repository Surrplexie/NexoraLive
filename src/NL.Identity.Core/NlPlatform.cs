namespace NL.Identity.Core;

public enum NlPlatform
{
    Steam,
    Epic,
    Ubisoft,
    Ea,
    Xbox,
    PlayStation,
    Riot,
    Itch,
}

public static class NlPlatformNames
{
    public static bool TryParse(string? value, out NlPlatform platform)
    {
        platform = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out platform);
    }

    public static string Normalize(NlPlatform platform) => platform.ToString().ToLowerInvariant();

    public static string LinkKey(NlPlatform platform, string externalUserId) =>
        $"{Normalize(platform)}:{externalUserId.Trim()}";
}
