using NL.Fork.Orchestrator;
using NL.Fork.Orchestrator.Core;
using NL.Fleet;
using NL.Fleet.Core;
using NL.Social;
using NL.Social.Core;

namespace NL.SessionHost.Web;

/// <summary>Phase S — autoscale tick, fork health incidents, moderation retention.</summary>
public sealed class NlFleetLifecycleHostedService : BackgroundService
{
    private readonly NlFleetHost _fleet;
    private readonly NlForkOrchestratorHost? _orchestrator;
    private readonly BusHostState _bus;
    private readonly NlSocialHost? _social;
    private readonly ILogger<NlFleetLifecycleHostedService> _log;
    private DateTimeOffset _idleSinceUtc = DateTimeOffset.UtcNow;

    public NlFleetLifecycleHostedService(
        NlFleetHost fleet,
        BusHostState bus,
        NlForkOrchestratorHost? orchestrator,
        NlSocialHost? social,
        ILogger<NlFleetLifecycleHostedService> log)
    {
        _fleet = fleet;
        _bus = bus;
        _orchestrator = orchestrator;
        _social = social;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_fleet.Settings.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Fleet lifecycle tick failed.");
            }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        var activeForks = _orchestrator?.Settings.Enabled == true
            ? _orchestrator.Orchestrator.ListActive().Count
            : 0;
        var nlsRunning = _bus.Sessions.IsRunning ? 1 : 0;

        if (nlsRunning > 0 || activeForks > 0)
        {
            _idleSinceUtc = DateTimeOffset.MinValue;
        }
        else if (_idleSinceUtc == DateTimeOffset.MinValue)
        {
            _idleSinceUtc = DateTimeOffset.UtcNow;
        }

        var anyLive = await AnyLiveStreamAsync(cancellationToken);
        var warm = _fleet.Autoscale.Evaluate(activeForks, anyLive || nlsRunning > 0, _idleSinceUtc == DateTimeOffset.MinValue ? null : _idleSinceUtc);
        if (warm.ScaleToZeroEligible)
        {
            _log.LogInformation("Fleet autoscale: scale-to-zero eligible (no live streams).");
        }

        if (_orchestrator?.Settings.Enabled == true)
        {
            await CheckForkHealthAsync(cancellationToken);
        }

        _fleet.Compliance.ApplyModerationRetention(NL.Core.NlPaths.ModerationLog);
    }

    private async Task<bool> AnyLiveStreamAsync(CancellationToken cancellationToken)
    {
        if (_social?.Settings.Enabled != true)
        {
            return _bus.Sessions.IsRunning;
        }

        var profile = _bus.GetProfile();
        var streamerId = string.IsNullOrWhiteSpace(profile.StreamerId)
            ? NL.Core.NlPaths.DefaultStreamerId
            : profile.StreamerId;
        var config = _social.Gate.GetStreamerConfig(streamerId);
        var live = await _social.LiveMonitor.GetStatusAsync(config, cancellationToken);
        return live.IsLive;
    }

    private async Task CheckForkHealthAsync(CancellationToken cancellationToken)
    {
        foreach (var session in _orchestrator!.Orchestrator.ListActive())
        {
            if (session.State != ForkSessionState.Running)
            {
                continue;
            }

            var statusPath = Path.Combine(session.WorkspacePath, "fork-status.json");
            var healthy = true;
            if (File.Exists(statusPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(statusPath, cancellationToken);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("connected", out var c))
                    {
                        healthy = c.GetBoolean();
                    }
                }
                catch
                {
                    healthy = false;
                }
            }

            _fleet.Metrics.RecordSessionSample(new FleetSessionMetricSample(
                session.SessionId,
                session.StreamerId,
                _bus.GetProfile().FleetPlacedRegionId ?? Environment.GetEnvironmentVariable("NL_FLEET_DEFAULT_REGION") ?? "us-east",
                _bus.Sessions.DecisionCount,
                0,
                healthy,
                DateTimeOffset.UtcNow));

            if (!healthy && session.IdleSinceUtc is { } idle
                && DateTimeOffset.UtcNow - idle > TimeSpan.FromMinutes(5))
            {
                var incident = _fleet.Runbook.RecordUnhealthyFork(session.SessionId, session.StreamerId);
                _log.LogWarning("Fleet incident {Id}: {Message}", incident.IncidentId, incident.Message);
                await _orchestrator.Orchestrator.DestroySessionAsync(session.SessionId, cancellationToken);
                var profile = _bus.GetProfile();
                if (File.Exists(profile.ConfigPath))
                {
                    var recreated = await _bus.ProvisionForkSessionAsync(
                        profile,
                        _orchestrator,
                        _fleet,
                        twitchFollowers: null,
                        cancellationToken);
                    if (recreated.Success)
                    {
                        profile.ForkSessionId = recreated.SessionId;
                        profile.FleetPlacedRegionId = recreated.RegionId;
                        _bus.SaveProfile(profile);
                    }
                }
            }
        }
    }
}
