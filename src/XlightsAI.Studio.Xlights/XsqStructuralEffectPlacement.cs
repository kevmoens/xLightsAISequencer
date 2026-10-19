namespace XlightsAI.Studio.Xlights;

/// <summary>One parsed <c>&lt;Effect&gt;</c> entry under <c>&lt;ElementEffects&gt;</c> — see <see cref="XsqStructuralReader"/>.</summary>
public sealed record XsqStructuralEffectPlacement(int Ref, string Name, long StartTimeMs, long EndTimeMs, int Palette);
