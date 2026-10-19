namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// PHASE-05 Section 3's proposed signature, verbatim shape: the analysis-window configuration an
/// <see cref="IEnergyEnvelopeAnalyzer"/> run is keyed by. Recorded alongside every
/// <see cref="EnergyEnvelope"/> it produces (Section 4: "every cache key includes algorithm/
/// model/settings versions" — applied here to the settings themselves, not just the analyzer's own
/// code version) so a later re-run with different windowing can never be silently confused with this
/// one's output.
/// </summary>
/// <param name="AnalysisSampleRate">
/// The sample rate, in Hz, this analysis run operates at. Step 031 requires this to equal the source
/// <c>SongAsset.SampleRate</c> exactly: <c>IAudioDecoder.DecodeMonoAsync</c> does not yet resample
/// (see <c>WavAudioDecoder</c>'s own disclosed "resampling is deliberately out of scope" note), so a
/// mismatched value here is treated as invalid input rather than silently decoded at the wrong rate.
/// </param>
/// <param name="FftSize">Window length in samples each <see cref="EnergyFrame"/> summarizes. Named "FftSize" per Section 3's proposed shape even though Step 031 itself computes only a time-domain RMS window, not yet a real FFT — spectral features are Step 032's onset-detection scope, not this step's.</param>
/// <param name="HopSamples">Samples to advance between the start of one window and the next. <c>HopSamples &lt;= FftSize</c> means overlapping windows; <c>HopSamples &gt; FftSize</c> means gaps between windows (neither is rejected — Section 3 states FFT size/hop/normalization only need to be "known," not fixed to one relationship).</param>
public sealed record RhythmSettings(int AnalysisSampleRate, int FftSize, int HopSamples);
