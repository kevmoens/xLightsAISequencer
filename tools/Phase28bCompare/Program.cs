using System.Diagnostics;
using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Director;
using XlightsAI.Studio.Distribution;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Phase28bCompare;

/// <summary>
/// Build-plan Phase 28b: real end-to-end flag-off vs flag-on comparison. Mirrors
/// <c>tools/GenerateOnce/Program.cs</c>'s own composition-root wiring exactly (same %AppData%
/// directories, same store/decoder/runner wiring, same already-configured real Director/Choreographer
/// provider) so this exercises the identical <see cref="ChoreographyGenerationPipeline"/> entrypoint
/// every other real-run evidence doc in this repo already uses -- no new invocation path. For each real
/// song: one real Director call produces ONE Show Bible, then that SAME Show Bible is compiled twice via
/// <see cref="ChoreographyGenerationPipeline.GenerateAsync"/> -- once with
/// <see cref="ChoreographyGenerationSettings.EnableSectionTimelines"/> <see langword="false"/>, once
/// <see langword="true"/> -- so the only varying input between the two legs is the flag under test.
/// </summary>
public static class Program
{
    private const string ShowDirectory = @"C:\xLights";
    private const string EffectMetadataDirectory = @"C:\Program Files\xLights\effectmetadata";

    private sealed record SongSpec(string Name, string AudioPath);

    private static readonly SongSpec[] Songs =
    [
        new("Carol of the Bells",
            @"C:\xLights\Imported\Carol of the Bells -  Brent Yakymyk\Xlights Show\_lost\09. Carol Of The Bells (Instrumental).mp3"),
    ];

    public static async Task<int> Main(string[] args)
    {
        var appDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XlightsAI.Studio");
        Console.WriteLine($"[Phase28bCompare] AppData directory: {appDataDirectory}");

        var roleProviderConfigStore = new JsonRoleProviderConfigStore(Path.Combine(appDataDirectory, "ai-providers"));
        foreach (var role in new[] { AiRole.Director, AiRole.Choreographer })
        {
            var existing = await roleProviderConfigStore.LoadAsync(role, CancellationToken.None);
            Console.WriteLine($"[Phase28bCompare] {role} provider config: Enabled={existing.Enabled}, Provider={existing.ProviderType}, Model={existing.ModelName}");
        }

        var downloadsDirectory = Path.Combine(appDataDirectory, "downloads");
        var assetInstaller = new AssetInstaller(downloadsDirectory, new HttpReleaseAssetDownloader());
        var ffmpegInstaller = new FfmpegInstaller(Path.Combine(appDataDirectory, "tools", "ffmpeg"), assetInstaller);
        var audioDecoder = new TranscodingAudioDecoder(new WavAudioDecoder(), ffmpegInstaller, Path.Combine(appDataDirectory, "audio-transcode-cache"));
        var songAssetStore = new JsonSongAssetStore(Path.Combine(appDataDirectory, "audio-assets"));

        var stemModelAssetManager = new ModelAssetManager(Path.Combine(appDataDirectory, "tools", "stem-separation-model"), new HttpModelAssetDownloader());
        var stemSeparator = new SpectrogramStemSeparatorAdapter(
            new SpectrogramOnnxStemSeparator(stemModelAssetManager, new TarBzip2ModelArchiveExpander(), new WavStereoAudioDecoder(), Path.Combine(appDataDirectory, "stem-separation-output")),
            SherpaOnnxSpleeterModelAsset.Bundle, SherpaOnnxSpleeterModelAsset.Recipe);

        var audioAnalysisRunner = new AudioAnalysisRunner(
            audioDecoder, stemSeparator, SherpaOnnxSpleeterModelAsset.Archive,
            new JsonOnsetAnalysisStore(Path.Combine(appDataDirectory, "analysis-cache", "onsets")),
            new JsonTempoAnalysisStore(Path.Combine(appDataDirectory, "analysis-cache", "tempo")),
            new JsonBeatGridStore(Path.Combine(appDataDirectory, "analysis-cache", "beatgrid")),
            new JsonEnergyEnvelopeStore(Path.Combine(appDataDirectory, "analysis-cache", "energy")),
            new JsonPitchTrackStore(Path.Combine(appDataDirectory, "analysis-cache", "pitch")),
            new JsonStemActivityResultStore(Path.Combine(appDataDirectory, "analysis-cache", "vocal-activity")),
            new JsonBandEnergyEnvelopeStore(Path.Combine(appDataDirectory, "analysis-cache", "band-energy")));

        var structureAnalysisRunner = new StructureAnalysisRunner(
            new JsonBarFingerprintStore(Path.Combine(appDataDirectory, "analysis-cache", "fingerprints")),
            new JsonSectionBoundaryStore(Path.Combine(appDataDirectory, "analysis-cache", "boundaries")),
            new JsonSectionLabelStore(Path.Combine(appDataDirectory, "analysis-cache", "labels")),
            new JsonSectionLabelOverrideStore(Path.Combine(appDataDirectory, "analysis-cache", "label-overrides")),
            new JsonPhraseSegmentationStore(Path.Combine(appDataDirectory, "analysis-cache", "phrases")),
            new JsonPhraseBoundaryOverrideStore(Path.Combine(appDataDirectory, "analysis-cache", "phrase-overrides")),
            new JsonMotifClusteringStore(Path.Combine(appDataDirectory, "analysis-cache", "motifs")));

        var credentialStore = CredentialStoreFactory.Create(Path.Combine(appDataDirectory, "credentials"));
        var httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan }; // same documented HttpClient-level-timeout workaround as GenerateOnce.
        var aiRoleClientResolver = new AiRoleClientResolver(roleProviderConfigStore, credentialStore, httpClient);

        var showLayoutImporter = new ShowLayoutImporter();
        Console.WriteLine($"[Phase28bCompare] Importing show layout from '{ShowDirectory}'...");
        var layout = await showLayoutImporter.ImportReadOnlyAsync(ShowDirectory, CancellationToken.None);
        var layoutSummary = LayoutSummary.Build(layout);
        Console.WriteLine($"[Phase28bCompare] Layout imported: {layout.Models.Count} models.");

        var catalogs = EffectCatalogFactory.BuildFromMetadataDirectory(EffectMetadataDirectory);
        var registry = new EffectRecipeRegistry(catalogs.Recipes);
        var recipeNameById = catalogs.Recipes.Recipes.ToDictionary(r => r.RecipeId, r => r.XlightsEffectName, StringComparer.Ordinal);

        var (directorClient, directorModel) = await aiRoleClientResolver.ResolveRequiredAsync(AiRole.Director, CancellationToken.None);
        var (rawChoreographerClient, choreographerModel) = await aiRoleClientResolver.ResolveRequiredAsync(AiRole.Choreographer, CancellationToken.None);
        var timing = new CallTimingRecorder();
        var choreographerClient = new TimingChatClient(rawChoreographerClient, timing);

        var corpusKnowledgeDirectory = Path.Combine(appDataDirectory, "corpus-knowledge");
        var (craftRules, exemplarIndex, patternLibrary, wholeCanvasCoOccurrence, layerComplexity) =
            await AiChoreographyWholeSongGenerator.LoadCorpusKnowledgeAsync(corpusKnowledgeDirectory, CancellationToken.None);
        var knowledge = new DirectorKnowledgeContext(
            catalogs.Vocabulary, craftRules, exemplarIndex, MultiLayerTemplateCatalogLoader.Load(corpusKnowledgeDirectory),
            EffectVisualCharacterCatalogLoader.Load(), patternLibrary, wholeCanvasCoOccurrence, layerComplexity,
            DirectorLessonCatalogLoader.Load(corpusKnowledgeDirectory));

        var reportLines = new List<string>();
        foreach (var songSpec in Songs)
        {
            if (!File.Exists(songSpec.AudioPath))
            {
                Console.WriteLine($"[Phase28bCompare] SKIP '{songSpec.Name}': audio file not found at '{songSpec.AudioPath}'.");
                continue;
            }

            Console.WriteLine();
            Console.WriteLine($"==================== {songSpec.Name} ====================");
            var song = await audioDecoder.ProbeAsync(songSpec.AudioPath, CancellationToken.None);
            await songAssetStore.SaveAsync(song, CancellationToken.None);

            var audio = await audioAnalysisRunner.AnalyzeAsync(song, CancellationToken.None);
            var structure = await structureAnalysisRunner.AnalyzeAsync(song.Id, song.Sha256, audio.BeatGrid, audio.Energy, audio.Melody, CancellationToken.None);

            var transientHits = audio.Onsets is { } onsets ? TransientHitDetector.Detect(onsets, audio.BandEnergy) : null;
            var energyTransitions = audio.BandEnergy is { } bandEnergyForTransitions ? EnergyTransitionDetector.Detect(bandEnergyForTransitions) : null;
            var leadInstrumentWindows = audio.BandEnergy is { } bandEnergyForLead ? LeadInstrumentWindowDetector.Detect(bandEnergyForLead) : null;
            var keyChanges = KeyDetector.DetectKeyChanges(structure.FingerprintSet);
            var music = new MusicModel(
                song.Id, song.DurationTicks, audio.BeatGrid, structure.Labels, structure.Phrases,
                BandEnergy: audio.BandEnergy, VocalActivity: audio.VocalActivity, VisemeMarks: audio.VisemeMarks,
                Melody: audio.Melody, Motifs: structure.Motifs, TransientHits: transientHits, EnergyTransitions: energyTransitions,
                Energy: audio.Energy, KeyChanges: keyChanges, Transcript: null, LeadInstrumentWindows: leadInstrumentWindows);

            Console.WriteLine($"[Phase28bCompare] {songSpec.Name}: {music.Sections.Labels.Count} sections, duration {TimeSpan.FromTicks(song.DurationTicks)}.");

            // Leg 1 (flag OFF): generates its own real Show Bible via the real Director call.
            var projectDirOff = Path.Combine(appDataDirectory, "ai-choreography", "phase28b-compare", $"{SanitizeForPath(songSpec.Name)}-off");
            var settingsOff = new ChoreographyGenerationSettings(
                layout, catalogs.Vocabulary, catalogs.Recipes, registry, DefaultEnergyCeilings.ArchitectureSection10, projectDirOff,
                EnableSectionTimelines: false);
            var pipelineOff = new ChoreographyGenerationPipeline(
                knowledge, StylePresetCatalog.HighEnergyEdmId, directorClient, directorModel, choreographerClient, choreographerModel, maxOutputTokens: 16000);

            timing.Reset();
            var offWatch = Stopwatch.StartNew();
            var songIdOff = Guid.NewGuid();
            var reportOff = await pipelineOff.GenerateAsync(music, layoutSummary, settingsOff, songIdOff, existingBible: null, CancellationToken.None);
            offWatch.Stop();
            var offTiming = timing.TakeSnapshot();
            Console.WriteLine($"[Phase28bCompare] {songSpec.Name} OFF leg: {offWatch.Elapsed}, {reportOff.SectionCount} sections, {reportOff.SectionsNeedingRetry} needing retry, {reportOff.CompiledResult.Violations.Count} violations.");

            // Leg 2 (flag ON): reuses the EXACT same Show Bible leg 1's real Director call produced, so
            // the Choreographer schema (flat vs. cue-bearing) is the only variable between the two legs.
            var projectDirOn = Path.Combine(appDataDirectory, "ai-choreography", "phase28b-compare", $"{SanitizeForPath(songSpec.Name)}-on");
            var settingsOn = settingsOff with { ProjectDirectory = projectDirOn, EnableSectionTimelines = true };
            var pipelineOn = new ChoreographyGenerationPipeline(
                knowledge, StylePresetCatalog.HighEnergyEdmId, directorClient: null, directorModelName: null, choreographerClient, choreographerModel, maxOutputTokens: 16000);

            timing.Reset();
            var onWatch = Stopwatch.StartNew();
            var songIdOn = Guid.NewGuid();
            string songSection;
            try
            {
                var reportOn = await pipelineOn.GenerateAsync(music, layoutSummary, settingsOn, songIdOn, existingBible: reportOff.Bible, CancellationToken.None);
                onWatch.Stop();
                var onTiming = timing.TakeSnapshot();
                Console.WriteLine($"[Phase28bCompare] {songSpec.Name} ON leg: {onWatch.Elapsed}, {reportOn.SectionCount} sections, {reportOn.SectionsNeedingRetry} needing retry, {reportOn.CompiledResult.Violations.Count} violations.");
                songSection = BuildSongSection(songSpec.Name, music, reportOff, reportOn, offTiming, onTiming, recipeNameById);
            }
            catch (Exception ex)
            {
                // Build-plan Phase 28b: a real crash in the ON leg (EnableSectionTimelines=true) must
                // never discard the OFF leg's already-real, already-persisted comparison data -- this
                // is reported as the finding itself (per this phase's own "stop and report a concrete
                // failing case, never silently patch" instruction), not swallowed.
                onWatch.Stop();
                Console.WriteLine($"[Phase28bCompare] {songSpec.Name} ON leg CRASHED after {onWatch.Elapsed}: {ex.GetType().Name}: {ex.Message}");
                songSection = $"""
                    ## {songSpec.Name}

                    Sections: {music.Sections.Labels.Count}. Duration: {TimeSpan.FromTicks(music.SongDurationTicks)}.

                    OFF leg: {offWatch.Elapsed}, {reportOff.SectionCount} sections, {reportOff.SectionsNeedingRetry} needing retry, {reportOff.CompiledResult.Violations.Count} violations.

                    **ON leg CRASHED** after {onWatch.Elapsed} with an unhandled `{ex.GetType().Name}`: {ex.Message}
                    ```
                    {ex}
                    ```
                    """;
            }

            reportLines.Add(songSection);

            // Write after EVERY song, not just at the end -- a later song's long real run must never
            // risk losing an already-completed song's real results if this process is interrupted.
            var evidencePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "implementation-evidence", "PHASE-28B-SECTION-TIMELINE-REAL-RUN.md");
            Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
            await File.WriteAllTextAsync(evidencePath, BuildDocument(reportLines));
            Console.WriteLine($"[Phase28bCompare] Wrote {Path.GetFullPath(evidencePath)} ({reportLines.Count}/{Songs.Length} song(s) so far).");
        }

        Console.WriteLine();
        Console.WriteLine("[Phase28bCompare] All songs complete.");

        return 0;
    }

    private static string SanitizeForPath(string name) => string.Concat(name.Where(c => char.IsLetterOrDigit(c)));

    private static string BuildDocument(IReadOnlyList<string> songSections) => $"""
        # Phase 28b -- Section Timeline real-run comparison (auto-generated by tools/Phase28bCompare)

        Real layout: `C:\xLights\xlights_rgbeffects.xml`. Director+Choreographer model: whatever is configured in the real
        app's own Settings screen at run time (see console output for the exact provider/model). Each song's real Show
        Bible is generated ONCE (real Director call) and reused for BOTH legs below -- `EnableSectionTimelines=false`
        (OFF, today's unchanged behavior) and `=true` (ON, build-plan Phase 28a's new path) -- so the flag is the only
        varying input between the two legs.

        {string.Join("\n\n", songSections)}
        """;

    private static string BuildSongSection(
        string songName, MusicModel music, ChoreographyGenerationReport off, ChoreographyGenerationReport on,
        CallTimingRecorder.Snapshot offTiming, CallTimingRecorder.Snapshot onTiming, IReadOnlyDictionary<string, string> recipeNameById)
    {
        var offTally = TargetTally(off.CompiledResult.IR.Effects);
        var onTally = TargetTally(on.CompiledResult.IR.Effects);
        var allTargets = offTally.Keys.Concat(onTally.Keys).Distinct(StringComparer.Ordinal).OrderByDescending(t => Math.Max(offTally.GetValueOrDefault(t), onTally.GetValueOrDefault(t))).ToList();

        var offBreadth = off.CompiledResult.Violations.Count(v => v.Rule == ChoreographyLegalityValidator.RuleSectionModelBreadth);
        var onBreadth = on.CompiledResult.Violations.Count(v => v.Rule == ChoreographyLegalityValidator.RuleSectionModelBreadth);
        var offTotal = off.CompiledResult.Violations.Count;
        var onTotal = on.CompiledResult.Violations.Count;

        var violationsByRuleOff = off.CompiledResult.Violations.GroupBy(v => v.Rule).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}={g.Count()}");
        var violationsByRuleOn = on.CompiledResult.Violations.GroupBy(v => v.Rule).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}={g.Count()}");

        var cuesSummary = BuildCueAnnotationCheck(on);

        var tallyRows = string.Join("\n", allTargets.Select(t => $"| {t} | {offTally.GetValueOrDefault(t)} | {onTally.GetValueOrDefault(t)} |"));

        return $"""
            ## {songName}

            Sections: {music.Sections.Labels.Count}. Duration: {TimeSpan.FromTicks(music.SongDurationTicks)}.

            ### Compiled-effect target tally (flag-off vs flag-on, same real Show Bible)
            | Target | OFF effects | ON effects |
            |---|---:|---:|
            {tallyRows}

            Total compiled effects: OFF={off.CompiledResult.IR.Effects.Count}, ON={on.CompiledResult.IR.Effects.Count}.

            ### (a) RuleSectionModelBreadth -- real counts, not estimated
            OFF: **{offBreadth}** `section-model-breadth` violation(s) out of {offTotal} total legality violations.
            ON: **{onBreadth}** `section-model-breadth` violation(s) out of {onTotal} total legality violations.
            OFF violations by rule: {string.Join(", ", violationsByRuleOff)}
            ON violations by rule: {string.Join(", ", violationsByRuleOn)}

            ### (b) Manual annotation-vs-compiled check (flag-on leg)
            {cuesSummary}

            ### (c) Latency/cost per section (Choreographer calls, flag-on leg; baseline ~6,200 input / ~300 output tokens, 3.4-4.7s/section)
            {onTiming.FormatAgainstBaseline()}
            OFF leg Choreographer timing (for reference): {offTiming.FormatAgainstBaseline()}
            """;
    }

    private static Dictionary<string, int> TargetTally(IReadOnlyList<EffectInstance> effects) =>
        effects.GroupBy(e => e.Target.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

    /// <summary>Build-plan Phase 28b §8 item 4b: pulls up to 4 real compiled effects that carry a cue's own SourceAnnotation and prints exactly what a human needs to judge whether the compiled prop/time/intensity matches what the Annotation claims.</summary>
    private static string BuildCueAnnotationCheck(ChoreographyGenerationReport on)
    {
        var annotated = on.CompiledResult.IR.Effects.Where(e => !string.IsNullOrWhiteSpace(e.SourceAnnotation)).Take(4).ToList();
        if (annotated.Count == 0)
        {
            var timelineCount = on.Timelines?.Values.Sum(t => t.CuesOrEmpty.Count) ?? 0;
            return $"No compiled effect carried a real SourceAnnotation this run (the model declared {timelineCount} total Cue(s) across every section's own real SectionTimeline; a Cue with no Annotation compiles with SourceAnnotation=null, which is valid but gives nothing to manually check here).";
        }

        var lines = new List<string> { $"{annotated.Count} real annotated compiled effect(s) found; each one read by hand below:" };
        foreach (var effect in annotated)
        {
            lines.Add($"- Target=`{effect.Target.Value}`, Window=[{TimeSpan.FromTicks(effect.StartTicks):mm\\:ss\\.ff}-{TimeSpan.FromTicks(effect.EndTicks):mm\\:ss\\.ff}), RecipeId=`{effect.RecipeId}`\n  Annotation: \"{effect.SourceAnnotation}\"");
        }

        return string.Join("\n", lines);
    }
}
