using System.Text.Json.Serialization;
using NL.Core;
using NL.Core.Security;
using NL.Core.Sp;
using NL.Fork.Core;
using NL.Fork.Catalog;
using NL.Partnership;
using NL.Partnership.Core;
using NL.Client;
using NL.Client.Core;
using NL.Fleet;
using NL.Fleet.Core;
using NL.Fork.Catalog.Core;
using NL.Fork.Orchestrator;
using NL.Fork.Orchestrator.Core;
using NL.Identity;
using NL.Identity.Core;
using NL.Moderation;
using NL.Moderation.Core;
using NL.NleEditor;
using NL.NleEditor.Model;
using NL.Server;
using NL.Server.Core.Integration;
using NL.Server.Core.Security;
using NL.SessionHost.Web;
using NL.Social;
using NL.Social.Core;
using NL.Web.Shared;

var security = NlSecuritySettings.LoadFromEnvironment();
var demoSettings = NlDemoSettings.LoadFromEnvironment();
var spectatorSettings = NlSpectatorSettings.LoadFromEnvironment();
var hardeningSettings = NlHardeningSettings.LoadFromEnvironment(security.PublicMode);
NlWebSocketConnectionGuard.Configure(hardeningSettings);
var bindHost = security.BindHost;
var httpPort = int.Parse(Environment.GetEnvironmentVariable("NL_HTTP_PORT") ?? NlSessionBusDefaults.HttpPort.ToString());
var wsPort = int.Parse(Environment.GetEnvironmentVariable("NL_WS_PORT") ?? NlSessionBusDefaults.WebSocketPort.ToString());
var modPort = int.Parse(Environment.GetEnvironmentVariable("NL_MOD_HTTP_PORT") ?? NlSessionServerDefaults.ModerationPort.ToString());
var busToken = NlSecuritySettings.ResolveBusToken(security);
var moderationLog = Environment.GetEnvironmentVariable("NL_MODERATION_LOG");
var spStore = Environment.GetEnvironmentVariable("NL_SP_STORE");

NlPaths.EnsureRoot();
DogfoodSetup.EnsureMockOwnership(DogfoodSetup.FindRepoRoot());

var identitySettings = NlIdentitySettings.LoadFromEnvironment();
var identityHost = new NlIdentityHost(identitySettings);
var socialSettings = NlSocialSettings.LoadFromEnvironment();
var socialHost = new NlSocialHost(socialSettings);
var catalogSettings = NlForkCatalogSettings.LoadFromEnvironment();
var catalogHost = new NlForkCatalogHost(catalogSettings);
var orchestratorSettings = NlForkOrchestratorSettings.LoadFromEnvironment();
var orchestratorHost = new NlForkOrchestratorHost(orchestratorSettings, catalogHost);
var partnershipSettings = NlPartnershipSettings.LoadFromEnvironment();
var partnershipHost = new NlPartnershipHost(partnershipSettings);
var fleetSettings = NlFleetSettings.LoadFromEnvironment();
var fleetHost = new NlFleetHost(fleetSettings);
var samplesRoot = ResolveSamplesRoot();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://{bindHost}:{httpPort}");
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var bus = new BusHostState(bindHost, httpPort, wsPort, busToken, modPort);
var moderation = new ModerationHostState(moderationLog, spStore);
var clientHost = CreateClientHost(bus, moderation, identityHost, socialHost, catalogHost, partnershipHost, orchestratorHost, fleetHost);
if (File.Exists(NlPaths.SessionProfile))
{
    bus.SaveProfile(NlSessionRunner.LoadProfile(NlPaths.SessionProfile));
}

builder.Services.AddSingleton(bus);
builder.Services.AddSingleton(moderation);
builder.Services.AddSingleton(identitySettings);
builder.Services.AddSingleton(identityHost);
builder.Services.AddSingleton(socialSettings);
builder.Services.AddSingleton(socialHost);
builder.Services.AddSingleton(catalogSettings);
builder.Services.AddSingleton(catalogHost);
builder.Services.AddSingleton(orchestratorSettings);
builder.Services.AddSingleton(orchestratorHost);
builder.Services.AddSingleton(partnershipSettings);
builder.Services.AddSingleton(partnershipHost);
builder.Services.AddSingleton(fleetSettings);
builder.Services.AddSingleton(fleetHost);
builder.Services.AddSingleton(clientHost);
builder.Services.AddSingleton(demoSettings);
builder.Services.AddSingleton(spectatorSettings);
builder.Services.AddSingleton(hardeningSettings);
builder.Services.AddSingleton(new NlPublicRateLimitService(hardeningSettings));
builder.Services.AddSingleton(new NlSpectatorService(spectatorSettings));
builder.Services.AddSingleton(new NlWebEditorStore());
builder.Services.AddNlWebSecurity(security);
if (demoSettings.Enabled)
{
    builder.Services.AddHostedService<NlDemoHostedService>();
}

if (socialSettings.Enabled && socialSettings.Mode != NlSocialMode.Off)
{
    builder.Services.AddHostedService<NlLiveOnlyHostedService>();
}

if (orchestratorSettings.Enabled)
{
    builder.Services.AddHostedService<NlForkOrchestratorLifecycleHostedService>();
}

if (fleetSettings.Enabled)
{
    builder.Services.AddHostedService<NlFleetLifecycleHostedService>();
}

var app = builder.Build();
app.UseCors();
app.UseNlPublicRateLimits();
app.UseNlOperatorAuth();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/v1/security", (NlSecuritySettings s) => Results.Json(s.ToPublicInfo()));

app.MapGet("/api/v1/bus", (BusHostState b, HttpContext ctx) =>
    Results.Json(NlSecurityRedaction.RedactBusInfo(b.BusInfo, NlWebSecurityExtensions.IsAuthorized(ctx))));

app.MapGet("/api/v1/session/manifest", (BusHostState b, NlForkOrchestratorHost orchestrator, NlFleetHost fleet, HttpContext ctx) =>
    Results.Json(NlSecurityRedaction.RedactManifest(b.GetManifest(orchestrator, fleet), NlWebSecurityExtensions.IsAuthorized(ctx))));

app.MapGet("/api/v1/session", (BusHostState b, NlForkOrchestratorHost orchestrator, NlFleetHost fleet, HttpContext ctx) =>
    Results.Json(b.GetStatus(NlWebSecurityExtensions.IsAuthorized(ctx), orchestrator, fleet)));

app.MapPost("/api/v1/session/admit", async (BusHostState b, NlIdentityHost identity, NlSocialHost social, NlForkCatalogHost catalog, NlPartnershipHost partnership, NlFleetHost fleet, NlAdmitPlayerRequest body, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(body.PlayerId))
    {
        return Results.BadRequest(new { error = "playerId required." });
    }

    try
    {
        var result = await b.AdmitAsync(body, identity, social, catalog, partnership, ct);
        if (fleet.Settings.Enabled)
        {
            fleet.Metrics.RecordAdmit(result.Admit, body.StreamerId ?? b.GetProfile().StreamerId);
            fleet.Metrics.RecordDecision(b.Sessions.DecisionCount);
        }

        return Results.Json(result);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/v1/identity/settings", (NlIdentitySettings s) => Results.Json(s.ToPublicInfo()));

app.MapPost("/api/v1/identity/accounts", (NlIdentityHost host, NlIdentitySettings settings, CreateIdentityAccountRequest body) =>
{
    if (!settings.Enabled)
    {
        return Results.Json(new { error = "Identity service disabled." }, statusCode: 503);
    }

    if (string.IsNullOrWhiteSpace(body.DisplayName))
    {
        return Results.BadRequest(new { error = "displayName required." });
    }

    var account = host.Identity.CreateAccount(body.DisplayName.Trim());
    return Results.Json(new { accountId = account.Id, displayName = account.DisplayName });
});

app.MapPost("/api/v1/identity/link", (NlIdentityHost host, NlIdentitySettings settings, LinkPlatformRequest body) =>
{
    if (!settings.Enabled)
    {
        return Results.Json(new { error = "Identity service disabled." }, statusCode: 503);
    }

    if (string.IsNullOrWhiteSpace(body.AccountId) || string.IsNullOrWhiteSpace(body.ExternalUserId))
    {
        return Results.BadRequest(new { error = "accountId and externalUserId required." });
    }

    if (!NlPlatformNames.TryParse(body.Platform, out var platform))
    {
        return Results.BadRequest(new { error = "Invalid platform." });
    }

    try
    {
        var account = host.Identity.LinkPlatform(
            body.AccountId.Trim(),
            platform,
            body.ExternalUserId.Trim(),
            body.RefreshToken);
        return Results.Json(new
        {
            accountId = account.Id,
            links = account.Links.Select(l => new { platform = l.Platform.ToString(), externalUserId = l.ExternalUserId }),
        });
    }
    catch (PlatformLinkConflictException ex)
    {
        return Results.Conflict(new { error = ex.Message, existingAccountId = ex.ExistingAccountId });
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapDelete("/api/v1/identity/link", (
    NlIdentityHost host,
    NlIdentitySettings settings,
    string accountId,
    string platform,
    string externalUserId) =>
{
    if (!settings.Enabled)
    {
        return Results.Json(new { error = "Identity service disabled." }, statusCode: 503);
    }

    if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(externalUserId))
    {
        return Results.BadRequest(new { error = "accountId and externalUserId required." });
    }

    if (!NlPlatformNames.TryParse(platform, out var parsedPlatform))
    {
        return Results.BadRequest(new { error = "Invalid platform." });
    }

    try
    {
        host.Identity.UnlinkPlatform(accountId.Trim(), parsedPlatform, externalUserId.Trim());
        return Results.Ok(new { ok = true });
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapGet("/api/v1/identity/accounts/{accountId}", (NlIdentityHost host, string accountId) =>
{
    var account = host.Identity.GetAccount(accountId);
    return account is null
        ? Results.NotFound(new { error = "Account not found." })
        : Results.Json(new
        {
            account.Id,
            account.DisplayName,
            account.CreatedAtUtc,
            links = account.Links.Select(l => new
            {
                platform = l.Platform.ToString(),
                l.ExternalUserId,
                l.LinkedAtUtc,
                hasToken = !string.IsNullOrWhiteSpace(l.ProtectedRefreshToken),
            }),
        });
});

app.MapGet("/api/v1/identity/accounts/by-platform/{platform}/{externalUserId}", (NlIdentityHost host, string platform, string externalUserId) =>
{
    if (!NlPlatformNames.TryParse(platform, out var parsed))
    {
        return Results.BadRequest(new { error = "Invalid platform." });
    }

    var account = host.Identity.GetAccountByPlatform(parsed, externalUserId);
    return account is null
        ? Results.NotFound(new { error = "No NL account linked to this platform user." })
        : Results.Json(new { accountId = account.Id, account.DisplayName });
});

app.MapGet("/api/v1/identity/oauth/steam/authorize", (
    NlIdentityHost host,
    NlIdentitySettings settings,
    HttpContext ctx,
    string accountId,
    string? returnUrl) =>
{
    if (!settings.Enabled)
    {
        return Results.Json(new { error = "Identity service disabled." }, statusCode: 503);
    }

    if (string.IsNullOrWhiteSpace(accountId))
    {
        return Results.BadRequest(new { error = "accountId required." });
    }

    if (host.Identity.GetAccount(accountId.Trim()) is null)
    {
        return Results.NotFound(new { error = "Account not found." });
    }

    var publicBase = ResolveIdentityPublicBase(ctx, settings);
    var redirect = host.SteamOpenId.BuildAuthorizeRedirect(accountId.Trim(), returnUrl, publicBase);
    return Results.Redirect(redirect);
});

app.MapGet("/api/v1/identity/oauth/steam/callback", async (
    NlIdentityHost host,
    NlIdentitySettings settings,
    HttpContext ctx,
    CancellationToken ct) =>
{
    if (!settings.Enabled)
    {
        return Results.Content("Identity service disabled.", "text/plain", statusCode: 503);
    }

    var query = ctx.Request.Query.ToDictionary(
        kv => kv.Key,
        kv => kv.Value.ToString(),
        StringComparer.OrdinalIgnoreCase);

    var result = await host.SteamOpenId.HandleCallbackAsync(query, host.Identity, ct);
    var landing = string.IsNullOrWhiteSpace(result.ReturnUrl)
        ? "/identity-link.html"
        : result.ReturnUrl!;

    var sep = landing.Contains('?') ? "&" : "?";
    if (result.Success)
    {
        return Results.Redirect(
            $"{landing}{sep}linked=steam&accountId={Uri.EscapeDataString(result.AccountId!)}&steamId={Uri.EscapeDataString(result.SteamId!)}");
    }

    return Results.Redirect(
        $"{landing}{sep}error={Uri.EscapeDataString(result.Error ?? "Steam sign-in failed.")}");
});

app.MapGet("/api/v1/identity/audit", (NlIdentityHost host, int? count) =>
    Results.Json(host.Audit.ReadRecent(count ?? 50)));

app.MapGet("/api/v1/social/settings", (NlSocialSettings s) => Results.Json(s.ToPublicInfo()));

app.MapGet("/api/v1/social/join-requirements", () =>
    Results.Json(JoinRequirementsStore.LoadOrDefault(NlPaths.JoinRequirements)));

app.MapPut("/api/v1/social/join-requirements", async (HttpRequest req) =>
{
    var body = await req.ReadFromJsonAsync<JoinRequirements>();
    if (body is null)
    {
        return Results.BadRequest(new { error = "Invalid join requirements JSON." });
    }

    JoinRequirementsStore.Save(NlPaths.JoinRequirements, body);
    return Results.Json(body);
});

app.MapGet("/api/v1/social/streamer-config", (NlSocialHost host, string? streamer) =>
{
    var streamerId = string.IsNullOrWhiteSpace(streamer) ? NlPaths.DefaultStreamerId : streamer.Trim();
    return Results.Json(host.StreamerStore.GetOrDefault(streamerId));
});

app.MapPut("/api/v1/social/streamer-config", (NlSocialHost host, StreamerSocialConfig body) =>
{
    if (string.IsNullOrWhiteSpace(body.StreamerId))
    {
        return Results.BadRequest(new { error = "streamerId required." });
    }

    host.StreamerStore.Save(body);
    host.Cache.InvalidateAll();
    return Results.Json(body);
});

app.MapGet("/api/v1/social/live-status", async (NlSocialHost host, string? streamer, CancellationToken ct) =>
{
    var streamerId = string.IsNullOrWhiteSpace(streamer) ? NlPaths.DefaultStreamerId : streamer.Trim();
    var config = host.Gate.GetStreamerConfig(streamerId);
    var status = await host.LiveMonitor.GetStatusAsync(config, ct);
    host.Cache.SetLive(streamerId, status);
    return Results.Json(status);
});

app.MapPost("/api/v1/social/link", (NlSocialHost host, SocialLinkRequest body) =>
{
    if (string.IsNullOrWhiteSpace(body.PlayerId))
    {
        return Results.BadRequest(new { error = "playerId required." });
    }

    var links = host.Gate.ResolveLinks(body.PlayerId.Trim(), new SocialLinkInput
    {
        TwitchUserId = body.TwitchUserId,
        YouTubeChannelId = body.YouTubeChannelId,
        KickUserId = body.KickUserId,
        DiscordUserId = body.DiscordUserId,
    });
    host.Cache.InvalidateAll();
    return Results.Json(links);
});

app.MapGet("/api/v1/social/links/{playerId}", (NlSocialHost host, string playerId) =>
    Results.Json(host.LinkStore.GetOrDefault(playerId)));

app.MapGet("/api/v1/fork/catalog/settings", (NlForkCatalogHost host) => Results.Json(host.Settings.ToPublicInfo()));

app.MapGet("/api/v1/fork/catalog/version-policy", (BusHostState bus, NlForkCatalogHost host, NlFleetHost fleet, string? streamerId) =>
{
    var sid = string.IsNullOrWhiteSpace(streamerId) ? bus.GetProfile().StreamerId : streamerId.Trim();
    var requirements = fleet.StreamerRequirements.GetOrDefault(sid);
    return Results.Json(new
    {
        defaultToLatestStable = host.VersionPolicy.DefaultToLatestStable,
        customMajorVersionBetaEnabled = host.VersionPolicy.CustomMajorVersionBetaEnabled,
        allowCustomMajorForStreamer = requirements.AllowCustomMajorVersion,
        streamerId = sid,
        latestStableByGame = host.VersionPolicy.BuildLatestStableIndex(),
    });
});

app.MapGet("/api/v1/fork/catalog/entries", (NlForkCatalogHost host, bool? includeDeprecated) =>
    Results.Json(host.Catalog.ListGames(includeDeprecated ?? false)));

app.MapGet("/api/v1/fork/catalog/mod-hub", (NlForkCatalogHost host) =>
    Results.Json(host.Catalog.GetManifest().ModHub));

app.MapGet("/api/v1/fork/catalog/entries/{gameId}/{majorVersion}", (NlForkCatalogHost host, string gameId, string majorVersion) =>
{
    var entry = host.Catalog.GetEntry(gameId, majorVersion);
    return entry is null ? Results.NotFound(new { error = $"Unknown entry '{gameId}@{majorVersion}'." }) : Results.Json(entry);
});

app.MapPost("/api/v1/fork/catalog/register", (NlForkCatalogHost host, ForkCatalogEntry body) =>
{
    try
    {
        var registered = host.Catalog.RegisterEntry(body);
        return Results.Json(registered);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/v1/fork/catalog/select", (BusHostState bus, NlForkCatalogHost host, NlFleetHost fleet, CatalogSelectRequest body) =>
{
    if (string.IsNullOrWhiteSpace(body.GameId))
    {
        return Results.BadRequest(new { error = "gameId required." });
    }

    try
    {
        var profile = bus.GetProfile();
        var streamerId = string.IsNullOrWhiteSpace(profile.StreamerId)
            ? NL.Core.NlPaths.DefaultStreamerId
            : profile.StreamerId;
        var requirements = fleet.StreamerRequirements.GetOrDefault(streamerId);
        var selection = host.VersionPolicy.ResolveSelection(
            body.GameId.Trim(),
            body.MajorVersion,
            body.ModIds ?? [],
            requirements.AllowCustomMajorVersion);
        var resolved = host.Catalog.ResolveSelection(selection, samplesRoot);
        profile = bus.ApplyCatalogSelection(resolved, samplesRoot);
        if (body.EnableOrchestrator == true)
        {
            profile.ForkOrchestratorEnabled = true;
            bus.SaveProfile(profile);
        }

        return Results.Json(new
        {
            profile,
            entry = resolved.Entry,
            nleTemplate = resolved.ResolvedNleTemplate,
            mods = resolved.ResolvedMods,
            resolvedMajorVersion = selection.MajorVersion,
            latestStable = host.VersionPolicy.IsLatestStable(selection.GameId, selection.MajorVersion),
        });
    }
    catch (ForkCatalogVersionAccessException ex)
    {
        return Results.Json(new { error = ex.Message, code = "custom_major_entitlement_required" }, statusCode: 403);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/v1/fork/orchestrator/settings", (NlForkOrchestratorSettings s) => Results.Json(s.ToPublicInfo()));

app.MapGet("/api/v1/fork/orchestrator/sessions", (NlForkOrchestratorHost host) =>
    Results.Json(host.Orchestrator.ListActive()));

app.MapGet("/api/v1/fork/orchestrator/sessions/{sessionId}", (NlForkOrchestratorHost host, string sessionId) =>
{
    var session = host.Orchestrator.GetSession(sessionId);
    return session is null ? Results.NotFound(new { error = "Session not found." }) : Results.Json(session);
});

app.MapPost("/api/v1/fork/orchestrator/create", async (
    BusHostState bus,
    NlForkCatalogHost catalogHost,
    NlForkOrchestratorHost host,
    NlFleetHost fleet,
    ForkOrchestratorCreateRequest body,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(body.StreamerId) || string.IsNullOrWhiteSpace(body.NlePath))
    {
        return Results.BadRequest(new { error = "streamerId and nlePath required." });
    }

    var profile = bus.GetProfile();
    profile.StreamerId = body.StreamerId.Trim();
    profile.ConfigPath = body.NlePath.Trim();
    profile.GameId = body.GameId?.Trim() ?? profile.GameId ?? "generic";
    profile.AttachedModIds = body.ModIds ?? [];
    profile.ForkReservedPrivilegedSlots = body.ReservedPrivilegedSlots ?? host.Settings.DefaultReservedPrivilegedSlots;
    if (!string.IsNullOrWhiteSpace(body.PreferredRegion))
    {
        profile.FleetPreferredRegion = body.PreferredRegion.Trim();
    }

    try
    {
        if (catalogHost.Settings.Enabled)
        {
            var requirements = fleet.StreamerRequirements.GetOrDefault(profile.StreamerId);
            var selection = catalogHost.VersionPolicy.ResolveSelection(
                profile.GameId,
                body.MajorVersion ?? profile.GameMajorVersion,
                profile.AttachedModIds,
                requirements.AllowCustomMajorVersion);
            profile.GameId = selection.GameId;
            profile.GameMajorVersion = selection.MajorVersion;
        }
        else
        {
            profile.GameMajorVersion = body.MajorVersion?.Trim() ?? profile.GameMajorVersion ?? "1.0";
        }
    }
    catch (ForkCatalogVersionAccessException ex)
    {
        return Results.Json(new { error = ex.Message, code = "custom_major_entitlement_required" }, statusCode: 403);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }

    var result = await bus.ProvisionForkSessionAsync(profile, host, fleet, body.TwitchFollowers, ct);
    if (!result.Success)
    {
        return Results.BadRequest(new { error = result.Error });
    }

    profile.ForkOrchestratorEnabled = true;
    profile.ForkSessionId = result.SessionId;
    profile.FleetPlacedRegionId = result.RegionId;
    bus.SaveProfile(profile);

    return Results.Json(new
    {
        sessionId = result.SessionId,
        regionId = result.RegionId,
        manifest = bus.GetManifest(host, fleet),
    });
});

app.MapPost("/api/v1/fork/orchestrator/destroy/{sessionId}", async (
    NlForkOrchestratorHost host,
    string sessionId,
    CancellationToken ct) =>
{
    var result = await host.Orchestrator.DestroySessionAsync(sessionId, ct);
    return result.Success ? Results.Ok(new { ok = true }) : Results.BadRequest(new { error = result.Error });
});

app.MapGet("/api/v1/fleet/settings", (NlFleetSettings s) => Results.Json(s.ToPublicInfo()));

app.MapGet("/api/v1/fleet/regions", (NlFleetHost fleet) => Results.Json(fleet.Regions.ListRegions()));

app.MapGet("/api/v1/fleet/observability", (NlFleetHost fleet, NlForkOrchestratorHost orchestrator, BusHostState bus) =>
{
    var activeForks = orchestrator.Settings.Enabled ? orchestrator.Orchestrator.ListActive().Count : 0;
    var activeNls = bus.Sessions.IsRunning ? 1 : 0;
    return Results.Json(fleet.Metrics.BuildSnapshot(activeForks, activeNls));
});

app.MapGet("/api/v1/fleet/slos", (NlFleetHost fleet, NlForkOrchestratorHost orchestrator, BusHostState bus) =>
{
    var activeForks = orchestrator.Settings.Enabled ? orchestrator.Orchestrator.ListActive().Count : 0;
    var activeNls = bus.Sessions.IsRunning ? 1 : 0;
    var snap = fleet.Metrics.BuildSnapshot(activeForks, activeNls);
    return Results.Json(fleet.Slo.Evaluate(snap, loadTest: null, fleet.Metrics, fleet.Incidents));
});

app.MapGet("/api/v1/fleet/validation", (NlFleetHost fleet, NlForkOrchestratorHost orchestrator, BusHostState bus) =>
{
    var activeForks = orchestrator.Settings.Enabled ? orchestrator.Orchestrator.ListActive().Count : 0;
    var activeNls = bus.Sessions.IsRunning ? 1 : 0;
    var snap = fleet.Metrics.BuildSnapshot(activeForks, activeNls);
    var last = fleet.ValidationStore.GetLast();
    var report = fleet.Validation.Evaluate(
        fleet.Settings,
        orchestrator.Settings.Mode.ToString(),
        snap,
        fleet.Metrics,
        fleet.Incidents,
        last?.LastLoadTest);
    return Results.Json(report);
});

app.MapPost("/api/v1/fleet/validation/run", (NlFleetHost fleet, NlForkOrchestratorHost orchestrator, BusHostState bus) =>
{
    var activeForks = orchestrator.Settings.Enabled ? orchestrator.Orchestrator.ListActive().Count : 0;
    var activeNls = bus.Sessions.IsRunning ? 1 : 0;
    var snap = fleet.Metrics.BuildSnapshot(activeForks, activeNls);
    var last = fleet.ValidationStore.GetLast();
    var report = fleet.Validation.Evaluate(
        fleet.Settings,
        orchestrator.Settings.Mode.ToString(),
        snap,
        fleet.Metrics,
        fleet.Incidents,
        last?.LastLoadTest);
    fleet.ValidationStore.Save(report);
    return Results.Json(report);
});

app.MapGet("/api/v1/fleet/incidents", (NlFleetHost fleet, int? count) =>
    Results.Json(fleet.Incidents.ListRecent(count ?? 50)));

app.MapGet("/api/v1/fleet/autoscale", (NlFleetHost fleet, NlForkOrchestratorHost orchestrator, BusHostState bus) =>
{
    var activeForks = orchestrator.Settings.Enabled ? orchestrator.Orchestrator.ListActive().Count : 0;
    var activeNls = bus.Sessions.IsRunning ? 1 : 0;
    return Results.Json(fleet.Autoscale.Evaluate(activeForks, activeNls > 0, null));
});

app.MapGet("/api/v1/fleet/streamer-requirements/{streamerId}", (NlFleetHost fleet, string streamerId) =>
    Results.Json(fleet.StreamerRequirements.GetOrDefault(streamerId)));

app.MapPut("/api/v1/fleet/streamer-requirements", (NlFleetHost fleet, FleetStreamerRequirements body) =>
{
    if (string.IsNullOrWhiteSpace(body.StreamerId))
    {
        return Results.BadRequest(new { error = "streamerId required." });
    }

    fleet.StreamerRequirements.Save(body with { StreamerId = body.StreamerId.Trim() });
    return Results.Ok(body);
});

app.MapGet("/api/v1/beta/settings", (NlFleetHost fleet) => Results.Json(fleet.BetaSettings.ToPublicInfo()));

app.MapGet("/api/v1/beta/status", (NlFleetHost fleet) => Results.Json(fleet.Beta.GetStatus()));

app.MapPost("/api/v1/beta/waitlist", (NlFleetHost fleet, BetaWaitlistSignupRequest body) =>
{
    try
    {
        var entry = fleet.Beta.SignUp(body.DisplayName ?? "", body.Contact ?? "", body.TwitchHandle, body.RequestedGameId);
        return Results.Json(entry);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/v1/beta/waitlist", (NlFleetHost fleet, HttpContext ctx) =>
{
    if (!NlWebSecurityExtensions.IsAuthorized(ctx))
    {
        return Results.Unauthorized();
    }

    return Results.Json(fleet.Beta.ListWaitlist());
});

app.MapPost("/api/v1/beta/waitlist/{entryId}/approve", (NlFleetHost fleet, HttpContext ctx, string entryId, BetaWaitlistApproveRequest? body) =>
{
    if (!NlWebSecurityExtensions.IsAuthorized(ctx))
    {
        return Results.Unauthorized();
    }

    try
    {
        var entry = fleet.Beta.Approve(entryId, body?.StreamerId);
        return Results.Json(entry);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/v1/beta/waitlist/{entryId}/reject", (NlFleetHost fleet, HttpContext ctx, string entryId) =>
{
    if (!NlWebSecurityExtensions.IsAuthorized(ctx))
    {
        return Results.Unauthorized();
    }

    try
    {
        return Results.Json(fleet.Beta.Reject(entryId));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/v1/beta/validation", (
    NlFleetHost fleet,
    NlForkOrchestratorHost orchestrator,
    BusHostState bus,
    NlIdentitySettings identity,
    NlSecuritySettings security) =>
    Results.Json(BuildBetaValidationReport(fleet, orchestrator, bus, identity, security)));

app.MapPost("/api/v1/beta/validation/run", (
    NlFleetHost fleet,
    NlForkOrchestratorHost orchestrator,
    BusHostState bus,
    NlIdentitySettings identity,
    NlSecuritySettings security) =>
    Results.Json(BuildBetaValidationReport(fleet, orchestrator, bus, identity, security)));

app.MapGet("/api/v1/ga/settings", (NlFleetHost fleet) => Results.Json(fleet.GaSettings.ToPublicInfo()));

app.MapGet("/api/v1/ga/status", (NlFleetHost fleet, NlForkCatalogHost catalog) =>
{
    var gameIds = catalog.Settings.Enabled
        ? catalog.Catalog.ListGames()
            .Select(e => e.GameId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
        : (IReadOnlyList<string>)[];
    return Results.Json(fleet.Ga.GetStatus(gameIds.Count));
});

app.MapGet("/api/v1/ga/catalog", (NlForkCatalogHost catalog) =>
{
    if (!catalog.Settings.Enabled)
    {
        return Results.Json(new { enabled = false, games = Array.Empty<object>() });
    }

    var games = catalog.Catalog.ListGames()
        .GroupBy(e => e.GameId, StringComparer.OrdinalIgnoreCase)
        .Select(g =>
        {
            var stable = catalog.Catalog.ResolveLatestStableEntry(g.Key);
            return new
            {
                gameId = g.Key,
                displayName = stable?.DisplayName ?? g.First().DisplayName,
                majorVersion = stable?.MajorVersion,
                tier = stable?.Tier.ToString(),
                dockerImage = stable?.DockerImage,
                status = stable?.Status.ToString(),
            };
        })
        .OrderBy(g => g.gameId)
        .ToList();
    return Results.Json(new { enabled = true, games });
});

app.MapPost("/api/v1/ga/streamers/register", (NlFleetHost fleet, GaStreamerRegisterRequest body) =>
{
    try
    {
        var entry = fleet.Ga.Register(
            body.DisplayName ?? "",
            body.Contact ?? "",
            body.TwitchHandle,
            body.PreferredGameId,
            body.StreamerId);
        return Results.Json(entry);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/v1/ga/streamers", (NlFleetHost fleet, HttpContext ctx) =>
{
    if (!NlWebSecurityExtensions.IsAuthorized(ctx))
    {
        return Results.Unauthorized();
    }

    return Results.Json(fleet.Ga.ListStreamers());
});

app.MapGet("/api/v1/ga/sla", (NlFleetHost fleet, NlForkOrchestratorHost orchestrator, BusHostState bus) =>
{
    var activeForks = orchestrator.Settings.Enabled ? orchestrator.Orchestrator.ListActive().Count : 0;
    var activeNls = bus.Sessions.IsRunning ? 1 : 0;
    var snap = fleet.Metrics.BuildSnapshot(activeForks, activeNls);
    var slos = fleet.Slo.EvaluateProduction(
        snap,
        fleet.ValidationStore.GetLast()?.LastLoadTest,
        fleet.Metrics,
        fleet.Incidents);
    return Results.Json(new
    {
        tier = fleet.GaSettings.SlaTier,
        definitions = FleetSloCatalog.ProductionDefaults,
        status = slos,
    });
});

app.MapGet("/api/v1/ga/validation", (
    NlFleetHost fleet,
    NlForkOrchestratorHost orchestrator,
    BusHostState bus,
    NlIdentitySettings identity,
    NlSecuritySettings security,
    NlForkCatalogHost catalog) =>
    Results.Json(BuildGaValidationReport(fleet, orchestrator, bus, identity, security, catalog)));

app.MapPost("/api/v1/ga/validation/run", (
    NlFleetHost fleet,
    NlForkOrchestratorHost orchestrator,
    BusHostState bus,
    NlIdentitySettings identity,
    NlSecuritySettings security,
    NlForkCatalogHost catalog) =>
    Results.Json(BuildGaValidationReport(fleet, orchestrator, bus, identity, security, catalog)));

app.MapGet("/api/v1/live-production/settings", (NlFleetHost fleet) =>
    Results.Json(fleet.LiveProductionSettings.ToPublicInfo()));

app.MapGet("/api/v1/live-production/status", (
    NlFleetHost fleet,
    NlIdentitySettings identity) =>
    Results.Json(new LiveProductionStatus(
        fleet.LiveProductionSettings.Enabled,
        fleet.LiveProductionSettings.DevMode,
        fleet.GaSettings.Enabled,
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STEAM_WEB_API_KEY")),
        identity.Mode.ToString(),
        identity.PublicBaseUrl ?? Environment.GetEnvironmentVariable("NL_PUBLIC_BASE_URL"),
        fleet.Settings.Relay.RelayWebSocketTemplate,
        fleet.Settings.Relay.TurnUri,
        DateTimeOffset.UtcNow)));

app.MapGet("/api/v1/live-production/validation", (
    NlFleetHost fleet,
    NlForkOrchestratorHost orchestrator,
    BusHostState bus,
    NlIdentitySettings identity,
    NlSecuritySettings security,
    NlForkCatalogHost catalog) =>
    Results.Json(BuildLiveProductionValidationReport(fleet, orchestrator, bus, identity, security, catalog)));

app.MapPost("/api/v1/live-production/validation/run", (
    NlFleetHost fleet,
    NlForkOrchestratorHost orchestrator,
    BusHostState bus,
    NlIdentitySettings identity,
    NlSecuritySettings security,
    NlForkCatalogHost catalog) =>
    Results.Json(BuildLiveProductionValidationReport(fleet, orchestrator, bus, identity, security, catalog)));

app.MapGet("/api/v1/multigame/settings", (NlFleetHost fleet) =>
    Results.Json(fleet.MultiGameSettings.ToPublicInfo()));

app.MapGet("/api/v1/multigame/status", (
    NlFleetHost fleet,
    NlForkCatalogHost catalog,
    NlPartnershipHost partnership) =>
    Results.Json(new MultiGameStatus(
        fleet.MultiGameSettings.Enabled,
        fleet.LiveProductionSettings.Enabled,
        fleet.GaSettings.Enabled,
        catalog.Settings.Enabled,
        partnership.Settings.Enabled,
        fleet.MultiGameSettings.RequiredGameIds,
        DateTimeOffset.UtcNow)));

app.MapGet("/api/v1/multigame/catalog", (NlFleetHost fleet, NlForkCatalogHost catalog) =>
{
    if (!catalog.Settings.Enabled)
    {
        return Results.Json(new { enabled = false, games = Array.Empty<object>() });
    }

    var games = fleet.MultiGameSettings.RequiredGameIds
        .Select(gameId =>
        {
            var stable = catalog.Catalog.ResolveLatestStableEntry(gameId);
            return new
            {
                gameId,
                displayName = stable?.DisplayName,
                majorVersion = stable?.MajorVersion,
                dockerImage = stable?.DockerImage ?? ForkGameProfiles.Resolve(gameId).DockerImage,
                nleTemplate = stable?.DefaultNleTemplate ?? ForkGameProfiles.Resolve(gameId).DefaultNleTemplate,
            };
        })
        .ToList();
    return Results.Json(new { enabled = true, games });
});

app.MapGet("/api/v1/multigame/validation", (
    NlFleetHost fleet,
    NlForkOrchestratorHost orchestrator,
    BusHostState bus,
    NlIdentitySettings identity,
    NlSecuritySettings security,
    NlForkCatalogHost catalog,
    NlPartnershipHost partnership) =>
    Results.Json(BuildMultiGameValidationReport(fleet, orchestrator, bus, identity, security, catalog, partnership, null)));

app.MapPost("/api/v1/multigame/validation/run", (
    NlFleetHost fleet,
    NlForkOrchestratorHost orchestrator,
    BusHostState bus,
    NlIdentitySettings identity,
    NlSecuritySettings security,
    NlForkCatalogHost catalog,
    NlPartnershipHost partnership,
    MultiGameValidationRunRequest? body) =>
    Results.Json(BuildMultiGameValidationReport(fleet, orchestrator, bus, identity, security, catalog, partnership, body)));

app.MapPost("/api/v1/fleet/compliance/export/{playerId}", (NlFleetHost fleet, ModerationHostState mod, string playerId) =>
{
    try
    {
        var profile = mod.Moderation.GetOrCreateProfile(playerId.Trim(), playerId.Trim());
        var export = fleet.Compliance.ExportSpProfile(playerId.Trim(), new { profile.Id, profile.DisplayName });
        return Results.Json(new { export.PlayerId, export.ExportedAtUtc, path = NlFleetPaths.ComplianceExports });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapDelete("/api/v1/fleet/compliance/sp/{playerId}", (NlFleetHost fleet, string playerId) =>
{
    try
    {
        fleet.Compliance.DeleteSpProfile(NlPaths.SpProfiles, playerId.Trim());
        return Results.Ok(new { deleted = playerId.Trim() });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/v1/fleet/load-test/report", (
    NlFleetHost fleet,
    NlForkOrchestratorHost orchestrator,
    BusHostState bus,
    FleetLoadTestReportRequest body) =>
{
    var activeForks = body.ActiveForkSessions > 0
        ? body.ActiveForkSessions
        : (orchestrator.Settings.Enabled ? orchestrator.Orchestrator.ListActive().Count : 0);
    var activeNls = body.ActiveNlsSessions > 0 ? body.ActiveNlsSessions : (bus.Sessions.IsRunning ? 1 : 0);
    var snap = fleet.Metrics.BuildSnapshot(activeForks, activeNls);
    var forkP99 = body.ForkCreateP99Ms > 0 ? body.ForkCreateP99Ms : fleet.Metrics.GetForkCreateP99Ms();
    var slos = fleet.Slo.Evaluate(snap, null, fleet.Metrics, fleet.Incidents);
    var load = new FleetLoadTestResult(
        body.ConcurrentSessionsTarget,
        body.AdmitsPerSecondTarget,
        body.AdmitsSucceeded,
        body.AdmitsFailed,
        body.ElapsedSeconds,
        forkP99,
        slos);
    load = load with
    {
        Slos = fleet.Slo.Evaluate(snap, load, fleet.Metrics, fleet.Incidents),
    };
    var report = fleet.Validation.Evaluate(
        fleet.Settings,
        orchestrator.Settings.Mode.ToString(),
        snap,
        fleet.Metrics,
        fleet.Incidents,
        load);
    fleet.ValidationStore.Save(report);
    return Results.Json(new
    {
        loadTest = load,
        slos = load.Slos,
        validation = report,
    });
});

app.MapGet("/api/v1/partnership/settings", (NlPartnershipSettings s) => Results.Json(s.ToPublicInfo()));

app.MapGet("/api/v1/partnership/legal/{gameId}", (NlPartnershipHost host, NlForkCatalogHost catalog, string gameId) =>
{
    var entry = catalog.Catalog.GetEntry(gameId, "1.0") ?? catalog.Catalog.ListGames(true).FirstOrDefault(e =>
        string.Equals(e.GameId, gameId, StringComparison.OrdinalIgnoreCase));
    var tier = entry?.Tier ?? PartnershipTier.AtOwnRisk;
    var legal = host.Gate.GetLegal(gameId, tier, entry?.EffectiveLegalNotice);
    return Results.Json(legal);
});

app.MapGet("/api/v1/partnership/acknowledgment/{playerId}/{gameId}", (NlPartnershipHost host, string playerId, string gameId) =>
{
    var ack = host.Acknowledgments.Get(playerId, gameId);
    return ack is null ? Results.NotFound(new { acknowledged = false }) : Results.Json(ack);
});

app.MapPost("/api/v1/partnership/acknowledge", (NlPartnershipHost host, NlForkCatalogHost catalog, PartnershipAcknowledgeRequest body) =>
{
    if (string.IsNullOrWhiteSpace(body.PlayerId) || string.IsNullOrWhiteSpace(body.GameId))
    {
        return Results.BadRequest(new { error = "playerId and gameId required." });
    }

    var entry = catalog.Catalog.ListGames(true).FirstOrDefault(e =>
        string.Equals(e.GameId, body.GameId, StringComparison.OrdinalIgnoreCase));
    var tier = entry?.Tier ?? PartnershipTier.AtOwnRisk;
    var ack = host.Gate.RecordAcknowledgment(body.PlayerId.Trim(), body.GameId.Trim(), tier);
    return Results.Json(ack);
});

app.MapGet("/api/v1/partnership/publishers", (NlPartnershipHost host) =>
    Results.Json(host.Publishers.List()));

app.MapPost("/api/v1/partnership/publishers/register", (NlPartnershipHost host, PublisherRegistration body) =>
{
    if (string.IsNullOrWhiteSpace(body.PublisherId) || string.IsNullOrWhiteSpace(body.DisplayName))
    {
        return Results.BadRequest(new { error = "publisherId and displayName required." });
    }

    return Results.Json(host.Publishers.Save(body));
});

app.MapPut("/api/v1/partnership/publishers/{publisherId}/titles/{gameId}", (
    NlPartnershipHost host,
    string publisherId,
    string gameId,
    PublisherTitleStatusRequest body) =>
{
    try
    {
        var pub = host.Publishers.SetTitleStatus(publisherId, gameId, body.Status);
        return Results.Json(pub);
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapGet("/api/v1/partnership/platform-opt-in", (NlPartnershipHost host) =>
    Results.Json(host.PlatformOptIn.List()));

app.MapPost("/api/v1/partnership/platform-opt-in", (NlPartnershipHost host, PlatformOptInEntry body) =>
{
    if (string.IsNullOrWhiteSpace(body.Platform) || string.IsNullOrWhiteSpace(body.AppId) || string.IsNullOrWhiteSpace(body.GameId))
    {
        return Results.BadRequest(new { error = "platform, appId, and gameId required." });
    }

    host.PlatformOptIn.Save(body);
    return Results.Json(body);
});

app.MapPost("/api/v1/partnership/ban-sync", (NlPartnershipHost host, NlPartnershipSettings settings, HttpRequest req, BanSyncWebhookRequest body) =>
{
    if (!string.IsNullOrWhiteSpace(settings.WebhookSecret))
    {
        var header = req.Headers["X-NL-Partnership-Secret"].FirstOrDefault()
            ?? req.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
        if (!string.Equals(header, settings.WebhookSecret, StringComparison.Ordinal))
        {
            return Results.Unauthorized();
        }
    }

    try
    {
        host.BanSync.Apply(body);
        return Results.Ok(new { ok = true });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/v1/partnership/dashboard/{publisherId}", (NlPartnershipHost host, string publisherId) =>
{
    try
    {
        return Results.Json(host.Dashboard.GetSnapshot(publisherId));
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapGet("/api/v1/partnership/sdk/spec", (BusHostState bus) =>
{
    var httpBase = bus.GetManifest().HttpBaseUrl;
    return Results.Json(PlayOnNlSdkSpecProvider.Create(httpBase));
});

app.MapPost("/api/v1/partnership/sdk/ownership-token", (PartnershipOwnershipTokenRequest body) =>
{
    if (string.IsNullOrWhiteSpace(body.PlatformUserId) || string.IsNullOrWhiteSpace(body.GameId))
    {
        return Results.BadRequest(new { error = "platformUserId and gameId required." });
    }

    var exp = DateTimeOffset.UtcNow.AddMinutes(15);
    return Results.Json(new
    {
        token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
        sub = body.PlatformUserId,
        game_id = body.GameId,
        app_id = body.AppId,
        platform_user_id = body.PlatformUserId,
        platform = body.Platform ?? "steam",
        exp = exp.ToUnixTimeSeconds(),
        note = "Stub ownership token for Play on NL SDK integration (Phase Q).",
    });
});

app.MapGet("/api/v1/client/settings", (NlClientHost client) => Results.Json(client.ToPublicSettings()));

app.MapGet("/api/v1/client/streamers", async (NlClientHost client, CancellationToken ct) =>
    Results.Json(await client.ListStreamersAsync(ct)));

app.MapPost("/api/v1/client/join-flow", async (NlClientHost client, NlClientJoinRequest body, CancellationToken ct) =>
{
    var result = await client.JoinFlow.ExecuteAsync(body, ct);
    return Results.Json(result);
});

app.MapPost("/api/v1/client/launch-params", (NlClientManifest body) =>
{
    var launch = NlClientLaunchBuilder.Build(body);
    return Results.Json(launch);
});

app.MapPost("/api/v1/client/block-invite", (NlClientBlockInviteRequest body, BusHostState bus) =>
{
    var host = bus.GetManifest().HttpBaseUrl;
    var result = NlInviteBlocker.Evaluate(body.InviteUrl ?? "", body.ExpectedHost ?? host);
    return Results.Json(result);
});

app.MapGet("/api/v1/client/overlay/{playerId}", (NlClientHost client, ModerationHostState mod, string playerId, string? streamer, BusHostState bus) =>
{
    var streamerId = string.IsNullOrWhiteSpace(streamer) ? bus.GetProfile().StreamerId : streamer.Trim();
    var profile = mod.Moderation.GetOrCreateProfile(playerId, playerId);
    return Results.Json(NlClientOverlayBuilder.Build(profile, streamerId));
});

app.MapPost("/api/v1/client/mobile/action", async (ModerationHostState mod, NlClientMobileActionRequest body, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(body.PlayerId) || string.IsNullOrWhiteSpace(body.Action))
    {
        return Results.BadRequest(new { error = "playerId and action required." });
    }

    var streamerId = string.IsNullOrWhiteSpace(body.StreamerId) ? NL.Core.NlPaths.DefaultStreamerId : body.StreamerId.Trim();
    var action = body.Action.Trim().ToLowerInvariant();
    var reason = string.IsNullOrWhiteSpace(body.Reason) ? "mobile-companion" : body.Reason.Trim();

    try
    {
        if (action is "warn" or "warning")
        {
            await mod.Moderation.IssueWarningAsync(streamerId, body.PlayerId.Trim(), "nl-client-mobile", reason, null, ct);
        }
        else if (action is "kick" or "ban")
        {
            await mod.Moderation.IssueBanAsync(streamerId, body.PlayerId.Trim(), "nl-client-mobile", reason, null, ct);
        }
        else
        {
            return Results.BadRequest(new { error = "Unknown action. Use warn or kick." });
        }

        return Results.Json(new NlClientMobileActionResult(true));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/v1/dogfood/setup", async (BusHostState bus, NlForkOrchestratorHost orchestrator, NlFleetHost fleet, NlIdentityHost identity, HttpRequest req) =>
{
    try
    {
        DogfoodSetupRequest? body = null;
        if (req.ContentLength is > 0)
        {
            body = await req.ReadFromJsonAsync<DogfoodSetupRequest>();
        }

        var root = DogfoodSetup.FindRepoRoot();
        DogfoodSetup.EnsureMockOwnership(root);
        identity.ReloadMockOwnership();
        var profile = DogfoodSetup.BuildProfile(root, body?.GameId);
        bus.SaveProfile(profile);
        NlSessionBusHelper.ApplyBusSource(profile, bus.BusInfo);
        bus.SaveProfile(profile);
        return Results.Ok(bus.GetStatus(includeSecrets: true, orchestrator, fleet));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/v1/dogfood/status", (BusHostState bus, NlForkOrchestratorHost orchestrator) =>
{
    var profile = bus.GetProfile();
    var activeForks = orchestrator.Settings.Enabled ? orchestrator.Orchestrator.ListActive().Count : 0;
    return Results.Json(new DogfoodStatus(
        bus.Sessions.IsRunning,
        profile.ForkOrchestratorEnabled,
        profile.ForkSessionId,
        activeForks,
        profile.StreamerId,
        File.Exists(NlIdentityPaths.MockOwnershipConfig)));
});

app.MapPut("/api/v1/session/profile", async (BusHostState b, HttpRequest req) =>
{
    var profile = await req.ReadFromJsonAsync<SessionProfileFile>();
    if (profile is null)
    {
        return Results.BadRequest(new { error = "Invalid profile JSON." });
    }

    b.SaveProfile(profile);
    return Results.Ok(b.GetStatus(includeSecrets: true));
});

app.MapPost("/api/v1/session/bus-defaults", (BusHostState b, string? config) =>
{
    b.LoadBusDefaults(config);
    return Results.Ok(b.GetStatus(includeSecrets: true));
});

app.MapPost("/api/v1/session/start", async (BusHostState b, NlSocialHost social, NlForkCatalogHost catalog, NlForkOrchestratorHost orchestrator, NlFleetHost fleet, HttpRequest req, CancellationToken ct) =>
{
    var body = await req.ReadFromJsonAsync<StartSessionRequest>();
    return await b.StartAsync(body?.ReplayOnce ?? false, ct, social, catalog, orchestrator, fleet);
});

app.MapPost("/api/v1/session/stop", (BusHostState b, NlForkOrchestratorHost orchestrator) => b.Stop(orchestrator));

app.MapGet("/api/v1/moderation", (ModerationHostState m) => Results.Json(m.GetStatus()));

app.MapGet("/api/v1/moderation/recent", async (ModerationHostState m, string? streamer, int? count, CancellationToken ct) =>
{
    var streamerId = string.IsNullOrWhiteSpace(streamer) ? NlPaths.DefaultStreamerId : streamer.Trim();
    var records = await m.Moderation.GetRecentActionsAsync(streamerId, count ?? 100, ct);
    return Results.Json(records);
});

app.MapGet("/api/v1/moderation/players/{playerId}/history", (ModerationHostState m, string playerId, string? streamer, bool? includeArchived) =>
{
    var streamerId = string.IsNullOrWhiteSpace(streamer) ? NlPaths.DefaultStreamerId : streamer.Trim();
    var history = m.Moderation.GetOffenseHistory(streamerId, playerId);
    if (history is null)
    {
        return Results.NotFound(new { error = $"Unknown SP '{playerId}'." });
    }

    if (includeArchived == false)
    {
        return Results.Json(new
        {
            history.StreamerId,
            history.Standing,
            history.ActiveOffenseCount,
            offenses = history.ActiveOffenses,
            activeOffenses = history.ActiveOffenses,
            archivedOffenses = Array.Empty<object>(),
        });
    }

    return Results.Json(history);
});

app.MapPost("/api/v1/moderation/profiles", (ModerationHostState m, CreateProfileRequest body) =>
{
    if (string.IsNullOrWhiteSpace(body.PlayerId))
    {
        return Results.BadRequest(new { error = "playerId required." });
    }

    var profile = m.Moderation.GetOrCreateProfile(body.PlayerId.Trim(), body.DisplayName?.Trim() ?? body.PlayerId.Trim());
    return Results.Ok(new { playerId = profile.Id, displayName = profile.DisplayName });
});

app.MapPost("/api/v1/moderation/warning", async (ModerationHostState m, ModerationActionRequest body, CancellationToken ct) =>
    await IssueModerationAsync(m, body, async (svc, s, p, by, reason, game) =>
        await svc.IssueWarningAsync(s, p, by, reason, game, ct)));

app.MapPost("/api/v1/moderation/ban", async (ModerationHostState m, ModerationActionRequest body, CancellationToken ct) =>
    await IssueModerationAsync(m, body, async (svc, s, p, by, reason, game) =>
        await svc.IssueBanAsync(s, p, by, reason, game, ct)));

app.MapPost("/api/v1/moderation/graylist", async (ModerationHostState m, ModerationActionRequest body, CancellationToken ct) =>
    await IssueModerationAsync(m, body, async (svc, s, p, by, reason, _) =>
        await svc.IssueGraylistHoldAsync(s, p, by, reason, ct)));

app.MapPost("/api/v1/moderation/clear", async (ModerationHostState m, ModerationActionRequest body, CancellationToken ct) =>
    await IssueModerationAsync(m, body, async (svc, s, p, by, reason, _) =>
        await svc.ClearStandingAsync(s, p, by, string.IsNullOrWhiteSpace(reason) ? null : reason, ct), requireReason: false));

app.MapGet("/health", (NlSecuritySettings security, NlHardeningSettings hardening, NlDemoSettings demo, BusHostState bus) =>
    Results.Json(new
    {
        status = "ok",
        service = "nl-session-server",
        uptimeSeconds = (long)NlOpsMetrics.Uptime.TotalSeconds,
        publicMode = security.PublicMode,
        hardening = hardening.Enabled,
        demoMode = demo.Enabled,
        sessionRunning = bus.Sessions.IsRunning,
    }));

app.MapGet("/api/v1/ops/status", (
    NlHardeningSettings hardening,
    NlPublicRateLimitService rateLimits,
    NlDemoSettings demo,
    NlSpectatorSettings spectator,
    BusHostState bus,
    NlForkOrchestratorHost orchestrator,
    NlFleetHost fleet) =>
{
    var wsGuard = NlWebSocketConnectionGuard.Current;
    var activeForks = orchestrator.Settings.Enabled ? orchestrator.Orchestrator.ListActive().Count : 0;
    var activeNls = bus.Sessions.IsRunning ? 1 : 0;
    var fleetObs = fleet.Settings.Enabled
        ? fleet.Metrics.BuildSnapshot(activeForks, activeNls)
        : null;
    var slos = fleetObs is not null
        ? fleet.Slo.Evaluate(fleetObs, loadTest: null, fleet.Metrics, fleet.Incidents)
        : null;
    var warm = fleet.Settings.Enabled
        ? fleet.Autoscale.Evaluate(activeForks, activeNls > 0, null)
        : null;

    return Results.Json(new
    {
        uptime = NlOpsMetrics.UptimePayload(),
        hardening = hardening.ToPublicInfo(),
        rateLimits = rateLimits.GetMetrics(),
        webSocket = wsGuard?.GetMetrics(),
        demo = demo.ToPublicInfo(bus.Sessions.IsRunning, bus.Sessions.DecisionCount, bus.GetProfile().ConfigPath),
        spectator = new { triggersEnabled = spectator.TriggersEnabled, triggerRatePerMinute = spectator.TriggerRatePerMinute },
        session = new { state = bus.Sessions.State.ToString(), decisions = bus.Sessions.DecisionCount },
        fleet = fleet.Settings.Enabled
            ? new
            {
                observability = fleetObs,
                slos,
                autoscale = warm,
                incidents = fleet.Incidents.ListRecent(10),
            }
            : null,
    });
});

app.MapGet("/api/v1/demo/status", (NlDemoSettings demo, BusHostState b) =>
    Results.Json(demo.ToPublicInfo(
        b.Sessions.IsRunning,
        b.Sessions.DecisionCount,
        b.GetProfile().ConfigPath)));

app.MapGet("/api/v1/fork/status", () =>
{
    var path = Environment.GetEnvironmentVariable("NL_FORK_STATUS") ?? NlPaths.ForkStatus;
    if (!File.Exists(path))
    {
        return Results.Json(new { connected = false, message = "No fork runtime status file yet." });
    }

    try
    {
        var json = File.ReadAllText(path);
        return Results.Content(json, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { connected = false, error = ex.Message });
    }
});

app.MapGet("/api/v1/spectator/status", (NlSpectatorService spectator, BusHostState b, NlDemoSettings demo) =>
    Results.Json(spectator.BuildStatus(
        b.Sessions.State,
        b.Sessions.IsRunning,
        b.Sessions.DecisionCount,
        demo.Enabled,
        b.GetProfile())));

app.MapGet("/api/v1/spectator/scenarios", (NlSpectatorService spectator) =>
    Results.Json(spectator.ListScenarios()));

app.MapGet("/api/v1/spectator/decisions", async (
    NlSpectatorService spectator,
    ModerationHostState moderation,
    BusHostState bus,
    string? streamer,
    string? since,
    int? count,
    CancellationToken ct) =>
{
    var streamerId = string.IsNullOrWhiteSpace(streamer) ? bus.GetProfile().StreamerId : streamer.Trim();
    DateTimeOffset? sinceUtc = DateTimeOffset.TryParse(since, out var parsed) ? parsed : null;
    var decisions = await spectator.GetDecisionsAsync(moderation, streamerId, sinceUtc, count, ct);
    return Results.Json(new { decisions });
});

app.MapPost("/api/v1/spectator/trigger", async (
    NlSpectatorService spectator,
    BusHostState bus,
    SpectatorTriggerRequest body,
    HttpContext ctx,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(body.ScenarioId))
    {
        return Results.BadRequest(new { error = "scenarioId required." });
    }

    var clientKey = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var result = await spectator.TriggerScenarioAsync(
        body.ScenarioId.Trim(),
        clientKey,
        bus.Sessions.IsRunning,
        bus.BindHost,
        bus.WsPort,
        bus.BusToken,
        ct);

    return Results.Json(result.Body, statusCode: result.StatusCode);
});

app.MapGet("/api/v1/editor/vocabulary", () => Results.Json(NlEditorVocabulary.ToPublicInfo()));

app.MapGet("/api/v1/editor/config", (NlWebEditorStore store, BusHostState bus) =>
{
    var profile = bus.GetProfile();
    var snap = store.Load(profile.ConfigPath);
    return Results.Json(new
    {
        model = snap.Model,
        nleText = snap.NleText,
        sourcePath = snap.SourcePath,
        isSandbox = snap.IsSandbox,
        sessionUsesSandbox = store.IsSandboxPath(profile.ConfigPath),
        sessionRunning = bus.Sessions.IsRunning,
    });
});

app.MapPut("/api/v1/editor/config", async (NlWebEditorStore store, HttpRequest req) =>
{
    var model = await req.ReadFromJsonAsync<ConfigModel>();
    if (model is null)
    {
        return Results.BadRequest(new { error = "Invalid config model JSON." });
    }

    try
    {
        var saved = store.Save(model);
        return Results.Ok(new { ok = true, nleText = saved.NleText, sourcePath = saved.SourcePath });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/v1/editor/evaluate", async (HttpRequest req) =>
{
    var body = await req.ReadFromJsonAsync<EditorEvaluateRequest>();
    if (body is null || string.IsNullOrWhiteSpace(body.EventName))
    {
        return Results.BadRequest(new { error = "eventName required." });
    }

    var result = NleEditorEvaluate.Evaluate(new NleEvaluateRequest(
        body.EventName,
        body.Properties,
        body.Model,
        body.NleText));

    if (!result.ParseOk)
    {
        return Results.BadRequest(new { error = result.Error, decision = result.Decision });
    }

    return Results.Json(new
    {
        decision = result.Decision,
        message = result.Message,
        allow = result.Decision.Equals("Allow", StringComparison.OrdinalIgnoreCase),
    });
});

app.MapPost("/api/v1/editor/apply", async (
    NlWebEditorStore store,
    BusHostState bus,
    NlSocialHost social,
    NlForkCatalogHost catalog,
    NlForkOrchestratorHost orchestrator,
    EditorApplyRequest? body,
    CancellationToken ct) =>
{
    if (!store.SandboxExists())
    {
        return Results.BadRequest(new { error = "Save rules to the sandbox first." });
    }

    var profile = bus.GetProfile();
    profile.ConfigPath = store.SandboxPath;
    bus.SaveProfile(profile);

    if (body?.RestartSession == false)
    {
        return Results.Ok(new
        {
            ok = true,
            configPath = store.SandboxPath,
            sessionRunning = bus.Sessions.IsRunning,
            restarted = false,
        });
    }

    if (bus.Sessions.IsRunning)
    {
        bus.Stop(orchestrator);
        await bus.WaitForIdleAsync(ct);
    }

    var start = await bus.StartAsync(replayOnce: false, ct, social, catalog, orchestrator);
    return start;
});

app.MapPost("/api/v1/editor/reset", (NlWebEditorStore store, NlDemoSettings demo) =>
{
    var template = demo.Enabled ? demo.ConfigFileName : "demo.nle";
    store.ResetFromTemplate(template);
    var snap = store.Load(null);
    return Results.Ok(new
    {
        ok = true,
        template,
        model = snap.Model,
        nleText = snap.NleText,
    });
});

var manifest = bus.GetManifest(orchestratorHost, fleetHost);
Console.WriteLine($"NL Session Server      → {manifest.HttpBaseUrl}");
Console.WriteLine($"Bridge (remote)        → {manifest.BridgeConnectUrl}");
Console.WriteLine($"Join admission         → {manifest.AdmitUrl}");
Console.WriteLine($"Moderation console     → {manifest.ModerationUrl}");
Console.WriteLine($"Public mode            → {security.PublicMode}");
Console.WriteLine($"Demo loop (Phase G)    → {demoSettings.Enabled}");
Console.WriteLine($"Spectator UX (Phase H) → triggers={spectatorSettings.TriggersEnabled}, rate={spectatorSettings.TriggerRatePerMinute}/min");
Console.WriteLine($"Hardening (Phase K)    → {hardeningSettings.Enabled} (admit={hardeningSettings.AdmitRatePerMinute}/min, ws max={hardeningSettings.WebSocketMaxConnections})");
Console.WriteLine($"Social gate (Phase M)  → {socialSettings.Enabled} mode={socialSettings.Mode}");
Console.WriteLine($"Fork catalog (Phase N) → {catalogSettings.Enabled} manifest={NlForkCatalogPaths.Manifest}");
Console.WriteLine($"Fork orchestrator (O)  → {orchestratorSettings.Enabled} mode={orchestratorSettings.Mode} provisioner={orchestratorHost.ResolveProvisionerKind()}");
Console.WriteLine($"Identity (Phase L)       → {identitySettings.Enabled} mode={identitySettings.Mode} steamOpenId=/identity-link.html");
Console.WriteLine($"Partnership (Phase Q)  → {partnershipSettings.Enabled} gate={partnershipSettings.RequireGateAtAdmit}");
Console.WriteLine($"NL Client (Phase R)    → /nl-client.html + NL.Client CLI");
Console.WriteLine($"Fleet ops (Phase S)    → {fleetSettings.Enabled} /fleet-ops.html max={fleetSettings.Autoscale.MaxConcurrentSessions}");
Console.WriteLine($"Public beta (Phase 5)  → {fleetHost.BetaSettings.Enabled} /beta.html waitlist={(fleetHost.BetaSettings.WaitlistOpen ? "open" : "closed")}");
Console.WriteLine($"Web editor (Phase I)   → /editor.html + /api/v1/editor/*");
if (demoSettings.Enabled)
{
    Console.WriteLine($"Demo config            → {demoSettings.ConfigFileName}");
    Console.WriteLine($"Demo reset interval    → {(demoSettings.ResetInterval.TotalMinutes > 0 ? $"{demoSettings.ResetInterval.TotalMinutes} min" : "startup only")}");
}
Console.WriteLine($"Operator auth          → {(security.RequireOperatorAuth ? "required" : "off (local dev)")}");
if (security.RequireOperatorAuth)
{
    Console.WriteLine($"Bus token              → {(string.IsNullOrEmpty(security.BusToken) ? busToken : "<configured>")}");
}
else
{
    Console.WriteLine($"Bus token              → {busToken}");
}

app.Run();

static BetaValidationReport BuildBetaValidationReport(
    NlFleetHost fleet,
    NlForkOrchestratorHost orchestrator,
    BusHostState bus,
    NlIdentitySettings identity,
    NlSecuritySettings security)
{
    var activeForks = orchestrator.Settings.Enabled ? orchestrator.Orchestrator.ListActive().Count : 0;
    var activeNls = bus.Sessions.IsRunning ? 1 : 0;
    var snap = fleet.Metrics.BuildSnapshot(activeForks, activeNls);
    var production = fleet.Validation.Evaluate(
        fleet.Settings,
        orchestrator.Settings.Mode.ToString(),
        snap,
        fleet.Metrics,
        fleet.Incidents,
        fleet.ValidationStore.GetLast()?.LastLoadTest);
    return fleet.BetaValidation.Evaluate(
        fleet.BetaSettings,
        !string.IsNullOrEmpty(security.OperatorKey),
        security.PublicMode,
        identity.Mode.ToString(),
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STEAM_WEB_API_KEY")),
        production.ProductionReady);
}

static GaValidationReport BuildGaValidationReport(
    NlFleetHost fleet,
    NlForkOrchestratorHost orchestrator,
    BusHostState bus,
    NlIdentitySettings identity,
    NlSecuritySettings security,
    NlForkCatalogHost catalog)
{
    var activeForks = orchestrator.Settings.Enabled ? orchestrator.Orchestrator.ListActive().Count : 0;
    var activeNls = bus.Sessions.IsRunning ? 1 : 0;
    var snap = fleet.Metrics.BuildSnapshot(activeForks, activeNls);
    var production = fleet.Validation.Evaluate(
        fleet.Settings,
        orchestrator.Settings.Mode.ToString(),
        snap,
        fleet.Metrics,
        fleet.Incidents,
        fleet.ValidationStore.GetLast()?.LastLoadTest);
    var activeGameIds = catalog.Settings.Enabled
        ? catalog.Catalog.ListGames()
            .Select(e => e.GameId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
        : (IReadOnlyList<string>)[];
    var catalogCheck = fleet.GaCatalog.Evaluate(catalog.Settings.Enabled, activeGameIds, fleet.GaSettings);
    var productionSlos = fleet.Slo.EvaluateProduction(
        snap,
        fleet.ValidationStore.GetLast()?.LastLoadTest,
        fleet.Metrics,
        fleet.Incidents);
    return fleet.GaValidation.Evaluate(
        fleet.GaSettings,
        fleet.BetaSettings,
        !string.IsNullOrEmpty(security.OperatorKey),
        security.PublicMode,
        identity.Mode.ToString(),
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STEAM_WEB_API_KEY")),
        production.ProductionReady,
        catalog.Settings.Enabled,
        catalogCheck,
        fleet.Compliance.RetentionPolicy,
        productionSlos);
}

static LiveProductionValidationReport BuildLiveProductionValidationReport(
    NlFleetHost fleet,
    NlForkOrchestratorHost orchestrator,
    BusHostState bus,
    NlIdentitySettings identity,
    NlSecuritySettings security,
    NlForkCatalogHost catalog)
{
    var activeForks = orchestrator.Settings.Enabled ? orchestrator.Orchestrator.ListActive().Count : 0;
    var activeNls = bus.Sessions.IsRunning ? 1 : 0;
    var snap = fleet.Metrics.BuildSnapshot(activeForks, activeNls);
    var production = fleet.Validation.Evaluate(
        fleet.Settings,
        orchestrator.Settings.Mode.ToString(),
        snap,
        fleet.Metrics,
        fleet.Incidents,
        fleet.ValidationStore.GetLast()?.LastLoadTest);
    var activeGameIds = catalog.Settings.Enabled
        ? catalog.Catalog.ListGames()
            .Select(e => e.GameId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
        : (IReadOnlyList<string>)[];
    var catalogCheck = fleet.GaCatalog.Evaluate(catalog.Settings.Enabled, activeGameIds, fleet.GaSettings);
    return fleet.LiveProductionValidation.Evaluate(
        fleet.LiveProductionSettings,
        fleet.GaSettings,
        fleet.BetaSettings,
        !string.IsNullOrEmpty(security.OperatorKey),
        security.PublicMode,
        identity.Mode.ToString(),
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STEAM_WEB_API_KEY")),
        production.ProductionReady,
        identity.PublicBaseUrl ?? Environment.GetEnvironmentVariable("NL_PUBLIC_BASE_URL"),
        fleet.Settings.Relay.RelayWebSocketTemplate,
        fleet.Settings.Relay.TurnUri,
        catalog.Settings.Enabled,
        catalogCheck,
        fleet.Compliance.RetentionPolicy);
}

static MultiGameValidationReport BuildMultiGameValidationReport(
    NlFleetHost fleet,
    NlForkOrchestratorHost orchestrator,
    BusHostState bus,
    NlIdentitySettings identity,
    NlSecuritySettings security,
    NlForkCatalogHost catalog,
    NlPartnershipHost partnership,
    MultiGameValidationRunRequest? body)
{
    var activeForks = orchestrator.Settings.Enabled ? orchestrator.Orchestrator.ListActive().Count : 0;
    var activeNls = bus.Sessions.IsRunning ? 1 : 0;
    var snap = fleet.Metrics.BuildSnapshot(activeForks, activeNls);
    var liveReport = fleet.LiveProductionValidation.Evaluate(
        fleet.LiveProductionSettings,
        fleet.GaSettings,
        fleet.BetaSettings,
        !string.IsNullOrEmpty(security.OperatorKey),
        security.PublicMode,
        identity.Mode.ToString(),
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STEAM_WEB_API_KEY")),
        fleet.Validation.Evaluate(
            fleet.Settings,
            orchestrator.Settings.Mode.ToString(),
            snap,
            fleet.Metrics,
            fleet.Incidents,
            fleet.ValidationStore.GetLast()?.LastLoadTest).ProductionReady,
        identity.PublicBaseUrl ?? Environment.GetEnvironmentVariable("NL_PUBLIC_BASE_URL"),
        fleet.Settings.Relay.RelayWebSocketTemplate,
        fleet.Settings.Relay.TurnUri,
        catalog.Settings.Enabled,
        fleet.GaCatalog.Evaluate(
            catalog.Settings.Enabled,
            catalog.Catalog.ListGames()
                .Select(e => e.GameId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            fleet.GaSettings),
        fleet.Compliance.RetentionPolicy);

    var catalogGames = fleet.MultiGameSettings.RequiredGameIds
        .Select(gameId =>
        {
            var stable = catalog.Catalog.ResolveLatestStableEntry(gameId);
            return (gameId, stable?.DockerImage, stable?.MajorVersion);
        })
        .ToList();
    var catalogCheck = fleet.MultiGameCatalog.Evaluate(catalog.Settings.Enabled, catalogGames, fleet.MultiGameSettings);

    return fleet.MultiGameValidation.Evaluate(
        fleet.MultiGameSettings,
        fleet.LiveProductionSettings,
        fleet.GaSettings,
        catalog.Settings.Enabled,
        catalogCheck,
        liveReport.LiveProductionPassed,
        partnership.Settings.Enabled,
        partnership.Settings.RequireGateAtAdmit,
        body?.HostImagesVerified ?? false,
        body?.VerifiedGameIds);
}

static string ResolveIdentityPublicBase(HttpContext ctx, NlIdentitySettings settings)
{
    if (!string.IsNullOrWhiteSpace(settings.PublicBaseUrl))
    {
        return settings.PublicBaseUrl.TrimEnd('/');
    }

    var req = ctx.Request;
    return $"{req.Scheme}://{req.Host}";
}

static string ResolveSamplesRoot()
{
    var overrideRoot = Environment.GetEnvironmentVariable("NL_SAMPLES_ROOT");
    if (!string.IsNullOrWhiteSpace(overrideRoot) && Directory.Exists(overrideRoot))
    {
        return Path.GetFullPath(overrideRoot);
    }

    var dir = AppContext.BaseDirectory;
    for (var i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
    {
        var candidate = Path.Combine(dir, "samples");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        dir = Directory.GetParent(dir)?.FullName ?? "";
    }

    return Path.Combine(Directory.GetCurrentDirectory(), "samples");
}

static async Task<IResult> IssueModerationAsync(
    ModerationHostState host,
    ModerationActionRequest body,
    Func<ModerationService, string, string, string, string, string?, Task> action,
    bool requireReason = true)
{
    if (string.IsNullOrWhiteSpace(body.PlayerId))
    {
        return Results.BadRequest(new { error = "playerId required." });
    }

    if (requireReason && string.IsNullOrWhiteSpace(body.Reason))
    {
        return Results.BadRequest(new { error = "reason required." });
    }

    var streamerId = string.IsNullOrWhiteSpace(body.StreamerId) ? NlPaths.DefaultStreamerId : body.StreamerId.Trim();
    var issuedBy = string.IsNullOrWhiteSpace(body.IssuedBy) ? "mod-web" : body.IssuedBy.Trim();
    var reason = body.Reason?.Trim() ?? "";

    try
    {
        await action(host.Moderation, streamerId, body.PlayerId.Trim(), issuedBy, reason, body.Game);
        return Results.Ok(new { ok = true });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}

static NlClientHost CreateClientHost(
    BusHostState bus,
    ModerationHostState moderation,
    NlIdentityHost identity,
    NlSocialHost social,
    NlForkCatalogHost catalog,
    NlPartnershipHost partnership,
    NlForkOrchestratorHost orchestrator,
    NlFleetHost fleet)
{
    NlSessionManifestDto MapManifest()
    {
        var m = bus.GetManifest(orchestrator, fleet);
        return new NlSessionManifestDto(
            m.SessionId,
            m.StreamerId,
            m.HttpBaseUrl,
            m.BridgeConnectUrl,
            m.AdmitUrl,
            m.ForkConnectEndpoint,
            m.PartnershipTier,
            m.RequiresAtOwnRiskAcknowledgment,
            m.SessionRunning,
            m.GameId,
            m.CatalogMajorVersion);
    }

    return new NlClientHost(
        bus.GetProfile,
        () => bus.Sessions.IsRunning,
        MapManifest,
        req => bus.AdmitAsync(req, identity, social, catalog, partnership),
        (playerId, gameId) =>
        {
            var entry = catalog.Catalog.ListGames(true).FirstOrDefault(e =>
                string.Equals(e.GameId, gameId, StringComparison.OrdinalIgnoreCase));
            var tier = entry?.Tier ?? PartnershipTier.AtOwnRisk;
            partnership.Gate.RecordAcknowledgment(playerId, gameId, tier);
            return Task.FromResult(true);
        },
        (playerId, streamerId) =>
        {
            var profile = moderation.Moderation.GetOrCreateProfile(playerId, playerId);
            return Task.FromResult<NlClientOverlayState?>(NlClientOverlayBuilder.Build(profile, streamerId));
        },
        social);
}

internal sealed class StartSessionRequest
{
    public bool ReplayOnce { get; set; }
}

internal sealed class EditorEvaluateRequest
{
    public string? EventName { get; set; }
    public Dictionary<string, double>? Properties { get; set; }
    public ConfigModel? Model { get; set; }
    public string? NleText { get; set; }
}

internal sealed class EditorApplyRequest
{
    public bool RestartSession { get; set; } = true;
}

internal sealed class CreateIdentityAccountRequest
{
    public string? DisplayName { get; set; }
}

internal sealed class LinkPlatformRequest
{
    public string? AccountId { get; set; }
    public string? Platform { get; set; }
    public string? ExternalUserId { get; set; }
    public string? RefreshToken { get; set; }
}

internal sealed class SocialLinkRequest
{
    public string? PlayerId { get; set; }
    public string? TwitchUserId { get; set; }
    public string? YouTubeChannelId { get; set; }
    public string? KickUserId { get; set; }
    public string? DiscordUserId { get; set; }
}

internal sealed class CatalogSelectRequest
{
    public string? GameId { get; set; }
    public string? MajorVersion { get; set; }
    public List<string>? ModIds { get; set; }
    public bool? EnableOrchestrator { get; set; }
}

internal sealed class ForkOrchestratorCreateRequest
{
    public string? StreamerId { get; set; }
    public string? GameId { get; set; }
    public string? MajorVersion { get; set; }
    public string? NlePath { get; set; }
    public List<string>? ModIds { get; set; }
    public string? DockerImage { get; set; }
    public int? ReservedPrivilegedSlots { get; set; }
    public string? PreferredRegion { get; set; }
    public int? TwitchFollowers { get; set; }
}

internal sealed class FleetLoadTestReportRequest
{
    public int ConcurrentSessionsTarget { get; set; } = 100;
    public int AdmitsPerSecondTarget { get; set; } = 10;
    public int AdmitsSucceeded { get; set; }
    public int AdmitsFailed { get; set; }
    public double ElapsedSeconds { get; set; }
    public int ActiveForkSessions { get; set; }
    public int ActiveNlsSessions { get; set; }
    public double ForkCreateP99Ms { get; set; }
}

internal sealed class PartnershipAcknowledgeRequest
{
    public string? PlayerId { get; set; }
    public string? GameId { get; set; }
}

internal sealed class PublisherTitleStatusRequest
{
    public PublisherTitleStatus Status { get; set; } = PublisherTitleStatus.OptedIn;
}

internal sealed class PartnershipOwnershipTokenRequest
{
    public string? PlatformUserId { get; set; }
    public string? GameId { get; set; }
    public string? AppId { get; set; }
    public string? Platform { get; set; }
}

internal sealed class NlClientBlockInviteRequest
{
    public string? InviteUrl { get; set; }
    public string? ExpectedHost { get; set; }
}

internal sealed class BetaWaitlistSignupRequest
{
    public string? DisplayName { get; set; }
    public string? Contact { get; set; }
    public string? TwitchHandle { get; set; }
    public string? RequestedGameId { get; set; }
}

internal sealed class BetaWaitlistApproveRequest
{
    public string? StreamerId { get; set; }
}

internal sealed class GaStreamerRegisterRequest
{
    public string? DisplayName { get; set; }
    public string? Contact { get; set; }
    public string? TwitchHandle { get; set; }
    public string? PreferredGameId { get; set; }
    public string? StreamerId { get; set; }
}

internal sealed class MultiGameValidationRunRequest
{
    public bool HostImagesVerified { get; set; }
    public IReadOnlyList<string>? VerifiedGameIds { get; set; }
}
