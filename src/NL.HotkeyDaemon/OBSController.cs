using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NL.HotkeyDaemon;

/// <summary>
/// Fires a SaveReplayBuffer request to a running OBS instance over the OBS WebSocket v5
/// protocol (built into OBS 28+). Uses only built-in .NET APIs — no extra NuGet package.
///
/// Error handling is deliberately graceful: if OBS isn't running, isn't reachable, has a
/// wrong password, or the replay buffer isn't active, this returns a descriptive string
/// for the tray notification rather than crashing the daemon.
/// </summary>
internal static class OBSController
{
    public static async Task<string> SaveReplayAsync(OBSConfig cfg, CancellationToken cancellationToken = default)
    {
        using var ws = new ClientWebSocket();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            var uri = new Uri($"ws://{cfg.Host}:{cfg.Port}");
            await ws.ConnectAsync(uri, cts.Token).ConfigureAwait(false);

            // Step 1: receive Hello
            var hello = await ReceiveJsonAsync(ws, cts.Token).ConfigureAwait(false);
            var opCode = hello.RootElement.GetProperty("op").GetInt32();
            if (opCode != 0)
            {
                return "OBS: unexpected initial message (not Hello)";
            }

            // Step 2: build Identify (with auth if required)
            var identifyData = BuildIdentifyPayload(hello, cfg.Password);
            await SendJsonAsync(ws, identifyData, cts.Token).ConfigureAwait(false);

            // Step 3: receive Identified (op 2)
            var identified = await ReceiveJsonAsync(ws, cts.Token).ConfigureAwait(false);
            var identifiedOp = identified.RootElement.GetProperty("op").GetInt32();
            if (identifiedOp != 2)
            {
                return "OBS: authentication failed or unexpected response";
            }

            // Step 4: send SaveReplayBuffer request (op 6)
            var request = new
            {
                op = 6,
                d = new
                {
                    requestType = "SaveReplayBuffer",
                    requestId = Guid.NewGuid().ToString("N")[..8],
                    requestData = new { },
                },
            };
            await SendJsonAsync(ws, request, cts.Token).ConfigureAwait(false);

            // Step 5: receive RequestResponse (op 7)
            var response = await ReceiveJsonAsync(ws, cts.Token).ConfigureAwait(false);
            var responseOp = response.RootElement.GetProperty("op").GetInt32();
            if (responseOp != 7)
            {
                return "OBS: unexpected response to SaveReplayBuffer";
            }

            var resultStatus = response.RootElement
                .GetProperty("d")
                .GetProperty("requestStatus");

            var result = resultStatus.GetProperty("result").GetBoolean();
            if (!result)
            {
                var code = resultStatus.GetProperty("code").GetInt32();
                return code == 703
                    ? "OBS: replay buffer is not active (start it in OBS first)"
                    : $"OBS: SaveReplayBuffer failed (code {code})";
            }

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None)
                .ConfigureAwait(false);
            return "Clip saved";
        }
        catch (OperationCanceledException)
        {
            return "OBS: timed out (is OBS running with WebSocket enabled?)";
        }
        catch (Exception ex)
        {
            return $"OBS: {ex.Message}";
        }
    }

    private static object BuildIdentifyPayload(JsonDocument hello, string password)
    {
        string? authentication = null;

        if (hello.RootElement.TryGetProperty("d", out var helloData)
            && helloData.TryGetProperty("authentication", out var authInfo)
            && !string.IsNullOrEmpty(password))
        {
            var challenge = authInfo.GetProperty("challenge").GetString() ?? "";
            var salt = authInfo.GetProperty("salt").GetString() ?? "";
            authentication = ComputeAuth(password, salt, challenge);
        }

        return new
        {
            op = 1,
            d = new
            {
                rpcVersion = 1,
                authentication,
                eventSubscriptions = 0,
            },
        };
    }

    /// <summary>OBS WebSocket v5 auth: Base64(SHA256(Base64(SHA256(password + salt)) + challenge))</summary>
    private static string ComputeAuth(string password, string salt, string challenge)
    {
        var step1Input = Encoding.UTF8.GetBytes(password + salt);
        var step1Hash = SHA256.HashData(step1Input);
        var step1B64 = Convert.ToBase64String(step1Hash);

        var step2Input = Encoding.UTF8.GetBytes(step1B64 + challenge);
        var step2Hash = SHA256.HashData(step2Input);
        return Convert.ToBase64String(step2Hash);
    }

    private static async Task SendJsonAsync(ClientWebSocket ws, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
    }

    private static async Task<JsonDocument> ReceiveJsonAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new ArraySegment<byte>(new byte[4096]);
        using var ms = new System.IO.MemoryStream();

        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);
            ms.Write(buffer.Array!, buffer.Offset, result.Count);
        }
        while (!result.EndOfMessage);

        ms.Seek(0, System.IO.SeekOrigin.Begin);
        return await JsonDocument.ParseAsync(ms, cancellationToken: ct).ConfigureAwait(false);
    }
}

/// <summary>Loaded from the obs.json sidecar file next to the .nle config.</summary>
public sealed class OBSConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 4455;
    public string Password { get; set; } = "";

    public static OBSConfig LoadFromFileOrDefault(string sceFilePath)
    {
        var dir = Path.GetDirectoryName(sceFilePath) ?? ".";
        var obsPath = Path.Combine(dir, "obs.json");

        if (!File.Exists(obsPath))
        {
            return new OBSConfig();
        }

        try
        {
            var json = File.ReadAllText(obsPath);
            return System.Text.Json.JsonSerializer.Deserialize<OBSConfig>(json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new OBSConfig();
        }
        catch
        {
            return new OBSConfig();
        }
    }
}
