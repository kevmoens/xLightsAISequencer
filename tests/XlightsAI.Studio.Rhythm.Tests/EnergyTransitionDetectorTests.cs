using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;

namespace XlightsAI.Studio.Rhythm.Tests;

/// <summary>Build-plan "richer analysis tools" Phase 4 (2026-11-02): <see cref="EnergyTransitionDetector"/>.</summary>
public sealed class EnergyTransitionDetectorTests
{
    private const int SampleRate = 8000;
    private static readonly RhythmSettings WindowSettings = new(AnalysisSampleRate: SampleRate, FftSize: 256, HopSamples: 128);
    private static readonly BandEnergySettings BandSettings = new(WindowSettings, BassCutoffHz: 150, HighCutoffHz: 4000);

    /// <summary>One frame per second, `count` seconds long, with `energyAtSecond(i)` as every band's level for second i.</summary>
    private static BandEnergyEnvelope BuildEnvelope(Func<int, float> energyAtSecond, int seconds)
    {
        var frames = Enumerable.Range(0, seconds)
            .Select(i => new BandEnergyFrame(i * (long)SampleRate, (i + 1) * (long)SampleRate, energyAtSecond(i), energyAtSecond(i), energyAtSecond(i)))
            .ToArray();
        return new BandEnergyEnvelope(Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", "decoder/1", "test/1", BandSettings, frames);
    }

    [Fact]
    public void Detect_SustainedRise_ReportsBuildup()
    {
        // Flat low energy for 10s, then a steep climb to peak within a few seconds -- the detector's
        // default 1-second before/after comparison window needs a rate steep enough to clear
        // BuildupThreshold (0.25) within that one window, not a slow multi-second ramp.
        var envelope = BuildEnvelope(i => i < 10 ? 0.1f : Math.Min(1.0f, 0.1f + (i - 10) * 0.4f), seconds: 20);

        var transitions = EnergyTransitionDetector.Detect(envelope);

        Assert.Contains(transitions, t => t.Kind == EnergyTransitionKind.Buildup);
    }

    [Fact]
    public void Detect_SuddenFall_ReportsDrop()
    {
        // Loud for 10s, then silent -- a hard drop at t=10.
        var envelope = BuildEnvelope(i => i < 10 ? 0.9f : 0.05f, seconds: 20);

        var transitions = EnergyTransitionDetector.Detect(envelope);

        Assert.Contains(transitions, t => t.Kind == EnergyTransitionKind.Drop);
    }

    [Fact]
    public void Detect_FlatEnergy_ReportsNothing()
    {
        var envelope = BuildEnvelope(_ => 0.5f, seconds: 20);

        Assert.Empty(EnergyTransitionDetector.Detect(envelope));
    }

    [Fact]
    public void Detect_AdjacentQualifyingInstants_DebouncedToStrongestOne()
    {
        var envelope = BuildEnvelope(i => i < 10 ? 0.9f : 0.0f, seconds: 20);

        var transitions = EnergyTransitionDetector.Detect(envelope, new EnergyTransitionSettings(WindowTicks: 10_000_000, StepTicks: 2_500_000, DropThreshold: 0.2));

        // Several candidate instants near t=10 all qualify as the same drop -- must collapse to one.
        var drops = transitions.Where(t => t.Kind == EnergyTransitionKind.Drop).ToList();
        Assert.Single(drops);
    }

    [Fact]
    public void Detect_EmptyEnvelope_ReturnsEmpty()
    {
        var envelope = new BandEnergyEnvelope(Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", "decoder/1", "test/1", BandSettings, []);

        Assert.Empty(EnergyTransitionDetector.Detect(envelope));
    }

    [Fact]
    public void Detect_NullEnvelope_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => EnergyTransitionDetector.Detect(null!));
    }
}
