namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Section 3's own proposed signature, implemented for real by Step 040: one derived,
/// separated audio source written to disk. <see cref="SourceOriginTicks"/> is carried over verbatim
/// from the source <c>SongAsset.OriginTicks</c> it was separated from -- Section 2's "preserve exact
/// OriginalOriginTicks" rule -- so a stem can always be checked for alignment against the original
/// without re-deriving it.
/// </summary>
/// <param name="Id">Stable identity for this one derived stem file.</param>
/// <param name="Kind">Which instrument/role this stem is declared to represent.</param>
/// <param name="FilePath">Absolute path to the written stem audio file (IEEE-float WAV; see <see cref="StemWavWriter"/>).</param>
/// <param name="SampleRate">The stem's sample rate in Hz -- always equal to the source song's own sample rate for Step 040 (no resampling is performed; see <see cref="StemSeparationRecipe"/>).</param>
/// <param name="SampleCount">Total PCM sample frames written -- must equal the source song's own <c>SampleCount</c> for a whole-clip, non-chunked separation pass (Step 040's own "audio sync assertion").</param>
/// <param name="SourceOriginTicks">Copied verbatim from the source <c>SongAsset.OriginTicks</c> this stem was derived from.</param>
/// <param name="Sha256">SHA-256 of the written stem file's bytes, lower-case hex.</param>
public sealed record StemAsset(
    Guid Id,
    StemKind Kind,
    string FilePath,
    int SampleRate,
    long SampleCount,
    long SourceOriginTicks,
    string Sha256);
