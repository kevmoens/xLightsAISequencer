using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// User follow-up (2026-11-10): measured directly against a real hand-crafted reference sequence, its
/// dominant emphasis technique was a short "On" flash landing on real detected transient hits -- nothing
/// in this compiler let a Look layer react to a real hit directly before this. These tests cover
/// <see cref="TimingTrackEmitter.Transients"/>/<see cref="TimingTrackEmitter.VocalNotes"/>, the new real,
/// named timing tracks that close that gap.
/// </summary>
public sealed class TimingTrackEmitterTransientTests
{
    private const long TicksPerSecond = 10_000_000L;

    private static BeatGrid BuildBeatGrid(int beatCount, long beatSpacingTicks)
    {
        var beats = Enumerable.Range(0, beatCount).Select(i => new BeatMarker(Guid.NewGuid(), i * beatSpacingTicks, i % 4, i / 4, 0.9, false)).ToArray();
        return new BeatGrid(Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1", new BeatGridSettings(4, 1000), new TempoHypothesis(120, 0.9, "4/4"), beats, [], 0);
    }

    [Fact]
    public void Transients_OneMarkPerRealHit_ShortAndNonOverlapping()
    {
        const long beatTicks = TicksPerSecond / 2; // 5,000,000 ticks.
        var beatGrid = BuildBeatGrid(beatCount: 20, beatTicks);
        var songEndTicks = 20 * beatTicks;

        var instants = new List<EmphasisInstant>
        {
            new(10 * beatTicks, EmphasisSignalKind.TransientHit),
            new(14 * beatTicks, EmphasisSignalKind.TransientHit),
            new(14 * beatTicks + 2_000, EmphasisSignalKind.VocalNoteOnset), // Different kind -- must not leak into the Transients track.
        };

        var track = TimingTrackEmitter.Transients(instants, beatGrid, songEndTicks);

        Assert.Equal("Transients", track.Name);
        Assert.Equal(2, track.Marks.Count);

        var first = track.Marks[0];
        Assert.Equal(10 * beatTicks, first.StartTicks);
        // A quarter beat (1,250,000 ticks) -- much shorter than the beat itself, never reaching the next hit.
        Assert.True(first.EndTicks - first.StartTicks <= beatTicks / 2);
        Assert.True(first.EndTicks < 14 * beatTicks);

        var second = track.Marks[1];
        Assert.Equal(14 * beatTicks, second.StartTicks);

        // Non-overlapping, sorted -- the only real XsqExporter requirement for a timing track.
        Assert.True(first.EndTicks <= second.StartTicks);
    }

    [Fact]
    public void VocalNotes_OneMarkPerRealOnset_IndependentOfTransientsTrack()
    {
        const long beatTicks = TicksPerSecond / 2;
        var beatGrid = BuildBeatGrid(beatCount: 20, beatTicks);
        var songEndTicks = 20 * beatTicks;

        var instants = new List<EmphasisInstant>
        {
            new(5 * beatTicks, EmphasisSignalKind.VocalNoteOnset),
            new(8 * beatTicks, EmphasisSignalKind.TransientHit),
        };

        var track = TimingTrackEmitter.VocalNotes(instants, beatGrid, songEndTicks);

        Assert.Equal("VocalNotes", track.Name);
        var mark = Assert.Single(track.Marks);
        Assert.Equal(5 * beatTicks, mark.StartTicks);
    }

    [Fact]
    public void VocalNotes_WithARealEndTick_SpansTheRealNoteDuration_NotTheShortFixedSnap()
    {
        // User follow-up (2026-11-10): "will any effects last as long as the note?" -- yes, when the
        // real note's own end tick is supplied, the mark covers that real duration, long or short.
        const long beatTicks = TicksPerSecond / 2;
        var beatGrid = BuildBeatGrid(beatCount: 20, beatTicks);
        var songEndTicks = 20 * beatTicks;

        var noteStart = 5 * beatTicks;
        var noteEnd = noteStart + (3 * beatTicks); // A long held note -- 3 full beats, far longer than the usual quarter-beat snap.
        var instants = new List<EmphasisInstant> { new(noteStart, EmphasisSignalKind.VocalNoteOnset, noteEnd) };

        var track = TimingTrackEmitter.VocalNotes(instants, beatGrid, songEndTicks);

        var mark = Assert.Single(track.Marks);
        Assert.Equal(noteStart, mark.StartTicks);
        Assert.Equal(noteEnd, mark.EndTicks);
    }

    [Fact]
    public void VocalNotes_WithARealEndTick_NeverOverlapsTheNextRealOnset()
    {
        const long beatTicks = TicksPerSecond / 2;
        var beatGrid = BuildBeatGrid(beatCount: 20, beatTicks);
        var songEndTicks = 20 * beatTicks;

        // This note's own claimed end overruns the NEXT real onset -- real pitch-tracked note boundaries
        // can be imprecise; the mark must still never overlap the next real instant.
        var instants = new List<EmphasisInstant>
        {
            new(5 * beatTicks, EmphasisSignalKind.VocalNoteOnset, 7 * beatTicks),
            new(6 * beatTicks, EmphasisSignalKind.VocalNoteOnset, 9 * beatTicks),
        };

        var track = TimingTrackEmitter.VocalNotes(instants, beatGrid, songEndTicks);

        Assert.Equal(2, track.Marks.Count);
        Assert.True(track.Marks[0].EndTicks <= track.Marks[1].StartTicks);
    }

    [Fact]
    public void Transients_NeverUsesARealEndTick_EvenIfOneIsSuppliedOnTheInstant()
    {
        // A TransientHit is a single instant with no real duration of its own -- EmphasisInstant.EndTimeTicks
        // is only ever meaningfully populated for a VocalNoteOnset, but confirm Transients would ignore it
        // even if a caller did supply one, rather than silently growing past its own documented short snap.
        const long beatTicks = TicksPerSecond / 2;
        var beatGrid = BuildBeatGrid(beatCount: 20, beatTicks);
        var songEndTicks = 20 * beatTicks;

        var instants = new List<EmphasisInstant> { new(5 * beatTicks, EmphasisSignalKind.TransientHit, 5 * beatTicks + (3 * beatTicks)) };

        var track = TimingTrackEmitter.Transients(instants, beatGrid, songEndTicks);

        var mark = Assert.Single(track.Marks);
        Assert.True(mark.EndTicks - mark.StartTicks <= beatTicks / 2, "Transients must stay a short, fixed snap regardless of any EndTimeTicks a caller supplies.");
    }

    [Fact]
    public void Transients_TwoHitsCloserThanAQuarterBeatApart_ProducesNonOverlappingMarksAnyway()
    {
        const long beatTicks = TicksPerSecond / 2;
        var beatGrid = BuildBeatGrid(beatCount: 20, beatTicks);
        var songEndTicks = 20 * beatTicks;

        var hitTicks = 10 * beatTicks;
        var instants = new List<EmphasisInstant>
        {
            new(hitTicks, EmphasisSignalKind.TransientHit),
            new(hitTicks + 100_000, EmphasisSignalKind.TransientHit), // Only 10ms later -- much closer than a quarter beat.
        };

        var track = TimingTrackEmitter.Transients(instants, beatGrid, songEndTicks);

        Assert.Equal(2, track.Marks.Count);
        Assert.True(track.Marks[0].EndTicks <= track.Marks[1].StartTicks);
    }

    [Fact]
    public void Transients_NoTransientInstants_ReturnsAnEmptyTrack()
    {
        const long beatTicks = TicksPerSecond / 2;
        var beatGrid = BuildBeatGrid(beatCount: 20, beatTicks);

        var track = TimingTrackEmitter.Transients([new EmphasisInstant(beatTicks, EmphasisSignalKind.VocalNoteOnset)], beatGrid, 20 * beatTicks);

        Assert.Empty(track.Marks);
    }
}
