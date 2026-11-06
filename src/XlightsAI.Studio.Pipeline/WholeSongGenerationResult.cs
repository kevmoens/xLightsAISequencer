using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Everything <see cref="IWholeSongGenerator.GenerateAsync"/> produces for one song: the final,
/// exportable <see cref="Document"/> the Show Import/Generate &amp; Export screen already consumed
/// before this type existed, plus the intermediate analysis artifacts (<see cref="Melody"/>,
/// <see cref="Phrases"/>, <see cref="Motifs"/>) the pipeline always computed internally to build
/// that document but previously discarded the moment <c>GenerateAsync</c> returned.
///
/// Those artifacts are exactly what SCREEN 10 PIANO ROLL (<see cref="XlightsAI.Studio.Melody.MelodyTrackRevisionState"/>'s
/// own editing session needs a <see cref="MelodyTrack"/> to begin from) and SCREEN 11 SONG STRUCTURE
/// (needs a <see cref="PhraseSegmentationAnalysis"/>/<see cref="MotifClusteringAnalysis"/> pair to
/// open) require — the same "a result was computed and thrown away" shape as
/// docs/UI-CLEANUP-PLAN.md item 1's <c>ProjectManifest</c> fix, applied here to unblock those two
/// screens instead of being a second, parallel analysis run the Shell would otherwise have to pay
/// for again.
/// </summary>
/// <param name="LookTemplateUsage">
/// Build-plan Phase 10 §6b task 4: which seed multi-layer template (if any, by its catalog id)
/// informed each generated Look, by Look id/name -- so the Shell can show a user watching a generation
/// run which named pattern a given Look actually drew from. Deliberately a flat, lightweight list of
/// primitive-typed records rather than carrying the full <c>ShowBibleContent</c>/<c>Look</c> type here:
/// those live in <c>XlightsAI.Studio.Director</c>/<c>XlightsAI.Studio.Choreography</c>, and this project
/// cannot reference either without a cyclic project reference. Empty (never null) for a generator that
/// predates this field or produced no templated Looks.
/// </param>
/// <param name="GenerationRunId">
/// Build-plan "richer analysis tools" post-processing phase (2026-11-02): only
/// <c>AiChoreographyWholeSongGenerator</c> sets this -- the internal id its own real LLM-driven pipeline
/// used to persist this run's Show Bible/SectionScripts under its project directory (<c>bible/</c>/
/// <c>scripts/</c>), so a later post-processing/touch-up prompt can reload and selectively patch THIS
/// exact run rather than guessing. <see langword="null"/> (the default) for every other generator, and
/// for any caller that predates this field.
/// </param>
public sealed record WholeSongGenerationResult(
    SequenceDocument Document,
    SongId SongId,
    MelodyTrack Melody,
    PhraseSegmentationAnalysis Phrases,
    MotifClusteringAnalysis Motifs,
    IReadOnlyList<GeneratedLookTemplateUsage>? LookTemplateUsage = null,
    Guid? GenerationRunId = null)
{
    public IReadOnlyList<GeneratedLookTemplateUsage> LookTemplateUsageOrEmpty => LookTemplateUsage ?? [];
}

/// <summary>One generated Look's own declared seed-template provenance (see <see cref="WholeSongGenerationResult.LookTemplateUsage"/>).</summary>
/// <param name="LookId">The Look's own id within its Show Bible's Look library.</param>
/// <param name="LookName">The Look's human-readable display name.</param>
/// <param name="TemplateId">The seed template catalog id this Look's layers were modeled on, or <see langword="null"/> when the Director did not declare one for this Look.</param>
public sealed record GeneratedLookTemplateUsage(string LookId, string LookName, string? TemplateId);
