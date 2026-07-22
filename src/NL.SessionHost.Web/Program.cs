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
builder.Services.AddNlWebSecurity(security);

var app = builder.Build();
app.UseCors();
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

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "nl-session-server" }));

var manifest = bus.GetManifest();
Console.WriteLine($"NL Session Server      → {manifest.HttpBaseUrl}");
Console.WriteLine($"Bridge (remote)        → {manifest.BridgeConnectUrl}");
Console.WriteLine($"Join admission         → {manifest.AdmitUrl}");
Console.WriteLine($"Moderation console     → {manifest.ModerationUrl}");
Console.WriteLine($"Public mode            → {security.PublicMode}");
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
