using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;

namespace XlightsAI.Studio.Rhythm.Tests;

/// <summary>
/// Build-plan "richer analysis tools" Phase 2 (2026-11-02); strength gate redesigned 2026-11-14 to a
/// locally-adaptive threshold (<see cref="TransientHitDetector"/>'s own doc comment explains the real,
/// measured reason: a fixed song-wide-peak-relative threshold discarded 87% of a real song's onsets).
/// </summary>
public sealed class TransientHitDetectorTests
{
    private const long TicksPerSecond = 10_000_000L;
    private static readonly RhythmSettings WindowSettings = new(AnalysisSampleRate: 8000, FftSize: 256, HopSamples: 128);
    private static readonly OnsetDetectionSettings OnsetSettings = new(SensitivityMultiplier: 1.5f, MinInterOnsetSamples: 256);
    private static readonly BandEnergySettings BandSettings = new(WindowSettings, BassCutoffHz: 150, HighCutoffHz: 4000);

    private static OnsetAnalysis BuildOnsets(params (long TimeTicks, float Strength)[] events) =>
        new(Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", "decoder/1", "test/1", WindowSettings, OnsetSettings,
            events.Select(e => new OnsetEvent(e.TimeTicks, e.Strength, "test")).ToArray());

    [Fact]
    public void Detect_OnsetLocallyLouderThanItsNeighbors_PromotedToTransientHit()
    {
        // Six quiet, evenly-spaced onsets (a continuous ostinato-like texture) plus one real standout
        // at t=3s -- the standout is well above the local mean+1.5*stdDev of its own neighbors.
        var onsets = BuildOnsets(
            (0, 0.1f), (1 * TicksPerSecond, 0.1f), (2 * TicksPerSecond, 0.1f),
            (3 * TicksPerSecond, 0.5f),
            (4 * TicksPerSecond, 0.1f), (5 * TicksPerSecond, 0.1f), (6 * TicksPerSecond, 0.1f));

        var hits = TransientHitDetector.Detect(onsets);

        var hit = Assert.Single(hits);
        Assert.Equal(3 * TicksPerSecond, hit.TimeTicks);
        Assert.Equal(0.5f, hit.Strength);
    }

    [Fact]
    public void Detect_OnsetBelowLocallyAdaptiveThreshold_NotPromoted()
    {
        // Every onset is equally "quiet" -- none stands out from its own neighbors, so nothing qualifies.
        var onsets = BuildOnsets(
            (0, 0.2f), (1 * TicksPerSecond, 0.2f), (2 * TicksPerSecond, 0.2f),
            (3 * TicksPerSecond, 0.2f), (4 * TicksPerSecond, 0.2f));

        Assert.Empty(TransientHitDetector.Detect(onsets));
    }

    [Fact]
    public void Detect_LocallyLoudOnset_PromotedEvenBelowTheOldFixedGlobalThreshold()
    {
        // The real motivating case: a dense cluster of quiet onsets (~0.1) with one real standout at
        // 0.5 -- well below the OLD flat MinimumStrength=0.6 global gate, but clearly the loudest real
        // event in its own neighborhood, so the new locally-adaptive gate correctly promotes it.
        var onsets = BuildOnsets(
            (0, 0.09f), (1 * TicksPerSecond, 0.11f), (2 * TicksPerSecond, 0.1f),
            (3 * TicksPerSecond, 0.5f),
            (4 * TicksPerSecond, 0.12f), (5 * TicksPerSecond, 0.1f));

        var hits = TransientHitDetector.Detect(onsets);

        Assert.Contains(hits, h => h.TimeTicks == 3 * TicksPerSecond);
    }

    [Fact]
    public void Detect_DistantLoudOnsetElsewhereInTheSong_NeverSuppressesALocalRegionsOwnRealOnset()
    {
        // The whole point of locality: a single very loud onset 30s away (the "song's one big swell")
        // must not drag down or suppress what counts as "loud" for an unrelated, much earlier region --
        // it sits far outside the default 15s local window.
        var onsets = BuildOnsets(
            (0, 0.1f), (1 * TicksPerSecond, 0.1f), (2 * TicksPerSecond, 0.1f),
            (3 * TicksPerSecond, 0.5f),
            (4 * TicksPerSecond, 0.1f), (5 * TicksPerSecond, 0.1f),
            (30 * TicksPerSecond, 0.95f));

        var hits = TransientHitDetector.Detect(onsets);

        Assert.Contains(hits, h => h.TimeTicks == 3 * TicksPerSecond);
        Assert.Contains(hits, h => h.TimeTicks == 30 * TicksPerSecond);
    }

    [Fact]
    public void Detect_IsolatedOnsetWithNoNearbyNeighbors_StillPromoted()
    {
        // A genuinely isolated onset (nothing else within the local window at all) must still qualify
        // once it clears the absolute floor -- it is never compared only against itself.
        var onsets = BuildOnsets((5 * TicksPerSecond, 0.3f));

        var hits = TransientHitDetector.Detect(onsets);

        Assert.Single(hits);
    }

    [Fact]
    public void Detect_OnsetBelowAbsoluteFloor_NeverPromoted_EvenIfLocallyLoudest()
    {
        // A near-silent passage's own tiny noise floor must never be promoted into a "hit" just because
        // it is locally the loudest thing around -- the absolute floor guards against exactly this.
        var onsets = BuildOnsets((0, 0.01f), (1 * TicksPerSecond, 0.005f), (2 * TicksPerSecond, 0.02f));

        Assert.Empty(TransientHitDetector.Detect(onsets));
    }

    [Fact]
    public void Detect_CustomSensitivityMultiplier_Respected()
    {
        // Neighbors (0.08, 0.12) give local mean=0.1, stdDev=0.02. Candidate 0.125: default threshold
        // (mean + 1.5*stdDev = 0.13) rejects it; a much lower multiplier (threshold = 0.102) accepts it.
        var onsets = BuildOnsets((0, 0.08f), (1 * TicksPerSecond, 0.12f), (2 * TicksPerSecond, 0.125f));

        Assert.DoesNotContain(TransientHitDetector.Detect(onsets), h => h.TimeTicks == 2 * TicksPerSecond);

        var lenientHits = TransientHitDetector.Detect(onsets, settings: new TransientHitSettings(LocalSensitivityMultiplier: 0.1));
        Assert.Contains(lenientHits, h => h.TimeTicks == 2 * TicksPerSecond);
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

        var hits = TransientHitDetector.Detect(onsets, bandEnergy, new TransientHitSettings(MinimumBassEnergy: 0.5));

        Assert.Empty(hits);
    }

    [Fact]
    public void Detect_BassCorroborationRequired_AcceptsStrongBassHeavyOnset()
    {
        var onsets = BuildOnsets((1_000_000, 0.9f));
        var bandEnergy = BuildBandEnergy((0, 2000, Bass: 0.95f));

        var hits = TransientHitDetector.Detect(onsets, bandEnergy, new TransientHitSettings(MinimumBassEnergy: 0.5));

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
