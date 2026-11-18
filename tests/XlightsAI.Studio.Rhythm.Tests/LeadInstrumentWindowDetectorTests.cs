using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;

namespace XlightsAI.Studio.Rhythm.Tests;

/// <summary>Build-plan Phase 27 (2026-11-14): <see cref="LeadInstrumentWindowDetector"/>.</summary>
public sealed class LeadInstrumentWindowDetectorTests
{
    private const int SampleRate = 8000;
    private static readonly RhythmSettings WindowSettings = new(AnalysisSampleRate: SampleRate, FftSize: 256, HopSamples: 128);
    private static readonly BandEnergySettings BandSettings = new(WindowSettings, BassCutoffHz: 150, HighCutoffHz: 4000);

    /// <summary>One frame per half-second, `seconds` long, with `midHighAtHalfSecond(i)` as both Mid and High's level for half-second index i (Bass held irrelevant/flat -- this detector only looks at Mid+High).</summary>
    private static BandEnergyEnvelope BuildEnvelope(Func<int, float> midHighAtHalfSecond, int halfSeconds)
    {
        var samplesPerStep = SampleRate / 2;
        var frames = Enumerable.Range(0, halfSeconds)
            .Select(i => new BandEnergyFrame(i * (long)samplesPerStep, (i + 1) * (long)samplesPerStep, 0.1f, midHighAtHalfSecond(i), midHighAtHalfSecond(i)))
            .ToArray();
        return new BandEnergyEnvelope(Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", "decoder/1", "test/1", BandSettings, frames);
    }

    [Fact]
    public void Detect_SustainedMidHighElevationOverKnownSpan_ProducesExactlyOneMatchingWindow()
    {
        // 16s flat low baseline (0.1) -- long enough that the trailing 8s rolling baseline is purely
        // pre-elevation at onset -- then a clearly elevated (0.5, 5x) sustained span from 16s-18s (2s
        // long, comfortably over the 1.5s minimum duration but short relative to the 8s rolling baseline
        // so the baseline does not catch up and swamp the ratio mid-span), then back down to baseline.
        var envelope = BuildEnvelope(i =>
        {
            var seconds = i * 0.5;
            return seconds is >= 16.0 and < 18.0 ? 0.5f : 0.1f;
        }, halfSeconds: 80);

        var windows = LeadInstrumentWindowDetector.Detect(envelope);

        var window = Assert.Single(windows);
        Assert.Equal(160_000_000L, window.StartTicks); // 16s in ticks
        Assert.Equal(180_000_000L, window.EndTicks); // 18s in ticks
        Assert.True(window.RelativeProminence >= LeadInstrumentWindowDetector.Default.ProminenceMultiplier);
    }

    [Fact]
    public void Detect_BriefSingleStepSpike_ProducesNoWindow()
    {
        // A single 0.5s step spike at 16s -- below MinimumDurationTicks (1.5s) -- must be rejected as a
        // transient, not reported as a sustained lead-instrument window (that's TransientHitDetector's job).
        var envelope = BuildEnvelope(i =>
        {
            var seconds = i * 0.5;
            return seconds is >= 16.0 and < 16.5 ? 0.5f : 0.1f;
        }, halfSeconds: 80);

        var windows = LeadInstrumentWindowDetector.Detect(envelope);

        Assert.Empty(windows);
    }

    [Fact]
    public void Detect_FlatEnergyThroughout_ProducesNoWindow()
    {
        var envelope = BuildEnvelope(_ => 0.3f, halfSeconds: 80);

        var windows = LeadInstrumentWindowDetector.Detect(envelope);

        Assert.Empty(windows);
    }

    [Fact]
    public void Detect_EmptyEnvelope_ReturnsEmpty()
    {
        var envelope = new BandEnergyEnvelope(Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", "decoder/1", "test/1", BandSettings, []);

        var windows = LeadInstrumentWindowDetector.Detect(envelope);

        Assert.Empty(windows);
    }
}
