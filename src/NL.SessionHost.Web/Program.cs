using NL.Core;
using NL.Server;
using NL.Server.Core.Integration;
using NL.SessionHost.Web;

var bindHost = Environment.GetEnvironmentVariable("NL_BIND") ?? "127.0.0.1";
var httpPort = int.Parse(Environment.GetEnvironmentVariable("NL_HTTP_PORT") ?? NlSessionBusDefaults.HttpPort.ToString());
var wsPort = int.Parse(Environment.GetEnvironmentVariable("NL_WS_PORT") ?? NlSessionBusDefaults.WebSocketPort.ToString());
var busToken = Environment.GetEnvironmentVariable("NL_BUS_TOKEN") ?? Guid.NewGuid().ToString("N");

NlPaths.EnsureRoot();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://{bindHost}:{httpPort}");

var bus = new BusHostState(bindHost, httpPort, wsPort, busToken);
if (File.Exists(NlPaths.SessionProfile))
{
    bus.SaveProfile(NlSessionRunner.LoadProfile(NlPaths.SessionProfile));
}

builder.Services.AddSingleton(bus);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/v1/bus", (BusHostState b) => Results.Json(b.BusInfo));
app.MapGet("/api/v1/session", (BusHostState b) => Results.Json(b.GetStatus()));

app.MapPut("/api/v1/session/profile", async (BusHostState b, HttpRequest req) =>
{
    var profile = await req.ReadFromJsonAsync<SessionProfileFile>();
    if (profile is null)
    {
        return Results.BadRequest(new { error = "Invalid profile JSON." });
    }

    b.SaveProfile(profile);
    return Results.Ok(b.GetStatus());
});

app.MapPost("/api/v1/session/bus-defaults", (BusHostState b, string? config) =>
{
    b.LoadBusDefaults(config);
    return Results.Ok(b.GetStatus());
});

app.MapPost("/api/v1/session/start", async (BusHostState b, HttpRequest req, CancellationToken ct) =>
{
    var body = await req.ReadFromJsonAsync<StartSessionRequest>();
    return await b.StartAsync(body?.ReplayOnce ?? false, ct);
});

app.MapPost("/api/v1/session/stop", (BusHostState b) => b.Stop());

Console.WriteLine($"NL Session Host (web) → http://{bindHost}:{httpPort}");
Console.WriteLine($"Bridge WebSocket      → {bus.BusInfo.BridgeConnectUrl}");
Console.WriteLine($"Bus token             → {busToken}");

app.Run();

internal sealed class StartSessionRequest
{
    public bool ReplayOnce { get; set; }
}
