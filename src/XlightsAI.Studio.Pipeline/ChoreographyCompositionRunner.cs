using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// SUPERSEDED, kept only as reference material — not wired to the real app (build-plan Phase 10 §3g,
/// decision recorded in <c>CHANGELOG.md</c> and <c>docs/MASTER-SYSTEM-ARCHITECTURE.md</c>). Only
/// <see cref="WholeSongGenerator"/> constructs this type, and <em>that</em> type is itself never assigned
/// to the real app's <c>App.WholeSongGenerator</c> (see its own doc comment) — so this runner, and the
/// whole <see cref="XlightsAI.Studio.Composition"/> musical-role/<see cref="ChoreographyCompiler"/> system
/// it drives, never executes in a real generation. Do not add new features here.
///
/// Historical summary (M3): compiles M3's real <see cref="MusicalRoleAssignment"/> list into a real
/// <see cref="SequenceDocument"/> using every real prop in the layout — real analysis (M1/M2) and real
/// heuristics (M3) feeding the real, unmodified <see cref="ChoreographyCompiler"/> (REUSE as-is — this
/// runner added no new choreography-composition logic of its own, only
/// <see cref="ChoreographyCandidateBuilder"/>'s bridging) — before the LLM-driven Director/Choreography
/// path replaced it.
/// </summary>
public sealed class ChoreographyCompositionRunner
{
    private readonly IRolePrecedenceResolver _precedenceResolver;
    private readonly IEffectLockPolicy _lockPolicy;
    private readonly IEffectRecipeRegistry _recipeRegistry;

    public ChoreographyCompositionRunner(
        IRolePrecedenceResolver? precedenceResolver = null,
        IEffectLockPolicy? lockPolicy = null,
        IEffectRecipeRegistry? recipeRegistry = null)
    {
        // Same "pure, stateless domain logic, nothing to configure" reasoning this codebase already
        // applies to ThreeEffectPlanFactory/XsqExporter/XlightsLauncher: all three real collaborators
        // here are stateless at construction, so they default to real instances rather than throwing
        // NullX stubs.
        _precedenceResolver = precedenceResolver ?? new RolePrecedenceResolver();
        _lockPolicy = lockPolicy ?? new EffectLockPolicy();
        _recipeRegistry = recipeRegistry ?? new EffectRecipeRegistry();
    }

    /// <exception cref="ArgumentException"><paramref name="roleAssignments"/> is empty, or references a phrase not present in <paramref name="phrases"/>.</exception>
    /// <exception cref="CompositionInvalidException">A candidate conflict could not be resolved within <see cref="ChoreographyCompiler.MaxLayers"/>, or another real compiler-side validation failure.</exception>
    public ChoreographyCompilationResult Compose(
        DisplayGraph layout,
        PhraseSegmentationAnalysis phrases,
        IReadOnlyList<MusicalRoleAssignment> roleAssignments,
        long songDurationTicks,
        SequenceDocument? baseDocument = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(phrases);
        ArgumentNullException.ThrowIfNull(roleAssignments);

        if (roleAssignments.Count == 0)
        {
            throw new ArgumentException("At least one role assignment is required to compose a choreography.", nameof(roleAssignments));
        }

        if (songDurationTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(songDurationTicks), songDurationTicks, "Song duration must be a positive number of ticks.");
        }

        var candidates = ChoreographyCandidateBuilder.BuildCandidates(roleAssignments, phrases);
        var allowedScope = new EditScope(
            new TimeWindow(0, songDurationTicks),
            layout.Models.Select(m => m.Id).ToHashSet());
        var document = baseDocument ?? new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: []);

        var compiler = new ChoreographyCompiler(_precedenceResolver, _lockPolicy, _recipeRegistry);
        return compiler.Compile(document, candidates, allowedScope);
    }
}
