namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 070 ("Implement build, silence, transitions, release and phrase-ending recipes"): the
/// reusable semantic transition kinds named verbatim in this step's own heading. Each kind maps to exactly
/// one deterministic start/end intensity recipe in <see cref="TransitionRecipePlanner"/>.
/// </summary>
public enum TransitionKind
{
    /// <summary>A rising intensity ramp leading into a louder/busier section -- "build."</summary>
    Build,

    /// <summary>An intentional full darkening of every targeted prop -- "silence." This step's own required
    /// acceptance, verbatim: "Musical break can intentionally darken all permitted props."</summary>
    Blackout,

    /// <summary>A steady, neutral bridge between two sections -- "transitions."</summary>
    Transition,

    /// <summary>A falling intensity ramp after a peak -- "release."</summary>
    Release,

    /// <summary>A taper toward (but never literally reaching) darkness at the close of a phrase -- "phrase-ending."
    /// Deliberately distinct from <see cref="Blackout"/>: a phrase ending resolves, it does not go fully dark.</summary>
    PhraseEnding,
}
