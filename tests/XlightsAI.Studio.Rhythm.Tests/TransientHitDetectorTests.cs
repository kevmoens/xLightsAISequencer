using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;

namespace XlightsAI.Studio.Rhythm.Tests;

/// <summary>Build-plan "richer analysis tools" Phase 2 (2026-11-02): <see cref="TransientHitDetector"/>.</summary>
public sealed class TransientHitDetectorTests
{
    private static readonly RhythmSettings WindowSettings = new(AnalysisSampleRate: 8000, FftSize: 256, HopSamples: 128);
    private static readonly OnsetDetectionSettings OnsetSettings = new(SensitivityMultiplier: 1.5f, MinInterOnsetSamples: 256);
    private static readonly BandEnergySettings BandSettings = new(WindowSettings, BassCutoffHz: 150, HighCutoffHz: 4000);

    private static OnsetAnalysis BuildOnsets(params (long TimeTicks, float Strength)[] events) =>
        new(Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", "decoder/1", "test/1", WindowSettings, OnsetSettings,
            events.Select(e => new OnsetEvent(e.TimeTicks, e.Strength, "test")).ToArray());

    [Fact]
    public void Detect_OnsetAboveThreshold_PromotedToTransientHit()
    {
        var onsets = BuildOnsets((1_000_000, 0.9f), (2_000_000, 0.2f));

        var hits = TransientHitDetector.Detect(onsets);

        var hit = Assert.Single(hits);
        Assert.Equal(1_000_000, hit.TimeTicks);
        Assert.Equal(0.9f, hit.Strength);
    }

    [Fact]
    public void Detect_OnsetBelowThreshold_NotPromoted()
    {
        var onsets = BuildOnsets((1_000_000, 0.59f));

        Assert.Empty(TransientHitDetector.Detect(onsets, settings: new TransientHitSettings(MinimumStrength: 0.6)));
    }

    [Fact]
    public void Detect_CustomThreshold_Respected()
    {
        var onsets = BuildOnsets((1_000_000, 0.5f));

        var hits = TransientHitDetector.Detect(onsets, settings: new TransientHitSettings(MinimumStrength: 0.4));

        Assert.Single(hits);
    }

    private static BandEnergyEnvelope BuildBandEnergy(params (long StartSample, long EndSample, float Bass)[] frames) =>
        new(Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", "decoder/1", "test/1", BandSettings,
            frames.Select(f => new BandEnergyFrame(f.StartSample, f.EndSample, f.Bass, Mid: 0f, High: 0f)).ToArray());

    [Fact]
    public void Detect_BassCorroborationRequired_RejectsStrongButBasslessOnset()
    {
        // Onset at tick 1_000_000 -> sample 1_000_000 * 8000 / 10_000_000 = 800 at 8kHz.
        var onsets = BuildOnsets((1_000_000, 0.9f));
        var bandEnergy = BuildBandEnergy((0, 2000, Bass: 0.05f));

        var hits = TransientHitDetector.Detect(onsets, bandEnergy, new TransientHitSettings(MinimumStrength: 0.6, MinimumBassEnergy: 0.5));

        Assert.Empty(hits);
    }

    [Fact]
    public void Detect_BassCorroborationRequired_AcceptsStrongBassHeavyOnset()
    {
        var onsets = BuildOnsets((1_000_000, 0.9f));
        var bandEnergy = BuildBandEnergy((0, 2000, Bass: 0.95f));

        var hits = TransientHitDetector.Detect(onsets, bandEnergy, new TransientHitSettings(MinimumStrength: 0.6, MinimumBassEnergy: 0.5));

        Assert.Single(hits);
    }

    [Fact]
    public void Detect_NoOnsets_ReturnsEmpty()
    {
        var onsets = BuildOnsets();

        Assert.Empty(TransientHitDetector.Detect(onsets));
    }

    [Fact]
    public void Detect_NullOnsets_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TransientHitDetector.Detect(null!));
    }
}
