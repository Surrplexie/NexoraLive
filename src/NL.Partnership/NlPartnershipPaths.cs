namespace NL.Partnership;

public static class NlPartnershipPaths
{
    public static string Root
    {
        get
        {
            var overrideRoot = Environment.GetEnvironmentVariable("NL_PARTNERSHIP_ROOT");
            if (!string.IsNullOrWhiteSpace(overrideRoot))
            {
                return Path.GetFullPath(overrideRoot);
            }

            return Path.Combine(NL.Core.NlPaths.Root, "partnership");
        }
    }

    public static string Acknowledgments =>
        Environment.GetEnvironmentVariable("NL_PARTNERSHIP_ACK_STORE")
        ?? Path.Combine(Root, "at-own-risk-acks.json");

    public static string Publishers =>
        Environment.GetEnvironmentVariable("NL_PARTNERSHIP_PUBLISHERS")
        ?? Path.Combine(Root, "publishers.json");

    public static string PlatformOptIn =>
        Environment.GetEnvironmentVariable("NL_PARTNERSHIP_PLATFORM_OPTIN")
        ?? Path.Combine(Root, "platform-opt-in.json");

    public static string Bans =>
        Environment.GetEnvironmentVariable("NL_PARTNERSHIP_BANS")
        ?? Path.Combine(Root, "publisher-bans.json");

    public static string Metrics =>
        Path.Combine(Root, "session-metrics.json");

    public static string Audit =>
        Path.Combine(Root, "partnership-audit.jsonl");

    public static void EnsureRoot()
    {
        Directory.CreateDirectory(Root);
    }
}
