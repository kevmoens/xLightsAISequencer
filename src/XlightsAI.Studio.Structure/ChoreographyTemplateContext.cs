using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// The already-built, already-approved-up-to-this-point inputs an <see cref="IChoreographyTemplatePlanner"/>
/// (re)definition call is computed from -- Step 056's own <see cref="MotifClusteringAnalysis"/> (which
/// phrases share a motif), Step 055's own <see cref="PhraseSegmentationAnalysis"/> (every phrase's own tick
/// window), and the already-accepted <see cref="SequenceDocument"/> a template's own effects are captured
/// from. This step never re-derives motif groups or phrase boundaries; it only captures and adapts
/// already-approved choreography.
/// </summary>
/// <param name="SongId">The song every supplied analysis input must share.</param>
/// <param name="SourceHash">The source audio hash <see cref="MotifClustering"/> and <see cref="PhraseAnalysis"/> must themselves have been built from -- "missing media" is an empty value here.</param>
/// <param name="MotifClustering">The approved motif groups a template's <c>MotifGroupId</c> must belong to.</param>
/// <param name="PhraseAnalysis">The approved, already-segmented phrases a template's <c>SourcePhraseId</c>/a target's phrase id are looked up in.</param>
/// <param name="SourceDocument">The already-accepted sequence document a template's own effects are captured from.</param>
public sealed record ChoreographyTemplateContext(
    SongId SongId,
    string SourceHash,
    MotifClusteringAnalysis MotifClustering,
    PhraseSegmentationAnalysis PhraseAnalysis,
    SequenceDocument SourceDocument);
