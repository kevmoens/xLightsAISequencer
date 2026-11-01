namespace XlightsAI.Studio.Audio;

/// <summary>Picks the real playback backend for the current OS: winmm on Windows, OpenAL on macOS, otherwise a typed "unsupported" stub.</summary>
public static class AudioTransportFactory
{
    public static IAudioTransport Create(IAudioDecoder decoder)
    {
        if (OperatingSystem.IsWindows())
        {
            return new WaveOutAudioTransport(decoder);
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacOsAudioTransport(decoder);
        }

        return new UnsupportedPlatformAudioTransport();
    }
}
