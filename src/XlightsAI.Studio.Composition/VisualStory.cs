namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 069 ("Build sectional energy and color/brightness story"): the full, versioned, per-song
/// intensity/palette story across every section -- PHASE-10.md Section 3's own proposed signature, with
/// <c>Sections</c> realized by this step's own narrower <see cref="SectionVisualDirection"/> (see its own
/// doc comment for the disclosed deviation).
/// </summary>
/// <param name="VersionId">A fresh identity minted by <see cref="ISectionVisualStoryPlanner.Plan"/> every time a story is (re)planned -- never reused across two independently planned stories, even over identical inputs.</param>
/// <param name="Sections">Every planned section direction, in the same order <see cref="ISectionVisualStoryPlanner.Plan"/> received its own section-energy inputs.</param>
public sealed record VisualStory(Guid VersionId, IReadOnlyList<SectionVisualDirection> Sections)
{
    /// <summary>
    /// Validates every section direction before any persistence is attempted -- this step's own required
    /// failure/edge case, verbatim: "Invalid input, cancellation or stale persisted state must not corrupt
    /// the project."
    /// </summary>
    /// <exception cref="CompositionInvalidException"><see cref="Sections"/> is empty, contains a duplicate <see cref="SectionVisualDirection.SectionId"/>, a blank <see cref="SectionVisualDirection.PaletteId"/>, or an <see cref="SectionVisualDirection.Intensity"/> outside <c>[0,1]</c> or non-finite.</exception>
    public void Validate()
    {
        if (Sections.Count == 0)
        {
            throw new CompositionInvalidException("A VisualStory must contain at least one section.");
        }

        var seen = new HashSet<Guid>();
        foreach (var section in Sections)
        {
            if (!seen.Add(section.SectionId))
            {
                throw new CompositionInvalidException($"Duplicate section id '{section.SectionId}' in VisualStory.");
            }

            if (string.IsNullOrWhiteSpace(section.PaletteId))
            {
                throw new CompositionInvalidException($"Section '{section.SectionId}' must have a non-blank PaletteId.");
            }

            if (!double.IsFinite(section.Intensity) || section.Intensity < 0.0 || section.Intensity > 1.0)
            {
                throw new CompositionInvalidException(
                    $"Section '{section.SectionId}' Intensity must be a finite number in [0,1] (was {section.Intensity}).");
            }
        }
    }
}
