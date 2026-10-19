using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14 Step 092 ("Apply motif variations by occurrence and climax scheduling") own analysis boundary:
/// reapplies every already-defined PHASE-08 Step 057 <c>ChoreographyTemplate</c> occurrence named in a
/// request set with a controlled, bounded color/intensity variation, and schedules exactly one occurrence
/// per template as its own "climax" -- the occurrence carrying the single highest
/// <see cref="MotifOccurrenceRequest.SectionEnergy"/> in that template's own occurrence group -- so the
/// motif's final (highest-energy) appearance always receives a strictly stronger, provable "intentional
/// lift" than every other occurrence of the same motif, while every occurrence keeps the exact same
/// <c>RecipeId</c>/<c>RecipeVersion</c>/<c>Target</c>/<c>Layer</c>/relative-timing shape its template already
/// defines ("Final chorus preserves recognizable motif with intentional lift," this step's own required
/// acceptance, verbatim).
/// </summary>
public interface IMotifVariationScheduler
{
    /// <summary>
    /// Previews and intensity-schedules every entry in <paramref name="occurrenceRequests"/>. Pure/read-only:
    /// never mutates <paramref name="templateAnalysis"/>, <paramref name="phraseAnalysis"/> or
    /// <paramref name="destinationDocument"/>, and never itself writes to any <see cref="SequenceDocument"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Any reference-type argument, or any element of <paramref name="occurrenceRequests"/>, is <see langword="null"/>.</exception>
    /// <exception cref="MotifVariationInvalidException">
    /// <paramref name="occurrenceRequests"/> is empty; any request fails its own <see cref="MotifOccurrenceRequest.Validate"/>;
    /// two requests share the same <c>(TemplateId, TargetPhraseId)</c> pair; or a previewed effect carries an
    /// <c>"Intensity."</c>-prefixed parameter whose value does not parse as a finite number.
    /// </exception>
    /// <exception cref="ChoreographyTemplateInvalidException">A request names an unknown <c>TemplateId</c>, or a <c>TargetPhraseId</c>/template source phrase that is not found in <paramref name="phraseAnalysis"/> -- propagated unchanged from the underlying <c>IChoreographyTemplatePlanner.PreviewInstance</c> call.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was already cancelled.</exception>
    MotifVariationPlan Schedule(
        SongId songId,
        ChoreographyTemplateAnalysis templateAnalysis,
        PhraseSegmentationAnalysis phraseAnalysis,
        SequenceDocument destinationDocument,
        IReadOnlyList<MotifOccurrenceRequest> occurrenceRequests,
        DateTimeOffset nowUtc,
        CancellationToken ct);
}
