using NL.Core;
using NL.Core.Security;
using NL.Moderation;
using NL.Moderation.Core;
using NL.Server;
using NL.Server.Core.Integration;
using NL.Server.Core.Security;
using NL.SessionHost.Web;
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

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://{bindHost}:{httpPort}");

var bus = new BusHostState(bindHost, httpPort, wsPort, busToken, modPort);
var moderation = new ModerationHostState(moderationLog, spStore);
if (File.Exists(NlPaths.SessionProfile))
{
    bus.SaveProfile(NlSessionRunner.LoadProfile(NlPaths.SessionProfile));
}

builder.Services.AddSingleton(bus);
builder.Services.AddSingleton(moderation);
builder.Services.AddSingleton(demoSettings);
builder.Services.AddSingleton(spectatorSettings);
builder.Services.AddSingleton(hardeningSettings);
builder.Services.AddSingleton(new NlPublicRateLimitService(hardeningSettings));
builder.Services.AddSingleton(new NlSpectatorService(spectatorSettings));
builder.Services.AddNlWebSecurity(security);
if (demoSettings.Enabled)
{
    builder.Services.AddHostedService<NlDemoHostedService>();
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

app.MapGet("/api/v1/session/manifest", (BusHostState b, HttpContext ctx) =>
    Results.Json(NlSecurityRedaction.RedactManifest(b.GetManifest(), NlWebSecurityExtensions.IsAuthorized(ctx))));

app.MapGet("/api/v1/session", (BusHostState b, HttpContext ctx) =>
    Results.Json(b.GetStatus(NlWebSecurityExtensions.IsAuthorized(ctx))));

app.MapPost("/api/v1/session/admit", (BusHostState b, NlAdmitPlayerRequest body) =>
{
    if (string.IsNullOrWhiteSpace(body.PlayerId))
    {
        return Results.BadRequest(new { error = "playerId required." });
    }

    try
    {
        return Results.Json(b.Admit(body));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
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

app.MapPost("/api/v1/session/start", async (BusHostState b, HttpRequest req, CancellationToken ct) =>
{
    var body = await req.ReadFromJsonAsync<StartSessionRequest>();
    return await b.StartAsync(body?.ReplayOnce ?? false, ct);
});

app.MapPost("/api/v1/session/stop", (BusHostState b) => b.Stop());

app.MapGet("/api/v1/moderation", (ModerationHostState m) => Results.Json(m.GetStatus()));

app.MapGet("/api/v1/moderation/recent", async (ModerationHostState m, string? streamer, int? count, CancellationToken ct) =>
{
    var streamerId = string.IsNullOrWhiteSpace(streamer) ? NlPaths.DefaultStreamerId : streamer.Trim();
    var records = await m.Moderation.GetRecentActionsAsync(streamerId, count ?? 100, ct);
    return Results.Json(records);
});

app.MapGet("/api/v1/moderation/players/{playerId}/history", (ModerationHostState m, string playerId, string? streamer) =>
{
    var streamerId = string.IsNullOrWhiteSpace(streamer) ? NlPaths.DefaultStreamerId : streamer.Trim();
    var history = m.Moderation.GetOffenseHistory(streamerId, playerId);
    return history is null ? Results.NotFound(new { error = $"Unknown SP '{playerId}'." }) : Results.Json(history);
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
    BusHostState bus) =>
{
    var wsGuard = NlWebSocketConnectionGuard.Current;
    return Results.Json(new
    {
        uptime = NlOpsMetrics.UptimePayload(),
        hardening = hardening.ToPublicInfo(),
        rateLimits = rateLimits.GetMetrics(),
        webSocket = wsGuard?.GetMetrics(),
        demo = demo.ToPublicInfo(bus.Sessions.IsRunning, bus.Sessions.DecisionCount, bus.GetProfile().ConfigPath),
        spectator = new { triggersEnabled = spectator.TriggersEnabled, triggerRatePerMinute = spectator.TriggerRatePerMinute },
        session = new { state = bus.Sessions.State.ToString(), decisions = bus.Sessions.DecisionCount },
    });
});

app.MapGet("/api/v1/demo/status", (NlDemoSettings demo, BusHostState b) =>
    Results.Json(demo.ToPublicInfo(
        b.Sessions.IsRunning,
        b.Sessions.DecisionCount,
        b.GetProfile().ConfigPath)));

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

var manifest = bus.GetManifest();
Console.WriteLine($"NL Session Server      → {manifest.HttpBaseUrl}");
Console.WriteLine($"Bridge (remote)        → {manifest.BridgeConnectUrl}");
Console.WriteLine($"Join admission         → {manifest.AdmitUrl}");
Console.WriteLine($"Moderation console     → {manifest.ModerationUrl}");
Console.WriteLine($"Public mode            → {security.PublicMode}");
Console.WriteLine($"Demo loop (Phase G)    → {demoSettings.Enabled}");
Console.WriteLine($"Spectator UX (Phase H) → triggers={spectatorSettings.TriggersEnabled}, rate={spectatorSettings.TriggerRatePerMinute}/min");
Console.WriteLine($"Hardening (Phase K)    → {hardeningSettings.Enabled} (admit={hardeningSettings.AdmitRatePerMinute}/min, ws max={hardeningSettings.WebSocketMaxConnections})");
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

internal sealed class StartSessionRequest
{
    public bool ReplayOnce { get; set; }
}
