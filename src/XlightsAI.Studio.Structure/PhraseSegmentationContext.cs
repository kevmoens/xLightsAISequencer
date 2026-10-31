using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// The already-built, already-approved-up-to-this-point analysis inputs an <see cref="IPhraseSegmenter"/>
/// run is computed from -- Step 054's own <see cref="SectionLabelAnalysis"/> (which sections exist and
/// their bar ranges), Step 052's own <see cref="SongFingerprintSet"/> (the per-bar evidence each phrase's
/// <see cref="PhraseCandidate.SignatureHash"/> is computed from) and PHASE-07's own optional
/// <see cref="MelodyTrack"/> (the melodic-cadence evidence source; <see langword="null"/> when no melody
/// track has been analyzed/approved yet for this song -- cadence snapping is then simply unavailable and
/// every internal boundary falls back to the bar-based hypothesis, never an error).
/// </summary>
/// <param name="SongId">The song every supplied analysis input must share.</param>
/// <param name="SourceHash">The source audio hash <see cref="LabelAnalysis"/> and <see cref="FingerprintSet"/> must themselves have been built from -- "missing media" is an empty value here.</param>
/// <param name="LabelAnalysis">The approved section labels this run segments into phrases.</param>
/// <param name="FingerprintSet">The approved, already-computed per-bar fingerprints this run's phrase signatures are computed from.</param>
/// <param name="MelodyTrack">The approved melody track (if any) this run's melodic-cadence evidence is computed from.</param>
/// <param name="MelodySourceAssetHash">
/// The hash <see cref="MelodyTrack"/>'s own <c>SourceAssetHash</c> is expected to equal -- required
/// (non-empty) whenever <see cref="MelodyTrack"/> is supplied, ignored otherwise. Deliberately a
/// separate value from <see cref="SourceHash"/>: a real <see cref="MelodyTrack"/> is routinely derived
/// from an isolated lead *stem* (see <see cref="StructureAnalysisRunner"/>'s own doc comment), whose hash
/// is legitimately never the full song's own hash -- that is the stem's correct cache-key identity, not
/// a mismatch to paper over by comparing it to <see cref="SourceHash"/> instead. Threading the real
/// expected hash through here (rather than fabricating a match, or giving up and passing a
/// <see langword="null"/> <see cref="MelodyTrack"/>) is what lets cadence-aware phrasing actually run
/// against a real, stem-derived melody.
/// </param>
public sealed record PhraseSegmentationContext(
    SongId SongId,
    string SourceHash,
    SectionLabelAnalysis LabelAnalysis,
    SongFingerprintSet FingerprintSet,
    MelodyTrack? MelodyTrack,
    string? MelodySourceAssetHash = null);
