namespace XlightsAI.Studio.Xlights;

/// <summary>One parsed <c>&lt;Element&gt;</c> entry — either a <c>&lt;DisplayElements&gt;</c> declaration (empty <see cref="Effects"/>) or an <c>&lt;ElementEffects&gt;</c> placement list — see <see cref="XsqStructuralReader"/>.</summary>
public sealed record XsqStructuralElement(string Type, string Name, IReadOnlyList<XsqStructuralEffectPlacement> Effects);
