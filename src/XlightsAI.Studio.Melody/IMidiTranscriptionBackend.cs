namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Step 048's own title, verbatim: "Support MIDI import and evaluate transcription backend behind
/// an interface." Section 3's own proposed <c>IMelodyAnalyzer</c> takes a <c>StemAsset</c> (audio) source,
/// which a trusted MIDI file is not -- this is this step's own narrower, disclosed sibling contract for
/// the "trustworthy MIDI" rung of Section 3's own fallback ladder ("trustworthy MIDI -&gt; high-confidence
/// isolated-stem note events -&gt; pitch contour direction and onset -&gt; rhythm-only events"), evaluated
/// (exercised against real/synthetic fixtures; see <c>Step048AcceptanceTests</c>) behind this interface so
/// a future caller can select or swap the concrete MIDI backend without depending on
/// <see cref="StandardMidiFileTranscriptionBackend"/> directly -- "create adapter to same track"
/// (implementation guidance, verbatim): every implementation returns the exact same canonical
/// <see cref="MelodyTrack"/> shape a pitch-tracking-derived track would.
/// </summary>
public interface IMidiTranscriptionBackend
{
    /// <summary>
    /// Parses <paramref name="midiData"/> as a trusted Standard MIDI File and returns a fully validated,
    /// gesture-classified <see cref="MelodyTrack"/> for its one selected melody channel (Section 3's own
    /// phase-wide polyphony deferral -- Step 051's later scope). Throws
    /// <see cref="MidiImportInvalidException"/> for any invalid input (malformed chunk structure,
    /// unsupported time division, invalid settings, or a selected channel with no note events) -- never
    /// silently repairs or returns a partial track.
    /// </summary>
    Task<MelodyTrack> ImportAsync(byte[] midiData, string sourceAssetHash, MidiImportSettings settings, CancellationToken ct);
}
