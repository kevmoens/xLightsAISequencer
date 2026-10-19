namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Section 3's own proposed signature, verbatim: the relative-motion classification a
/// <see cref="MelodicGesture"/> carries. Deliberately relative (Repeat/StepUp/StepDown/LeapUp/LeapDown),
/// never an absolute pitch name -- Section 3's own architectural design note: "build the gesture mapping
/// from relative motion so an octave error does not necessarily ruin a lighting phrase." Step 047 (not
/// this step) is responsible for actually classifying contour into these values from noisy pitch data;
/// this step only defines the fixed vocabulary every later step must use.
/// </summary>
public enum PitchMotion
{
    /// <summary>Same pitch as the previous sounding note.</summary>
    Repeat,

    /// <summary>A small upward interval from the previous sounding note (step, not leap; threshold is a Step 047 concern).</summary>
    StepUp,

    /// <summary>A small downward interval from the previous sounding note.</summary>
    StepDown,

    /// <summary>A large upward interval from the previous sounding note.</summary>
    LeapUp,

    /// <summary>A large downward interval from the previous sounding note.</summary>
    LeapDown,

    /// <summary>Explicit musical silence -- no note sounding (Section 3: "Represent rests explicitly").</summary>
    Rest,

    /// <summary>The same note sustained across what would otherwise be a new onset boundary.</summary>
    Hold,
}
