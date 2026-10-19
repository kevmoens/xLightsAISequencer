namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Step 046's own "configurable" windowing/search knobs for fundamental-frequency tracking,
/// layered the same way <c>XlightsAI.Studio.Rhythm.OnsetDetectionSettings</c> layers on top of
/// <c>RhythmSettings</c>: recorded verbatim alongside every <see cref="PitchTrackAnalysis"/> it
/// produces, and part of that analysis's own cache key (Section 4: "every cache key includes
/// algorithm/model/settings versions").
/// </summary>
/// <param name="FrameSize">
/// Analysis window length in samples. Must exceed the maximum lag implied by <see cref="MinFrequencyHz"/>
/// at the source's own sample rate (<c>sampleRate / MinFrequencyHz</c>) -- otherwise the lowest
/// frequency this run is configured to detect could never complete a full period inside one window.
/// </param>
/// <param name="HopSamples">Sample-domain stride between consecutive analysis frames. Must be positive.</param>
/// <param name="MinFrequencyHz">Lowest fundamental frequency this run will ever report (Hz). Must be positive.</param>
/// <param name="MaxFrequencyHz">Highest fundamental frequency this run will ever report (Hz). Must exceed <see cref="MinFrequencyHz"/>.</param>
/// <param name="VoicingThreshold">
/// The YIN absolute threshold (Section 3 architectural design notes: "Implement pitch detector... test
/// sine tones, instrument samples and octaves"): the first local minimum of the cumulative mean
/// normalized difference function below this value is accepted as the frame's true period. Must be in
/// <c>(0, 1)</c>; a typical musically useful value is <c>0.1</c>-<c>0.2</c> (lower = stricter/more
/// confident voiced decisions, higher = more permissive).
/// </param>
public sealed record PitchTrackingSettings(
    int FrameSize,
    int HopSamples,
    double MinFrequencyHz,
    double MaxFrequencyHz,
    double VoicingThreshold);
