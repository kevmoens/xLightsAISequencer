using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Regression test for a real-song export failure: "Timing track 'Percussion' mark
/// (2309921088-2309921315 ticks) rounds to a non-positive duration at the 25ms xLights frame grid; no
/// file was written." <see cref="TimingTrackEmitter.Percussion"/>'s own
/// <c>EnforceMinimumMarkDuration</c> helper claims (in its doc comment) that a too-short final
/// remainder is "absorbed into the last emitted mark rather than dropped" -- but the code used to fall
/// through and emit that final run as its own brand-new, still-too-short mark whenever it happened to
/// be the very last original per-hop mark in the track. A 227-tick (~0.0227ms) trailing mark is nowhere
/// near the 25ms xLights frame grid, so <c>XsqExporter</c> rejected the whole export.
/// </summary>
public sealed class TimingTrackEmitterPercussionTests
{
    /// <summary>1 sample = 1 tick at this rate, so exact tick-level control over frame boundaries is trivial.</summary>
    private const int SampleRate = 10_000_000;

    private static BandEnergyEnvelope BuildEnvelope(params BandEnergyFrame[] frames) => new(
        Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", "t/1", "t/1",
        new BandEnergySettings(new RhythmSettings(SampleRate, 2048, 512), 150, 4000), frames);

    [Fact]
    public void Percussion_TinyFinalFrame_IsAbsorbedIntoThePreviousMark_NotEmittedAsItsOwnShortMark()
    {
        // Frame 0 covers a normal-length span well above the 250,000-tick (25ms) minimum.
        // Frame 1 (the last) starts right where frame 0 ends but has a 227-tick-wide analysis window --
        // the exact real-world shape that produced the reported export failure.
        var frame0 = new BandEnergyFrame(0, 2048, 0f, 0f, 0f);
        var frame1 = new BandEnergyFrame(400_000, 400_227, 0f, 0f, 0f);
        var envelope = BuildEnvelope(frame0, frame1);

        var track = TimingTrackEmitter.Percussion(envelope);

        // The whole track must still cover [0, 400227) exactly, as one mark -- never a separate
        // trailing mark narrower than the minimum duration.
        Assert.Single(track.Marks);
        Assert.Equal(0, track.Marks[0].StartTicks);
        Assert.Equal(400_227, track.Marks[0].EndTicks);
    }

    [Fact]
    public void Percussion_EveryEmittedMark_MeetsTheMinimumDuration_EvenWhenTheRealTrailingFrameIsTiny()
    {
        const long frameIntervalTicks = 250_000; // FrameClock.FrameIntervalTicks (25ms).

        // Two normal-length hops (each already above the minimum, so each flushes its own mark the
        // ordinary way) followed by a tiny 227-tick final frame -- the realistic shape of the real
        // export failure (plenty of real audio before a short trailing analysis window).
        var frame0 = new BandEnergyFrame(0, 2048, 0f, 0f, 0f);
        var frame1 = new BandEnergyFrame(300_000, 302_048, 0f, 0f, 0f);
        var frame2 = new BandEnergyFrame(600_000, 600_227, 0f, 0f, 0f); // Last frame, tiny on its own.
        var envelope = BuildEnvelope(frame0, frame1, frame2);

        var track = TimingTrackEmitter.Percussion(envelope);

        Assert.All(track.Marks, mark => Assert.True(mark.EndTicks - mark.StartTicks >= frameIntervalTicks));

        // Total coverage of the original frames' own span is preserved exactly -- nothing dropped.
        Assert.Equal(0, track.Marks[0].StartTicks);
        Assert.Equal(600_227, track.Marks[^1].EndTicks);
    }

    [Fact]
    public void Percussion_TinyFinalFrame_WithARealDetectedLabel_KeepsThatLabel_NotDropped()
    {
        // The actual real-song failure shape: the quiet run before the tiny trailing frame is
        // unlabeled ("" -- nothing of interest), but the tiny trailing frame itself is a genuine
        // detected hit (a real Bass spike well above the local mean+1.5*stddev threshold). Absorbing
        // that trailing run into the previous mark must promote its real label onto the merged mark,
        // not silently discard it.
        var frames = new List<BandEnergyFrame>();
        for (var i = 0; i < 16; i++)
        {
            frames.Add(new BandEnergyFrame(i * 50_000, (i * 50_000) + 2048, 0f, 0f, 0f));
        }

        // Frame 15 -> frame 16 hop is only 300 ticks (well under the 250,000-tick minimum), and frame
        // 16's own analysis window is 227 ticks wide -- producing exactly the tiny trailing raw mark
        // the bug above was found against. Its Bass is a real spike relative to the 16 quiet frames.
        const long frame16Start = (15 * 50_000) + 300;
        frames.Add(new BandEnergyFrame(frame16Start, frame16Start + 227, 1000f, 0f, 0f));

        var envelope = BuildEnvelope(frames.ToArray());

        var track = TimingTrackEmitter.Percussion(envelope);

        Assert.Contains(track.Marks, mark => mark.Label == "kick");
        Assert.All(track.Marks, mark => Assert.True(mark.EndTicks - mark.StartTicks >= 250_000));
        Assert.Equal(frame16Start + 227, track.Marks[^1].EndTicks);
    }
}
