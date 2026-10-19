namespace XlightsAI.Studio.Xlights;

/// <summary>
/// Thrown by <see cref="IThreeEffectPlanFactory"/>/<see cref="IEffectRecipeRegistry"/> when a
/// requested effect plan is malformed: not exactly three distinct real targets, a target that does
/// not resolve against the imported <see cref="DisplayGraph"/>, a non-positive song duration, an
/// unknown recipe id, or an invalid/empty time window. Always thrown before any
/// <see cref="SequenceDocument"/> is constructed or persisted — PHASE-02 Step 012's required failure
/// path: "Malformed proposal... must not auto-commit" (no partial/invalid document is ever produced
/// or saved).
/// </summary>
public sealed class EffectPlanInvalidException : Exception
{
    public EffectPlanInvalidException(string message)
        : base(message)
    {
    }
}
