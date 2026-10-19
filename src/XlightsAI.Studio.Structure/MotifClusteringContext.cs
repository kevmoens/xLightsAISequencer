using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// The already-built, already-approved-up-to-this-point analysis inputs an <see cref="IMotifClusterer"/>
/// run is computed from -- Step 055's own <see cref="PhraseSegmentationAnalysis"/> (which phrases exist and
/// their bar ranges) and Step 052's own <see cref="SongFingerprintSet"/> (the per-bar evidence every
/// phrase's comparable embedding is resampled from). This step never re-derives phrase boundaries or
/// section labels; it only compares already-approved phrases against each other.
/// </summary>
/// <param name="SongId">The song every supplied analysis input must share.</param>
/// <param name="SourceHash">The source audio hash <see cref="PhraseAnalysis"/> and <see cref="FingerprintSet"/> must themselves have been built from -- "missing media" is an empty value here.</param>
/// <param name="PhraseAnalysis">The approved, already-segmented phrases this run clusters into motif groups.</param>
/// <param name="FingerprintSet">The approved, already-computed per-bar fingerprints this run's phrase embeddings are resampled from.</param>
public sealed record MotifClusteringContext(
    SongId SongId,
    string SourceHash,
    PhraseSegmentationAnalysis PhraseAnalysis,
    SongFingerprintSet FingerprintSet);
