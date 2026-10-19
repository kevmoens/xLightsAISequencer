namespace XlightsAI.Studio.Melody;

/// <summary>
/// Thrown by <see cref="StandardMidiFileTranscriptionBackend"/> when raw bytes cannot be parsed or
/// adapted as a trusted Standard MIDI File -- this step's own named failure/edge case, verbatim:
/// "Invalid input... must be handled explicitly." Covers a missing/incorrect <c>MThd</c>/<c>MTrk</c> chunk
/// signature, a truncated chunk (declared length longer than the remaining bytes), an unsupported SMPTE
/// (negative) time division, invalid settings, and a requested/auto-selected melody channel with no note
/// events at all. Mirrors <see cref="MelodyTrackInvalidException"/>'s own "rejected before any persistence"
/// precedent -- never thrown after a partial write.
/// </summary>
public sealed class MidiImportInvalidException : Exception
{
    public MidiImportInvalidException(string reason) : base(reason)
    {
        Reason = reason;
    }

    public string Reason { get; }
}
