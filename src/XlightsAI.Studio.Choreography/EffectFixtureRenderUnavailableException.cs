namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 18 §3's own disclosed hard dependency: "This phase cannot run without a real, configured
/// xLights executable... no synthetic/approximate renderer substitutes for it." Mirrors
/// <c>XlightsAI.Studio.Director.QualityPassUnavailableException</c>'s own "surface the requirement, never
/// silently fall back" discipline -- thrown once, up front, before any per-fixture work is attempted, never
/// per-fixture (a per-fixture render failure is instead recorded as an <see cref="EffectFixtureRenderOutcome"/>).
/// </summary>
public sealed class EffectFixtureRenderUnavailableException : Exception
{
    public EffectFixtureRenderUnavailableException(string message)
        : base(message)
    {
    }
}
