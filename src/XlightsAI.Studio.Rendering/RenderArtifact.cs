namespace XlightsAI.Studio.Rendering;

/// <summary>
/// PHASE-13 Section 3 proposed contract, verbatim fields. The outcome of one <see cref="IRenderAdapter.RenderAsync"/>
/// attempt. <see cref="Success"/> is this phase's own hard evaluation gate (Section 2: "Treat render
/// failures as blocking gate"); <see langword="false"/> covers both an unsupported installed build that was
/// never launched at all (graceful fallback to manual verification) and a launched-but-failed/unverifiable
/// render -- distinguished only by <see cref="Warnings"/>' own text. A future caller (Step 089) must never
/// treat either outcome as a passing gate.
/// </summary>
/// <param name="RendererVersion">The probed executable's own reported version, or <c>"unknown"</c>.</param>
/// <param name="FseqPath">The real, on-disk rendered <c>.fseq</c> path when <see cref="Success"/> is <see langword="true"/>, else <see langword="null"/>.</param>
/// <param name="EvidenceHash">
/// SHA-256 of the exact exported <c>.xsq</c> bytes actually submitted, plus the observed process exit
/// outcome -- ties this artifact to one specific real file and one specific real process result, never a
/// fabricated or assumed value.
/// </param>
public sealed record RenderArtifact(
    Guid Id,
    RenderAuthority Authority,
    bool Success,
    string RendererVersion,
    string? FseqPath,
    IReadOnlyList<string> Warnings,
    string EvidenceHash);
