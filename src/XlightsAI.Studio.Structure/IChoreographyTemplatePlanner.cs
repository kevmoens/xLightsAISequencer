using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Step 057's own analysis boundary: defines/refreshes a <see cref="ChoreographyTemplate"/> from an
/// already-approved motif occurrence, records per-occurrence <see cref="ChoreographyInstance"/> overrides,
/// and previews the real adapted effect list for any one occurrence. A phrase-local, concrete precursor to
/// PHASE-08 Section 3's proposed broader <c>IMotifInheritancePlanner</c> -- this step never wires its own
/// preview output into a <see cref="SequenceDocument"/> edit transaction or any UI (Step 058's own later
/// scope: "Build phrase relationship/propagation inspector").
/// </summary>
public interface IChoreographyTemplatePlanner
{
    /// <summary>
    /// (Re)defines the template for <paramref name="motifGroupId"/>/<paramref name="sourcePhraseId"/>:
    /// captures every effect in <paramref name="context"/>'s own <see cref="ChoreographyTemplateContext.SourceDocument"/>
    /// that is fully contained within the source phrase's own tick window, relative to that phrase's own
    /// start. Calling this again for the same pair after the source document's own effects changed is
    /// "editing the master" -- the same <see cref="ChoreographyTemplate.Id"/> and every existing
    /// <see cref="ChoreographyTemplate.Instances"/> entry are preserved across the refresh.
    /// </summary>
    /// <param name="current">The caller's own last-known persisted analysis for this song, or <see langword="null"/> when none has ever been saved. Must match what is actually persisted (Step 057's own "stale persisted state must not corrupt the project" failure/edge case, verbatim), or the call throws rather than silently overwriting a concurrent change.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/>, <paramref name="mask"/>, or <paramref name="rhythmLayers"/> is <see langword="null"/>.</exception>
    /// <exception cref="ChoreographyTemplateInvalidException">
    /// Missing media, a mismatched/stale provenance chain, an unknown <paramref name="motifGroupId"/>, a
    /// <paramref name="sourcePhraseId"/> that is not a member of that group or not found in <paramref name="context"/>'s
    /// own phrase analysis, or a stale <paramref name="current"/>.
    /// </exception>
    Task<ChoreographyTemplateAnalysis> DefineTemplateAsync(
        ChoreographyTemplateAnalysis? current,
        ChoreographyTemplateContext context,
        Guid motifGroupId,
        Guid sourcePhraseId,
        ChoreographyInheritanceMask mask,
        IReadOnlySet<int> rhythmLayers,
        CancellationToken ct);

    /// <summary>
    /// Records (or replaces) one instance-level override for <paramref name="targetPhraseId"/>'s own
    /// occurrence of <paramref name="templateId"/>. Never affects the template itself or any other
    /// occurrence's own preview (Step 057's own required acceptance, verbatim: "Edit master/motif occurrence
    /// without surprising cross-section changes").
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="current"/>, <paramref name="context"/>, or <paramref name="override"/> is <see langword="null"/>.</exception>
    /// <exception cref="ChoreographyTemplateInvalidException">
    /// An unknown <paramref name="templateId"/>, a <paramref name="targetPhraseId"/> that is not a member of
    /// the template's own motif group, a malformed <paramref name="override"/> (e.g. <c>EndTicks &lt;= StartTicks</c>), or a stale <paramref name="current"/>.
    /// </exception>
    Task<ChoreographyTemplateAnalysis> SetInstanceOverrideAsync(
        ChoreographyTemplateAnalysis current,
        ChoreographyTemplateContext context,
        Guid templateId,
        Guid targetPhraseId,
        ChoreographyInstanceOverride @override,
        CancellationToken ct);

    /// <summary>
    /// Computes the real, adapted effect list <paramref name="targetPhraseId"/>'s own occurrence of
    /// <paramref name="templateId"/> would receive if accepted -- "preview effect list and respect locks"
    /// (Step 057's own implementation guidance, verbatim). Pure/read-only: never mutates
    /// <paramref name="analysis"/> or <paramref name="destinationDocument"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="ChoreographyTemplateInvalidException">An unknown <paramref name="templateId"/>, or <paramref name="targetPhraseId"/>/the template's own source phrase id is not found in <paramref name="phraseAnalysis"/>.</exception>
    ChoreographyPreviewResult PreviewInstance(
        ChoreographyTemplateAnalysis analysis,
        Guid templateId,
        Guid targetPhraseId,
        PhraseSegmentationAnalysis phraseAnalysis,
        SequenceDocument destinationDocument);
}
