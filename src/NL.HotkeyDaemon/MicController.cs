using NAudio.CoreAudioApi;

namespace NL.HotkeyDaemon;

/// <summary>
/// Real (not simulated) microphone control via the Windows Core Audio API, wrapped by NAudio.
/// Targets the default communications recording device, i.e. whatever Windows currently uses
/// for voice chat/mic input - the same device most streaming software defaults to.
/// </summary>
internal sealed class MicController
{
    /// <summary>Flips mute state and returns whether the mic is now muted.</summary>
    public bool ToggleMute()
    {
        using var device = GetDefaultRecordingDevice();
        device.AudioEndpointVolume.Mute = !device.AudioEndpointVolume.Mute;
        return device.AudioEndpointVolume.Mute;
    }

    public bool IsMuted()
    {
        using var device = GetDefaultRecordingDevice();
        return device.AudioEndpointVolume.Mute;
    }

    private static MMDevice GetDefaultRecordingDevice()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
    }
}
