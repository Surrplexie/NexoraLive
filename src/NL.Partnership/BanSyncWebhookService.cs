using NL.Partnership.Core;

namespace NL.Partnership;

public sealed class BanSyncWebhookService
{
    private readonly IPublisherBanStore _bans;
    private readonly JsonlPartnershipAuditStore _audit;

    public BanSyncWebhookService(IPublisherBanStore bans, JsonlPartnershipAuditStore audit)
    {
        _bans = bans;
        _audit = audit;
    }

    public void Apply(BanSyncWebhookRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GameId) || string.IsNullOrWhiteSpace(request.PlatformUserId))
        {
            throw new ArgumentException("gameId and platformUserId required.");
        }

        var action = request.Action.Trim().ToLowerInvariant();
        if (action is "ban" or "add")
        {
            _bans.Ban(new PublisherBanEntry(
                request.GameId.Trim(),
                request.PlatformUserId.Trim(),
                request.Reason ?? "Publisher ban sync",
                DateTimeOffset.UtcNow,
                request.ExpiresAtUtc,
                request.PublisherId));
            _audit.Append("ban_sync", request);
            return;
        }

        if (action is "unban" or "remove")
        {
            _bans.Unban(request.GameId.Trim(), request.PlatformUserId.Trim());
            _audit.Append("unban_sync", request);
            return;
        }

        throw new ArgumentException($"Unknown ban sync action '{request.Action}'.");
    }
}

public sealed class JsonlPartnershipAuditStore
{
    private readonly string _path;
    private readonly object _lock = new();

    public JsonlPartnershipAuditStore(string? path = null) =>
        _path = path ?? NlPartnershipPaths.Audit;

    public void Append(string action, object payload)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var line = System.Text.Json.JsonSerializer.Serialize(new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            action,
            payload,
        });

        lock (_lock)
        {
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }
}
