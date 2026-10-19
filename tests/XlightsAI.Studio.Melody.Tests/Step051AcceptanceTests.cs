using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Melody.Tests;

/// <summary>
/// PHASE-07 Step 051 — "Handle polyphony, rests, note gaps, variable durations and frame quantization
/// gracefully." Required acceptance (verbatim): "Fallback to contour or onsets when pitch unreliable; no
/// invalid effects." Failure/edge case (verbatim, shared with Steps 045/047/050): "Silence, invalid timing
/// boundaries and missing media must be handled explicitly."
/// </summary>
public sealed class Step051AcceptanceTests
{
    private const string Hash = "deadbeef";

    private static readonly IEffectRecipeRegistry Registry = new EffectRecipeRegistry();
    private static readonly IMelodicGestureMapper Mapper = new MelodicGestureMapper();

    private static NoteEvent Note(long startTicks, long endTicks, int midiNote, double confidence = 0.9, double velocity = 0.8) =>
        new(Guid.NewGuid(), startTicks, endTicks, midiNote, velocity, confidence, UserCorrected: false);

    private static MelodyTrack Classified(params NoteEvent[] notes)
    {
        var track = new MelodyTrack(Guid.NewGuid(), Hash, notes, []);
        return MelodicContourClassifier.Classify(track, GestureClassificationSettings.Default);
    }

    private static IReadOnlyList<ModelId> Path(int count) =>
        Enumerable.Range(1, count).Select(i => new ModelId($"Arch{i}")).ToList();

    private static readonly TimeWindow WholeSong = new(0L, 1_000_000_000L);

    [Fact]
    public void Step051_Acceptance_Passes_LowConfidencePitchFallsBackToContourThenOnset_NoInvalidEffects()
    {
        // Required acceptance, verbatim: "Fallback to contour or onsets when pitch unreliable; no invalid
        // effects." Four notes over a 5-arch path: a confident StepUp establishes Arch2, then a
        // low-but-not-floor-confidence LeapUp (a falsely large interval -- an octave-error-shaped
        // misdetection) must downgrade to a mere StepUp (contour: direction trusted, magnitude not) ->
        // Arch3, not the full two-position leap -> Arch4. A then very-low-confidence StepDown (pitch
        // essentially untrustworthy) must fall back to an onset-only pulse at the walk's current,
        // unmoved position (Arch3), not move at all.
        var first = Note(0L, 1_000L, 60, confidence: 0.95);          // Arch1 (walk start)
        var second = Note(1_100L, 2_000L, 61, confidence: 0.95);     // StepUp (+1) -> Arch2, fully confident
        var third = Note(2_100L, 3_000L, 66, confidence: 0.4);       // LeapUp (+5), confidence between the onset floor (0.25) and MinGestureConfidence (0.5) -> downgraded to StepUp -> Arch3
        var fourth = Note(3_100L, 4_000L, 60, confidence: 0.1);      // StepDown (-1) relative to the last *confident* note (second, midi 61 -- third was never confident enough to become the new reference), confidence below the onset floor (0.25) -> onset-only pulse, stays at Arch3
        var track = Classified(first, second, third, fourth);

        Assert.Equal(PitchMotion.StepUp, track.Gestures[1].Motion);
        Assert.Equal(PitchMotion.LeapUp, track.Gestures[2].Motion); // classifier itself is unaffected by the mapper's own fallback
        Assert.Equal(PitchMotion.StepDown, track.Gestures[3].Motion);

        var effects = Mapper.MapToEffects(track, Path(5), WholeSong, GestureMapSettings.Default, Registry);

        Assert.Equal(4, effects.Count);
        Assert.Equal("Arch1", effects[0].Target.Value);
        Assert.Equal("Arch2", effects[1].Target.Value);
        Assert.Equal("Arch3", effects[2].Target.Value); // contour fallback: +1, never +2
        Assert.Equal("Arch3", effects[3].Target.Value); // onset fallback: unmoved

        // "No invalid effects": every produced effect still traces back to a verified recipe and its own
        // real source note timing, never a fabricated or evenly-subdivided window.
        foreach (var effect in effects)
        {
            Assert.Equal(EffectRecipeRegistry.SimpleOnRecipeId, effect.RecipeId);
        }
        Assert.Equal(third.StartTicks, effects[2].StartTicks);
        Assert.Equal(third.EndTicks, effects[2].EndTicks);
        Assert.Equal(fourth.StartTicks, effects[3].StartTicks);
        Assert.Equal(fourth.EndTicks, effects[3].EndTicks);
    }

    [Fact]
    public void Polyphony_TwoSimultaneousNotes_ReducedToOneDeterministicLeadLine_NotRejected()
    {
        // Step title, verbatim: "Handle polyphony... gracefully." Two real, simultaneous onsets (a chord,
        // or polyphonic interference bleeding into an isolated-lead stem) -- MelodyTrackValidator alone
        // would reject this outright (FullyContainedOverlap_CannotBeNormalized_...NegativePath, Step 045's
        // own already-committed negative path); MelodyPolyphonyReducer must instead produce a usable,
        // monophonic result rather than throwing.
        var higherConfidenceLead = Note(0L, 10_000L, 67, confidence: 0.95); // the real melody note
        var weakerHarmonyNote = Note(0L, 4_000L, 60, confidence: 0.5);     // a simultaneous, weaker harmony note, fully inside the lead's span
        var raw = new MelodyTrack(Guid.NewGuid(), Hash, [weakerHarmonyNote, higherConfidenceLead], []);

        var reduced = MelodyPolyphonyReducer.Reduce(raw);

        Assert.Single(reduced.Notes); // the weaker, fully-subordinate note never surfaces
        Assert.Equal(higherConfidenceLead.Id, reduced.Notes[0].Id);
        Assert.Equal(higherConfidenceLead.MidiNote, reduced.Notes[0].MidiNote);
    }

    [Fact]
    public void Polyphony_OverlappingDominantNoteStartsLater_TrimsPriorLeadRatherThanDroppingIt()
    {
        // The dominant note starts partway through the current lead's own span (e.g. a stem-separation
        // artifact bleeding a louder note in mid-phrase) -- the current lead is trimmed to the point the
        // new dominant note actually starts, rather than erased entirely or rejected.
        var earlierWeakerLead = Note(0L, 10_000L, 60, confidence: 0.5);
        var laterDominantNote = Note(4_000L, 12_000L, 67, confidence: 0.95);
        var raw = new MelodyTrack(Guid.NewGuid(), Hash, [earlierWeakerLead, laterDominantNote], []);

        var reduced = MelodyPolyphonyReducer.Reduce(raw);

        Assert.Equal(2, reduced.Notes.Count);
        Assert.Equal(earlierWeakerLead.Id, reduced.Notes[0].Id);
        Assert.Equal(0L, reduced.Notes[0].StartTicks);
        Assert.Equal(4_000L, reduced.Notes[0].EndTicks); // trimmed to the dominant note's own start, not dropped
        Assert.Equal(laterDominantNote.Id, reduced.Notes[1].Id);
        Assert.Equal(4_000L, reduced.Notes[1].StartTicks);
        Assert.Equal(12_000L, reduced.Notes[1].EndTicks);
    }

    [Fact]
    public void Polyphony_NonOverlappingNotes_PassThroughUnchanged()
    {
        var first = Note(0L, 1_000L, 60);
        var second = Note(1_100L, 2_000L, 62);
        var raw = new MelodyTrack(Guid.NewGuid(), Hash, [first, second], []);

        var reduced = MelodyPolyphonyReducer.Reduce(raw);

        Assert.Equal(2, reduced.Notes.Count);
        Assert.Equal(first.Id, reduced.Notes[0].Id);
        Assert.Equal(second.Id, reduced.Notes[1].Id);
    }

    [Fact]
    public void Silence_EmptyTrack_PolyphonyReductionProducesEmptyTrack_NoCrash_EdgeCase()
    {
        var raw = new MelodyTrack(Guid.NewGuid(), Hash, [], []);

        var reduced = MelodyPolyphonyReducer.Reduce(raw);

        Assert.Empty(reduced.Notes);
    }

    [Fact]
    public void MissingMedia_EmptySourceAssetHash_ThrowsMelodyTrackInvalidException_NegativePath()
    {
        var raw = new MelodyTrack(Guid.NewGuid(), "", [Note(0L, 1_000L, 60)], []);

        Assert.Throws<MelodyTrackInvalidException>(() => MelodyPolyphonyReducer.Reduce(raw));
    }

    [Fact]
    public void InvalidTimingBoundary_NoteEndNotAfterStart_ThrowsMelodyTrackInvalidException_NegativePath()
    {
        var invalidNote = new NoteEvent(Guid.NewGuid(), 1_000L, 1_000L, 60, 0.8, 0.9, UserCorrected: false);
        var raw = new MelodyTrack(Guid.NewGuid(), Hash, [invalidNote], []);

        Assert.Throws<MelodyTrackInvalidException>(() => MelodyPolyphonyReducer.Reduce(raw));
    }

    [Fact]
    public void NullTrack_ThrowsArgumentNullException_NegativePath()
    {
        Assert.Throws<ArgumentNullException>(() => MelodyPolyphonyReducer.Reduce(null!));
    }

    [Fact]
    public void OnsetOnlyConfidenceFloorAboveMinGestureConfidence_ThrowsEffectPlanInvalidException_NegativePath()
    {
        var track = Classified(Note(0L, 1_000L, 60));
        var settings = GestureMapSettings.Default with { MinGestureConfidence = 0.2, OnsetOnlyConfidenceFloor = 0.5 };

        Assert.Throws<EffectPlanInvalidException>(
            () => Mapper.MapToEffects(track, Path(5), WholeSong, settings, Registry));
    }

    [Theory]
    [InlineData(-0.1, 0.0)]
    [InlineData(1.1, 0.0)]
    public void OutOfRangeMinGestureConfidence_ThrowsEffectPlanInvalidException_NegativePath(double min, double floor)
    {
        var track = Classified(Note(0L, 1_000L, 60));
        var settings = GestureMapSettings.Default with { MinGestureConfidence = min, OnsetOnlyConfidenceFloor = floor };

        Assert.Throws<EffectPlanInvalidException>(
            () => Mapper.MapToEffects(track, Path(5), WholeSong, settings, Registry));
    }

    [Fact]
    public void ZeroFallbackThresholds_PreservesStep050BehaviorExactly_NoDowngrade()
    {
        // Backward-compatibility guarantee: the default-constructed (pre-Step-051) thresholds (0.0/0.0)
        // never downgrade any motion, regardless of confidence -- every Step 050 construction site/test
        // keeps its exact original behavior.
        var first = Note(0L, 1_000L, 64, confidence: 0.95);  // Arch1; confident enough to be the reference point for the next note
        var second = Note(1_100L, 2_000L, 67, confidence: 0.01); // LeapUp relative to first, but the gesture's own Confidence (min(0.01, 0.95)) is deliberately near-zero
        var track = Classified(first, second);

        var settings = GestureMapSettings.Default with { MinGestureConfidence = 0.0, OnsetOnlyConfidenceFloor = 0.0 };
        var effects = Mapper.MapToEffects(track, Path(5), WholeSong, settings, Registry);

        Assert.Equal("Arch1", effects[0].Target.Value);
        Assert.Equal("Arch3", effects[1].Target.Value); // full +2 leap, unmodified -- fallback disabled
    }

    [Fact]
    public void RestAndHold_NeverAffectedByConfidenceFallback_RegardlessOfConfidence()
    {
        // Hold's duration and Rest's silence are timing decisions, not pitch decisions -- a very low
        // confidence must not reroute either of them through the onset-only pulse path.
        var first = Note(0L, 5_000L, 60, confidence: 0.9); // confident enough to be the reference point for both later notes
        var sustained = Note(5_000L, 12_000L, 60, confidence: 0.05); // touches exactly -- Hold; its own confidence is deliberately near-zero
        var afterRest = Note(2_010_000L, 2_020_000L, 64, confidence: 0.05); // real musical rest gap (>= RestGapTicks) from first's own EndTicks; also deliberately near-zero confidence
        var track = Classified(first, sustained, afterRest);

        Assert.Equal(PitchMotion.Hold, track.Gestures[1].Motion);
        Assert.Equal(PitchMotion.Rest, track.Gestures[2].Motion);

        var effects = Mapper.MapToEffects(track, Path(5), WholeSong, GestureMapSettings.Default, Registry);

        Assert.Single(effects); // the Hold extended the first effect in place; the Rest produced no stray effect
        Assert.Equal(0L, effects[0].StartTicks);
        Assert.Equal(12_000L, effects[0].EndTicks); // Hold's own real note-off tick, unaffected by low confidence
    }
}
