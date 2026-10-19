namespace XlightsAI.Studio.Audio;

/// <summary>
/// Thrown when no usable audio output device can be opened — "missing output device handled"
/// (Step 025 required acceptance). Always carries the underlying winmm <c>MMRESULT</c> code so a
/// caller/log can distinguish "no driver installed" from "device already in use" from "unsupported
/// format" rather than collapsing every failure into one generic message.
/// </summary>
public sealed class AudioOutputDeviceUnavailableException : Exception
{
    public AudioOutputDeviceUnavailableException(string reason, int mmResultCode)
        : base($"No audio output device is available: {reason} (winmm result code {mmResultCode}).")
    {
        Reason = reason;
        MmResultCode = mmResultCode;
    }

    /// <summary>Human-readable explanation of <see cref="MmResultCode"/>.</summary>
    public string Reason { get; }

    /// <summary>The raw winmm <c>MMRESULT</c> code that caused this failure.</summary>
    public int MmResultCode { get; }
}
