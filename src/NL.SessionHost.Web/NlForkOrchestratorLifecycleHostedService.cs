using NL.Fork.Orchestrator;
using NL.Social;
using NL.Social.Core;

namespace NL.SessionHost.Web;

/// <summary>
/// Phase O — grace destroy, max session duration, idle detection, stream-end teardown.
/// </summary>
public sealed class NlForkOrchestratorLifecycleHostedService : BackgroundService
{
    private readonly BusHostState _bus;
    private readonly NlForkOrchestratorHost _orchestrator;
    private readonly NlSocialHost? _social;
    private readonly ILogger<NlForkOrchestratorLifecycleHostedService> _log;

    public NlForkOrchestratorLifecycleHostedService(
        BusHostState bus,
        NlForkOrchestratorHost orchestrator,
        NlSocialHost? social,
        ILogger<NlForkOrchestratorLifecycleHostedService> log)
    {
        _bus = bus;
        _orchestrator = orchestrator;
        _social = social;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_orchestrator.Settings.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await _orchestrator.Orchestrator.TickLifecycleAsync(stoppingToken);
                await CheckStreamEndedAsync(stoppingToken);
                await CheckNlsStoppedAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Fork orchestrator lifecycle tick failed.");
            }
        }
    }

    private async Task CheckStreamEndedAsync(CancellationToken cancellationToken)
    {
        var profile = _bus.GetProfile();
        if (!profile.RequireLiveStream || !_bus.Sessions.IsRunning || _social?.Settings.Enabled != true)
        {
            return;
        }

        var streamerId = string.IsNullOrWhiteSpace(profile.StreamerId)
            ? NL.Core.NlPaths.DefaultStreamerId
            : profile.StreamerId;
        var config = _social.Gate.GetStreamerConfig(streamerId);
        var live = await _social.LiveMonitor.GetStatusAsync(config, cancellationToken);
        if (!live.IsLive && _orchestrator.Orchestrator.GetActiveForStreamer(streamerId) is not null)
        {
            _log.LogInformation("Stream ended — scheduling fork grace destroy for {StreamerId}.", streamerId);
            await _orchestrator.Orchestrator.ScheduleGraceDestroyForStreamerAsync(streamerId, cancellationToken);
        }
    }

    private async Task CheckNlsStoppedAsync(CancellationToken cancellationToken)
    {
        if (_bus.Sessions.IsRunning)
        {
            return;
        }

        var profile = _bus.GetProfile();
        if (!profile.ForkOrchestratorEnabled)
        {
            return;
        }

        var streamerId = string.IsNullOrWhiteSpace(profile.StreamerId)
            ? NL.Core.NlPaths.DefaultStreamerId
            : profile.StreamerId;
        if (_orchestrator.Orchestrator.GetActiveForStreamer(streamerId) is null)
        {
            return;
        }

        await _orchestrator.Orchestrator.ScheduleGraceDestroyForStreamerAsync(streamerId, cancellationToken);
    }
}
