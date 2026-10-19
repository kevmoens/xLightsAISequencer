using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Composition;

/// <summary>Default <see cref="ISectionVisualStoryPlanner"/>. See the interface and <c>implementation-evidence/PHASE-10/STEP-069.md</c> for the full rationale.</summary>
public sealed class SectionVisualStoryPlanner : ISectionVisualStoryPlanner
{
    /// <summary>
    /// The lowest intensity a planned section can ever receive -- never literal <c>0.0</c>, so a section is
    /// never planned as fully, permanently dark by this step's own normalization alone (Step 070's own later
    /// "build, silence, transitions" scope is what intentionally darkens a section, not this one).
    /// </summary>
    private const double FloorIntensity = 0.05;

    /// <summary>
    /// The highest intensity a planned section can ever receive -- strictly below <c>1.0</c>. Implementation
    /// guidance, verbatim: "avoid maximum brightness continuously" -- realized here as a hard ceiling no
    /// section's own normalized energy can cross, so even the single loudest section in a song is never
    /// planned at literal maximum brightness.
    /// </summary>
    private const double CeilingIntensity = 0.92;

    /// <summary>The intensity assigned to every section when every <see cref="SectionEnergyInput.RawEnergy"/> is identical -- a moderate midpoint, neither the floor nor the ceiling, since a perfectly flat input carries no energy hierarchy to preserve.</summary>
    private const double FlatInputIntensity = (FloorIntensity + CeilingIntensity) / 2.0;

    private static readonly IReadOnlyDictionary<SectionKind, string> PaletteByKind = new Dictionary<SectionKind, string>
    {
        [SectionKind.Intro] = "Soft",
        [SectionKind.Verse] = "Calm",
        [SectionKind.PreChorus] = "Build",
        [SectionKind.Chorus] = "Vivid",
        [SectionKind.Bridge] = "Transition",
        [SectionKind.Solo] = "Spotlight",
        [SectionKind.Outro] = "Soft",
        [SectionKind.Unknown] = "Neutral",
    };

    public VisualStory Plan(IReadOnlyList<SectionEnergyInput> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);

        if (sections.Count == 0)
        {
            throw new CompositionInvalidException("At least one SectionEnergyInput is required to plan a VisualStory.");
        }

        var seen = new HashSet<Guid>();
        foreach (var section in sections)
        {
            if (!seen.Add(section.SectionId))
            {
                throw new CompositionInvalidException($"Duplicate section id '{section.SectionId}' in planner input.");
            }

            if (!double.IsFinite(section.RawEnergy) || section.RawEnergy < 0.0)
            {
                throw new CompositionInvalidException(
                    $"Section '{section.SectionId}' RawEnergy must be a non-negative finite number (was {section.RawEnergy}).");
            }
        }

        var min = sections.Min(s => s.RawEnergy);
        var max = sections.Max(s => s.RawEnergy);
        var range = max - min;

        var directions = new List<SectionVisualDirection>(sections.Count);
        foreach (var section in sections)
        {
            var intensity = range <= 0.0
                ? FlatInputIntensity
                : FloorIntensity + ((section.RawEnergy - min) / range * (CeilingIntensity - FloorIntensity));

            directions.Add(new SectionVisualDirection(section.SectionId, section.Kind, PaletteId(section.Kind), intensity));
        }

        return new VisualStory(Guid.NewGuid(), directions);
    }

    private static string PaletteId(SectionKind kind) => PaletteByKind.TryGetValue(kind, out var palette) ? palette : "Neutral";
}
