using System.Security.Cryptography;
using System.Text;
using NL.Fork.Core;
using NL.Fork.Catalog.Core;

namespace NL.Fork.Catalog;

/// <summary>
/// Resolves streamer-attached mod ids from the verified hub into a <see cref="ForkModManifest"/>.
/// </summary>
public sealed class ForkModSlotResolver
{
    private readonly IForkCatalogRepository _repository;

    public ForkModSlotResolver(IForkCatalogRepository repository) => _repository = repository;

    public ForkModManifest Resolve(IReadOnlyList<string> modIds)
    {
        var manifest = _repository.Load();
        var entries = new List<ForkModEntry>();

        foreach (var modId in modIds)
        {
            var hub = manifest.ModHub.FirstOrDefault(m =>
                string.Equals(m.Id, modId, StringComparison.OrdinalIgnoreCase));
            if (hub is null)
            {
                throw new InvalidOperationException($"Mod '{modId}' is not in the verified mod hub.");
            }

            entries.Add(new ForkModEntry
            {
                Id = hub.Id,
                Description = hub.Description,
                Sha256 = hub.Sha256,
                Props = hub.Props is null
                    ? new Dictionary<string, double>()
                    : new Dictionary<string, double>(hub.Props),
            });
        }

        return new ForkModManifest { Mods = entries };
    }

    public static bool VerifySha256(ModHubEntry hub, string? contentUtf8)
    {
        if (string.IsNullOrWhiteSpace(contentUtf8))
        {
            return false;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contentUtf8)));
        return string.Equals(hash, hub.Sha256, StringComparison.OrdinalIgnoreCase);
    }
}
