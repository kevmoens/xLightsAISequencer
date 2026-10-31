using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Architecture §10's structural-arc ceiling table, as a reusable default for callers (the Shell's AI
/// choreography wiring) that have no other source of per-<see cref="SectionKind"/> energy ceilings yet.
/// The real <see cref="SectionKind"/> enum has no distinct Verse2/Chorus2/Final-chorus rows, so each
/// table row below collapses onto the nearest matching <see cref="SectionKind"/> using the row's single
/// midpoint value where architecture §10 gives a range -- <see cref="SectionKind.Solo"/> has no
/// architecture §10 row at all, so it is treated as a featured, chorus-adjacent moment rather than left
/// unconstrained.
/// </summary>
public static class DefaultEnergyCeilings
{
    public static readonly IReadOnlyDictionary<SectionKind, double> ArchitectureSection10 = new Dictionary<SectionKind, double>
    {
        [SectionKind.Unknown] = 0.50,
        [SectionKind.Intro] = 0.20,
        [SectionKind.Verse] = 0.35,
        [SectionKind.PreChorus] = 0.55,
        [SectionKind.Chorus] = 0.85,
        [SectionKind.Bridge] = 0.22,
        [SectionKind.Solo] = 0.80,
        [SectionKind.Outro] = 0.10,
    };
}
