using NL.Moderation;
using NL.Server;

namespace NL.SessionHost.Web;

/// <summary>
/// Phase G — auto-start a demo session, keep it running, and periodically reset moderation data.
/// </summary>
public sealed class NlDemoHostedService : BackgroundService
{
    private readonly BusHostState _bus;
    private readonly ModerationHostState _moderation;
    private readonly NlDemoSettings _settings;
    private readonly ILogger<NlDemoHostedService> _log;
    private readonly SemaphoreSlim _cycleLock = new(1, 1);

    public NlDemoHostedService(
        BusHostState bus,
        ModerationHostState moderation,
        NlDemoSettings settings,
        ILogger<NlDemoHostedService> log)
    {
        _bus = bus;
        _moderation = moderation;
        _settings = settings;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            return;
        }

        if (_settings.StartupDelay > TimeSpan.Zero)
        {
            await Task.Delay(_settings.StartupDelay, stoppingToken);
        }

        await RunCycleAsync(restartSession: false, stoppingToken);

        if (_settings.ResetInterval <= TimeSpan.Zero)
        {
            _log.LogInformation("NL demo mode: periodic reset disabled (interval=0).");
            return;
        }

        using var timer = new PeriodicTimer(_settings.ResetInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCycleAsync(restartSession: true, stoppingToken);
        }
    }

    private async Task RunCycleAsync(bool restartSession, CancellationToken cancellationToken)
    {
        await _cycleLock.WaitAsync(cancellationToken);
        try
        {
            if (restartSession)
            {
                _log.LogInformation("NL demo reset: stopping session …");
                _bus.Stop();
                await _bus.WaitForIdleAsync(cancellationToken);
            }

            _log.LogInformation("NL demo reset: clearing moderation + SP data …");
            NlDemoReset.ResetAndReload(_moderation);

            _bus.ApplyDemoProfile(_settings.ConfigFileName);

            if (_bus.Sessions.IsRunning)
            {
                _log.LogInformation("NL demo: session already running.");
                return;
            }

            _log.LogInformation("NL demo: starting session with {Config} …", _settings.ConfigFileName);
            await _bus.StartAsync(replayOnce: false, cancellationToken);
            _log.LogInformation("NL demo: session running (decisions will appear as the bridge emits events).");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "NL demo cycle failed.");
        }
        finally
        {
            _cycleLock.Release();
        }
    }
}
