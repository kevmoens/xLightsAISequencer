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
/// <param name="FillerPool">The always-moving rule's own rotating filler pool.</param>
/// <param name="EnergyCeilingByKind">Per-<see cref="SectionKind"/> energy ceiling (architecture §10's structural arc).</param>
/// <param name="ProjectDirectory">The project root -- <c>bible/</c>/<c>scripts/</c>/<c>llm/</c>/<c>exports/</c> (architecture §13) are created under this.</param>
public sealed record ChoreographyGenerationSettings(
    DisplayGraph Layout,
    EffectVocabularyCatalog Vocabulary,
    EffectRecipeCatalog RecipeCatalog,
    IEffectRecipeRegistry RecipeRegistry,
    FillerPool FillerPool,
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

    public ChoreographyGenerationPipeline(
        DirectorKnowledgeContext knowledge, string style,
        IChatCompletionClient? directorClient, string? directorModelName,
        IChatCompletionClient? choreographerClient, string? choreographerModelName,
        int maxOutputTokens = 4000)
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

        // 1. Get-or-generate the Show Bible.
        var bible = existingBible ?? await GenerateShowBibleAsync(music, layoutSummary, settings, songId, callLog, ct).ConfigureAwait(false);
        await new JsonShowBibleStore(bibleDir).SaveNextVersionAsync(bible, ct).ConfigureAwait(false);

        // 2. Per-section SectionScripts.
        var scripts = await GenerateSectionScriptsAsync(bible, music, layoutSummary, settings.Layout, settings.EnergyCeilingByKind, songId, callLog, ct).ConfigureAwait(false);
        await PersistScriptsAsync(scriptsDir, songId, scripts, ct).ConfigureAwait(false);

        var sectionsNeedingRetry = scripts.Count < music.Sections.Labels.Count ? music.Sections.Labels.Count - scripts.Count : 0;

        // 3. SectionScript + Look -> effective per-section Look library (build-plan Phase 6 task 3),
        // plus (build-plan Phase 10 §3e) any declared phrase-scoped sub-section variations, joined against
        // this song's own real phrases (grouped by their own owning section).
        var phrasesBySectionId = music.Phrases.Phrases
            .GroupBy(p => p.SectionLabelId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PhraseCandidate>)g.OrderBy(p => p.StartBarIndex).ToList());
        var (effectiveLibrary, lookIdBySectionId, phraseLookAssignmentsBySectionId) =
            SectionScriptCompiler.Compile(bible.Content.Looks, scripts, phrasesBySectionId);

        // 4. Palette library from the Show Bible's own declared palettes.
        var paletteLibrary = BuildPaletteLibrary(bible.Content.Palettes);

        var compilerSettings = new LookCompilerSettings(settings.Vocabulary, settings.RecipeCatalog, settings.RecipeRegistry, paletteLibrary, settings.FillerPool);

        // 5. Compile via Phase 4's own unmodified engine.
        var engineInput = new ChoreographyEngine.Input(
            settings.Layout, music.BeatGrid, music.Sections, music.Phrases, music.BandEnergy, music.VocalActivity,
            effectiveLibrary, lookIdBySectionId, settings.EnergyCeilingByKind, music.SongDurationTicks, compilerSettings,
            VisemeMarks: music.VisemeMarks, PhraseLookAssignmentsBySectionId: phraseLookAssignmentsBySectionId);
        var compiled = ChoreographyEngine.Compile(engineInput);

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

        var prompt = DirectorPromptBuilder.Build(_knowledge, _style, music, layoutSummary);
        var generator = new ShowBibleGenerator(_directorClient, callLog: callLog, modelName: _directorModelName);
        var report = await generator.GenerateAsync(prompt, songId, settings.Layout, settings.Vocabulary, settings.RecipeCatalog, _maxOutputTokens, ct, _knowledge.TemplatesOrEmpty).ConfigureAwait(false);

        return report.Bible ?? throw new InvalidOperationException(
            $"Show Bible generation exhausted {report.AttemptCount} attempt(s) without validating: {string.Join("; ", report.FinalViolations.Select(v => v.Message))}");
    }

    private async Task<IReadOnlyDictionary<Guid, SectionScript>> GenerateSectionScriptsAsync(
        ShowBible bible, MusicModel music, LayoutSummary layoutSummary, DisplayGraph layout,
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

        var items = orderedSections.Select(section =>
        {
            var window = barGrid.WindowForBarRange(new BarIndex(section.StartBarIndex), new BarIndex(section.EndBarIndexExclusive))
                ?? new TimeWindow(0, music.SongDurationTicks);
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

            return new SectionScriptBatchItem(
                section.Id,
                SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layoutSummary, occurrenceIndex, totalOccurrences, melodicBrief, motifLink, phrases));
        }).ToList();

        var batchGenerator = new SectionScriptBatchGenerator(_choreographerClient, callLog: callLog, modelName: _choreographerModelName);
        var reports = await batchGenerator.GenerateAllAsync(stablePrefix, items, songId, bible.Content.Looks, layout, _maxOutputTokens, ct).ConfigureAwait(false);

        return reports
            .Where(kvp => kvp.Value.Script is not null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Script!);
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
}
