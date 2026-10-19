using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// The already-built, already-approved-up-to-this-point analysis input an
/// <see cref="ISectionBoundaryDetector"/> run is computed from -- Step 052's own
/// <see cref="SongFingerprintSet"/> plus the <c>BeatGrid</c> provenance it was partitioned from
/// (carried here only to cross-check provenance; bar windows themselves are already final per-bar
/// data on <see cref="FingerprintSet"/>).
/// </summary>
/// <param name="SongId">The song every supplied analysis input must share.</param>
/// <param name="SourceHash">The source audio hash <see cref="FingerprintSet"/> must itself have been built from -- "missing media" is an empty value here.</param>
/// <param name="FingerprintSet">The approved, already-computed per-bar fingerprints this run detects novelty/boundaries against.</param>
public sealed record SectionBoundaryContext(
    SongId SongId,
    string SourceHash,
    SongFingerprintSet FingerprintSet);
