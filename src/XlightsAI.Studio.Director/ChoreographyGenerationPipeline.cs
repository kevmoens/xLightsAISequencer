using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Rendering;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director;

/// <summary>Everything <see cref="ChoreographyGenerationPipeline"/> needs besides the song itself and the two optional AI role clients.</summary>
/// <param name="Layout">The real show layout this song is compiled against.</param>
/// <param name="Vocabulary">The generated effect vocabulary (architecture §8a).</param>
/// <param name="RecipeCatalog">Registered effect recipes (<c>GeneratedEffectRecipeFactory.RegisterAll</c>).</param>
/// <param name="RecipeRegistry">Verifies/constructs each concrete effect instance.</param>
/// <param name="EnergyCeilingByKind">Per-<see cref="SectionKind"/> energy ceiling (architecture §10's structural arc).</param>
/// <param name="ProjectDirectory">The project root -- <c>bible/</c>/<c>scripts/</c>/<c>llm/</c>/<c>exports/</c> (architecture §13) are created under this.</param>
/// <param name="ShowDirectory">
/// Build-plan "richer analysis tools" Phase 3 (2026-11-02): the real, on-disk xLights show directory
/// (<see cref="Rendering.RenderRequest.ShowDirectory"/>) -- required only when
/// <see cref="CustomGenerationPrompt.EnableQualityPass"/> is requested; <see langword="null"/> (the
/// default, and every pre-existing caller's behavior) for every run that never asks for a quality pass.
/// </param>
/// <param name="XlightsExecutablePath">
/// Build-plan "richer analysis tools" Phase 3 (2026-11-02): the real, configured xLights executable
/// (the same setting the Shell's own "Open in xLights" action already uses) -- required only when
/// <see cref="CustomGenerationPrompt.EnableQualityPass"/> is requested; <see langword="null"/> (the
/// default) for every run that never asks for one.
/// </param>
public sealed record ChoreographyGenerationSettings(
    DisplayGraph Layout,
    EffectVocabularyCatalog Vocabulary,
    EffectRecipeCatalog RecipeCatalog,
    IEffectRecipeRegistry RecipeRegistry,
    IReadOnlyDictionary<SectionKind, double> EnergyCeilingByKind,
    string ProjectDirectory,
    string? ShowDirectory = null,
    string? XlightsExecutablePath = null);

/// <summary>One full pipeline run's outcome.</summary>
public sealed record ChoreographyGenerationReport(
    ShowBible Bible,
    IReadOnlyDictionary<Guid, SectionScript> Scripts,
    ChoreographyEngine.Result CompiledResult,
    ExportResult Export,
    int SectionCount,
    int SectionsNeedingRetry);

/// <summary>
/// Build-plan Phase 6 task 4: "End-to-end first full generation: song + layout -&gt; Show Bible -&gt;
/// per-section scripts -&gt; compiled IR -&gt; exported .xsq, with no render/critique loop yet. This is the
/// first point a complete LLM-driven sequence exists." A thin orchestrator over already-proven pieces
/// (<see cref="ShowBibleGenerator"/>, <see cref="SectionScriptBatchGenerator"/>, <see cref="SectionScriptCompiler"/>,
/// <see cref="ChoreographyEngine"/>, <see cref="XsqExporter"/>) -- it makes no choreography decisions of
/// its own and introduces no new validation rules; every rule it relies on already lives in one of those
/// pieces.
/// </summary>
public sealed class ChoreographyGenerationPipeline
{
    /// <summary>100ns ticks/second -- same unit/constant as every other tick field in this codebase.</summary>
    private const long TicksPerSecond = 10_000_000L;

    /// <summary>
    /// Build-plan "richer analysis tools" Phase 3 (2026-11-02) default bound for an opt-in quality pass:
    /// iteration 0 (the already-compiled starting point) plus up to this many more revise/re-render
    /// iterations, stopping earlier whenever <see cref="IterationLoop.RunNextAsync"/> itself reports
    /// convergence (returns <see langword="null"/>). Deliberately smaller than
    /// <see cref="IterationLoop.RunBestOfNAsync"/>'s own 4-8 best-of-N search bound -- this is a
    /// sequential revise loop paying for a real render each step, not a parallel search, so a modest
    /// default keeps the "genuinely slower" cost this toggle discloses bounded rather than open-ended.
    /// </summary>
    private const int QualityPassMaxIterations = 3;

    private readonly IChatCompletionClient? _directorClient;
    private readonly string? _directorModelName;
    private readonly IChatCompletionClient? _choreographerClient;
    private readonly string? _choreographerModelName;
    private readonly DirectorKnowledgeContext _knowledge;
    private readonly string _style;
    private readonly int _maxOutputTokens;
    private readonly CustomGenerationPrompt? _customPrompt;
    private readonly IVisionCapableChatCompletionClient? _criticClient;
    private readonly string? _criticModelName;
    private readonly ILogger<ChoreographyGenerationPipeline> _logger;

    /// <param name="customPrompt">
    /// Build-plan Phase 14 §2c: the user's own free-text creative direction for this run, or
    /// <see langword="null"/>/empty for the pre-existing default -- every call site before this
    /// phase (including every existing test and <c>tools/GenerateOnce</c>) keeps compiling and
    /// behaving identically.
    /// </param>
    /// <param name="criticClient">
    /// Build-plan "richer analysis tools" Phase 3 (2026-11-02): resolved from <see cref="AiRole.Critic"/>
    /// by the caller, or <see langword="null"/> (the default) when that role is not configured. Only
    /// consulted when <see cref="CustomGenerationPrompt.EnableQualityPass"/> is requested -- never
    /// required for it: a quality pass with no configured Critic still runs, judging convergence on
    /// numeric metrics alone (<see cref="IterationLoop"/> already supports a <see langword="null"/>
    /// <c>VisualCritic</c>).
    /// </param>
    public ChoreographyGenerationPipeline(
        DirectorKnowledgeContext knowledge, string style,
        IChatCompletionClient? directorClient, string? directorModelName,
        IChatCompletionClient? choreographerClient, string? choreographerModelName,
        int maxOutputTokens = 4000,
        CustomGenerationPrompt? customPrompt = null,
        IVisionCapableChatCompletionClient? criticClient = null,
        string? criticModelName = null,
        ILogger<ChoreographyGenerationPipeline>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        ArgumentException.ThrowIfNullOrWhiteSpace(style);

        _knowledge = knowledge;
        _style = style;
        _directorClient = directorClient;
        _directorModelName = directorModelName;
        _choreographerClient = choreographerClient;
        _choreographerModelName = choreographerModelName;
        _maxOutputTokens = maxOutputTokens;
        _customPrompt = customPrompt;
        _criticClient = criticClient;
        _criticModelName = criticModelName;
        _logger = logger ?? NullLogger<ChoreographyGenerationPipeline>.Instance;
    }

    /// <exception cref="InvalidOperationException">No Show Bible was supplied and no Director client is configured, or a Show Bible/SectionScript generation never produced a validated result within its own retry budget.</exception>
    public async Task<ChoreographyGenerationReport> GenerateAsync(
        MusicModel music, LayoutSummary layoutSummary, ChoreographyGenerationSettings settings, Guid songId,
        ShowBible? existingBible, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(music);
        ArgumentNullException.ThrowIfNull(layoutSummary);
        ArgumentNullException.ThrowIfNull(settings);

        var overall = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Choreography generation starting for song {SongId} ({SectionCount} sections, style '{Style}').",
            songId, music.Sections.Labels.Count, _style);

        var bibleDir = Path.Combine(settings.ProjectDirectory, "bible");
        var scriptsDir = Path.Combine(settings.ProjectDirectory, "scripts");
        var llmDir = Path.Combine(settings.ProjectDirectory, "llm");
        var exportsDir = Path.Combine(settings.ProjectDirectory, "exports");
        var callLog = new JsonAiCallLogStore(llmDir);

        // Build-plan Phase 14 §2c.3: record the exact custom-prompt text actually used for this run
        // next to the Show Bible/call log, so "why does this generation look like this" stays
        // answerable from disk later -- keyed by songId so repeated runs never overwrite each
        // other's own record. Skipped entirely for the (pre-existing default) empty/null case -- no
        // new file appears for a run that never asked for one.
        if (_customPrompt is { } cp && (!cp.IsEmpty || !cp.StayCloseToExemplars))
        {
            await new JsonCustomPromptStore(llmDir, $"custom-prompt-used-{songId:N}.json")
                .SaveAsync(_customPrompt, ct).ConfigureAwait(false);
        }

        // 1. Get-or-generate the Show Bible.
        var bibleStepWatch = System.Diagnostics.Stopwatch.StartNew();
        var bible = existingBible ?? await GenerateShowBibleAsync(music, layoutSummary, settings, songId, callLog, ct).ConfigureAwait(false);
        await new JsonShowBibleStore(bibleDir).SaveNextVersionAsync(bible, ct).ConfigureAwait(false);
        _logger.LogInformation("Show Bible {Status} for song {SongId} in {ElapsedMs}ms ({LookCount} looks).",
            existingBible is null ? "generated" : "reused", songId, bibleStepWatch.ElapsedMilliseconds, bible.Content.Looks.Looks.Count);

        // 2. Per-section SectionScripts.
        var scriptsStepWatch = System.Diagnostics.Stopwatch.StartNew();
        var scriptGeneration = await GenerateSectionScriptsAsync(bible, music, layoutSummary, settings.Layout, settings.Vocabulary, settings.EnergyCeilingByKind, songId, callLog, ct).ConfigureAwait(false);
        var scripts = scriptGeneration.Scripts;
        await PersistScriptsAsync(scriptsDir, songId, scripts, ct).ConfigureAwait(false);
        _logger.LogInformation("Section scripts generated for song {SongId} in {ElapsedMs}ms ({ScriptCount}/{SectionCount} sections).",
            songId, scriptsStepWatch.ElapsedMilliseconds, scripts.Count, music.Sections.Labels.Count);

        var sectionsNeedingRetry = scripts.Count < music.Sections.Labels.Count ? music.Sections.Labels.Count - scripts.Count : 0;

        // 4/5. Compile via Phase 4's own unmodified engine -- a local function so the model-coverage
        // repair pass below can recompile against a patched set of scripts without duplicating this logic.
        ChoreographyEngine.Result CompileFromScripts(IReadOnlyDictionary<Guid, SectionScript> currentScripts) =>
            CompileSectionScripts(bible, music, layoutSummary, settings, currentScripts);

        var compiled = CompileFromScripts(scripts);

        // User feedback (2026-10-31): a model (e.g. an Arch) that compiled with zero effects anywhere in
        // the whole show is no longer just a soft prompt instruction nobody checked compliance on --
        // ChoreographyLegalityValidator.RuleModelCoverage now detects it, and this self-heals it with one
        // real, targeted Choreographer regeneration of the single longest section (see
        // RevisionDirectiveDeriver.FromModelCoverageViolations's own doc comment for why that section),
        // then recompiles once more against the patched scripts. Deliberately a single repair attempt, not
        // a loop -- this pipeline has no iteration/convergence budget of its own (that is IterationLoop's
        // job, not used by this straight-through pipeline); if the one regeneration still leaves a model
        // uncovered, ValidateShow's own violation still surfaces in the final compiled result rather than
        // retrying indefinitely.
        // Build-plan "richer analysis tools" Phase 1 (2026-11-01): the same single-repair-pass
        // philosophy as the model-coverage self-heal above, extended to cover a second violation
        // source -- a user-requested timed cue (e.g. "hit BANG near 0:24") that the first attempt did
        // not satisfy. Both violation sources are combined into ONE regeneration call so a section
        // needing both fixes is only ever regenerated once, not twice.
        if (_choreographerClient is not null && _choreographerModelName is not null)
        {
            var coverageViolations = compiled.Violations.Where(v => v.Rule == ChoreographyLegalityValidator.RuleModelCoverage).ToList();
            var coverageDirectives = RevisionDirectiveDeriver.FromModelCoverageViolations(coverageViolations, scriptGeneration.SectionWindowsById);

            var missedCues = TimedCueComplianceChecker.FindMissedCues(compiled.IR, scriptGeneration.TimedCues);
            var timedCueDirectives = RevisionDirectiveDeriver.FromMissedTimedCues(missedCues, scriptGeneration.SectionWindowsById);

            // Build-plan "richer analysis tools" Phase 8 (2026-11-02): a third violation source, same
            // single-repair-pass philosophy -- a model the user explicitly asked to be targeted
            // individually (never only via a group) that the previous attempt only ever reached through
            // a group.
            var requiredIndividualModels = SingleModelDirectiveParser.Parse(_customPrompt?.Text, settings.Layout);
            var groupedViolations = SingleModelComplianceChecker.FindGroupedViolations(compiled.IR, requiredIndividualModels, settings.Layout);
            var singleModelDirectives = RevisionDirectiveDeriver.FromSingleModelViolations(groupedViolations, scriptGeneration.SectionWindowsById);

            var repairDirectives = coverageDirectives.Concat(timedCueDirectives).Concat(singleModelDirectives).ToList();
            if (repairDirectives.Count > 0)
            {
                _logger.LogInformation("Repairing {DirectiveCount} section(s) for song {SongId} ({CoverageCount} coverage, {TimedCueCount} timed-cue, {SingleModelCount} single-model).",
                    repairDirectives.Count, songId, coverageDirectives.Count, timedCueDirectives.Count, singleModelDirectives.Count);
                var repairBatchGenerator = new SectionScriptBatchGenerator(_choreographerClient, callLog: callLog, modelName: _choreographerModelName);
                scripts = await TargetedSectionRegenerator.RegenerateAsync(
                    repairBatchGenerator, scriptGeneration.StablePrefix, scripts, repairDirectives, scriptGeneration.VolatileSuffixBySectionId,
                    songId, bible.Content.Looks, settings.Layout, settings.Vocabulary, _maxOutputTokens, ct).ConfigureAwait(false);
                await PersistScriptsAsync(scriptsDir, songId, scripts, ct).ConfigureAwait(false);
                compiled = CompileFromScripts(scripts);
            }
        }

        _logger.LogInformation("Compiled result for song {SongId}: {ViolationCount} legality violation(s).", songId, compiled.Violations.Count);

        // 6. Export.
        Directory.CreateDirectory(exportsDir);
        var exporter = new XsqExporter(settings.RecipeCatalog, settings.Vocabulary);
        var outputPath = Path.Combine(exportsDir, $"{songId:N}.xsq");
        var metadata = new SequenceMetadata(Song: music.SongId.Value.ToString());
        var exportResult = await exporter.ExportAsync(
            settings.Layout, compiled.IR.ToSequenceDocument(metadata), outputPath, "2026.03", XsqExportPolicy.AppendCollisionSuffix, ct).ConfigureAwait(false);
        _logger.LogInformation("Exported song {SongId} to {OutputPath}.", songId, outputPath);

        // Build-plan "richer analysis tools" Phase 3 (2026-11-02): opt-in render -> measure -> critique ->
        // revise loop (IterationLoop, built in an earlier phase but never wired into real generation until
        // now) layered on top of the straight-through result above. Off by default -- see
        // CustomGenerationPrompt.EnableQualityPass's own doc comment -- so every pre-existing caller/test
        // is unaffected. Never requires a vision-capable Critic to be configured: runs numeric-only
        // convergence when one is not (see IterationLoop's own null-VisualCritic support), adds visual
        // critique on top when one is.
        if (_customPrompt?.EnableQualityPass == true && _choreographerClient is not null && _choreographerModelName is not null)
        {
            if (string.IsNullOrWhiteSpace(settings.ShowDirectory) || string.IsNullOrWhiteSpace(settings.XlightsExecutablePath))
            {
                throw new QualityPassUnavailableException();
            }

            var qualityBatchGenerator = new SectionScriptBatchGenerator(_choreographerClient, callLog: callLog, modelName: _choreographerModelName);
            var iterationExportCounter = 0;

            async Task<(ChoreographyEngine.Result Compiled, ExportResult Export)> CompileAndExportIterationAsync(IReadOnlyDictionary<Guid, SectionScript> currentScripts)
            {
                var iterationCompiled = CompileFromScripts(currentScripts);
                iterationExportCounter++;
                var iterationOutputPath = Path.Combine(exportsDir, $"{songId:N}-qp{iterationExportCounter}.xsq");
                var iterationExport = await exporter.ExportAsync(
                    settings.Layout, iterationCompiled.IR.ToSequenceDocument(metadata), iterationOutputPath, "2026.03",
                    XsqExportPolicy.AppendCollisionSuffix, ct).ConfigureAwait(false);
                return (iterationCompiled, iterationExport);
            }

            var renderPipeline = new RenderPipeline(
                new XlightsRenderAdapter(new XlightsRenderCapabilityProbe(), settings.XlightsExecutablePath!),
                new FseqRenderedFrameDecoder(),
                new XsqStructuralReader());
            var renderOutputRoot = Path.Combine(settings.ProjectDirectory, "renders");
            Directory.CreateDirectory(renderOutputRoot);
            var renderCounter = 0;

            async Task<IReadOnlyList<PreviewFrame>> RenderIterationAsync(ExportResult export, CancellationToken innerCt)
            {
                renderCounter++;
                var outputDir = Path.Combine(renderOutputRoot, $"qp-iter{renderCounter}-{songId:N}");
                var request = new RenderRequest(songId, settings.ShowDirectory!, export.Path, outputDir, Selection: null, RenderAuthority.ActualXlights);
                var result = await renderPipeline.RunAsync(request, settings.Layout, innerCt).ConfigureAwait(false);
                return result.Frames ?? [];
            }

            var visualCritic = _criticClient is not null && _criticModelName is not null
                ? new VisualCritic(_criticClient, _criticModelName)
                : null;

            var phrasesBySectionIdForIntent = music.Phrases.Phrases
                .GroupBy(p => p.SectionLabelId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<PhraseCandidate>)g.OrderBy(p => p.StartBarIndex).ToList());
            var (_, lookIdBySectionIdForIntent, _, _) = SectionScriptCompiler.Compile(bible.Content.Looks, scripts, phrasesBySectionIdForIntent);
            var sectionIntentById = new Dictionary<Guid, string>();
            foreach (var (sectionId, lookId) in lookIdBySectionIdForIntent)
            {
                var look = bible.Content.Looks.Looks.FirstOrDefault(l => l.Id == lookId)
                    ?? bible.Content.Looks.Looks.FirstOrDefault(l => lookId.StartsWith(l.Id, StringComparison.Ordinal));
                if (look is not null)
                {
                    sectionIntentById[sectionId] = look.Intent;
                }
            }

            var loop = new IterationLoop(
                qualityBatchGenerator, scriptGeneration.StablePrefix, scriptGeneration.VolatileSuffixBySectionId, sectionIntentById,
                bible.Content.Looks, settings.Layout, settings.Vocabulary, CompileAndExportIterationAsync, RenderIterationAsync,
                scriptGeneration.SectionWindowsById, (int)Math.Max(1, layoutSummary.TotalPixels), visualCritic, _maxOutputTokens);

            _logger.LogInformation("Quality pass starting for song {SongId} (up to {MaxIterations} iteration(s)).", songId, QualityPassMaxIterations);
            var current = await loop.RunFirstAsync(songId, scripts, ct).ConfigureAwait(false);
            for (var i = 0; i < QualityPassMaxIterations - 1; i++)
            {
                var next = await loop.RunNextAsync(songId, current, ct).ConfigureAwait(false);
                if (next is null)
                {
                    _logger.LogInformation("Quality pass converged for song {SongId} after iteration {Iteration}.", songId, i + 1);
                    break;
                }

                current = next;
                _logger.LogInformation("Quality pass iteration {Iteration} completed for song {SongId}.", i + 2, songId);
            }

            scripts = current.Scripts;
            compiled = current.CompiledResult;
            exportResult = current.Export;
            await PersistScriptsAsync(scriptsDir, songId, scripts, ct).ConfigureAwait(false);
        }

        _logger.LogInformation("Choreography generation completed for song {SongId} in {ElapsedMs}ms ({SectionsNeedingRetry} section(s) needing retry).",
            songId, overall.ElapsedMilliseconds, sectionsNeedingRetry);
        return new ChoreographyGenerationReport(bible, scripts, compiled, exportResult, music.Sections.Labels.Count, sectionsNeedingRetry);
    }

    private async Task<ShowBible> GenerateShowBibleAsync(MusicModel music, LayoutSummary layoutSummary, ChoreographyGenerationSettings settings, Guid songId, JsonAiCallLogStore callLog, CancellationToken ct)
    {
        if (_directorClient is null || _directorModelName is null)
        {
            throw new InvalidOperationException("No Show Bible was supplied and no Director client is configured.");
        }

        var prompt = DirectorPromptBuilder.Build(_knowledge, _style, music, layoutSummary, _customPrompt);
        var generator = new ShowBibleGenerator(_directorClient, callLog: callLog, modelName: _directorModelName);
        var report = await generator.GenerateAsync(prompt, songId, settings.Layout, settings.Vocabulary, settings.RecipeCatalog, _maxOutputTokens, ct, _knowledge.TemplatesOrEmpty).ConfigureAwait(false);

        return report.Bible ?? throw new InvalidOperationException(
            $"Show Bible generation exhausted {report.AttemptCount} attempt(s) without validating: {string.Join("; ", report.FinalViolations.Select(v => v.Message))}");
    }

    /// <summary>
    /// User feedback (2026-10-31): bundles the stable prefix and per-section volatile suffix alongside the
    /// generated scripts -- <see cref="GenerateAsync"/>'s own model-coverage repair pass needs both to call
    /// <see cref="TargetedSectionRegenerator.RegenerateAsync"/> without re-deriving the Choreographer
    /// prompt a second time, and <see cref="SectionWindowsById"/> to pick which section a coverage
    /// directive should target (see <see cref="RevisionDirectiveDeriver.FromModelCoverageViolations"/>).
    /// </summary>
    private sealed record SectionScriptGenerationResult(
        IReadOnlyDictionary<Guid, SectionScript> Scripts,
        string StablePrefix,
        IReadOnlyDictionary<Guid, string> VolatileSuffixBySectionId,
        IReadOnlyDictionary<Guid, (long Start, long End)> SectionWindowsById,
        IReadOnlyList<TimedCue> TimedCues);

    /// <summary>
    /// Build-plan "richer analysis tools" post-processing phase (2026-11-02): everything
    /// <see cref="GenerateSectionScriptsAsync"/> computes BEFORE ever calling the Choreographer -- the
    /// stable prefix, every section's own volatile suffix, its real time window, and the whole song's
    /// parsed/resolved timed cues. Deliberately contains no I/O/LLM calls of its own, so
    /// <see cref="ApplyPostProcessingPromptAsync"/> can recompute the EXACT same per-section prompt
    /// material a prior full generation used (these are pure functions of the already-persisted
    /// <see cref="ShowBible"/>/<see cref="MusicModel"/>/style/knowledge -- never separately persisted
    /// themselves, see <see cref="SectionScriptPromptBuilder"/>'s own doc comment) without re-running the
    /// expensive per-section Choreographer batch for sections nobody asked to change.
    /// </summary>
    private sealed record SectionPromptMaterial(
        string StablePrefix,
        IReadOnlyDictionary<Guid, string> VolatileSuffixBySectionId,
        IReadOnlyDictionary<Guid, (long Start, long End)> SectionWindowsById,
        IReadOnlyList<TimedCue> TimedCues,
        IReadOnlyList<SectionScriptBatchItem> Items);

    private SectionPromptMaterial BuildSectionPromptMaterial(
        ShowBible bible, MusicModel music, LayoutSummary layoutSummary, IReadOnlyDictionary<SectionKind, double> energyCeilingByKind)
    {
        var stablePrefix = SectionScriptPromptBuilder.BuildStablePrefix(bible.Content, _knowledge, _style);
        var barGrid = BarGrid.FromBeatGrid(music.BeatGrid, music.SongDurationTicks);
        var tracks = BuildTimingTracksForPrompt(music, barGrid);

        // Each section's Choreographer call is independent/stateless (architecture §2's reasoning-tax
        // design, batched per §14) -- it never sees what a sibling section picked. Confirmed against a
        // real run: without SOME signal of where a section sits among its own repeats, the Choreographer
        // converged on the same handful of Looks for nearly every section of a repetitive/loop-based song
        // (this song's own structure analysis classified almost the whole thing as one Kind). An
        // occurrence index/total -- "this is repeat 3 of 6" -- is the one piece of cross-section context
        // a stateless call CAN be given cheaply, letting it reason about escalation/restraint without
        // needing to see the other five calls' own output.
        var orderedSections = music.Sections.Labels.OrderBy(s => s.StartBarIndex).ToList();
        var totalByKind = orderedSections.GroupBy(s => s.Kind).ToDictionary(g => g.Key, g => g.Count());
        var occurrenceByKind = new Dictionary<SectionKind, int>();
        var melodicBriefsBySectionId = MelodicContourBriefBuilder.ComputeSectionBriefs(music).ToDictionary(b => b.SectionId);
        var motifLinksBySectionId = MotifLinkedPredecessorBuilder.ComputeSectionLinks(music).ToDictionary(l => l.SectionId);
        var phrasesBySectionId = music.Phrases.Phrases
            .GroupBy(p => p.SectionLabelId)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.StartBarIndex).ToList());

        // Build-plan "richer analysis tools" Phase 1 (2026-11-01): parsed once for the whole song, then
        // filtered per-section below by real-time overlap -- never the whole cue list pasted into every
        // section's own prompt (the original problem: a user's 9 scattered timestamps were previously
        // sent, unfiltered, to every single section's call).
        var allTimedCues = CustomPromptTimedCueParser.Parse(_customPrompt?.Text);

        // Build-plan "richer analysis tools" multi-hit phase (2026-11-02): a ranged cue meant to cover a
        // cluster of repeated hits ("BANG BANG BANG" over 2 seconds) becomes one cue PER real repeated
        // occurrence found inside it, so the compliance checker below independently verifies each one --
        // applied BEFORE the single-nearest-match snaps, which would otherwise collapse the whole cluster
        // onto just one instant.
        allTimedCues = TimedCueMultiHitExpander.Expand(allTimedCues, music.Transcript, music.TransientHits);

        // Build-plan "richer analysis tools" Phase 2 (2026-11-02): snap each cue onto a real nearby
        // detected transient (if any), tightening the guard rail to check against the verified moment
        // instead of the user's raw typed guess.
        allTimedCues = TimedCueTransientResolver.Resolve(allTimedCues, music.TransientHits);

        // Build-plan "richer analysis tools" Phase 6 (2026-11-02): a real transcribed word named in the
        // cue's own instruction (e.g. "hit the word bang") is a stronger, more specific signal than a
        // loud transient alone -- applied after the transient snap, so a cue can still snap to a matching
        // word within its (by-then-tightened) window.
        allTimedCues = TimedCueTranscriptResolver.Resolve(allTimedCues, music.Transcript);

        var volatileSuffixBySectionId = new Dictionary<Guid, string>();
        var sectionWindowsById = new Dictionary<Guid, (long Start, long End)>();
        var items = orderedSections.Select(section =>
        {
            var window = barGrid.WindowForBarRange(new BarIndex(section.StartBarIndex), new BarIndex(section.EndBarIndexExclusive))
                ?? new TimeWindow(0, music.SongDurationTicks);
            sectionWindowsById[section.Id] = (window.StartTicks, window.EndTicks);

            // The real ceiling enforcement happens in ChoreographyEngine itself, from settings, not
            // duplicated here -- but the Choreographer prompt's own "declared energy ceiling"/target
            // change-rate lines (build-plan Phase 12 §3b) should still show the section's real declared
            // value, the same per-SectionKind lookup ChoreographyEngine.CompileOneWindow performs, rather
            // than a constant that makes every section's prompt line read identically.
            var ceiling = energyCeilingByKind.TryGetValue(section.Kind, out var c) ? c : 1.0;
            var vocalActive = SectionMusicContext.IsVocalActiveMajority(music.VocalActivity, window);
            var targetChangeRate = SectionDensityMapper.TargetChangeRatePerSecond(ceiling);
            var context = new SectionMusicContext(window, section.Kind, ceiling, barGrid, tracks, vocalActive, targetChangeRate);

            occurrenceByKind[section.Kind] = occurrenceByKind.GetValueOrDefault(section.Kind) + 1;
            var occurrenceIndex = occurrenceByKind[section.Kind];
            var totalOccurrences = totalByKind[section.Kind];
            melodicBriefsBySectionId.TryGetValue(section.Id, out var melodicBrief);
            motifLinksBySectionId.TryGetValue(section.Id, out var motifLink);
            var phrases = phrasesBySectionId.GetValueOrDefault(section.Id, []);

            var sectionStartSeconds = window.StartTicks / (double)TicksPerSecond;
            var sectionEndSeconds = window.EndTicks / (double)TicksPerSecond;
            var matchingCues = allTimedCues.Where(c => c.OverlapsSeconds(sectionStartSeconds, sectionEndSeconds)).ToList();

            var volatileSuffix = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layoutSummary, occurrenceIndex, totalOccurrences, melodicBrief, motifLink, phrases, _customPrompt?.Text, matchingCues);
            volatileSuffixBySectionId[section.Id] = volatileSuffix;

            return new SectionScriptBatchItem(section.Id, volatileSuffix);
        }).ToList();

        return new SectionPromptMaterial(stablePrefix, volatileSuffixBySectionId, sectionWindowsById, allTimedCues, items);
    }

    private async Task<SectionScriptGenerationResult> GenerateSectionScriptsAsync(
        ShowBible bible, MusicModel music, LayoutSummary layoutSummary, DisplayGraph layout, EffectVocabularyCatalog vocabulary,
        IReadOnlyDictionary<SectionKind, double> energyCeilingByKind, Guid songId, JsonAiCallLogStore callLog, CancellationToken ct)
    {
        if (_choreographerClient is null || _choreographerModelName is null)
        {
            throw new InvalidOperationException("No SectionScripts were supplied and no Choreographer client is configured.");
        }

        var material = BuildSectionPromptMaterial(bible, music, layoutSummary, energyCeilingByKind);

        var batchGenerator = new SectionScriptBatchGenerator(_choreographerClient, callLog: callLog, modelName: _choreographerModelName);
        var reports = await batchGenerator.GenerateAllAsync(material.StablePrefix, material.Items, songId, bible.Content.Looks, layout, vocabulary, _maxOutputTokens, ct).ConfigureAwait(false);

        var scripts = reports
            .Where(kvp => kvp.Value.Script is not null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Script!);

        return new SectionScriptGenerationResult(scripts, material.StablePrefix, material.VolatileSuffixBySectionId, material.SectionWindowsById, material.TimedCues);
    }

    private static Dictionary<string, TimingTrack> BuildTimingTracksForPrompt(MusicModel music, BarGrid barGrid)
    {
        var tracks = new Dictionary<string, TimingTrack>(StringComparer.Ordinal)
        {
            ["Beats"] = TimingTrackEmitter.Beats(music.BeatGrid, music.SongDurationTicks),
            ["Bars"] = TimingTrackEmitter.Bars(barGrid),
            ["Phrases"] = TimingTrackEmitter.Phrases(music.Phrases),
            ["Sections"] = TimingTrackEmitter.Sections(music.Sections, barGrid),
        };
        if (music.BandEnergy is not null)
        {
            tracks["Percussion"] = TimingTrackEmitter.Percussion(music.BandEnergy);
        }

        return tracks;
    }

    private static async Task PersistScriptsAsync(string directory, Guid songId, IReadOnlyDictionary<Guid, SectionScript> scripts, CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        foreach (var (sectionId, script) in scripts)
        {
            var path = Path.Combine(directory, $"section-script-{songId:N}-{sectionId:N}.json");
            var json = System.Text.Json.JsonSerializer.Serialize(script, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            var tempPath = $"{path}.tmp-{Guid.NewGuid():N}";
            await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);
            File.Move(tempPath, path, overwrite: true); // Re-running this pipeline for the same song/section intentionally refreshes its own last-generated script.
        }
    }

    /// <summary>
    /// Build-plan "richer analysis tools" post-processing phase (2026-11-02): reads back every
    /// <see cref="SectionScript"/> <see cref="PersistScriptsAsync"/> wrote for <paramref name="songId"/> --
    /// the real inputs <see cref="ApplyPostProcessingPromptAsync"/> needs to touch up a prior generation
    /// without ever re-generating anything nobody asked to change. Keyed by each script's own
    /// <see cref="SectionScript.SectionId"/> field (not re-derived from the filename), the authoritative
    /// source. Returns an empty dictionary (never throws) when the directory has nothing for this
    /// <paramref name="songId"/> yet -- an honest "nothing to load," left for the caller to decide is an error.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, SectionScript>> LoadPersistedScriptsAsync(
        string scriptsDirectory, Guid songId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptsDirectory);

        if (!Directory.Exists(scriptsDirectory))
        {
            return new Dictionary<Guid, SectionScript>();
        }

        var scripts = new Dictionary<Guid, SectionScript>();
        foreach (var path in Directory.GetFiles(scriptsDirectory, $"section-script-{songId:N}-*.json"))
        {
            ct.ThrowIfCancellationRequested();
            await using var stream = File.OpenRead(path);
            var script = await System.Text.Json.JsonSerializer.DeserializeAsync<SectionScript>(stream, cancellationToken: ct).ConfigureAwait(false);
            if (script is not null)
            {
                scripts[script.SectionId] = script;
            }
        }

        return scripts;
    }

    private static Dictionary<string, ColorPaletteDefinition> BuildPaletteLibrary(IReadOnlyList<ShowBiblePalette> palettes) =>
        palettes.ToDictionary(p => p.Id, p => PaletteBuilder.Build(p.Colors.Select(c => c.HexColor).ToList()), StringComparer.Ordinal);

    /// <summary>
    /// Build-plan "richer analysis tools" post-processing phase (2026-11-02): extracted out of
    /// <see cref="GenerateAsync"/>'s own former local function so <see cref="ApplyPostProcessingPromptAsync"/>
    /// can recompile against its own patched scripts using the exact same logic -- SectionScript + Look -&gt;
    /// effective per-section Look library (build-plan Phase 6 task 3), any declared phrase-scoped
    /// sub-section variations, each section's resolved filler schedule, then Phase 4's own unmodified
    /// engine.
    /// </summary>
    private static ChoreographyEngine.Result CompileSectionScripts(
        ShowBible bible, MusicModel music, LayoutSummary layoutSummary, ChoreographyGenerationSettings settings,
        IReadOnlyDictionary<Guid, SectionScript> scripts)
    {
        var phrasesBySectionId = music.Phrases.Phrases
            .GroupBy(p => p.SectionLabelId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PhraseCandidate>)g.OrderBy(p => p.StartBarIndex).ToList());
        var paletteLibrary = BuildPaletteLibrary(bible.Content.Palettes);
        var compilerSettings = new LookCompilerSettings(settings.Vocabulary, settings.RecipeCatalog, settings.RecipeRegistry, paletteLibrary);

        var (effectiveLibrary, lookIdBySectionId, phraseLookAssignmentsBySectionId, _) =
            SectionScriptCompiler.Compile(bible.Content.Looks, scripts, phrasesBySectionId);

        // Build-plan Phase 15 §3: each section's own declared filler plan, resolved into real
        // FillerPools (recipe ids) and keyed by that section's own bar-range window + classified
        // target role.
        var fillerSchedule = BuildFillerSchedule(scripts, music, layoutSummary, settings.RecipeCatalog);

        var engineInput = new ChoreographyEngine.Input(
            settings.Layout, music.BeatGrid, music.Sections, music.Phrases, music.BandEnergy, music.VocalActivity,
            effectiveLibrary, lookIdBySectionId, settings.EnergyCeilingByKind, music.SongDurationTicks, compilerSettings, fillerSchedule,
            VisemeMarks: music.VisemeMarks, PhraseLookAssignmentsBySectionId: phraseLookAssignmentsBySectionId, AudioEnergy: music.Energy);
        return ChoreographyEngine.Compile(engineInput);
    }

    /// <summary>
    /// Build-plan "richer analysis tools" post-processing phase (2026-11-02): the user's own explicit ask
    /// ("a post-processing prompt... where I can put time ranges and have it alter the existing sequence,
    /// so it would only fix what I ask and leave the rest alone"). Reuses the exact same machinery as
    /// <see cref="GenerateAsync"/>'s own model-coverage/timed-cue self-healing repair pass -- parse the
    /// prompt's timestamp tokens into <see cref="TimedCue"/>s (same <see cref="CustomPromptTimedCueParser"/>,
    /// same <see cref="TimedCueTransientResolver"/>/<see cref="TimedCueTranscriptResolver"/> snapping),
    /// derive a <see cref="RevisionDirective"/> for every REAL section whose own time window overlaps a
    /// cue's tolerance-expanded window (unlike the repair pass's own "closest single section" heuristic for
    /// an automatically-detected miss, a user-authored edit that spans a section boundary should touch
    /// every section it actually overlaps), then <see cref="TargetedSectionRegenerator.RegenerateAsync"/>
    /// -- which guarantees every other section keeps its EXACT prior <see cref="SectionScript"/> instance,
    /// never re-sent to the Choreographer, the real mechanism behind "leave the rest alone."
    /// </summary>
    /// <param name="existingScripts">The full prior run's own <see cref="SectionScript"/>s (e.g. reloaded from the <c>scripts/</c> directory <see cref="GenerateAsync"/> itself persists to) -- every section with no matching cue is returned completely unchanged.</param>
    /// <param name="postProcessingPromptText">Free text with one or more <c>mm:ss</c>/<c>hh:mm:ss</c>(-range) tokens, same literal syntax as the pre-generation custom prompt's own timed cues.</param>
    /// <exception cref="PostProcessingPromptHasNoTimedInstructionsException">No timestamp token was found anywhere in <paramref name="postProcessingPromptText"/> -- nothing for this method to act on.</exception>
    /// <exception cref="PostProcessingPromptMatchesNoSectionException">Every parsed cue's own window fell entirely outside every real section's window (e.g. past the end of the song).</exception>
    public async Task<ChoreographyGenerationReport> ApplyPostProcessingPromptAsync(
        MusicModel music, LayoutSummary layoutSummary, ChoreographyGenerationSettings settings, Guid songId,
        ShowBible existingBible, IReadOnlyDictionary<Guid, SectionScript> existingScripts, string postProcessingPromptText, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(music);
        ArgumentNullException.ThrowIfNull(layoutSummary);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(existingBible);
        ArgumentNullException.ThrowIfNull(existingScripts);
        ArgumentException.ThrowIfNullOrWhiteSpace(postProcessingPromptText);

        if (_choreographerClient is null || _choreographerModelName is null)
        {
            throw new InvalidOperationException("No Choreographer client is configured -- cannot apply a post-processing prompt.");
        }

        var cues = CustomPromptTimedCueParser.Parse(postProcessingPromptText);
        cues = TimedCueMultiHitExpander.Expand(cues, music.Transcript, music.TransientHits);
        cues = TimedCueTransientResolver.Resolve(cues, music.TransientHits);
        cues = TimedCueTranscriptResolver.Resolve(cues, music.Transcript);
        if (cues.Count == 0)
        {
            throw new PostProcessingPromptHasNoTimedInstructionsException();
        }

        var material = BuildSectionPromptMaterial(existingBible, music, layoutSummary, settings.EnergyCeilingByKind);

        var directives = RevisionDirectiveDeriver.FromPostProcessingCues(cues, material.SectionWindowsById);
        if (directives.Count == 0)
        {
            throw new PostProcessingPromptMatchesNoSectionException();
        }

        var scriptsDir = Path.Combine(settings.ProjectDirectory, "scripts");
        var llmDir = Path.Combine(settings.ProjectDirectory, "llm");
        var exportsDir = Path.Combine(settings.ProjectDirectory, "exports");
        var callLog = new JsonAiCallLogStore(llmDir);

        var batchGenerator = new SectionScriptBatchGenerator(_choreographerClient, callLog: callLog, modelName: _choreographerModelName);
        var updatedScripts = await TargetedSectionRegenerator.RegenerateAsync(
            batchGenerator, material.StablePrefix, existingScripts, directives, material.VolatileSuffixBySectionId,
            songId, existingBible.Content.Looks, settings.Layout, settings.Vocabulary, _maxOutputTokens, ct).ConfigureAwait(false);
        await PersistScriptsAsync(scriptsDir, songId, updatedScripts, ct).ConfigureAwait(false);

        var compiled = CompileSectionScripts(existingBible, music, layoutSummary, settings, updatedScripts);

        // User feedback (2026-11-03): "didn't do a good job of fixing it" -- the touch-up path regenerated
        // each targeted section exactly once and trusted whatever the Choreographer produced, with nothing
        // verifying the cue was actually satisfied. Same single-repair-pass verification GenerateAsync's
        // own model-coverage/timed-cue self-heal already does: check whether each requested cue actually
        // landed, and if not, give the Choreographer one more targeted attempt at exactly that section
        // before giving up -- never a silent "the AI said it did it" trust.
        var missedCues = TimedCueComplianceChecker.FindMissedCues(compiled.IR, cues);
        var repairDirectives = RevisionDirectiveDeriver.FromMissedTimedCues(missedCues, material.SectionWindowsById);
        if (repairDirectives.Count > 0)
        {
            updatedScripts = await TargetedSectionRegenerator.RegenerateAsync(
                batchGenerator, material.StablePrefix, updatedScripts, repairDirectives, material.VolatileSuffixBySectionId,
                songId, existingBible.Content.Looks, settings.Layout, settings.Vocabulary, _maxOutputTokens, ct).ConfigureAwait(false);
            await PersistScriptsAsync(scriptsDir, songId, updatedScripts, ct).ConfigureAwait(false);
            compiled = CompileSectionScripts(existingBible, music, layoutSummary, settings, updatedScripts);
        }

        Directory.CreateDirectory(exportsDir);
        var exporter = new XsqExporter(settings.RecipeCatalog, settings.Vocabulary);
        var outputPath = Path.Combine(exportsDir, $"{songId:N}.xsq");
        var metadata = new SequenceMetadata(Song: music.SongId.Value.ToString());
        var exportResult = await exporter.ExportAsync(
            settings.Layout, compiled.IR.ToSequenceDocument(metadata), outputPath, "2026.03", XsqExportPolicy.AppendCollisionSuffix, ct).ConfigureAwait(false);

        return new ChoreographyGenerationReport(existingBible, updatedScripts, compiled, exportResult, music.Sections.Labels.Count, SectionsNeedingRetry: 0);
    }

    private static string FormatTimestamp(TimeSpan time) => $"{(int)time.TotalMinutes}:{time.Seconds:00}";

    /// <summary>
    /// Build-plan Phase 15 §3: turns every validated <see cref="SectionScript"/>'s own declared filler
    /// plan (<see cref="SectionScriptCompiler"/>'s name-based <see cref="SectionFillerPlan"/> output) into
    /// a real <see cref="SectionFillerSchedule"/> -- resolving each section's own bar-range window (via
    /// <see cref="BarGrid"/>), each declared effect name to a recipe id (via <see cref="LookCompiler.RecipeIdFor"/>,
    /// the same rule <see cref="LookCompiler"/> itself uses, not re-derived), and the role-key-to-target
    /// classification (via <see cref="BandEnergyGroupRoleMapper.Classify"/>) that resolves which section's
    /// own role-specific list a given target should draw from. <see langword="public static"/> (not an
    /// inline step of <see cref="GenerateAsync"/>) so <c>Phase7RealIterationLoopTests</c> -- which builds
    /// its own <see cref="ChoreographyEngine.Input"/> directly, bypassing this pipeline -- can reuse the
    /// exact same resolution this pipeline itself relies on, rather than a second, divergent copy.
    /// </summary>
    /// <exception cref="InvalidOperationException">No section's own window resolved against this song's own bar grid -- the always-moving rule must always have at least one real window to fall back to.</exception>
    public static SectionFillerSchedule BuildFillerSchedule(
        IReadOnlyDictionary<Guid, SectionScript> scripts, MusicModel music, LayoutSummary layoutSummary, EffectRecipeCatalog recipeCatalog)
    {
        ArgumentNullException.ThrowIfNull(scripts);
        ArgumentNullException.ThrowIfNull(music);
        ArgumentNullException.ThrowIfNull(layoutSummary);
        ArgumentNullException.ThrowIfNull(recipeCatalog);

        var barGrid = BarGrid.FromBeatGrid(music.BeatGrid, music.SongDurationTicks);
        var roleByTargetName = BandEnergyGroupRoleMapper.Classify(layoutSummary).ToDictionary(
            e => e.Reference.StartsWith("group:", StringComparison.Ordinal) ? e.Reference["group:".Length..] : e.Reference,
            e => e.Role,
            StringComparer.OrdinalIgnoreCase);

        string ResolveRecipeId(string effectName) => LookCompiler.RecipeIdFor(effectName, recipeCatalog);

        var windows = new List<SectionFillerWindow>();
        foreach (var section in music.Sections.Labels.OrderBy(s => s.StartBarIndex))
        {
            if (!scripts.TryGetValue(section.Id, out var script))
            {
                continue; // No validated SectionScript for this section -- it contributes no window; resolve() falls back to a neighboring section's own window.
            }

            var window = barGrid.WindowForBarRange(new BarIndex(section.StartBarIndex), new BarIndex(section.EndBarIndexExclusive));
            if (window is null)
            {
                continue;
            }

            var flatPool = new FillerPool(script.FillerEffectsOrEmpty.Select(ResolveRecipeId).ToList());
            Dictionary<string, FillerPool>? byRoleKey = null;
            if (script.FillerEffectsByRoleOrEmpty.Count > 0)
            {
                byRoleKey = script.FillerEffectsByRoleOrEmpty.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new FillerPool(kvp.Value.Select(ResolveRecipeId).ToList()),
                    StringComparer.Ordinal);
            }

            windows.Add(new SectionFillerWindow(window.Value, flatPool, byRoleKey));
        }

        if (windows.Count == 0)
        {
            throw new InvalidOperationException("No section produced a real filler window -- every validated SectionScript must declare FillerEffects, and every section's own bar range must resolve against the song's own bar grid.");
        }

        return new SectionFillerSchedule(windows, target => roleByTargetName.TryGetValue(target.Value, out var role) ? role.ToString() : null);
    }
}
