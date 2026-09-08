using System.Text.Json;
using NL.Core;

namespace NL.Fork.Core;

/// <summary>Server-side mod manifest — mods run on fork only; SP clients unchanged.</summary>
public sealed class ForkModManifest
{
    public List<ForkModEntry> Mods { get; init; } = [];
}

public sealed class ForkModEntry
{
    public required string Id { get; init; }

    public string? Description { get; init; }

    /// <summary>Optional SHA-256 of mod manifest bytes for hub verification (Phase N).</summary>
    public string? Sha256 { get; init; }

    /// <summary>Numeric prop overrides applied when building session events.</summary>
    public Dictionary<string, double> Props { get; init; } = new();
}

/// <summary>Loads verified server-side mods from JSON (hash check optional).</summary>
public static class ForkModLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static ForkModManifest LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            return new ForkModManifest();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ForkModManifest>(json, JsonOptions) ?? new ForkModManifest();
    }

    public static ForkModManifest LoadFromJson(string json) =>
        JsonSerializer.Deserialize<ForkModManifest>(json, JsonOptions) ?? new ForkModManifest();

    /// <summary>Merges mod prop overrides into event props (server-side only).</summary>
    public static Dictionary<string, double> ApplyMods(
        ForkModManifest manifest,
        Dictionary<string, double> baseProps)
    {
        var merged = new Dictionary<string, double>(baseProps, StringComparer.Ordinal);
        foreach (var mod in manifest.Mods)
        {
            foreach (var (key, value) in mod.Props)
            {
                if (merged.TryGetValue(key, out var existing) && key.Contains("Multiplier", StringComparison.OrdinalIgnoreCase))
                {
                    merged[key] = existing * value;
                }
                else
                {
                    merged[key] = value;
                }
            }
        }

        return merged;
    }
}
