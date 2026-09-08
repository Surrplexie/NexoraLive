using System.Text.Json;
using System.Text.Json.Serialization;

namespace NL.Fork.Core;

/// <summary>JSON shape for <c>adapter.manifest.json</c> (Phase T0).</summary>
public sealed class GameForkAdapterManifestDto
{
    [JsonPropertyName("gameId")]
    public string GameId { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("majorVersion")]
    public string MajorVersion { get; set; } = "1.0";

    [JsonPropertyName("tier")]
    public string Tier { get; set; } = "AtOwnRisk";

    [JsonPropertyName("connectScheme")]
    public string ConnectScheme { get; set; } = "";

    [JsonPropertyName("playerConnectPort")]
    public int? PlayerConnectPort { get; set; }

    [JsonPropertyName("dockerImage")]
    public string DockerImage { get; set; } = "";

    [JsonPropertyName("dockerfile")]
    public string Dockerfile { get; set; } = "";

    [JsonPropertyName("defaultNleTemplate")]
    public string DefaultNleTemplate { get; set; } = "";

    [JsonPropertyName("integrationDir")]
    public string IntegrationDir { get; set; } = "";

    [JsonPropertyName("dogfoodScript")]
    public string DogfoodScript { get; set; } = "scripts/nl-dogfood-flow.ps1";

    [JsonPropertyName("buildImageKey")]
    public string BuildImageKey { get; set; } = "";

    [JsonPropertyName("requiredEvents")]
    public List<string> RequiredEvents { get; set; } = new();

    [JsonPropertyName("requiredActions")]
    public List<string> RequiredActions { get; set; } = new();

    [JsonPropertyName("steamAppIds")]
    public List<string> SteamAppIds { get; set; } = new();

    [JsonPropertyName("health")]
    public GameForkHealthProbeDto? Health { get; set; }

    [JsonPropertyName("catalogSnippet")]
    public GameForkCatalogSnippetDto? CatalogSnippet { get; set; }

    [JsonPropertyName("minClientVersion")]
    public string? MinClientVersion { get; set; }

    [JsonPropertyName("noProgressTransfer")]
    public bool NoProgressTransfer { get; set; } = true;

    /// <summary>Optional native plugin / Harmony mod folder (Paper plugin, RimWorld About.xml, …).</summary>
    [JsonPropertyName("nativePlugin")]
    public string? NativePlugin { get; set; }
}

public sealed class GameForkHealthProbeDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "none";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("readyField")]
    public string? ReadyField { get; set; }

    [JsonPropertyName("readyHttpStatus")]
    public int? ReadyHttpStatus { get; set; }
}

public sealed class GameForkCatalogSnippetDto
{
    [JsonPropertyName("imageDigest")]
    public string ImageDigest { get; set; } = "sha256:replace-me";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Active";
}

/// <summary><see cref="IGameForkAdapter"/> backed by an <c>adapter.manifest.json</c>.</summary>
public sealed class ManifestGameForkAdapter : IGameForkAdapter
{
    private readonly GameForkAdapterManifestDto _dto;
    private readonly GameForkHealthProbe _health;

    public ManifestGameForkAdapter(GameForkAdapterManifestDto dto)
    {
        _dto = dto ?? throw new ArgumentNullException(nameof(dto));
        _health = dto.Health is null
            ? GameForkHealthProbe.None
            : new GameForkHealthProbe(
                dto.Health.Type,
                dto.Health.Path,
                dto.Health.ReadyField,
                dto.Health.ReadyHttpStatus);
    }

    public string GameId => _dto.GameId;
    public string DisplayName => _dto.DisplayName;
    public string MajorVersion => _dto.MajorVersion;
    public string ConnectScheme => string.IsNullOrWhiteSpace(_dto.ConnectScheme) ? _dto.GameId : _dto.ConnectScheme;
    public int? PlayerConnectPort => _dto.PlayerConnectPort;
    public string DockerImage => _dto.DockerImage;
    public string Dockerfile => _dto.Dockerfile;
    public string DefaultNleTemplate => _dto.DefaultNleTemplate;
    public string IntegrationDir => _dto.IntegrationDir;
    public string DogfoodScript => _dto.DogfoodScript;
    public string BuildImageKey => string.IsNullOrWhiteSpace(_dto.BuildImageKey) ? _dto.GameId : _dto.BuildImageKey;
    public IReadOnlyList<string> RequiredEvents => _dto.RequiredEvents;
    public IReadOnlyList<string> RequiredActions => _dto.RequiredActions;
    public GameForkHealthProbe Health => _health;
    public IReadOnlyList<string> SteamAppIds => _dto.SteamAppIds;
    public GameForkAdapterManifestDto Dto => _dto;

    public Task<GameForkHealthResult> ProbeHealthAsync(
        GameForkHealthContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GameForkHealth.Evaluate(_health, context));
    }

    public ForkGameProfile ToForkGameProfile()
    {
        var kind = ForkGameProfiles.ParseGameArg(GameId);
        return new ForkGameProfile(
            kind,
            DockerImage,
            NormalizeNleForProfile(DefaultNleTemplate),
            PlayerConnectPort,
            ConnectScheme);
    }

    /// <summary>Catalog / profile paths historically omit the <c>samples/</c> prefix.</summary>
    public static string NormalizeNleForProfile(string path)
    {
        var p = path.Replace('\\', '/').TrimStart('/');
        const string samples = "samples/";
        if (p.StartsWith(samples, StringComparison.OrdinalIgnoreCase))
        {
            p = p[samples.Length..];
        }

        return p;
    }
}

/// <summary>Load and discover game fork adapter manifests.</summary>
public static class GameForkAdapterManifest
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public static ManifestGameForkAdapter Parse(string json)
    {
        var dto = JsonSerializer.Deserialize<GameForkAdapterManifestDto>(json, JsonOptions)
                  ?? throw new InvalidOperationException("adapter manifest JSON was empty");
        ValidateRequired(dto);
        return new ManifestGameForkAdapter(dto);
    }

    public static ManifestGameForkAdapter LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return Parse(json);
    }

    public static void ValidateRequired(GameForkAdapterManifestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.GameId))
        {
            throw new InvalidOperationException("adapter.manifest.json missing gameId");
        }

        if (string.IsNullOrWhiteSpace(dto.DisplayName))
        {
            throw new InvalidOperationException($"{dto.GameId}: missing displayName");
        }

        if (string.IsNullOrWhiteSpace(dto.DockerImage))
        {
            throw new InvalidOperationException($"{dto.GameId}: missing dockerImage");
        }

        if (string.IsNullOrWhiteSpace(dto.Dockerfile))
        {
            throw new InvalidOperationException($"{dto.GameId}: missing dockerfile");
        }

        if (string.IsNullOrWhiteSpace(dto.DefaultNleTemplate))
        {
            throw new InvalidOperationException($"{dto.GameId}: missing defaultNleTemplate");
        }

        if (string.IsNullOrWhiteSpace(dto.IntegrationDir))
        {
            throw new InvalidOperationException($"{dto.GameId}: missing integrationDir");
        }

        if (dto.RequiredEvents.Count == 0)
        {
            throw new InvalidOperationException($"{dto.GameId}: requiredEvents must be non-empty");
        }

        if (dto.RequiredActions.Count == 0)
        {
            throw new InvalidOperationException($"{dto.GameId}: requiredActions must be non-empty");
        }

        foreach (var action in dto.RequiredActions)
        {
            if (action is not ("warn" or "kick" or "tell" or "recover" or "mute" or "despawn" or "custom"))
            {
                throw new InvalidOperationException(
                    $"{dto.GameId}: requiredActions entry '{action}' is not a standard NL verb");
            }
        }
    }

    /// <summary>
    /// Discover <c>integrations/*/adapter.manifest.json</c> (skips <c>_template</c>).
    /// </summary>
    public static IReadOnlyList<ManifestGameForkAdapter> Discover(string repoRoot)
    {
        var integrations = Path.Combine(repoRoot, "integrations");
        if (!Directory.Exists(integrations))
        {
            return Array.Empty<ManifestGameForkAdapter>();
        }

        var list = new List<ManifestGameForkAdapter>();
        foreach (var dir in Directory.EnumerateDirectories(integrations))
        {
            var name = Path.GetFileName(dir);
            if (name.StartsWith('_') || string.Equals(name, "generic", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Skip language bridge folders that are not per-game adapters.
            if (name is "python" or "nodejs" or "lua" or "dotnet" or "unity" or "unreal" or "godot" or "rust" or "fork")
            {
                continue;
            }

            var manifestPath = Path.Combine(dir, "adapter.manifest.json");
            if (!File.Exists(manifestPath))
            {
                // Nested e.g. integrations/minecraft/paper — look one level down for manifest at game root only.
                continue;
            }

            list.Add(LoadFromFile(manifestPath));
        }

        return list;
    }
}

/// <summary>In-memory registry of discovered <see cref="IGameForkAdapter"/> instances.</summary>
public sealed class GameForkAdapterRegistry
{
    private readonly Dictionary<string, IGameForkAdapter> _byId;

    public GameForkAdapterRegistry(IEnumerable<IGameForkAdapter> adapters)
    {
        _byId = new Dictionary<string, IGameForkAdapter>(StringComparer.OrdinalIgnoreCase);
        foreach (var adapter in adapters)
        {
            _byId[adapter.GameId] = adapter;
        }
    }

    public static GameForkAdapterRegistry FromRepo(string repoRoot) =>
        new(GameForkAdapterManifest.Discover(repoRoot));

    public IReadOnlyCollection<IGameForkAdapter> All => _byId.Values;

    public bool TryGet(string gameId, out IGameForkAdapter adapter) =>
        _byId.TryGetValue(gameId.Trim(), out adapter!);

    public IGameForkAdapter GetRequired(string gameId) =>
        TryGet(gameId, out var adapter)
            ? adapter
            : throw new KeyNotFoundException($"No game fork adapter registered for '{gameId}'");
}
