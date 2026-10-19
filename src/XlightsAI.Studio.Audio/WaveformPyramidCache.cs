using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Audio;

/// <summary>
/// PHASE-04 Step 026 — the persisted, versioned, zoom-independent multiresolution waveform peaks
/// cache (Section 4: "every cache key includes algorithm/model/settings versions"). This step's own
/// first definition of what <c>IWaveformBuilder.BuildPyramidAsync</c>'s returned path actually
/// contains — see <c>implementation-evidence/PHASE-04/STEP-026.md</c> §3.
/// </summary>
/// <param name="SongId">The <see cref="SongAsset.Id"/> this pyramid was built from.</param>
/// <param name="ContentHash">The source <see cref="SongAsset.Sha256"/> at build time — the cache-key content-identity half: a changed song (re-probed from different/edited bytes) never silently serves a stale pyramid built from the old content.</param>
/// <param name="SourceDecoderVersion">The <see cref="SongAsset.DecoderVersion"/> at build time — a decoder upgrade invalidates previously built pyramids rather than silently mixing old/new decode behavior.</param>
/// <param name="BuilderVersion">Identifies which builder (and its version) produced this pyramid — mirrors <see cref="SongAsset.DecoderVersion"/>'s own cache-key role, applied to the pyramid-building algorithm itself.</param>
/// <param name="SourceSampleRate">The <see cref="SongAsset.SampleRate"/> this pyramid's sample indices are expressed in.</param>
/// <param name="SampleCount">The total decoded sample count this pyramid covers; every <see cref="WaveformResolutionLevel"/>'s envelopes must contiguously cover exactly <c>[0, SampleCount)</c>.</param>
/// <param name="Resolutions">One entry per pyramid zoom level, ordered from finest (smallest <see cref="WaveformResolutionLevel.BlockSizeSamples"/>) to coarsest.</param>
public sealed record WaveformPyramidCache(
    SongId SongId,
    string ContentHash,
    string SourceDecoderVersion,
    string BuilderVersion,
    int SourceSampleRate,
    long SampleCount,
    WaveformResolutionLevel[] Resolutions);
