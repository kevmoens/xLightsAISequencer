namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 069's own planning boundary: turns raw per-section energy inputs into a normalized,
/// hierarchy-preserving <see cref="VisualStory"/>. Pure and deterministic -- the same section-energy inputs
/// (in any order) always produce the same set of <see cref="SectionVisualDirection"/>s (excluding
/// <see cref="VisualStory.VersionId"/>, which is intentionally fresh every call).
/// </summary>
public interface ISectionVisualStoryPlanner
{
    /// <summary>
    /// Plans a <see cref="VisualStory"/> from <paramref name="sections"/>. Required acceptance, verbatim:
    /// "Verse/chorus intensity curve follows plan" -- louder-energy sections are planned at a strictly
    /// higher <see cref="SectionVisualDirection.Intensity"/> than quieter ones. Implementation guidance,
    /// verbatim: "Plan section intensity normalized across song; preserve energy hierarchy and avoid maximum
    /// brightness continuously."
    /// </summary>
    /// <exception cref="CompositionInvalidException"><paramref name="sections"/> is empty, contains a duplicate <see cref="SectionEnergyInput.SectionId"/>, or a negative/non-finite <see cref="SectionEnergyInput.RawEnergy"/> -- thrown before any normalization is attempted (this step's own required failure/edge case, verbatim: "Invalid input ... must not corrupt the project").</exception>
    VisualStory Plan(IReadOnlyList<SectionEnergyInput> sections);
}
