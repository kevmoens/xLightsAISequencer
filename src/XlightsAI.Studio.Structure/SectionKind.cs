namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Section 3's own proposed enum, verbatim. Candidate song-section labels Step 054 assigns to
/// each already-approved <see cref="SectionBoundaryCandidate"/> segment. <see cref="Unknown"/> is a
/// first-class, valid outcome (Step 054's own implementation guidance, verbatim: "Unknown is valid;
/// never force classical song structure") -- never a fallback that implies a classification failure.
/// </summary>
public enum SectionKind
{
    Unknown,
    Intro,
    Verse,
    PreChorus,
    Chorus,
    Bridge,
    Solo,
    Outro,
}
