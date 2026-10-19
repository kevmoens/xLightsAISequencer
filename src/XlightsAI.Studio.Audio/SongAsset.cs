using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Audio;

/// <summary>
/// PHASE-04 Step 024 — canonical, immutable, hash-addressed identity of one imported local audio
/// file (Section 3's proposed signature, extended per this step's own "record decoder version,
/// duration and original audio origin/priming" implementation guidance — see
/// <c>implementation-evidence/PHASE-04/STEP-24.md</c> §3 for the disclosed extension beyond the
/// spec's bare proposed shape). Contains no Avalonia types and performs no I/O itself.
/// </summary>
/// <param name="Id">Stable identity for this probed import, independent of <see cref="FilePath"/> (a file can be re-probed after being moved).</param>
/// <param name="FilePath">The original local path this asset was probed from. Never mutated after a successful probe; "audio file moved after import" is a disclosed known risk (Section 7), not silently tolerated here.</param>
/// <param name="Sha256">SHA-256 of the full file's bytes, lower-case hex — the content-identity half of "hash-addressed" (Section 2).</param>
/// <param name="SampleCount">Total decodable PCM sample frames (not raw bytes) in the file's audio data, after <see cref="EncoderDelaySamples"/> priming is accounted for.</param>
/// <param name="SampleRate">The file's own native sample rate in Hz, as declared by its container/codec — never a resampled/target rate.</param>
/// <param name="Channels">Channel count as declared by the container (1 = mono, 2 = stereo, ...).</param>
/// <param name="OriginTicks">The canonical monotonic-clock tick (100 ns units, Section 2) that this song's sample index 0 maps to. Always 0 for a freshly probed file; a later step may re-origin a song against a shared project timeline.</param>
/// <param name="BitsPerSample">Container-declared bit depth of each PCM sample (8/16/24/32).</param>
/// <param name="EncoderDelaySamples">Decoder/encoder priming ("the original audio origin/priming" this step's acceptance names): sample frames at the start of the decoded stream that exist only as codec priming and are not part of the audible signal. Always 0 for the plain PCM WAV container this step's decoder supports; preserved as a named field so a later MP3/AAC-capable decoder can populate it without changing this record's shape.</param>
/// <param name="DecoderVersion">Identifies which decoder (and its version) produced this probe, so a cache or later re-probe can detect a decoder upgrade — Section 4's "every cache key includes algorithm/model/settings versions" rule, applied to probing.</param>
public sealed record SongAsset(
    SongId Id,
    string FilePath,
    string Sha256,
    long SampleCount,
    int SampleRate,
    int Channels,
    long OriginTicks,
    int BitsPerSample,
    long EncoderDelaySamples,
    string DecoderVersion)
{
    /// <summary>100 ns ticks/s, the canonical unit conversion factor (Section 2: <see cref="TimelineTick"/>).</summary>
    private const long TicksPerSecond = 10_000_000L;

    /// <summary>
    /// Playable duration in canonical 100 ns ticks, derived from <see cref="SampleCount"/>/<see cref="SampleRate"/>
    /// — never stored redundantly, so it can never itself drift out of sync with the sample data it describes.
    /// </summary>
    public long DurationTicks => SampleRate <= 0
        ? 0
        : (long)Math.Round(SampleCount * (TicksPerSecond / (double)SampleRate));
}
