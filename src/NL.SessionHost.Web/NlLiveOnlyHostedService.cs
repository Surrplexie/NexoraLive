using NL.Social;
using NL.Social.Core;

namespace NL.SessionHost.Web;

/// <summary>
/// Phase M — poll live stream status and auto-stop NLS when the streamer goes offline.
/// </summary>
public sealed class NlLiveOnlyHostedService : BackgroundService
{
    private readonly BusHostState _bus;
    private readonly NlSocialHost _social;
    private readonly ILogger<NlLiveOnlyHostedService> _log;

    public NlLiveOnlyHostedService(
        BusHostState bus,
        NlSocialHost social,
        ILogger<NlLiveOnlyHostedService> log)
    {
        _bus = bus;
        _social = social;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_social.Settings.Enabled || _social.Settings.Mode == NlSocialMode.Off)
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(_social.Settings.LiveCheckIntervalSeconds);
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CheckLiveAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Live-only session check failed.");
            }
        }
    }

    private async Task CheckLiveAsync(CancellationToken cancellationToken)
    {
        var profile = _bus.GetProfile();
        if (!profile.RequireLiveStream || !_bus.Sessions.IsRunning)
        {
            return;
        }

        var streamerId = string.IsNullOrWhiteSpace(profile.StreamerId)
            ? NL.Core.NlPaths.DefaultStreamerId
            : profile.StreamerId;
        var config = _social.Gate.GetStreamerConfig(streamerId);
        var live = await _social.LiveMonitor.GetStatusAsync(config, cancellationToken);
        _social.Cache.SetLive(streamerId, live);

        if (!live.IsLive)
        {
            _log.LogInformation(
                "Streamer '{StreamerId}' is offline — stopping live-only NLS session.",
                streamerId);
            _bus.Stop();
        }
    }
}
