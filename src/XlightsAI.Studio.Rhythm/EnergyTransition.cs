namespace XlightsAI.Studio.Rhythm;

/// <summary>Which direction one detected <see cref="EnergyTransition"/> moved.</summary>
public enum EnergyTransitionKind
{
    /// <summary>A sustained climb in overall band energy over the detector's own comparison window -- a deliberate build-up candidate.</summary>
    Buildup,

    /// <summary>A sharp fall in overall band energy over the detector's own comparison window -- a sudden drop/release candidate.</summary>
    Drop,
}

/// <summary>
/// Build-plan "richer analysis tools" Phase 4 (2026-11-02): one real, measured moment where this song's
/// own overall band energy rose or fell sharply -- a candidate drop/build-up moment, as opposed to the
/// Director prompt's existing "design for pops and build-ups" guidance (<c>DirectorPromptBuilder</c>),
/// which until this phase was pure unverified prose with nothing real to point at. Produced by
/// <see cref="EnergyTransitionDetector"/> as a pure post-analysis pass over <see cref="BandEnergyEnvelope"/>
/// -- no new signal processing, just a derivative/slope comparison over data this codebase already
/// computes.
/// </summary>
/// <param name="TimeTicks">The instant (100ns ticks from song start) the transition is centered on.</param>
/// <param name="Kind">Which direction the energy moved.</param>
/// <param name="Magnitude">The measured before/after average band-energy delta driving this detection, always positive regardless of <see cref="Kind"/> -- larger is a more pronounced transition.</param>
public sealed record EnergyTransition(long TimeTicks, EnergyTransitionKind Kind, float Magnitude);
