using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Section 3 proposed contract, Step 012 ("Create effect plan containing three hardcoded
/// simple effects on selected real props"): one concrete, recipe-identified effect placement on a
/// single real <see cref="DisplayModel"/> or <see cref="DisplayModelGroup"/> target. <see cref="StartTicks"/>/
/// <see cref="EndTicks"/> form a half-open <c>[Start, End)</c> window (PHASE-02 Section 2's canonical
/// timing rule), expressed in the same 100 ns <see cref="TimelineTick"/> unit as the rest of the
/// contracts. <see cref="RecipeId"/>/<see cref="RecipeVersion"/> identify *which known-good recipe*
/// produced this instance; <see cref="Parameters"/> carries that recipe's own named inputs — neither
/// is a final xLights XML effect-settings string (Section 3: "Store recipe identity independent of
/// final xLights XML settings representation"), which remains Step 013's own concern.
///
/// PHASE-03 Step 019 additive, backward-compatible field: <see cref="Locked"/>. The Phase 03 table
/// (Section 2) names "effect locks" as a PHASE-03-owned concern, and every numbered step from 018
/// onward (Section 6) carries the identical required failure case "Out-of-scope, locked, or stale
/// revision must be rejected without partial mutation" — a real, checkable lock bit has to exist for
/// that failure path to be anything other than untestable prose. Full lock-toggle UI/persistence-
/// across-restart is Step 022's own scope ("Undo/redo transactions and lock semantics for approved
/// effects"); this step only adds the data bit <see cref="IEffectLockPolicy"/> reads. Added as the
/// last positional parameter with a default value so every existing Step 012-017 construction site
/// and every already-serialized <see cref="SequenceDocument"/> JSON file keeps compiling/deserializing
/// unchanged (a missing JSON property defaults to <see langword="false"/>).
///
/// PHASE-1b (docs/AI-CHOREOGRAPHY-BUILD-PLAN.md, "The effect recipe system"): <see cref="PaletteIndex"/>,
/// <see cref="BlendMode"/>, <see cref="Transitions"/> and <see cref="ValueCurves"/> are the IR fields
/// architecture §12 blocker 2 says a real recipe abstraction must carry beyond
/// <c>(recipeId, ModelId, startTicks, endTicks)</c> — a palette reference, blend mode, transitions and
/// value curves. Each is appended as a trailing optional parameter (the same convention
/// <see cref="Locked"/> already established) so every existing construction site keeps compiling.
/// </summary>
public sealed record EffectInstance(
    Guid Id,
    ModelId Target,
    int Layer,
    long StartTicks,
    long EndTicks,
    string RecipeId,
    string RecipeVersion,
    IReadOnlyDictionary<string, string> Parameters,
    bool Locked = false,
    int? PaletteIndex = null,
    string BlendMode = "Normal",
    IReadOnlyList<EffectTransition>? Transitions = null,
    IReadOnlyList<EffectValueCurve>? ValueCurves = null)
{
    public IReadOnlyList<EffectTransition> TransitionsOrEmpty => Transitions ?? [];

    public IReadOnlyList<EffectValueCurve> ValueCurvesOrEmpty => ValueCurves ?? [];
}
