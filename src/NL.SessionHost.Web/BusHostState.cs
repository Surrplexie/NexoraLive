using NL.Core;
using NL.Server;
using NL.Server.Core.Integration;
using NL.Server.Core.Security;

namespace NL.SessionHost.Web;

public sealed class BusHostState
{
    private readonly object _lock = new();
    private SessionProfileFile _profile = new();
    private Task? _backgroundRun;

    public BusHostState(string bindHost, int httpPort, int wsPort, string busToken, int modPort = NlSessionServerDefaults.ModerationPort)
    {
        BindHost = bindHost;
        HttpPort = httpPort;
        WsPort = wsPort;
        ModPort = modPort;
        BusToken = busToken;
        SessionId = Guid.NewGuid().ToString("N")[..12];
        BusInfo = NlSessionBusHelper.CreateBusInfo(bindHost, httpPort, wsPort, busToken, SessionId);
        Sessions = new SessionHostService();
        Sessions.LogAppended += _ => { };
    }

    public string BindHost { get; }
    public int HttpPort { get; }
    public int WsPort { get; }
    public int ModPort { get; }
    public string BusToken { get; }
    public string SessionId { get; }
    public NlSessionBusInfo BusInfo { get; }
    public SessionHostService Sessions { get; }

    public SessionProfileFile GetProfile()
    {
        lock (_lock)
        {
            return CloneProfile(_profile);
        }
    }

    public void SaveProfile(SessionProfileFile profile)
    {
        lock (_lock)
        {
            _profile = CloneProfile(profile);
            NlSessionRunner.SaveProfile(NlPaths.SessionProfile, _profile);
        }
    }

    public void LoadBusDefaults(string? configPath = null)
    {
        var profile = GetProfile();
        NlSessionBusHelper.ApplyBusSource(profile, BusInfo);
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            profile.ConfigPath = configPath;
        }
        else if (string.IsNullOrWhiteSpace(profile.ConfigPath))
        {
            profile.ConfigPath = ResolveSampleConfig("generic.nle");
        }

        profile.AntiCheat = true;
        profile.JoinGate = false;
        SaveProfile(profile);
    }

    /// <summary>Phase G — preloaded profile for the public demo loop.</summary>
    public void ApplyDemoProfile(string configFileName = "demo.nle")
    {
        var profile = new SessionProfileFile
        {
            StreamerId = NlPaths.DefaultStreamerId,
            Game = "generic",
            ConfigPath = ResolveSampleConfig(configFileName),
            AntiCheat = false,
            JoinGate = false,
            AnomalyAutoMod = false,
            UseDefaultDataPaths = true,
            UseSessionBus = true,
        };
        NlSessionBusHelper.ApplyBusSource(profile, BusInfo);
        SaveProfile(profile);
    }

    public async Task WaitForIdleAsync(CancellationToken cancellationToken = default)
    {
        while (Sessions.IsRunning)
        {
            await Task.Delay(100, cancellationToken);
        }
    }

    public async Task<IResult> StartAsync(bool replayOnce, CancellationToken cancellationToken)
    {
        if (Sessions.IsRunning)
        {
            return Results.Conflict(new { error = "Session already running." });
        }

        var profile = GetProfile();
        if (string.IsNullOrWhiteSpace(profile.ConfigPath) || !File.Exists(profile.ConfigPath))
        {
            return Results.BadRequest(new { error = "Config (.nle) path missing or not found." });
        }

        if (profile.UseSessionBus || string.IsNullOrWhiteSpace(profile.SourcePath))
        {
            NlSessionBusHelper.ApplyBusSource(profile, BusInfo);
            SaveProfile(profile);
        }

        if (!NlSessionBusHelper.IsNetworkSource(profile.SourcePath) && !replayOnce && !File.Exists(profile.SourcePath))
        {
            try
            {
                var dir = Path.GetDirectoryName(profile.SourcePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                await File.WriteAllTextAsync(profile.SourcePath, "", cancellationToken);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = $"Could not create source file: {ex.Message}" });
            }
        }

        var options = profile.ToSessionOptions(replay: replayOnce);

        _backgroundRun = Task.Run(async () =>
        {
            try
            {
                await Sessions.StartAsync(options, cancellationToken);
            }
            catch
            {
                // logged via SessionHostService
            }
        }, CancellationToken.None);

        await Task.Delay(50, cancellationToken);
        return Results.Ok(new { state = Sessions.State.ToString(), bus = BusInfo });
    }

    public IResult Stop()
    {
        Sessions.Stop();
        return Results.Ok(new { state = Sessions.State.ToString() });
    }

    public object GetStatus(bool includeSecrets = true) => new
    {
        state = Sessions.State.ToString(),
        decisions = Sessions.DecisionCount,
        bus = includeSecrets ? BusInfo : NlSecurityRedaction.RedactStatusBus(BusInfo, includeSecrets: false),
        manifest = NlSecurityRedaction.RedactManifest(GetManifest(), includeSecrets),
        profile = includeSecrets ? GetProfile() : RedactProfileForPublic(GetProfile()),
        log = includeSecrets ? Sessions.GetLogSnapshot() : Array.Empty<string>(),
    };

    private static object RedactProfileForPublic(SessionProfileFile p) => new
    {
        streamerId = p.StreamerId,
        game = p.Game,
        joinGate = p.JoinGate,
        antiCheat = p.AntiCheat,
        anomalyAutoMod = p.AnomalyAutoMod,
        useSessionBus = p.UseSessionBus,
    };

    public NlSessionManifest GetManifest()
    {
        var profile = GetProfile();
        return NlSessionServerHelper.CreateManifest(
            BusInfo, profile, BindHost, HttpPort, WsPort, ModPort, Sessions.IsRunning);
    }

    public NlJoinAdmissionResult Admit(NlAdmitPlayerRequest request)
    {
        var profile = GetProfile();
        var streamerId = string.IsNullOrWhiteSpace(request.StreamerId)
            ? profile.StreamerId
            : request.StreamerId.Trim();
        var admission = NlJoinAdmissionService.CreateDefault(streamerId);
        return admission.Evaluate(request.PlayerId, request.DisplayName);
    }

    private static SessionProfileFile CloneProfile(SessionProfileFile p) => new()
    {
        StreamerId = p.StreamerId,
        Game = p.Game,
        ConfigPath = p.ConfigPath,
        SourcePath = p.SourcePath,
        RconEndpoint = p.RconEndpoint,
        BeamngCommandEndpoint = p.BeamngCommandEndpoint,
        NlActionEndpoint = p.NlActionEndpoint,
        UseSessionBus = p.UseSessionBus,
        BusToken = p.BusToken,
        AntiCheat = p.AntiCheat,
        JoinGate = p.JoinGate,
        AnomalyAutoMod = p.AnomalyAutoMod,
        UseDefaultDataPaths = p.UseDefaultDataPaths,
    };

    private static string ResolveSampleConfig(string name)
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples", "configs", name)),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "samples", "configs", name)),
        };

        return candidates.FirstOrDefault(File.Exists) ?? name;
    }
}
