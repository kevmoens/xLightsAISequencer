namespace XlightsAI.Studio.Audio;

/// <summary>
/// Thrown when a file cannot be probed as valid, supported audio — "Invalid/corrupt files fail
/// clearly" (Step 024 required acceptance). Always thrown by inspecting the file's actual
/// contents (magic numbers/chunk structure), never by filename/extension alone (Step 024
/// implementation guidance: "Validate extension and decoder support by probing contents, not
/// only filename").
/// </summary>
public sealed class AudioProbeInvalidException : Exception
{
    public AudioProbeInvalidException(string path, string reason)
        : base($"'{path}' is not a valid/supported audio file: {reason}")
    {
        Path = path;
        Reason = reason;
    }

    public string Path { get; }

    public string Reason { get; }
}
