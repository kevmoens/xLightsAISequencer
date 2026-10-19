using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14 Step 090: the full, versioned, per-song creative-direction plan across every section -- a
/// narrower, explicitly-scoped realization of PHASE-14.md Section 3's own proposed
/// <c>WholeSongPlan(Guid Id, Guid SongId, IReadOnlyList&lt;SongSectionPlan&gt; Sections,
/// IReadOnlyList&lt;TransitionPlan&gt; Transitions, long BaseRevision)</c>. <c>Transitions</c> is
/// deliberately omitted here -- disclosed in <c>implementation-evidence/PHASE-14/STEP-090.md</c> -- because
/// transition-aware seam assembly is Step 091's own later, disclosed scope; this step owns only the
/// song-level direction layer (energy, contrast, palette, focal role, motif identity) that Step 091 will
/// later consume as one of its own inputs.
/// </summary>
/// <param name="Id">A fresh identity minted by <see cref="ISongDirectionPlanner.Plan"/> every time a plan is (re)planned -- never reused across two independently planned plans, even over identical inputs.</param>
/// <param name="SongId">The <c>SongAsset.Id</c>/<see cref="SongId"/> this plan was created for.</param>
/// <param name="SourceSectionLabelVersionId">The exact <c>SectionLabelAnalysis.VersionId</c> this plan's section ids were validated against -- provenance proving which upstream structure analysis produced the section ids this plan links to (Step 090's own "User-visible outcome," verbatim: "Saved audio/analysis artifact with timestamp provenance").</param>
/// <param name="SourceMotifClusteringVersionId">The exact <c>MotifClusteringAnalysis.VersionId</c> this plan's motif group links were validated against.</param>
/// <param name="BaseRevision">A caller-supplied non-negative revision/provenance marker for the upstream state this plan was derived from (the same "BaseRevision" convention already used by <c>LoopRun</c>/<c>EditProposal</c>/<c>VisualStory</c>'s own sibling records across this repository) -- never negative.</param>
/// <param name="CreatedAtUtc">The exact timestamp this plan was created -- Step 090's own required "timestamp provenance," verbatim.</param>
/// <param name="Sections">Every section's own validated creative direction. Always ordered by the section's own <c>SectionLabelCandidate.StartBarIndex</c> ascending -- the same ordering <see cref="ISongDirectionPlanner.Plan"/> uses to build <see cref="SongDirectionTimelineEntry"/> ("Plan renders as inspectable timeline summary," Step 090's own required acceptance, verbatim).</param>
public sealed record SongDirectionPlan(
    Guid Id,
    SongId SongId,
    Guid SourceSectionLabelVersionId,
    Guid SourceMotifClusteringVersionId,
    long BaseRevision,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<SongSectionDirection> Sections)
{
    /// <summary>
    /// Structural self-validation with no external dependency -- mirrors <c>VisualStory.Validate()</c>'s own
    /// convention. Cross-reference validation against a specific song's section/motif ids is
    /// <see cref="ISongDirectionPlanner.Plan"/>'s own separate responsibility (it has access to the source
    /// analyses this record does not retain beyond their version ids).
    /// </summary>
    /// <exception cref="SongDirectionInvalidException"><see cref="Sections"/> is empty, contains a duplicate <see cref="SongSectionDirection.SectionId"/>, a blank <see cref="SongSectionDirection.PaletteId"/>, a <see cref="SongSectionDirection.TargetEnergy"/>/<see cref="SongSectionDirection.TargetContrast"/> outside <c>[0,1]</c> or non-finite, or a negative <see cref="BaseRevision"/>.</exception>
    public void Validate()
    {
        if (BaseRevision < 0)
        {
            throw new SongDirectionInvalidException($"SongDirectionPlan '{Id}' BaseRevision must not be negative (was {BaseRevision}).");
        }

        if (Sections.Count == 0)
        {
            throw new SongDirectionInvalidException($"SongDirectionPlan '{Id}' must contain at least one section.");
        }

        var seen = new HashSet<Guid>();
        foreach (var section in Sections)
        {
            if (!seen.Add(section.SectionId))
            {
                throw new SongDirectionInvalidException($"Duplicate section id '{section.SectionId}' in SongDirectionPlan '{Id}'.");
            }

            if (string.IsNullOrWhiteSpace(section.PaletteId))
            {
                throw new SongDirectionInvalidException($"Section '{section.SectionId}' must have a non-blank PaletteId.");
            }

            if (!double.IsFinite(section.TargetEnergy) || section.TargetEnergy is < 0.0 or > 1.0)
            {
                throw new SongDirectionInvalidException(
                    $"Section '{section.SectionId}' TargetEnergy must be a finite number in [0,1] (was {section.TargetEnergy}).");
            }

            if (!double.IsFinite(section.TargetContrast) || section.TargetContrast is < 0.0 or > 1.0)
            {
                throw new SongDirectionInvalidException(
                    $"Section '{section.SectionId}' TargetContrast must be a finite number in [0,1] (was {section.TargetContrast}).");
            }

            var seenMotif = new HashSet<Guid>();
            foreach (var motifGroupId in section.MotifGroupIds)
            {
                if (!seenMotif.Add(motifGroupId))
                {
                    throw new SongDirectionInvalidException(
                        $"Section '{section.SectionId}' lists motif group '{motifGroupId}' more than once.");
                }
            }
        }
    }
}
