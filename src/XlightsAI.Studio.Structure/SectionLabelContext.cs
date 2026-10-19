using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// The already-built, already-approved-up-to-this-point analysis inputs an
/// <see cref="ISectionLabelClassifier"/> run is computed from -- Step 053's own
/// <see cref="SectionBoundaryAnalysis"/> (which segment boundaries exist) plus Step 052's own
/// <see cref="SongFingerprintSet"/> (the per-bar evidence each segment's classification is scored from).
/// </summary>
/// <param name="SongId">The song every supplied analysis input must share.</param>
/// <param name="SourceHash">The source audio hash both <see cref="BoundaryAnalysis"/> and <see cref="FingerprintSet"/> must themselves have been built from -- "missing media" is an empty value here.</param>
/// <param name="BoundaryAnalysis">The approved segment boundaries this run classifies.</param>
/// <param name="FingerprintSet">The approved, already-computed per-bar fingerprints this run's evidence (repetition, position) is scored from.</param>
public sealed record SectionLabelContext(
    SongId SongId,
    string SourceHash,
    SectionBoundaryAnalysis BoundaryAnalysis,
    SongFingerprintSet FingerprintSet);
