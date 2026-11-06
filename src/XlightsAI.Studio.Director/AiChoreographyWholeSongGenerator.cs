using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director;

/// <summary>
/// The real <see cref="IWholeSongGenerator"/> the Shell composition root should wire in place of
/// <see cref="WholeSongGenerator"/>: it reuses that type's own audio/structure analysis (so Melody/
/// Phrases/Motifs/SongId keep feeding the Piano Roll and Structure Inspector screens exactly as before),
/// then drives the real LLM-driven <see cref="ChoreographyGenerationPipeline"/> (architecture §§2-10,
/// build-plan Phases 5-8) for the actual choreography instead of <c>ChoreographyCompositionRunner</c>'s
/// old M1-M4 path, whose only registered effect recipe is a flat, uniform "On".
///
/// Deliberately lives in <c>XlightsAI.Studio.Director</c>, not alongside <see cref="WholeSongGenerator"/>
/// in <c>XlightsAI.Studio.Pipeline</c>: <c>Director</c> already references <c>Pipeline</c> (for
/// <see cref="CraftRules"/>/<see cref="ExemplarIndex"/>/the analysis runners), so a reverse reference
/// from <c>Pipeline</c> to <c>Director</c> for this type would be a cyclic project reference. Implementing
/// the same <see cref="IWholeSongGenerator"/> seam from this side is sufficient for the Shell to consume
/// it identically.
/// </summary>
public sealed class AiChoreographyWholeSongGenerator : IWholeSongGenerator
{
    private readonly AudioAnalysisRunner _audioAnalysis;
    private readonly StructureAnalysisRunner _structureAnalysis;
    private readonly AiRoleClientResolver _roleClientResolver;
    private readonly string _effectMetadataDirectory;
    private readonly string _projectDirectory;
    private readonly string? _knowledgeDirectory;
    private readonly string _style;
    private readonly IReadOnlyDictionary<SectionKind, double> _energyCeilingByKind;
    private readonly int _maxOutputTokens;
    private readonly string? _showDirectory;
    private readonly string? _xlightsExecutablePath;
    private readonly ILyricTranscriber? _transcriber;
    private readonly IAudioDecoder? _transcriptAudioDecoder;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AiChoreographyWholeSongGenerator> _logger;

    /// <param name="audioAnalysis">Same real runner <see cref="WholeSongGenerator"/> uses -- shared so both generators cache/produce identical analysis artifacts.</param>
    /// <param name="structureAnalysis">Same real runner <see cref="WholeSongGenerator"/> uses.</param>
    /// <param name="roleClientResolver">Resolves the Director/Choreographer roles' configured clients, throwing <see cref="AiProviderNotConfiguredException"/> when a role is not configured.</param>
    /// <param name="effectMetadataDirectory">Passed to <see cref="EffectCatalogFactory.BuildFromMetadataDirectory"/> on every generation (no existing Shell setting names a configured xLights install directory -- see this type's own composition-root caller for the fallback this resolves to).</param>
    /// <param name="projectDirectory">Root for this pipeline run's own <c>bible/</c>/<c>scripts/</c>/<c>llm/</c>/<c>exports/</c> artifacts (architecture §13).</param>
    /// <param name="style">
    /// Architecture §10 style preset id. No style-selection UI/setting exists on the Show Import screen
    /// yet (confirmed: neither <see cref="ShowImportViewModel"/> nor <c>AppSettings</c> has one) -- this
    /// is an honest, disclosed scope boundary, not an oversight; <see cref="StylePresetCatalog.TraditionalClassicId"/>
    /// is the safe, most conservative default until that UI exists.
    /// </param>
    /// <param name="energyCeilingByKind">Defaults to <see cref="DefaultEnergyCeilings.ArchitectureSection10"/> when omitted.</param>
    /// <param name="maxOutputTokens">
    /// Confirmed against a real local "thinking" model (Ollama <c>ornith-1.5:9b</c>): its reasoning
    /// tokens count against the same <c>max_tokens</c> budget as the final JSON content, so
    /// <see cref="ChoreographyGenerationPipeline"/>'s own 4000-token default -- sized for a non-reasoning
    /// model emitting only the Show Bible/SectionScript JSON itself -- is routinely exhausted by
    /// reasoning alone before any content is written, surfacing as an empty
    /// <c>choices[0].message.content</c>. 16000 leaves real headroom for both reasoning and a full
    /// Show Bible on a model with a large context window; still small next to this model's own
    /// 262144-token context.
    /// </param>
    /// <param name="knowledgeDirectory">
    /// Build-plan Phase 10 §3f ("Activate corpus mining"): the directory <see cref="CorpusMiningRunner"/>
    /// persists its <see cref="CraftRules"/>/<see cref="ExemplarIndex"/> under (via
    /// <see cref="JsonCraftRulesStore"/>/<see cref="JsonExemplarIndexStore"/>), read back on every
    /// generation so the Director prompt's "Verified craft rules"/"Retrieved exemplars" sections carry
    /// real mined content once corpus mining has actually been run. Nullable/defaults to
    /// <see langword="null"/> for backward compatibility with every pre-existing call site (and any
    /// fresh install that has never run corpus mining): <see cref="GenerateAsync"/> falls back to the
    /// same empty <see cref="CraftRules"/>/<see cref="ExemplarIndex"/> this type has always hardcoded
    /// whenever this is <see langword="null"/> or the directory has no persisted files yet -- a missing
    /// corpus run must never crash or block a generation.
    /// </param>
    /// <param name="showDirectory">
    /// Build-plan "richer analysis tools" Phase 3 (2026-11-02): the real, on-disk xLights show directory --
    /// required only when a run's own <see cref="CustomGenerationPrompt.EnableQualityPass"/> is requested;
    /// <see langword="null"/> (the default) for every run that never asks for one.
    /// </param>
    /// <param name="xlightsExecutablePath">
    /// Build-plan "richer analysis tools" Phase 3 (2026-11-02): the same configured xLights executable the
    /// Shell's own "Open in xLights" action already uses -- required only when a run's own
    /// <see cref="CustomGenerationPrompt.EnableQualityPass"/> is requested; <see langword="null"/> (the
    /// default) for every run that never asks for one.
    /// </param>
    /// <param name="transcriber">
    /// Build-plan "richer analysis tools" Phase 6 (2026-11-02): resolved speech-to-text over this song's
    /// own isolated vocals stem, or <see langword="null"/> (the default) to skip transcription entirely --
    /// <see cref="MusicModel.Transcript"/> stays <see langword="null"/> and every downstream consumer
    /// (e.g. word-sync for timed cues) behaves exactly as it did before this phase. A not-yet-installed
    /// model (<see cref="ModelAssetConsentRequiredException"/>) is caught and treated the same as "no
    /// transcriber supplied" -- transcription is a disclosed, optional enhancement, never a reason to
    /// block or fail a generation.
    /// </param>
    /// <param name="transcriptAudioDecoder">Required alongside <paramref name="transcriber"/> (ignored otherwise) -- decodes the vocals stem for <see cref="StemSongAssetFactory.ProbeAsync"/>.</param>
    public AiChoreographyWholeSongGenerator(
        AudioAnalysisRunner audioAnalysis,
        StructureAnalysisRunner structureAnalysis,
        AiRoleClientResolver roleClientResolver,
        string effectMetadataDirectory,
        string projectDirectory,
        string? knowledgeDirectory = null,
        string style = StylePresetCatalog.TraditionalClassicId,
        IReadOnlyDictionary<SectionKind, double>? energyCeilingByKind = null,
        int maxOutputTokens = 16000,
        string? showDirectory = null,
        string? xlightsExecutablePath = null,
        ILyricTranscriber? transcriber = null,
        IAudioDecoder? transcriptAudioDecoder = null,
        ILoggerFactory? loggerFactory = null)
    {
        _audioAnalysis = audioAnalysis ?? throw new ArgumentNullException(nameof(audioAnalysis));
        _structureAnalysis = structureAnalysis ?? throw new ArgumentNullException(nameof(structureAnalysis));
        _roleClientResolver = roleClientResolver ?? throw new ArgumentNullException(nameof(roleClientResolver));
        ArgumentException.ThrowIfNullOrWhiteSpace(effectMetadataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(style);
        _effectMetadataDirectory = effectMetadataDirectory;
        _projectDirectory = projectDirectory;
        _knowledgeDirectory = knowledgeDirectory;
        _style = style;
        _energyCeilingByKind = energyCeilingByKind ?? DefaultEnergyCeilings.ArchitectureSection10;
        _maxOutputTokens = maxOutputTokens;
        _showDirectory = showDirectory;
        _xlightsExecutablePath = xlightsExecutablePath;
        _transcriber = transcriber;
        _transcriptAudioDecoder = transcriptAudioDecoder;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<AiChoreographyWholeSongGenerator>();
    }

    /// <param name="customPrompt">
    /// Build-plan Phase 14 §2c: the user's own free-text creative direction for this specific run
    /// (per-call, not per-instance -- unlike <see cref="_style"/>, a fresh value can legitimately
    /// differ on every generation), or <see langword="null"/>/empty for the pre-existing default
    /// behavior every caller before this phase (and every caller with none) gets.
    /// </param>
    /// <param name="showDirectory">Build-plan "richer analysis tools" Phase 3 (2026-11-02): overrides this instance's own constructor-supplied <see cref="_showDirectory"/> for this one run, when supplied (per-call, same "fresh value can legitimately differ on every generation" reasoning as <paramref name="customPrompt"/>).</param>
    /// <param name="xlightsExecutablePath">Same per-call override reasoning as <paramref name="showDirectory"/>, for <see cref="_xlightsExecutablePath"/>.</param>
    public async Task<WholeSongGenerationResult> GenerateAsync(
        SongAsset song, DisplayGraph layout, CancellationToken ct = default, CustomGenerationPrompt? customPrompt = null,
        string? showDirectory = null, string? xlightsExecutablePath = null)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentNullException.ThrowIfNull(layout);

        var overall = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Whole-song generation starting for song {SongId} ({DurationTicks} ticks, style '{Style}').", song.Id, song.DurationTicks, customPrompt?.Style ?? _style);

        // Resolved before the (comparatively expensive) analysis/vocabulary work below, so a user who
        // has not configured either role fails fast with a clear, actionable message rather than paying
        // for a full audio analysis pass first.
        var (directorClient, directorModel) = await _roleClientResolver.ResolveRequiredAsync(AiRole.Director, ct).ConfigureAwait(false);
        var (choreographerClient, choreographerModel) = await _roleClientResolver.ResolveRequiredAsync(AiRole.Choreographer, ct).ConfigureAwait(false);

        var audio = await _audioAnalysis.AnalyzeAsync(song, ct).ConfigureAwait(false);
        var structure = await _structureAnalysis
            .AnalyzeAsync(song.Id, song.Sha256, audio.BeatGrid, audio.Energy, audio.Melody, ct)
            .ConfigureAwait(false);

        var transientHits = audio.Onsets is { } onsets
            ? TransientHitDetector.Detect(onsets, audio.BandEnergy)
            : null;
        var energyTransitions = audio.BandEnergy is { } bandEnergyForTransitions
            ? EnergyTransitionDetector.Detect(bandEnergyForTransitions)
            : null;
        var keyChanges = KeyDetector.DetectKeyChanges(structure.FingerprintSet);
        var transcript = await TryTranscribeAsync(audio.Stems, ct).ConfigureAwait(false);
        var music = new MusicModel(
            song.Id, song.DurationTicks, audio.BeatGrid, structure.Labels, structure.Phrases,
            BandEnergy: audio.BandEnergy, VocalActivity: audio.VocalActivity, VisemeMarks: audio.VisemeMarks,
            Melody: audio.Melody, Motifs: structure.Motifs, TransientHits: transientHits, EnergyTransitions: energyTransitions,
            Energy: audio.Energy, KeyChanges: keyChanges, Transcript: transcript);
        var layoutSummary = LayoutSummary.Build(layout);

        var catalogs = EffectCatalogFactory.BuildFromMetadataDirectory(_effectMetadataDirectory);

        // Build-plan Phase 10 §3f ("Activate corpus mining"): load the real, persisted CraftRules/
        // ExemplarIndex a CorpusMiningRunner run wrote under _knowledgeDirectory, falling back to the
        // same empty values this type has always hardcoded whenever no knowledge directory was
        // configured, or corpus mining has simply never been run yet against it (FileNotFoundException
        // from either store) -- a missing/not-yet-run corpus must never crash or block a generation.
        //
        // Build-plan Phase 10 §6b task 3: the shippable multi-layer Tree/Matrix template catalog (manual
        // first pass over real §6a research) IS wired in here -- loaded once from this assembly's own
        // embedded seed data, no offline step required.
        var (craftRules, exemplarIndex) = await LoadCorpusKnowledgeAsync(_knowledgeDirectory, ct).ConfigureAwait(false);
        var knowledge = new DirectorKnowledgeContext(
            catalogs.Vocabulary,
            craftRules,
            exemplarIndex,
            MultiLayerTemplateCatalogLoader.Load());

        var registry = new EffectRecipeRegistry(catalogs.Recipes);

        var settings = new ChoreographyGenerationSettings(
            layout, catalogs.Vocabulary, catalogs.Recipes, registry, _energyCeilingByKind, _projectDirectory,
            showDirectory ?? _showDirectory, xlightsExecutablePath ?? _xlightsExecutablePath);

        // Build-plan "richer analysis tools" Phase 3 (2026-11-02): the Critic role is never required --
        // only consulted when this run actually asked for a quality pass, and even then a quality pass
        // runs numeric-only when this role is not configured (see ChoreographyGenerationPipeline's own
        // remarks). Resolving it unconditionally for every run (quality pass or not) would surface a
        // confusing AiProviderNotConfiguredException for the overwhelming majority of runs that never ask
        // for one.
        IVisionCapableChatCompletionClient? criticClient = null;
        string? criticModel = null;
        if (customPrompt?.EnableQualityPass == true)
        {
            try
            {
                var (resolvedCriticClient, resolvedCriticModel) = await _roleClientResolver.ResolveRequiredAsync(AiRole.Critic, ct).ConfigureAwait(false);
                if (resolvedCriticClient is IVisionCapableChatCompletionClient visionClient)
                {
                    criticClient = visionClient;
                    criticModel = resolvedCriticModel;
                }
            }
            catch (AiProviderNotConfiguredException)
            {
                // Disclosed, expected fallback -- see this block's own remarks above.
            }
        }

        // User feedback (2026-11-01): a non-null CustomGenerationPrompt.Style overrides this instance's
        // own startup-configured _style for this one run -- see that property's own doc comment.
        var pipeline = new ChoreographyGenerationPipeline(
            knowledge, customPrompt?.Style ?? _style,
            directorClient, directorModel,
            choreographerClient, choreographerModel,
            _maxOutputTokens,
            customPrompt,
            criticClient,
            criticModel,
            _loggerFactory.CreateLogger<ChoreographyGenerationPipeline>());

        var songId = Guid.NewGuid();
        var report = await pipeline.GenerateAsync(music, layoutSummary, settings, songId, existingBible: null, ct).ConfigureAwait(false);

        var document = WithMediaFile(report.CompiledResult.IR.ToSequenceDocument(new SequenceMetadata(Song: song.Id.Value.ToString())), song);

        _logger.LogInformation("Whole-song generation completed for song {SongId} (run {GenerationRunId}) in {ElapsedMs}ms.",
            song.Id, songId, overall.ElapsedMilliseconds);
        return new WholeSongGenerationResult(
            document, song.Id, audio.Melody, structure.Phrases, structure.Motifs, BuildLookTemplateUsage(report.Bible), GenerationRunId: songId);
    }

    /// <summary>
    /// Build-plan "richer analysis tools" post-processing phase (2026-11-02): the user's own explicit ask
    /// -- "a post-processing prompt... where I can put time ranges and have it alter the existing
    /// sequence, so it would only fix what I ask and leave the rest alone." Reloads the exact Show
    /// Bible/SectionScripts a prior <see cref="GenerateAsync"/> run (identified by its own returned
    /// <see cref="WholeSongGenerationResult.GenerationRunId"/>) persisted, re-derives the same
    /// <see cref="MusicModel"/>/<see cref="LayoutSummary"/>/<see cref="ChoreographyGenerationSettings"/>
    /// that run used (cheap -- every real analysis stage is cached on disk, keyed by source hash), and
    /// delegates the actual touch-up to <see cref="ChoreographyGenerationPipeline.ApplyPostProcessingPromptAsync"/>.
    /// </summary>
    /// <param name="generationRunId">The prior run's own <see cref="WholeSongGenerationResult.GenerationRunId"/> to touch up.</param>
    /// <param name="postProcessingPromptText">Free text with one or more <c>mm:ss</c>/<c>hh:mm:ss</c>(-range) tokens naming what to change and when -- same literal syntax as the pre-generation custom prompt's own timed cues.</param>
    /// <exception cref="InvalidOperationException">No Show Bible is persisted for <paramref name="generationRunId"/> (it was never generated, or its project directory has since been cleared).</exception>
    public async Task<WholeSongGenerationResult> ApplyPostProcessingPromptAsync(
        SongAsset song, DisplayGraph layout, Guid generationRunId, string postProcessingPromptText, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentException.ThrowIfNullOrWhiteSpace(postProcessingPromptText);

        var (choreographerClient, choreographerModel) = await _roleClientResolver.ResolveRequiredAsync(AiRole.Choreographer, ct).ConfigureAwait(false);

        var audio = await _audioAnalysis.AnalyzeAsync(song, ct).ConfigureAwait(false);
        var structure = await _structureAnalysis
            .AnalyzeAsync(song.Id, song.Sha256, audio.BeatGrid, audio.Energy, audio.Melody, ct)
            .ConfigureAwait(false);

        var transientHits = audio.Onsets is { } onsets ? TransientHitDetector.Detect(onsets, audio.BandEnergy) : null;
        var keyChanges = KeyDetector.DetectKeyChanges(structure.FingerprintSet);
        var transcript = await TryTranscribeAsync(audio.Stems, ct).ConfigureAwait(false);
        var music = new MusicModel(
            song.Id, song.DurationTicks, audio.BeatGrid, structure.Labels, structure.Phrases,
            BandEnergy: audio.BandEnergy, VocalActivity: audio.VocalActivity, VisemeMarks: audio.VisemeMarks,
            Melody: audio.Melody, Motifs: structure.Motifs, TransientHits: transientHits,
            Energy: audio.Energy, KeyChanges: keyChanges, Transcript: transcript);
        var layoutSummary = LayoutSummary.Build(layout);

        var catalogs = EffectCatalogFactory.BuildFromMetadataDirectory(_effectMetadataDirectory);
        var registry = new EffectRecipeRegistry(catalogs.Recipes);
        var settings = new ChoreographyGenerationSettings(
            layout, catalogs.Vocabulary, catalogs.Recipes, registry, _energyCeilingByKind, _projectDirectory,
            _showDirectory, _xlightsExecutablePath);

        var bibleDir = Path.Combine(_projectDirectory, "bible");
        var scriptsDir = Path.Combine(_projectDirectory, "scripts");
        var existingBible = await new JsonShowBibleStore(bibleDir).LoadLatestAsync(generationRunId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"No Show Bible is persisted for generation run '{generationRunId:N}' -- generate the whole-song plan first.");
        var existingScripts = await ChoreographyGenerationPipeline.LoadPersistedScriptsAsync(scriptsDir, generationRunId, ct).ConfigureAwait(false);

        var (craftRules, exemplarIndex) = await LoadCorpusKnowledgeAsync(_knowledgeDirectory, ct).ConfigureAwait(false);
        var knowledge = new DirectorKnowledgeContext(catalogs.Vocabulary, craftRules, exemplarIndex, MultiLayerTemplateCatalogLoader.Load());

        var pipeline = new ChoreographyGenerationPipeline(
            knowledge, _style, directorClient: null, directorModelName: null, choreographerClient, choreographerModel, _maxOutputTokens);

        var report = await pipeline.ApplyPostProcessingPromptAsync(
            music, layoutSummary, settings, generationRunId, existingBible, existingScripts, postProcessingPromptText, ct).ConfigureAwait(false);

        var document = WithMediaFile(report.CompiledResult.IR.ToSequenceDocument(new SequenceMetadata(Song: song.Id.Value.ToString())), song);

        return new WholeSongGenerationResult(
            document, song.Id, audio.Melody, structure.Phrases, structure.Motifs, BuildLookTemplateUsage(report.Bible), GenerationRunId: generationRunId);
    }

    /// <summary>
    /// Build-plan Phase 10 §3f: loads the real, persisted <see cref="CraftRules"/>/<see cref="ExemplarIndex"/>
    /// from <paramref name="knowledgeDirectory"/> via <see cref="JsonCraftRulesStore"/>/<see cref="JsonExemplarIndexStore"/>,
    /// falling back independently per-store to the same empty defaults this type has always hardcoded --
    /// each store's own <see cref="FileNotFoundException"/> (no knowledge directory configured, or
    /// corpus mining has simply never been run yet) is caught on its own, so e.g. a real CraftRules
    /// file with no matching ExemplarIndex file yet (or vice versa) still loads whichever one exists.
    /// <see langword="public static"/>, taking the directory explicitly (rather than reading the
    /// instance's own <c>_knowledgeDirectory</c>), so this fallback behavior is directly unit-testable
    /// without needing a fully-configured <see cref="AiChoreographyWholeSongGenerator"/> (which requires
    /// real configured Director/Choreographer AI roles before it reaches this call).
    /// </summary>
    /// <summary>
    /// Build-plan "richer analysis tools" Phase 6 (2026-11-02): transcribes the vocals stem if a
    /// transcriber was configured and this song's stem separation actually produced one -- never throws
    /// into the caller: a not-yet-installed model (<see cref="ModelAssetConsentRequiredException"/>) is
    /// treated as "no transcript available," the same disclosed, optional-enhancement framing as this
    /// constructor's own doc comment for <see cref="_transcriber"/>.
    /// </summary>
    private async Task<IReadOnlyList<TranscribedWord>?> TryTranscribeAsync(StemManifest stems, CancellationToken ct)
    {
        if (_transcriber is null || _transcriptAudioDecoder is null)
        {
            return null;
        }

        var vocalsStem = stems.Stems.FirstOrDefault(s => s.Kind == StemKind.Vocals);
        if (vocalsStem is null)
        {
            return null;
        }

        try
        {
            var vocalsSongAsset = await StemSongAssetFactory.ProbeAsync(vocalsStem, _transcriptAudioDecoder, ct).ConfigureAwait(false);
            return await _transcriber.TranscribeAsync(vocalsSongAsset, ct).ConfigureAwait(false);
        }
        catch (ModelAssetConsentRequiredException)
        {
            return null;
        }
    }

    public static async Task<(CraftRules CraftRules, ExemplarIndex ExemplarIndex)> LoadCorpusKnowledgeAsync(string? knowledgeDirectory, CancellationToken ct)
    {
        var emptyCraftRules = new CraftRules(DateTimeOffset.UtcNow, MinConfidenceThreshold: 0.7, MinSongCountThreshold: 3, Promoted: [], RejectedCandidates: []);
        var emptyExemplarIndex = new ExemplarIndex(DateTimeOffset.UtcNow, []);

        if (knowledgeDirectory is null)
        {
            return (emptyCraftRules, emptyExemplarIndex);
        }

        CraftRules craftRules;
        try
        {
            craftRules = await new JsonCraftRulesStore(knowledgeDirectory).LoadAsync(ct).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            craftRules = emptyCraftRules;
        }

        ExemplarIndex exemplarIndex;
        try
        {
            exemplarIndex = await new JsonExemplarIndexStore(knowledgeDirectory).LoadAsync(ct).ConfigureAwait(false);

            // Build-plan Phase 17 §2e: a user-disabled exemplar (or whole source song) must never reach
            // a generation's Director prompt, even though it stays in exemplar-index.json itself (that
            // file is the automatic mining result; exemplar-overrides.json is the separate, re-mine-
            // surviving curation layer -- see ExemplarOverrides/JsonExemplarOverrideStore).
            var disabledByKey = await new JsonExemplarOverrideStore(knowledgeDirectory).LoadAsync(ct).ConfigureAwait(false);
            exemplarIndex = ExemplarOverrides.ApplyOverrides(exemplarIndex, disabledByKey);
        }
        catch (FileNotFoundException)
        {
            exemplarIndex = emptyExemplarIndex;
        }

        return (craftRules, exemplarIndex);
    }

    /// <summary>
    /// Fixes the "mediaFile was empty, so no audio in the xLights timeline" bug: architecture §8b notes
    /// real shared/exported sequences carry an *absolute* host <c>mediaFile</c> path (never bundled,
    /// never shipped relative) -- so <see cref="SongAsset.OriginalFilePath"/>, the user's own real source
    /// file, is exactly the right value. Confirmed as a real follow-on bug against a real export:
    /// <see cref="SongAsset.FilePath"/> instead names <c>TranscodingAudioDecoder</c>'s own internal
    /// transcoded-WAV cache entry for any non-WAV source (every real song here, since real songs are
    /// rarely WAV) -- xLights has no reason to know that path exists. Extracted as its own static method
    /// so this fix is unit-testable without a real LLM call.
    /// </summary>
    public static SequenceDocument WithMediaFile(SequenceDocument document, SongAsset song)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(song);

        var metadata = document.MetadataOrDefault with { MediaFile = song.OriginalFilePath };
        return document with { Metadata = metadata };
    }

    /// <summary>
    /// Build-plan Phase 10 §6b task 4: flattens the generated <see cref="ShowBible"/>'s own Look library
    /// into the lightweight, cross-project-safe shape <see cref="WholeSongGenerationResult.LookTemplateUsage"/>
    /// carries -- extracted as its own static method, same "pure, independently testable" convention as
    /// <see cref="WithMediaFile"/> above, so this mapping is unit-testable without a real LLM call.
    /// </summary>
    public static IReadOnlyList<GeneratedLookTemplateUsage> BuildLookTemplateUsage(ShowBible bible)
    {
        ArgumentNullException.ThrowIfNull(bible);

        return bible.Content.Looks.Looks
            .Select(look => new GeneratedLookTemplateUsage(look.Id, look.Name, look.TemplateRef))
            .ToList();
    }
}
