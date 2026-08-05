using System.Security.Cryptography;
using System.Text.Json;
using NL.Fleet.Core;

namespace NL.Fleet;

/// <summary>Phase 11 — NL Client download manifest + deep link metadata for auto-update.</summary>
public sealed class DistributionClientManifestService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DistributionClientManifestPublic Build(
        NlDistributionSettings settings,
        string? publicBaseUrl,
        string wwwrootPath)
    {
        var baseUrl = (publicBaseUrl ?? "http://127.0.0.1:27020").TrimEnd('/');
        var winPath = settings.WinPackageRelativePath.TrimStart('/');
        var fullPath = Path.Combine(wwwrootPath, winPath.Replace('/', Path.DirectorySeparatorChar));
        var exists = File.Exists(fullPath);
        string? sha256 = null;
        if (exists)
        {
            sha256 = ComputeSha256(fullPath);
        }

        var sidecar = Path.Combine(Path.GetDirectoryName(fullPath)!, "nl-client-manifest.json");
        if (File.Exists(sidecar))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(sidecar));
                if (doc.RootElement.TryGetProperty("sha256", out var hashProp))
                {
                    sha256 = hashProp.GetString() ?? sha256;
                }
            }
            catch
            {
                // ignore corrupt sidecar
            }
        }

        var releases = new List<DistributionClientRelease>
        {
            new(
                "win-x64",
                $"{baseUrl}/{winPath}",
                sha256,
                exists),
        };

        return new DistributionClientManifestPublic(
            settings.ClientVersion,
            settings.DeepLinkScheme,
            $"{settings.DeepLinkScheme}://join?streamer={{streamerId}}&game={{gameId}}&major={{majorVersion}}",
            $"{baseUrl}/nl-client.html",
            $"{baseUrl}/ga.html",
            $"{baseUrl}/fork-catalog.html",
            releases,
            DateTimeOffset.UtcNow);
    }

    public DistributionOnboardingPaths BuildOnboardingPaths(NlGaSettings ga) => new(
        LandingPageEnabled: true,
        DownloadPageEnabled: true,
        WebClientEnabled: true,
        StreamerSignupEnabled: ga.Enabled && ga.OpenSignup,
        IdentityLinkEnabled: true,
        CatalogBrowserEnabled: true);

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
