using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Step 042 — the one place a <see cref="StemAsset"/> is turned into a playable/waveform-able
/// <see cref="SongAsset"/>. A plain <c>IAudioDecoder.ProbeAsync</c> call alone is not enough: it always
/// mints a fresh random <see cref="XlightsAI.Studio.Contracts.SongId"/> and always sets
/// <c>OriginTicks = 0</c> (Section 2's "preserve exact OriginalOriginTicks" rule would be silently lost
/// otherwise). This factory re-probes the stem's real on-disk file (so a moved/corrupted/stale stem
/// file is still caught, exactly like any other audio import), cross-checks the probe against the
/// stem's own already-declared metadata, and then overrides <c>Id</c>/<c>OriginTicks</c> so repeated
/// calls for the same stem resolve to the same deterministic <see cref="SongAsset.Id"/> (needed so
/// <see cref="WaveformPyramidBuilder"/>'s own cache-by-SongId reopens instead of rebuilding every call)
/// while keeping the stem's real, preserved source-origin alignment visible.
/// </summary>
public static class StemSongAssetFactory
{
    /// <exception cref="FileNotFoundException"><paramref name="stem"/>'s file no longer exists.</exception>
    /// <exception cref="StemMixdownIncompatibleStemsException">The file's actual, re-probed contents no longer match <paramref name="stem"/>'s own declared sample rate, sample count or hash (stale/corrupt stem).</exception>
    public static async Task<SongAsset> ProbeAsync(StemAsset stem, IAudioDecoder decoder, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stem);
        ArgumentNullException.ThrowIfNull(decoder);

        var probed = await decoder.ProbeAsync(stem.FilePath, ct).ConfigureAwait(false);

        if (probed.SampleRate != stem.SampleRate || probed.SampleCount != stem.SampleCount || probed.Sha256 != stem.Sha256)
        {
            throw new StemMixdownIncompatibleStemsException(
                $"Stem '{stem.Id}' at '{stem.FilePath}' no longer matches its own declared manifest metadata " +
                $"(declared: {stem.SampleRate} Hz / {stem.SampleCount} samples / {stem.Sha256}; " +
                $"actual: {probed.SampleRate} Hz / {probed.SampleCount} samples / {probed.Sha256}) -- stale or corrupt stem file.");
        }

        return probed with
        {
            Id = new SongId(stem.Id),
            OriginTicks = stem.SourceOriginTicks,
        };
    }
}
