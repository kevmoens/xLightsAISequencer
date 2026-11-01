using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
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
public sealed record ChoreographyGenerationSettings(
    DisplayGraph Layout,
    EffectVocabularyCatalog Vocabulary,
    EffectRecipeCatalog RecipeCatalog,
    IEffectRecipeRegistry RecipeRegistry,
    IReadOnlyDictionary<SectionKind, double> EnergyCeilingByKind,
    string ProjectDirectory);

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
    private readonly IChatCompletionClient? _directorClient;
    private readonly string? _directorModelName;
    private readonly IChatCompletionClient? _choreographerClient;
    private readonly string? _choreographerModelName;
    private readonly DirectorKnowledgeContext _knowledge;
    private readonly string _style;
    private readonly int _maxOutputTokens;
    private readonly CustomGenerationPrompt? _customPrompt;

    /// <param name="customPrompt">
    /// Build-plan Phase 14 §2c: the user's own free-text creative direction for this run, or
    /// <see langword="null"/>/empty for the pre-existing default -- every call site before this
    /// phase (including every existing test and <c>tools/GenerateOnce</c>) keeps compiling and
    /// behaving identically.
    /// </param>
    public ChoreographyGenerationPipeline(
        DirectorKnowledgeContext knowledge, string style,
        IChatCompletionClient? directorClient, string? directorModelName,
        IChatCompletionClient? choreographerClient, string? choreographerModelName,
        int maxOutputTokens = 4000,
        CustomGenerationPrompt? customPrompt = null)
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
    }

    /// <exception cref="InvalidOperationException">No Show Bible was supplied and no Director client is configured, or a Show Bible/SectionScript generation never produced a validated result within its own retry budget.</exception>
    public async Task<ChoreographyGenerationReport> GenerateAsync(
        MusicModel music, LayoutSummary layoutSummary, ChoreographyGenerationSettings settings, Guid songId,
        ShowBible? existingBible, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(music);
        ArgumentNullException.ThrowIfNull(layoutSummary);
        ArgumentNullException.ThrowIfNull(settings);

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
        var bible = existingBible ?? await GenerateShowBibleAsync(music, layoutSummary, settings, songId, callLog, ct).ConfigureAwait(false);
        await new JsonShowBibleStore(bibleDir).SaveNextVersionAsync(bible, ct).ConfigureAwait(false);

        // 2. Per-section SectionScripts.
        var scriptGeneration = await GenerateSectionScriptsAsync(bible, music, layoutSummary, settings.Layout, settings.Vocabulary, settings.EnergyCeilingByKind, songId, callLog, ct).ConfigureAwait(false);
        var scripts = scriptGeneration.Scripts;
        await PersistScriptsAsync(scriptsDir, songId, scripts, ct).ConfigureAwait(false);

        var sectionsNeedingRetry = scripts.Count < music.Sections.Labels.Count ? music.Sections.Labels.Count - scripts.Count : 0;

        // 3. SectionScript + Look -> effective per-section Look library (build-plan Phase 6 task 3),
        // plus (build-plan Phase 10 §3e) any declared phrase-scoped sub-section variations, joined against
        // this song's own real phrases (grouped by their own owning section). Neither of these depends on
        // which scripts are current, so they are computed once and reused by CompileFromScripts below,
        // including for the model-coverage repair pass's own recompile.
        var phrasesBySectionId = music.Phrases.Phrases
            .GroupBy(p => p.SectionLabelId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PhraseCandidate>)g.OrderBy(p => p.StartBarIndex).ToList());
        var paletteLibrary = BuildPaletteLibrary(bible.Content.Palettes);
        var compilerSettings = new LookCompilerSettings(settings.Vocabulary, settings.RecipeCatalog, settings.RecipeRegistry, paletteLibrary);

        // 4/5. Compile via Phase 4's own unmodified engine -- a local function so the model-coverage
        // repair pass below can recompile against a patched set of scripts without duplicating this logic.
        ChoreographyEngine.Result CompileFromScripts(IReadOnlyDictionary<Guid, SectionScript> currentScripts)
        {
            var (effectiveLibrary, lookIdBySectionId, phraseLookAssignmentsBySectionId, _) =
                SectionScriptCompiler.Compile(bible.Content.Looks, currentScripts, phrasesBySectionId);

            // Build-plan Phase 15 §3: each section's own declared filler plan, resolved into real
            // FillerPools (recipe ids) and keyed by that section's own bar-range window + classified
            // target role.
            var fillerSchedule = BuildFillerSchedule(currentScripts, music, layoutSummary, settings.RecipeCatalog);

            var engineInput = new ChoreographyEngine.Input(
                settings.Layout, music.BeatGrid, music.Sections, music.Phrases, music.BandEnergy, music.VocalActivity,
                effectiveLibrary, lookIdBySectionId, settings.EnergyCeilingByKind, music.SongDurationTicks, compilerSettings, fillerSchedule,
                VisemeMarks: music.VisemeMarks, PhraseLookAssignmentsBySectionId: phraseLookAssignmentsBySectionId);
            return ChoreographyEngine.Compile(engineInput);
        }

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
        if (_choreographerClient is not null && _choreographerModelName is not null)
        {
            var coverageViolations = compiled.Violations.Where(v => v.Rule == ChoreographyLegalityValidator.RuleModelCoverage).ToList();
            if (coverageViolations.Count > 0)
            {
                var coverageDirectives = RevisionDirectiveDeriver.FromModelCoverageViolations(coverageViolations, scriptGeneration.SectionWindowsById);
                if (coverageDirectives.Count > 0)
                {
                    var repairBatchGenerator = new SectionScriptBatchGenerator(_choreographerClient, callLog: callLog, modelName: _choreographerModelName);
                    scripts = await TargetedSectionRegenerator.RegenerateAsync(
                        repairBatchGenerator, scriptGeneration.StablePrefix, scripts, coverageDirectives, scriptGeneration.VolatileSuffixBySectionId,
                        songId, bible.Content.Looks, settings.Layout, settings.Vocabulary, _maxOutputTokens, ct).ConfigureAwait(false);
                    await PersistScriptsAsync(scriptsDir, songId, scripts, ct).ConfigureAwait(false);
                    compiled = CompileFromScripts(scripts);
                }
            }
        }

        // 6. Export.
        Directory.CreateDirectory(exportsDir);
        var exporter = new XsqExporter(settings.RecipeCatalog, settings.Vocabulary);
        var outputPath = Path.Combine(exportsDir, $"{songId:N}.xsq");
        var metadata = new SequenceMetadata(Song: music.SongId.Value.ToString());
        var exportResult = await exporter.ExportAsync(
            settings.Layout, compiled.IR.ToSequenceDocument(metadata), outputPath, "2026.03", XsqExportPolicy.AppendCollisionSuffix, ct).ConfigureAwait(false);

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
        IReadOnlyDictionary<Guid, (long Start, long End)> SectionWindowsById);

    private async Task<SectionScriptGenerationResult> GenerateSectionScriptsAsync(
        ShowBible bible, MusicModel music, LayoutSummary layoutSummary, DisplayGraph layout, EffectVocabularyCatalog vocabulary,
        IReadOnlyDictionary<SectionKind, double> energyCeilingByKind, Guid songId, JsonAiCallLogStore callLog, CancellationToken ct)
    {
        if (_choreographerClient is null || _choreographerModelName is null)
        {
            throw new InvalidOperationException("No SectionScripts were supplied and no Choreographer client is configured.");
        }

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

            var volatileSuffix = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layoutSummary, occurrenceIndex, totalOccurrences, melodicBrief, motifLink, phrases, _customPrompt?.Text);
            volatileSuffixBySectionId[section.Id] = volatileSuffix;

            return new SectionScriptBatchItem(section.Id, volatileSuffix);
        }).ToList();

        var batchGenerator = new SectionScriptBatchGenerator(_choreographerClient, callLog: callLog, modelName: _choreographerModelName);
        var reports = await batchGenerator.GenerateAllAsync(stablePrefix, items, songId, bible.Content.Looks, layout, vocabulary, _maxOutputTokens, ct).ConfigureAwait(false);

        var scripts = reports
            .Where(kvp => kvp.Value.Script is not null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Script!);

        return new SectionScriptGenerationResult(scripts, stablePrefix, volatileSuffixBySectionId, sectionWindowsById);
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

    private static Dictionary<string, ColorPaletteDefinition> BuildPaletteLibrary(IReadOnlyList<ShowBiblePalette> palettes) =>
        palettes.ToDictionary(p => p.Id, p => PaletteBuilder.Build(p.Colors.Select(c => c.HexColor).ToList()), StringComparer.Ordinal);

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
