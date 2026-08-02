namespace NL.Social.Core;

public interface ILiveStreamMonitor
{
    Task<LiveStreamStatus> GetStatusAsync(
        StreamerSocialConfig config,
        CancellationToken cancellationToken = default);
}
