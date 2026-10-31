using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// One coherent, cache-key-consistent set of analysis settings for <see cref="AudioAnalysisRunner"/>,
/// derived purely from the source's sample rate so a caller never has to hand-tune seven independent
/// settings objects. <see cref="ForSampleRate"/>'s window/hop sizing reproduces the exact ratio
/// already proven in this repository's own real fixture
/// (<c>tests/XlightsAI.Studio.Rhythm.Tests/Step034AcceptanceTests.cs</c>: 256-sample FFT / 128-sample hop
/// at an 8 kHz fixture rate is a ~23 ms window / ~50% overlap) rather than inventing new, untested ratios.
/// </summary>
public sealed record AudioAnalysisSettingsBundle(
    RhythmSettings Rhythm,
    OnsetDetectionSettings Onset,
    TempoDetectionSettings Tempo,
    BeatGridSettings BeatGrid,
    PitchTrackingSettings PitchTracking,
    NoteSegmentationSettings NoteSegmentation,
    GestureClassificationSettings GestureClassification,
    BandEnergySettings BandEnergy)
{
    private const long TicksPerSecond = 10_000_000L;

    /// <summary>~23 ms analysis window (see class doc comment for where this ratio comes from), 50% hop overlap.</summary>
    private const double TargetWindowSeconds = 256.0 / 8000.0;

    public static AudioAnalysisSettingsBundle ForSampleRate(int sampleRate)
    {
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Sample rate must be positive.");
        }

        var fftSize = NextPowerOfTwo((int)Math.Round(sampleRate * TargetWindowSeconds));
        var hop = fftSize / 2;
        var alignmentToleranceTicks = 2L * hop * (TicksPerSecond / sampleRate);

        return new AudioAnalysisSettingsBundle(
            Rhythm: new RhythmSettings(AnalysisSampleRate: sampleRate, FftSize: fftSize, HopSamples: hop),
            Onset: new OnsetDetectionSettings(SensitivityMultiplier: 1.5f, MinInterOnsetSamples: hop),
            Tempo: new TempoDetectionSettings(MinBpm: 60, MaxBpm: 200, TimeResolutionSeconds: 0.01, MaxCandidates: 5),
            BeatGrid: new BeatGridSettings(BeatsPerBar: 4, AlignmentToleranceTicks: alignmentToleranceTicks),
            // PitchTrackingSettings.FrameSize must exceed sampleRate/MinFrequencyHz (80 Hz here) so the
            // lowest detectable pitch can complete a full period inside one window; fftSize*2 clears that
            // bound at any realistic sample rate (it only needs to clear sampleRate/80, far smaller).
            PitchTracking: new PitchTrackingSettings(FrameSize: fftSize * 2, HopSamples: hop, MinFrequencyHz: 80, MaxFrequencyHz: 1000, VoicingThreshold: 0.15),
            NoteSegmentation: NoteSegmentationSettings.Default,
            GestureClassification: GestureClassificationSettings.Default,
            // Same bass/mid cutoffs already established across this codebase's own real-track tests
            // (Phase4RealTrackChoreographyEngineTests/Phase6EndToEndRealTrackTests/Phase7RealIterationLoopTests) --
            // not invented here, reused for consistency.
            BandEnergy: new BandEnergySettings(
                new RhythmSettings(AnalysisSampleRate: sampleRate, FftSize: fftSize, HopSamples: hop),
                BassCutoffHz: 250, HighCutoffHz: 2000));
    }

    private static int NextPowerOfTwo(int value)
    {
        var power = 1;
        while (power < value)
        {
            power <<= 1;
        }

        return power;
    }
}
