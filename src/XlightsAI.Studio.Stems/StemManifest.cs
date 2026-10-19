namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Section 3's own proposed signature. The complete, inspectable output of one separation
/// run: every produced <see cref="StemAsset"/> plus provenance (which song, which model) and any
/// non-fatal disclosed limitation (<see cref="Warnings"/>) -- e.g. Step 040's own mono-only pipeline
/// decision (see <c>implementation-evidence/PHASE-06/STEP-040.md</c> §3).
/// </summary>
/// <param name="RunId">Stable identity for this one separation run.</param>
/// <param name="OriginalSongHash">The source song's <c>SongAsset.Sha256</c>, so a manifest can always be traced back to the exact input it was produced from.</param>
/// <param name="ModelId">The <see cref="ModelAsset.Id"/> that produced this run's stems.</param>
/// <param name="Stems">Every produced stem, in <see cref="StemSeparationRecipe.StemOrder"/> order.</param>
/// <param name="Warnings">Non-fatal, disclosed limitations of this run (e.g. "mono-only pipeline"). Never used to hide a real failure -- a real failure throws instead of being downgraded to a warning.</param>
public sealed record StemManifest(
    Guid RunId,
    string OriginalSongHash,
    string ModelId,
    IReadOnlyList<StemAsset> Stems,
    IReadOnlyList<string> Warnings);
