namespace XlightsAI.Studio.Audio;

/// <summary>
/// PHASE-04 Step 026 — one "zoom level" of a <see cref="WaveformPyramidCache"/>: every
/// <see cref="Envelopes"/> entry in this level summarizes exactly <see cref="BlockSizeSamples"/>
/// source samples (the final entry may summarize fewer, if <c>SampleCount</c> is not an exact
/// multiple of the block size). Not named in Section 3's own proposed signatures — Section 3 only
/// names <see cref="WaveformEnvelope"/> itself and <c>IWaveformBuilder.BuildPyramidAsync</c>'s bare
/// <c>Task&lt;string&gt;</c> return; this type is this step's own first definition of what that
/// returned cache file actually holds (see <c>implementation-evidence/PHASE-04/STEP-026.md</c> §3).
/// </summary>
/// <param name="BlockSizeSamples">Source samples summarized per <see cref="WaveformEnvelope"/> at this resolution level — smaller values give finer, more expensive-to-render detail; larger values give a fast, coarse zoomed-out overview.</param>
/// <param name="Envelopes">Contiguous, gap-free, half-open envelopes covering <c>[0, SampleCount)</c> of the source song, ordered by <see cref="WaveformEnvelope.StartSample"/>.</param>
public sealed record WaveformResolutionLevel(int BlockSizeSamples, WaveformEnvelope[] Envelopes);
