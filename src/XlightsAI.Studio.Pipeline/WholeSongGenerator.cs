using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.WholeSong;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// SUPERSEDED, kept only as reference material — not wired to the real app (build-plan Phase 10 §3g,
/// decision recorded in <c>CHANGELOG.md</c> and <c>docs/MASTER-SYSTEM-ARCHITECTURE.md</c>). Program.cs
/// constructs this type but assigns <c>AiChoreographyWholeSongGenerator</c> (the real LLM-driven
/// Phases 5-8 pipeline) to <c>App.WholeSongGenerator</c> instead, so nothing here ever executes in a real
/// generation. Do not add new features here; port specific ideas into the live Director/Choreography path
/// instead (one such candidate — <see cref="XlightsAI.Studio.Composition.VisualBudget"/>-style deterministic
/// density/concurrency capping as a future <c>ChoreographyLegalityValidator</c> rule — is noted but not
/// implemented, per that decision).
///
/// Historical summary (M1-M4): the single facade wrapping the whole old pipeline: a probed
/// <see cref="SongAsset"/> and an already-imported <see cref="DisplayGraph"/> in, a real, exportable
/// <see cref="SequenceDocument"/> using every real prop out — sequencing the four runners/heuristics this
/// project built and tested independently, before the LLM-driven path replaced it.
///
/// Never grants the stem-separation model's own download consent (see <see cref="AudioAnalysisRunner"/>'s
/// own doc comment): a not-yet-installed model surfaces as
/// <see cref="Stems.ModelAssetConsentRequiredException"/>, unchanged, for the Shell layer to handle with an
/// explicit "Download separation model" action.
/// </summary>
public sealed class WholeSongGenerator : IWholeSongGenerator
{
    private readonly AudioAnalysisRunner _audioAnalysis;
    private readonly StructureAnalysisRunner _structureAnalysis;
    private readonly ChoreographyCompositionRunner _composition;
    private readonly SongDirectionPlanner _songDirectionPlanner = new();

    public WholeSongGenerator(
        AudioAnalysisRunner audioAnalysis,
        StructureAnalysisRunner structureAnalysis,
        ChoreographyCompositionRunner? composition = null)
    {
        _audioAnalysis = audioAnalysis ?? throw new ArgumentNullException(nameof(audioAnalysis));
        _structureAnalysis = structureAnalysis ?? throw new ArgumentNullException(nameof(structureAnalysis));
        _composition = composition ?? new ChoreographyCompositionRunner();
    }

    /// <param name="customPrompt">Build-plan Phase 14: unused by this superseded generator -- see <see cref="IWholeSongGenerator.GenerateAsync"/>.</param>
    public async Task<WholeSongGenerationResult> GenerateAsync(SongAsset song, DisplayGraph layout, CancellationToken ct = default, CustomGenerationPrompt? customPrompt = null)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentNullException.ThrowIfNull(layout);

        var audio = await _audioAnalysis.AnalyzeAsync(song, ct).ConfigureAwait(false);
        var structure = await _structureAnalysis
            .AnalyzeAsync(song.Id, song.Sha256, audio.BeatGrid, audio.Energy, audio.Melody, ct)
            .ConfigureAwait(false);

        var sectionInputs = DeterministicSectionDirectionPlanner.BuildSectionInputs(structure);

        // Real, free validation: SongDirectionPlan.Validate() (invoked internally by Plan) catches a
        // malformed heuristic output early with an already-tested, clear exception, even though nothing
        // downstream in M4 consumes the resulting SongDirectionPlan itself yet (confirmed during planning
        // that no production code bridges SongDirectionPlan to Composition) -- cheap insurance, not dead
        // code: a genuinely broken DeterministicSectionDirectionPlanner output fails loudly right here
        // instead of producing a subtly-wrong SequenceDocument three steps later.
        _songDirectionPlanner.Plan(structure.Labels, structure.Motifs, sectionInputs, baseRevision: 0, DateTimeOffset.UtcNow);

        var roleAssignments = DeterministicPropRoleAssigner.AssignRoles(layout, structure.Phrases, sectionInputs);
        var composed = _composition.Compose(layout, structure.Phrases, roleAssignments, song.DurationTicks);

        return new WholeSongGenerationResult(composed.Document, song.Id, audio.Melody, structure.Phrases, structure.Motifs);
    }
}
