namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-1c (docs/AI-CHOREOGRAPHY-BUILD-PLAN.md task 10, "Re-run the Phase 0 round-trip test... with
/// a hand-built multi-layer, multi-timing-track IR"): one re-parsed <c>&lt;Element type="timing"&gt;</c>
/// from <see cref="XsqStructuralReader"/>, kept separate from <see cref="XsqStructuralDocument.ElementEffects"/>
/// (whose <see cref="XsqStructuralEffectPlacement"/> shape assumes a ref/palette every timing mark does
/// not carry) rather than overloading that existing, already-asserted-against shape.
/// </summary>
public sealed record XsqStructuralTimingTrack(string Name, IReadOnlyList<XsqStructuralTimingMark> Marks);
