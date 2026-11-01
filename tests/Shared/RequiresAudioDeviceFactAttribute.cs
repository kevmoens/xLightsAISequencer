using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Tests.Shared;

/// <summary>
/// A <see cref="FactAttribute"/> for tests that drive a real audio output device. They run on developer
/// machines and are skipped (not failed) on headless machines such as CI runners that have no sound card.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresAudioDeviceFactAttribute : FactAttribute
{
    private static readonly bool DeviceAvailable = AudioOutputProbe.IsOutputDeviceAvailable();

    public RequiresAudioDeviceFactAttribute()
    {
        if (!DeviceAvailable)
        {
            Skip = "No audio output device is available on this machine.";
        }
    }
}
