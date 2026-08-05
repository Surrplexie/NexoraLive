namespace NL.Fleet;

public static class NlFleetPaths
{
    public static string Root
    {
        get
        {
            var overrideRoot = Environment.GetEnvironmentVariable("NL_FLEET_ROOT");
            if (!string.IsNullOrWhiteSpace(overrideRoot))
            {
                return Path.GetFullPath(overrideRoot);
            }

            return Path.Combine(NL.Core.NlPaths.Root, "fleet");
        }
    }

    public static string Metrics => Path.Combine(Root, "metrics.json");

    public static string Incidents => Path.Combine(Root, "incidents.jsonl");

    public static string StreamerRequirements => Path.Combine(Root, "streamer-requirements.json");

    public static string ComplianceExports => Path.Combine(Root, "compliance-exports");

    public static string ValidationReport => Path.Combine(Root, "validation-last.json");

    public static string BetaWaitlist => Path.Combine(Root, "beta-waitlist.json");

    public static string GaStreamers => Path.Combine(Root, "ga-streamers.json");

    public static string LoadTestReport => Path.Combine(Root, "load-test-last.json");

    public static string LegalComplianceAudit => Path.Combine(Root, "legal-compliance-audit.json");

    public static string PublicGaLaunchSignoff => Path.Combine(Root, "public-ga-launch-signoff.json");

    public static void EnsureRoot()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ComplianceExports);
    }
}
