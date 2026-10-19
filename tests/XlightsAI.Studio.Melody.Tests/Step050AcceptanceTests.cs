using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Melody.Tests;

/// <summary>
/// PHASE-07 Step 050 — "Compile melody to stepping, holding, jumping choreography across ordered props."
/// Required acceptance (verbatim): "Five/seven-note fixtures produce correct path order and durations."
/// Failure/edge case (verbatim, shared with Steps 045/047/051): "Silence, invalid timing boundaries and
/// missing media must be handled explicitly."
/// </summary>
public sealed class Step050AcceptanceTests
{
    private const string Hash = "deadbeef";

    private static readonly IEffectRecipeRegistry Registry = new EffectRecipeRegistry();
    private static readonly IMelodicGestureMapper Mapper = new MelodicGestureMapper();

    private static NoteEvent Note(long startTicks, long endTicks, int midiNote, double confidence = 0.9) =>
        new(Guid.NewGuid(), startTicks, endTicks, midiNote, Velocity: 0.8, confidence, UserCorrected: false);

    private static MelodyTrack Classified(params NoteEvent[] notes)
    {
        var track = new MelodyTrack(Guid.NewGuid(), Hash, notes, []);
        return MelodicContourClassifier.Classify(track, GestureClassificationSettings.Default);
    }

    private static IReadOnlyList<ModelId> Path(int count) =>
        Enumerable.Range(1, count).Select(i => new ModelId($"Arch{i}")).ToList();

    private static readonly TimeWindow WholeSong = new(0L, 1_000_000_000L);

    [Fact]
    public void Step050_Acceptance_Passes_SevenNoteFixtureProducesCorrectPathOrderAndDurations()
    {
        // Section 12's own worked example, verbatim: C4-D4-E4-G4-E4-D4-C4 against a five-arch path
        // produces the normalized ascending/descending mapping "1,2,3,5,3,2,1" (one-based).
        var notes = new[]
        {
            Note(0L, 4_000_000L, 60),           // C4
            Note(4_200_000L, 7_900_000L, 62),   // D4
            Note(8_000_000L, 11_500_000L, 64),  // E4
            Note(11_600_000L, 16_000_000L, 67), // G4
            Note(16_100_000L, 19_000_000L, 64), // E4
            Note(19_100_000L, 22_000_000L, 62), // D4
            Note(22_100_000L, 26_000_000L, 60), // C4
        };
        var track = Classified(notes);
        var path = Path(5);

        var effects = Mapper.MapToEffects(track, path, WholeSong, GestureMapSettings.Default, Registry);

        Assert.Equal(7, effects.Count);

        var expectedArchOneBased = new[] { 1, 2, 3, 5, 3, 2, 1 };
        for (var i = 0; i < expectedArchOneBased.Length; i++)
        {
            Assert.Equal($"Arch{expectedArchOneBased[i]}", effects[i].Target.Value);
            // Duration must follow the real source note timing, never an even subdivision.
            Assert.Equal(notes[i].StartTicks, effects[i].StartTicks);
            Assert.Equal(notes[i].EndTicks, effects[i].EndTicks);
            Assert.Equal(EffectRecipeRegistry.SimpleOnRecipeId, effects[i].RecipeId);
        }
    }

    [Fact]
    public void RepeatedNote_PulsesSameTarget_NotRandomProp()
    {
        // C4, C4 (audible re-attack, sub-rest gap) -- the full-phase test matrix's own item 2, verbatim:
        // "Repeated note pulses same target, not random prop."
        var first = Note(0L, 5_000L, 60);
        var second = Note(5_500L, 10_000L, 60);
        var track = Classified(first, second);

        var effects = Mapper.MapToEffects(track, Path(5), WholeSong, GestureMapSettings.Default, Registry);

        Assert.Equal(2, effects.Count);
        Assert.Equal(effects[0].Target, effects[1].Target);
        Assert.Equal("Arch1", effects[0].Target.Value); // the walk's own starting position
    }

    [Fact]
    public void Rest_AfterAlreadyEmittedEffect_IntroducesSilence_NoStrayEffect()
    {
        // Full-phase test matrix item 4, verbatim: "Rest introduces silence/no stray effect." The first
        // note always sounds (nothing to be silent against); the note arriving after a real musical rest
        // must not.
        var first = Note(0L, 5_000L, 60);
        var second = Note(2_000_000L, 2_005_000L, 64); // 1,995,000-tick silent gap -- a real musical rest
        var track = Classified(first, second);
        Assert.Equal(PitchMotion.Rest, track.Gestures[1].Motion); // confirm the fixture actually classifies as Rest

        var effects = Mapper.MapToEffects(track, Path(5), WholeSong, GestureMapSettings.Default, Registry);

        Assert.Single(effects); // only the first note sounds; the rest-arriving second note produced no stray effect
        Assert.Equal(first.StartTicks, effects[0].StartTicks);
    }

    [Fact]
    public void Rest_WithRespectRestsFalse_StillSoundsAtCurrentPosition()
    {
        var first = Note(0L, 5_000L, 60);
        var second = Note(2_000_000L, 2_005_000L, 64);
        var track = Classified(first, second);

        var settings = GestureMapSettings.Default with { RespectRests = false };
        var effects = Mapper.MapToEffects(track, Path(5), WholeSong, settings, Registry);

        Assert.Equal(2, effects.Count);
        Assert.Equal(effects[0].Target, effects[1].Target); // Rest pulses the current position, like Repeat
    }

    [Fact]
    public void LeapInterval_SkipsTargetPositionsByDeclaredMappingPolicy()
    {
        // Full-phase test matrix item 5, verbatim: "Jump interval skips target positions by declared
        // mapping policy." E4 -> G4 is a minor-third leap (3 semitones); the declared policy moves the
        // walk by MelodicGestureMapper.LeapIndexDelta (2 positions), never by the raw semitone count.
        var first = Note(0L, 1_000L, 64);  // E4 -> Arch1 (walk start)
        var second = Note(1_100L, 2_000L, 67); // G4 -> LeapUp
        var track = Classified(first, second);

        var effects = Mapper.MapToEffects(track, Path(5), WholeSong, GestureMapSettings.Default, Registry);

        Assert.Equal("Arch1", effects[0].Target.Value);
        Assert.Equal("Arch3", effects[1].Target.Value); // +2 positions, not +3
    }

    [Fact]
    public void SustainedNote_HoldExtendsPriorEffectUntilNoteOff_RatherThanStartingANewOne()
    {
        var first = Note(0L, 5_000L, 60);
        var second = Note(5_000L, 12_000L, 60); // touches exactly -- Hold, same sustained note
        var track = Classified(first, second);
        Assert.Equal(PitchMotion.Hold, track.Gestures[1].Motion);

        var effects = Mapper.MapToEffects(track, Path(5), WholeSong, GestureMapSettings.Default, Registry);

        Assert.Single(effects); // no second effect created for the Hold
        Assert.Equal(0L, effects[0].StartTicks);
        Assert.Equal(12_000L, effects[0].EndTicks); // extended all the way to the note-off event
    }

    [Theory]
    [InlineData(MelodicGestureMapper.RelativeStepClampPolicy, "Arch3")] // index 1 + 2 = 3, clamped to the last valid index (2 => Arch3)
    [InlineData(MelodicGestureMapper.RelativeStepWrapPolicy, "Arch1")]  // index 1 + 2 = 3, wraps to index 0 => Arch1
    public void PropPathLengthMismatch_LeapPastPathEdge_RespectsDeclaredClampOrWrapPolicy(string policy, string expectedTarget)
    {
        // Known technical risk, Section 7, verbatim: "prop path length mismatch."
        var first = Note(0L, 1_000L, 60);       // Arch1 (walk start, path length 3)
        var second = Note(1_100L, 2_000L, 61);  // StepUp (+1 semitone) -> Arch2
        var third = Note(2_100L, 3_000L, 66);   // LeapUp (+5 semitones) from Arch2 -> index 3, out of a 3-long path's bounds
        var track = Classified(first, second, third);

        var settings = GestureMapSettings.Default with { PitchPolicy = policy };
        var effects = Mapper.MapToEffects(track, Path(3), WholeSong, settings, Registry);

        Assert.Equal(3, effects.Count);
        Assert.Equal(expectedTarget, effects[2].Target.Value);
    }

    [Fact]
    public void Silence_EmptyTrack_ProducesEmptyEffectList_NoCrash_EdgeCase()
    {
        var track = Classified();

        var effects = Mapper.MapToEffects(track, Path(5), WholeSong, GestureMapSettings.Default, Registry);

        Assert.Empty(effects);
    }

    [Fact]
    public void NotesOutsideWindow_AreSilentlyExcluded_NotAnError()
    {
        var inWindow = Note(100L, 1_000L, 60);
        var outsideWindow = Note(2_000_000L, 2_001_000L, 62);
        var track = Classified(inWindow, outsideWindow);

        var effects = Mapper.MapToEffects(track, Path(5), new TimeWindow(0L, 1_500_000L), GestureMapSettings.Default, Registry);

        Assert.Single(effects);
        Assert.Equal(inWindow.StartTicks, effects[0].StartTicks);
    }

    [Fact]
    public void InvalidTimingBoundary_WindowEndNotAfterStart_ThrowsEffectPlanInvalidException_NegativePath()
    {
        var track = Classified(Note(0L, 1_000L, 60));

        Assert.Throws<EffectPlanInvalidException>(
            () => Mapper.MapToEffects(track, Path(5), new TimeWindow(5_000L, 5_000L), GestureMapSettings.Default, Registry));
    }

    [Fact]
    public void EmptyOrderedPath_ThrowsEffectPlanInvalidException_NegativePath()
    {
        var track = Classified(Note(0L, 1_000L, 60));

        Assert.Throws<EffectPlanInvalidException>(
            () => Mapper.MapToEffects(track, [], WholeSong, GestureMapSettings.Default, Registry));
    }

    [Fact]
    public void UnknownPitchPolicy_ThrowsEffectPlanInvalidException_NegativePath()
    {
        var track = Classified(Note(0L, 1_000L, 60));
        var settings = GestureMapSettings.Default with { PitchPolicy = "NotARealPolicy" };

        var ex = Assert.Throws<EffectPlanInvalidException>(
            () => Mapper.MapToEffects(track, Path(5), WholeSong, settings, Registry));
        Assert.Contains("NotARealPolicy", ex.Message);
    }

    [Fact]
    public void UnknownRecipeId_ThrowsEffectPlanInvalidException_NegativePath()
    {
        var track = Classified(Note(0L, 1_000L, 60));
        var settings = GestureMapSettings.Default with { RecipeId = "effect.not-real.v1" };

        Assert.Throws<EffectPlanInvalidException>(
            () => Mapper.MapToEffects(track, Path(5), WholeSong, settings, Registry));
    }

    [Fact]
    public void NullTrack_ThrowsArgumentNullException_NegativePath()
    {
        Assert.Throws<ArgumentNullException>(
            () => Mapper.MapToEffects(null!, Path(5), WholeSong, GestureMapSettings.Default, Registry));
    }

    [Fact]
    public void NullOrderedPath_ThrowsArgumentNullException_NegativePath()
    {
        var track = Classified(Note(0L, 1_000L, 60));

        Assert.Throws<ArgumentNullException>(
            () => Mapper.MapToEffects(track, null!, WholeSong, GestureMapSettings.Default, Registry));
    }

    [Fact]
    public void NullSettings_ThrowsArgumentNullException_NegativePath()
    {
        var track = Classified(Note(0L, 1_000L, 60));

        Assert.Throws<ArgumentNullException>(
            () => Mapper.MapToEffects(track, Path(5), WholeSong, null!, Registry));
    }

    [Fact]
    public void NullRecipeRegistry_ThrowsArgumentNullException_NegativePath()
    {
        var track = Classified(Note(0L, 1_000L, 60));

        Assert.Throws<ArgumentNullException>(
            () => Mapper.MapToEffects(track, Path(5), WholeSong, GestureMapSettings.Default, null!));
    }
}
