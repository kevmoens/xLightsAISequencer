using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Composition;

/// <summary>
/// SUPERSEDED, kept only as reference material — the whole <see cref="XlightsAI.Studio.Composition"/>
/// namespace (musical-role assignment, <see cref="VisualBudget"/> density budgeting, <see cref="TransitionPlan"/>
/// gesture recipes, this compiler) backs only <c>XlightsAI.Studio.Pipeline.ChoreographyCompositionRunner</c>/
/// <c>WholeSongGenerator</c>, neither of which the real app ever wires to <c>App.WholeSongGenerator</c> — see
/// those types' own doc comments. Build-plan Phase 10 §3g recorded this decision (see <c>CHANGELOG.md</c> and
/// <c>docs/MASTER-SYSTEM-ARCHITECTURE.md</c>): archive, don't extend. Do not add new features here.
///
/// Historical summary — PHASE-10 Step 071 ("Compose and resolve overlapping effects with layer/priority strategies"):
/// combines overlapping role plans into a single scoped <see cref="SequenceDocument"/>, resolving
/// same-target time collisions by layer/priority rather than letting two candidates silently overwrite
/// the same layer. A deliberately narrower realization of PHASE-10.md Section 3's own proposed
/// <c>IChoreographyCompiler.Compile(ChoreographyPlan, SequenceDocument, EditScope)</c> signature --
/// <see cref="ChoreographyCandidate"/> stands in for the spec's own proposed <c>ChoreographyPlan</c>/
/// <c>PlannedGesture</c> pair (disclosed as an architecture decision in
/// <c>implementation-evidence/PHASE-10/STEP-071.md</c>, the same reasoning Steps 069/070's own ADRs
/// already used for narrowing that same proposed shape).
/// </summary>
public interface IChoreographyCompiler
{
    /// <summary>
    /// Resolves every <paramref name="candidates"/> entry's own layer/priority admission outcome against
    /// <paramref name="baseDocument"/>, and compiles every admitted candidate into a concrete, verified
    /// <see cref="EffectInstance"/> appended to a new document revision. Candidates are considered in
    /// <see cref="IRolePrecedenceResolver"/> order (highest precedence first) -- a lower-precedence
    /// candidate is the one reassigned to a new layer or dropped when two or more compete for the same
    /// target+window+layer, never the other way around. <paramref name="baseDocument"/>'s own existing
    /// effects (locked or not) are always carried forward byte-for-byte unchanged; a candidate can never
    /// silently overwrite or re-layer an existing effect.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="baseDocument"/>, <paramref name="candidates"/>, or <paramref name="allowedScope"/> is <see langword="null"/>, or any element of <paramref name="candidates"/> is <see langword="null"/>.</exception>
    /// <exception cref="CompositionInvalidException">
    /// <paramref name="candidates"/> is empty (a clean machine with no planned gestures yet has none, and
    /// must be reported clearly rather than compiled as a silent no-op); two candidates share the same
    /// <see cref="ChoreographyCandidate.Id"/>; any candidate fails its own <see cref="ChoreographyCandidate.Validate"/>;
    /// or any candidate's own <see cref="ChoreographyCandidate.Target"/>/<see cref="ChoreographyCandidate.Window"/>
    /// falls outside <paramref name="allowedScope"/>'s own permitted/verified targetable model set or time
    /// window.
    /// </exception>
    /// <exception cref="EditCommandRejectedException">
    /// <paramref name="allowedScope"/> reaches an already-locked effect in <paramref name="baseDocument"/>
    /// (<see cref="IEffectLockPolicy.AssertEditable"/>) -- the entire compile is rejected before any
    /// candidate is resolved, the same "no partial mutation" rule every other Editing-project command
    /// already enforces.
    /// </exception>
    /// <exception cref="XlightsAI.Studio.Xlights.EffectPlanInvalidException">
    /// Any admitted candidate's own <see cref="ChoreographyCandidate.RecipeId"/> is not a recipe this
    /// build's <see cref="IEffectRecipeRegistry"/> actually knows -- "unsupported runtime" must produce a
    /// user-readable diagnostic, never an invented effect shape.
    /// </exception>
    ChoreographyCompilationResult Compile(
        SequenceDocument baseDocument,
        IReadOnlyList<ChoreographyCandidate> candidates,
        EditScope allowedScope);
}
