using NL.Core;
using NL.Fork.Catalog;
using NL.Fork.Catalog.Core;
using NL.Fork.Orchestrator;
using NL.Fork.Orchestrator.Core;
using NL.Fleet;
using NL.Fleet.Core;
using NL.Partnership;
using NL.Identity;
using NL.Server;
using NL.Server.Core.Integration;
using NL.Server.Core.Security;
using NL.Social;
using NL.Social.Core;

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

    public async Task<IResult> StartAsync(
        bool replayOnce,
        CancellationToken cancellationToken,
        NlSocialHost? social = null,
        NlForkCatalogHost? catalog = null,
        NlForkOrchestratorHost? orchestrator = null,
        NlFleetHost? fleet = null)
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

        if (profile.RequireLiveStream && social?.Settings.Enabled == true)
        {
            var streamerId = string.IsNullOrWhiteSpace(profile.StreamerId)
                ? NlPaths.DefaultStreamerId
                : profile.StreamerId;
            var config = social.Gate.GetStreamerConfig(streamerId);
            var live = await social.LiveMonitor.GetStatusAsync(config, cancellationToken);
            if (!live.IsLive)
            {
                return Results.BadRequest(new
                {
                    error = "Session cannot start until the streamer is live on a connected channel.",
                    liveStatus = live,
                });
            }
        }

        if (profile.CatalogEnforced && catalog?.Settings.Enabled == true)
        {
            var gameId = profile.GameId ?? profile.Game;
            var major = profile.GameMajorVersion;
            if (string.IsNullOrWhiteSpace(major))
            {
                var latest = catalog.Catalog.ResolveLatestStableEntry(gameId);
                if (latest is null)
                {
                    return Results.BadRequest(new { error = "Catalog-enforced session requires a catalog game with an active stable major." });
                }

                major = latest.MajorVersion;
                profile.GameMajorVersion = major;
                SaveProfile(profile);
            }

            var validation = catalog.Catalog.ValidateSelection(new ForkCatalogSelection(
                gameId,
                major,
                profile.AttachedModIds));
            if (!validation.IsValid)
            {
                return Results.BadRequest(new { error = validation.Error });
            }
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

        if (profile.ForkOrchestratorEnabled && orchestrator?.Settings.Enabled == true)
        {
            var forkResult = await ProvisionForkSessionAsync(profile, orchestrator, fleet, twitchFollowers: null, cancellationToken);
            if (!forkResult.Success)
            {
                Sessions.Stop();
                return Results.BadRequest(new { error = forkResult.Error ?? "Fork orchestrator create failed." });
            }

            profile.ForkSessionId = forkResult.SessionId;
            profile.FleetPlacedRegionId = forkResult.RegionId;
            SaveProfile(profile);
        }

        return Results.Ok(new { state = Sessions.State.ToString(), bus = BusInfo, forkSessionId = profile.ForkSessionId });
    }

    public IResult Stop(NlForkOrchestratorHost? orchestrator = null)
    {
        Sessions.Stop();
        if (orchestrator?.Settings.Enabled == true)
        {
            var profile = GetProfile();
            if (profile.ForkOrchestratorEnabled)
            {
                _ = orchestrator.Orchestrator.ScheduleGraceDestroyForStreamerAsync(profile.StreamerId);
            }
        }

        return Results.Ok(new { state = Sessions.State.ToString() });
    }

    public object GetStatus(bool includeSecrets = true, NlForkOrchestratorHost? orchestrator = null, NlFleetHost? fleet = null) => new
    {
        state = Sessions.State.ToString(),
        decisions = Sessions.DecisionCount,
        bus = includeSecrets ? BusInfo : NlSecurityRedaction.RedactStatusBus(BusInfo, includeSecrets: false),
        manifest = NlSecurityRedaction.RedactManifest(GetManifest(orchestrator, fleet), includeSecrets),
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

    public NlSessionManifest GetManifest(NlForkOrchestratorHost? orchestrator = null, NlFleetHost? fleet = null)
    {
        var profile = GetProfile();
        ForkManifestConnectInfo? forkConnect = null;
        string? fleetRegionId = profile.FleetPlacedRegionId ?? fleet?.Settings.DefaultRegion;
        string? fleetTurnUri = fleet?.Settings.Enabled == true ? fleet.Settings.Relay.TurnUri : null;

        if (orchestrator?.Settings.Enabled == true && !string.IsNullOrWhiteSpace(profile.ForkSessionId))
        {
            var fork = orchestrator.Orchestrator.GetSession(profile.ForkSessionId)
                ?? orchestrator.Orchestrator.GetActiveForStreamer(profile.StreamerId);
            if (fork is not null)
            {
                var endpoint = fork.ForkConnectEndpoint;
                fleetRegionId ??= fleet?.Settings.DefaultRegion ?? "us-east";
                if (fleet?.Settings.Enabled == true && !string.IsNullOrWhiteSpace(endpoint))
                {
                    endpoint = fleet.Relay.MaskEndpoint(endpoint, fleetRegionId, fork.SessionId).PublicConnectUrl;
                }

                forkConnect = new ForkManifestConnectInfo(
                    fork.SessionId,
                    endpoint,
                    fork.Provisioner.ToString(),
                    fork.ReservedPrivilegedSlots);
            }
        }

        return NlSessionServerHelper.CreateManifest(
            BusInfo, profile, BindHost, HttpPort, WsPort, ModPort, Sessions.IsRunning, forkConnect, fleetRegionId, fleetTurnUri);
    }

    /// <summary>Phase S — shared fork create with abuse gate, region placement, and metrics.</summary>
    public async Task<ForkProvisionResult> ProvisionForkSessionAsync(
        SessionProfileFile profile,
        NlForkOrchestratorHost orchestrator,
        NlFleetHost? fleet,
        int? twitchFollowers,
        CancellationToken cancellationToken)
    {
        var manifest = GetManifest(orchestrator, fleet);
        var streamerId = string.IsNullOrWhiteSpace(profile.StreamerId)
            ? NlPaths.DefaultStreamerId
            : profile.StreamerId;
        var followers = ResolveTwitchFollowers(twitchFollowers);
        var regionId = profile.FleetPlacedRegionId ?? fleet?.Settings.DefaultRegion ?? "us-east";

        if (fleet?.Settings.Enabled == true)
        {
            var abuse = fleet.Abuse.CheckForkCreate(streamerId, followers);
            if (!abuse.Allowed)
            {
                return new ForkProvisionResult(false, abuse.DenyReason, null, null);
            }

            var beta = fleet.Beta.CheckStreamer(streamerId);
            if (!beta.Allowed && !(fleet.GaSettings.Enabled && fleet.GaSettings.OpenSignup))
            {
                return new ForkProvisionResult(false, beta.DenyReason, null, null);
            }

            if (fleet.GaSettings.Enabled && !fleet.GaSettings.OpenSignup && !fleet.Ga.IsStreamerAllowed(streamerId))
            {
                return new ForkProvisionResult(false, "Streamer is not registered for general availability. Sign up at /ga.html", null, null);
            }

            var placement = fleet.Regions.Place(
                new FleetPlacementRequest(streamerId, profile.FleetPreferredRegion, profile.FleetGeoHint),
                manifest.HttpBaseUrl);
            regionId = placement.RegionId;
        }

        var gameId = profile.GameId ?? profile.Game;
        var major = profile.GameMajorVersion ?? "1.0";
        var forkSw = System.Diagnostics.Stopwatch.StartNew();
        var create = await orchestrator.Orchestrator.CreateSessionAsync(
            new CreateForkSessionRequest(
                streamerId,
                gameId,
                major,
                profile.ConfigPath,
                profile.AttachedModIds,
                ReservedPrivilegedSlots: profile.ForkReservedPrivilegedSlots > 0
                    ? profile.ForkReservedPrivilegedSlots
                    : orchestrator.Settings.DefaultReservedPrivilegedSlots),
            manifest.BridgeConnectUrl,
            manifest.AdmitUrl,
            BusToken,
            cancellationToken);

        if (!create.Success)
        {
            return new ForkProvisionResult(false, create.Error, null, null);
        }

        forkSw.Stop();
        fleet?.Metrics.RecordForkCreate(streamerId, regionId);
        fleet?.Metrics.RecordForkCreateLatency(forkSw.Elapsed.TotalMilliseconds);
        return new ForkProvisionResult(true, null, create.Session!.SessionId, regionId);
    }

    private static int? ResolveTwitchFollowers(int? overrideFollowers)
    {
        if (overrideFollowers is >= 0)
        {
            return overrideFollowers;
        }

        var raw = Environment.GetEnvironmentVariable("NL_FLEET_DEV_TWITCH_FOLLOWERS");
        return int.TryParse(raw, out var n) ? n : null;
    }

    public NlJoinAdmissionResult Admit(NlAdmitPlayerRequest request, NlIdentityHost? identity = null) =>
        AdmitAsync(request, identity).GetAwaiter().GetResult();

    public async Task<NlJoinAdmissionResult> AdmitAsync(
        NlAdmitPlayerRequest request,
        NlIdentityHost? identity = null,
        NlSocialHost? social = null,
        NlForkCatalogHost? catalog = null,
        NlPartnershipHost? partnership = null,
        CancellationToken cancellationToken = default)
    {
        var profile = GetProfile();
        var streamerId = string.IsNullOrWhiteSpace(request.StreamerId)
            ? profile.StreamerId
            : request.StreamerId.Trim();
        var admission = NlJoinAdmissionService.CreateDefault(streamerId);
        var useSocial = profile.SocialGateEnabled ? social : null;
        var useCatalog = profile.CatalogEnforced ? catalog : null;
        var usePartnership = profile.PartnershipGateEnabled ? partnership : null;
        return await admission.EvaluateAsync(request, profile, identity, useSocial, useCatalog, usePartnership, cancellationToken);
    }

    /// <summary>Phase N — apply catalog game + major to session profile.</summary>
    public SessionProfileFile ApplyCatalogSelection(
        ForkCatalogResolveResult resolved,
        string? samplesRoot = null)
    {
        var profile = GetProfile();
        var entry = resolved.Entry;
        profile.GameId = entry.GameId;
        profile.Game = entry.GameId;
        profile.GameMajorVersion = entry.MajorVersion;
        profile.CatalogEnforced = true;
        profile.PartnershipTier = entry.Tier.ToString();
        profile.NoProgressTransfer = entry.NoProgressTransfer;
        profile.CatalogLegalNotice = entry.EffectiveLegalNotice;
        profile.AttachedModIds = resolved.AttachedModIds.ToList();

        if (!string.IsNullOrWhiteSpace(resolved.ResolvedNleTemplate))
        {
            profile.ConfigPath = resolved.ResolvedNleTemplate;
        }
        else if (!string.IsNullOrWhiteSpace(entry.DefaultNleTemplate) && !string.IsNullOrWhiteSpace(samplesRoot))
        {
            profile.ConfigPath = Path.Combine(samplesRoot, entry.DefaultNleTemplate);
        }

        SaveProfile(profile);
        return GetProfile();
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
        RequireGameOwnership = p.RequireGameOwnership,
        GameId = p.GameId,
        PlatformAppId = p.PlatformAppId,
        GameMajorVersion = p.GameMajorVersion,
        OwnershipPlatform = p.OwnershipPlatform,
        StrictOwnershipUnknown = p.StrictOwnershipUnknown,
        RequireLiveStream = p.RequireLiveStream,
        SocialGateEnabled = p.SocialGateEnabled,
        CatalogEnforced = p.CatalogEnforced,
        AttachedModIds = p.AttachedModIds.ToList(),
        PartnershipTier = p.PartnershipTier,
        NoProgressTransfer = p.NoProgressTransfer,
        CatalogLegalNotice = p.CatalogLegalNotice,
        ForkOrchestratorEnabled = p.ForkOrchestratorEnabled,
        ForkDestroyGraceSeconds = p.ForkDestroyGraceSeconds,
        ForkMaxSessionHours = p.ForkMaxSessionHours,
        ForkReservedPrivilegedSlots = p.ForkReservedPrivilegedSlots,
        ForkSessionId = p.ForkSessionId,
        PartnershipGateEnabled = p.PartnershipGateEnabled,
        FleetPreferredRegion = p.FleetPreferredRegion,
        FleetGeoHint = p.FleetGeoHint,
        FleetPlacedRegionId = p.FleetPlacedRegionId,
    };

    private static string ResolveSampleConfig(string name) => NlSampleConfigPaths.Resolve(name);
}

public sealed record ForkProvisionResult(
    bool Success,
    string? Error,
    string? SessionId,
    string? RegionId);
