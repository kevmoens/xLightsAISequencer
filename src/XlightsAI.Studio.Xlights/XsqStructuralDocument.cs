namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Step 013 ("Write a structural roundtrip test with a reference reader"): the independently
/// re-parsed shape of one <see cref="XsqExporter"/>-produced <c>.xsq</c> file, as read back by
/// <see cref="XsqStructuralReader"/>. A plain, inspectable value — not the xLights-internal
/// <c>XsqDocument</c> model itself (this codebase has no C# port of the upstream Python generator's
/// own in-memory types) — used only to prove the file this exporter wrote is well-formed and
/// internally consistent, never to claim it renders in real xLights (Step 014's own scope).
/// </summary>
public sealed record XsqStructuralDocument(
    string XlightsVersion,
    int FixedPointTimingMs,
    double SequenceDurationSeconds,
    IReadOnlyList<string> ColorPalettes,
    IReadOnlyList<string> EffectDb,
    IReadOnlyList<XsqStructuralElement> DisplayElements,
    IReadOnlyList<XsqStructuralElement> ElementEffects);
