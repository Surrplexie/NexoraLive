using NL.Core;
using NL.Core.Security;
using NL.Moderation;
using NL.Moderation.Core;
using NL.Web.Shared;

var security = NlSecuritySettings.LoadFromEnvironment();
var bindHost = security.BindHost;
var httpPort = int.Parse(Environment.GetEnvironmentVariable("NL_MOD_HTTP_PORT") ?? "27030");
var moderationLog = Environment.GetEnvironmentVariable("NL_MODERATION_LOG");
var spStore = Environment.GetEnvironmentVariable("NL_SP_STORE");

NlPaths.EnsureRoot();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://{bindHost}:{httpPort}");

var host = new ModerationHostState(moderationLog, spStore);
builder.Services.AddSingleton(host);
builder.Services.AddNlWebSecurity(security);

var app = builder.Build();
app.UseCors();
app.UseNlOperatorAuth();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/v1/security", (NlSecuritySettings s) => Results.Json(s.ToPublicInfo()));
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "moderation" }));
app.MapGet("/api/v1/moderation", (ModerationHostState h) => Results.Json(h.GetStatus()));

app.MapGet("/api/v1/moderation/recent", async (ModerationHostState h, string? streamer, int? count, CancellationToken ct) =>
{
    var streamerId = string.IsNullOrWhiteSpace(streamer) ? NlPaths.DefaultStreamerId : streamer.Trim();
    var records = await h.Moderation.GetRecentActionsAsync(streamerId, count ?? 100, ct);
    return Results.Json(records);
});

app.MapGet("/api/v1/moderation/players/{playerId}/history", (ModerationHostState h, string playerId, string? streamer) =>
{
    var streamerId = string.IsNullOrWhiteSpace(streamer) ? NlPaths.DefaultStreamerId : streamer.Trim();
    var history = h.Moderation.GetOffenseHistory(streamerId, playerId);
    return history is null ? Results.NotFound(new { error = $"Unknown SP '{playerId}'." }) : Results.Json(history);
});

app.MapPost("/api/v1/moderation/profiles", (ModerationHostState h, CreateProfileRequest body) =>
{
    if (string.IsNullOrWhiteSpace(body.PlayerId))
    {
        return Results.BadRequest(new { error = "playerId required." });
    }

    var profile = h.Moderation.GetOrCreateProfile(body.PlayerId.Trim(), body.DisplayName?.Trim() ?? body.PlayerId.Trim());
    return Results.Ok(new { playerId = profile.Id, displayName = profile.DisplayName });
});

app.MapPost("/api/v1/moderation/warning", async (ModerationHostState h, ModerationActionRequest body, CancellationToken ct) =>
    await IssueAsync(h, body, async (svc, s, p, by, reason, game) =>
        await svc.IssueWarningAsync(s, p, by, reason, game, ct)));

app.MapPost("/api/v1/moderation/ban", async (ModerationHostState h, ModerationActionRequest body, CancellationToken ct) =>
    await IssueAsync(h, body, async (svc, s, p, by, reason, game) =>
        await svc.IssueBanAsync(s, p, by, reason, game, ct)));

app.MapPost("/api/v1/moderation/graylist", async (ModerationHostState h, ModerationActionRequest body, CancellationToken ct) =>
    await IssueAsync(h, body, async (svc, s, p, by, reason, _) =>
        await svc.IssueGraylistHoldAsync(s, p, by, reason, ct)));

app.MapPost("/api/v1/moderation/clear", async (ModerationHostState h, ModerationActionRequest body, CancellationToken ct) =>
    await IssueAsync(h, body, async (svc, s, p, by, reason, _) =>
        await svc.ClearStandingAsync(s, p, by, string.IsNullOrWhiteSpace(reason) ? null : reason, ct), requireReason: false));

Console.WriteLine($"NL Moderation Console (web) → http://{bindHost}:{httpPort}");
Console.WriteLine($"Data root                  → {NlPaths.Root}");
Console.WriteLine($"Public mode                → {security.PublicMode}");
Console.WriteLine($"Operator auth              → {(security.RequireOperatorAuth ? "required" : "off (local dev)")}");

app.Run();

static async Task<IResult> IssueAsync(
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
