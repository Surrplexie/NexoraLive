using System.Text.Json;
using NL.Core;
using NL.Fork.Catalog;
using NL.Fork.Catalog.Core;
using NL.Fork.Core;
using NL.Fork.Orchestrator.Core;

namespace NL.Fork.Orchestrator;

public sealed class NlForkOrchestratorService
{
    private static readonly JsonSerializerOptions ModJsonOptions = new() { WriteIndented = true };

    private readonly NlForkOrchestratorSettings _settings;
    private readonly IForkSessionStore _store;
    private readonly JsonlForkOrchestratorAuditStore _audit;
    private readonly IReadOnlyDictionary<ForkProvisionerKind, IForkProvisioner> _provisioners;
    private readonly NlForkCatalogHost? _catalog;
    private readonly object _lock = new();

    public NlForkOrchestratorService(
        NlForkOrchestratorSettings settings,
        IForkSessionStore store,
        JsonlForkOrchestratorAuditStore audit,
        IReadOnlyDictionary<ForkProvisionerKind, IForkProvisioner> provisioners,
        NlForkCatalogHost? catalog = null)
    {
        _settings = settings;
        _store = store;
        _audit = audit;
        _provisioners = provisioners;
        _catalog = catalog;
    }

    public ForkOrchestratorSession? GetSession(string sessionId) => _store.Get(sessionId);

    public ForkOrchestratorSession? GetActiveForStreamer(string streamerId) =>
        _store.GetActiveForStreamer(streamerId);

    public IReadOnlyList<ForkOrchestratorSession> ListActive() => _store.ListActive();

    public async Task<ForkOrchestratorCreateResult> CreateSessionAsync(
        CreateForkSessionRequest request,
        string bridgeWebSocketUrl,
        string admitUrl,
        string busToken,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return new ForkOrchestratorCreateResult(false, Error: "Fork orchestrator is disabled.");
        }

        if (string.IsNullOrWhiteSpace(request.StreamerId))
        {
            return new ForkOrchestratorCreateResult(false, Error: "streamerId required.");
        }

        if (string.IsNullOrWhiteSpace(request.NlePath) || !File.Exists(request.NlePath))
        {
            return new ForkOrchestratorCreateResult(false, Error: "Valid nlePath required.");
        }

        lock (_lock)
        {
            var existing = _store.GetActiveForStreamer(request.StreamerId);
            if (existing is not null)
            {
                return new ForkOrchestratorCreateResult(
                    false,
                    Error: $"Streamer already has active fork session '{existing.SessionId}'.");
            }
        }

        if (_catalog?.Settings.Enabled == true)
        {
            var validation = _catalog.Catalog.ValidateSelection(new ForkCatalogSelection(
                request.GameId,
                request.MajorVersion,
                request.ModIds));
            if (!validation.IsValid)
            {
                return new ForkOrchestratorCreateResult(false, Error: validation.Error);
            }
        }

        var sessionId = Guid.NewGuid().ToString("N")[..12];
        var modsJsonPath = WriteModsManifest(sessionId, request.ModIds);
        var layout = ForkSessionWorkspace.Prepare(sessionId, request.NlePath, modsJsonPath);

        var provisionerKind = ResolveProvisionerKind();
        if (!_provisioners.TryGetValue(provisionerKind, out var provisioner))
        {
            return new ForkOrchestratorCreateResult(false, Error: $"Provisioner '{provisionerKind}' not registered.");
        }

        var reserved = request.ReservedPrivilegedSlots > 0
            ? request.ReservedPrivilegedSlots
            : _settings.DefaultReservedPrivilegedSlots;

        var session = new ForkOrchestratorSession(
            sessionId,
            request.StreamerId.Trim(),
            request.GameId.Trim(),
            request.MajorVersion.Trim(),
            ForkSessionState.Pending,
            provisionerKind,
            layout.Root,
            bridgeWebSocketUrl,
            admitUrl,
            ForkConnectEndpoint: "",
            DateTimeOffset.UtcNow,
            DestroyAfterUtc: DateTimeOffset.UtcNow.AddHours(_settings.MaxSessionHours),
            ContainerOrProcessId: null,
            BusToken: busToken,
            ReservedPrivilegedSlots: reserved,
            StreamerQuotaUnits: _settings.StreamerQuotaPlaceholder);

        _store.Save(session);
        _audit.Append("create_pending", session);

        var gameProfile = ForkGameProfiles.Resolve(request.GameId);
        string? catalogDockerImage = null;
        if (_catalog?.Settings.Enabled == true)
        {
            catalogDockerImage = _catalog.Catalog.GetEntry(request.GameId, request.MajorVersion)?.DockerImage;
        }

        var dockerImage = request.DockerImage
            ?? catalogDockerImage
            ?? gameProfile.DockerImage
            ?? _settings.DefaultDockerImage;

        var start = await provisioner.StartAsync(new ForkProvisionerStartRequest(
            sessionId,
            layout.Root,
            bridgeWebSocketUrl,
            admitUrl,
            layout.ModsJsonPath,
            layout.RulesNlePath,
            dockerImage,
            request.GameId), cancellationToken);

        if (!start.Success)
        {
            session = session with
            {
                State = ForkSessionState.Failed,
                LastError = start.Error,
            };
            _store.Save(session);
            _audit.Append("create_failed", session, start.Error);
            ForkSessionWorkspace.Destroy(layout.Root);
            return new ForkOrchestratorCreateResult(false, Error: start.Error);
        }

        session = session with
        {
            State = ForkSessionState.Running,
            ContainerOrProcessId = start.ContainerOrProcessId,
            ForkConnectEndpoint = start.ForkConnectEndpoint ?? "",
        };
        _store.Save(session);
        _audit.Append("create_running", session);

        return new ForkOrchestratorCreateResult(true, session);
    }

    public async Task<ForkOrchestratorDestroyResult> DestroySessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = _store.Get(sessionId);
        if (session is null)
        {
            return new ForkOrchestratorDestroyResult(false, Error: "Session not found.");
        }

        return await DestroySessionCoreAsync(session, cancellationToken);
    }

    public async Task<ForkOrchestratorDestroyResult> ScheduleGraceDestroyForStreamerAsync(
        string streamerId,
        CancellationToken cancellationToken = default)
    {
        var session = _store.GetActiveForStreamer(streamerId);
        if (session is null)
        {
            return new ForkOrchestratorDestroyResult(true);
        }

        if (session.State == ForkSessionState.Stopping && session.GraceDestroyAtUtc is not null)
        {
            return new ForkOrchestratorDestroyResult(true);
        }

        if (_settings.DestroyGraceSeconds <= 0)
        {
            return await DestroySessionCoreAsync(session, cancellationToken);
        }

        var graceAt = DateTimeOffset.UtcNow.AddSeconds(_settings.DestroyGraceSeconds);
        session = session with
        {
            State = ForkSessionState.Stopping,
            GraceDestroyAtUtc = graceAt,
        };
        _store.Save(session);
        _audit.Append("grace_scheduled", session, graceAt.ToString("O"));
        return new ForkOrchestratorDestroyResult(true);
    }

    public async Task TickLifecycleAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var session in _store.ListActive())
        {
            if (session.State == ForkSessionState.Stopping
                && session.GraceDestroyAtUtc is { } grace
                && now >= grace)
            {
                await DestroySessionCoreAsync(session, cancellationToken);
                continue;
            }

            if (session.DestroyAfterUtc is { } max && now >= max)
            {
                _audit.Append("max_duration", session);
                await DestroySessionCoreAsync(session, cancellationToken);
                continue;
            }

            if (_settings.IdleDetectionMinutes > 0 && IsIdle(session))
            {
                _audit.Append("idle_detected", session);
                await ScheduleGraceDestroyForStreamerAsync(session.StreamerId, cancellationToken);
            }
        }
    }

    public ForkProvisionerKind ResolveProvisionerKind()
    {
        return _settings.Mode switch
        {
            NlForkProvisionerMode.Mock => ForkProvisionerKind.Mock,
            NlForkProvisionerMode.Process => ForkProvisionerKind.Process,
            NlForkProvisionerMode.Docker => ForkProvisionerKind.Docker,
            NlForkProvisionerMode.Kubernetes => ForkProvisionerKind.Kubernetes,
            _ => ResolveAutoProvisioner(),
        };
    }

    private ForkProvisionerKind ResolveAutoProvisioner()
    {
        var probe = new ProcessForkProvisioner();
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
        {
            var candidate = Path.Combine(dir, "src", "NL.Fork.Runtime", "bin", "Release", "net8.0", "NL.Fork.Runtime.dll");
            if (File.Exists(candidate))
            {
                return ForkProvisionerKind.Process;
            }

            dir = Directory.GetParent(dir)?.FullName ?? "";
        }

        return ForkProvisionerKind.Mock;
    }

    private async Task<ForkOrchestratorDestroyResult> DestroySessionCoreAsync(
        ForkOrchestratorSession session,
        CancellationToken cancellationToken)
    {
        if (!_provisioners.TryGetValue(session.Provisioner, out var provisioner))
        {
            return new ForkOrchestratorDestroyResult(false, Error: "Provisioner missing.");
        }

        session = session with { State = ForkSessionState.Stopping };
        _store.Save(session);

        try
        {
            await provisioner.StopAsync(session, cancellationToken);
        }
        catch (Exception ex)
        {
            session = session with { LastError = ex.Message };
        }

        ForkSessionWorkspace.Destroy(session.WorkspacePath);

        session = session with { State = ForkSessionState.Destroyed };
        _store.Remove(session.SessionId);
        _audit.Append("destroyed", session);
        return new ForkOrchestratorDestroyResult(true);
    }

    private string WriteModsManifest(string sessionId, IReadOnlyList<string> modIds)
    {
        NlForkOrchestratorPaths.EnsureRoot();
        var path = Path.Combine(NlForkOrchestratorPaths.Root, $"{sessionId}-mods.json");
        ForkModManifest manifest;
        if (_catalog?.Settings.Enabled == true && modIds.Count > 0)
        {
            manifest = _catalog.Catalog.ResolveMods(modIds);
        }
        else
        {
            manifest = new ForkModManifest();
        }

        File.WriteAllText(path, JsonSerializer.Serialize(manifest, ModJsonOptions));
        return path;
    }

    private bool IsIdle(ForkOrchestratorSession session)
    {
        var statusPath = Path.Combine(session.WorkspacePath, "fork-status.json");
        if (!File.Exists(statusPath))
        {
            return session.IdleSinceUtc is null
                ? false
                : DateTimeOffset.UtcNow - session.IdleSinceUtc >= TimeSpan.FromMinutes(_settings.IdleDetectionMinutes);
        }

        try
        {
            var json = File.ReadAllText(statusPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("connected", out var connected) && connected.GetBoolean())
            {
                if (session.IdleSinceUtc is not null)
                {
                    _store.Save(session with { IdleSinceUtc = null });
                }

                return false;
            }
        }
        catch
        {
            // treat as idle signal missing
        }

        var idleSince = session.IdleSinceUtc ?? DateTimeOffset.UtcNow;
        if (session.IdleSinceUtc is null)
        {
            _store.Save(session with { IdleSinceUtc = idleSince });
        }

        return DateTimeOffset.UtcNow - idleSince >= TimeSpan.FromMinutes(_settings.IdleDetectionMinutes);
    }
}
