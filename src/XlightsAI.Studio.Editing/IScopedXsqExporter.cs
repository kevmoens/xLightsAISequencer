using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 023 ("Scope-aware export and regeneration contract"): wraps
/// <see cref="XlightsAI.Studio.Xlights.IXsqExporter"/> with a verification gate proving a scoped edit's
/// own export "export[s] scoped edits without regenerating other effect recipes" (Section 6, Step 023
/// implementation guidance) — every effect outside <paramref name="scope"/> (every call site's own
/// <c>scope</c> parameter) must hash identically between <paramref name="before"/> and
/// <paramref name="after"/> before any file is written, and the exported XSQ is read back and inspected
/// to confirm those same unaffected effects' actual timing materialized unchanged in the real XML.
/// </summary>
public interface IScopedXsqExporter
{
    /// <exception cref="ScopedExportContractViolatedException">
    /// Any effect outside <paramref name="scope"/> was added, removed, or changed between
    /// <paramref name="before"/> and <paramref name="after"/>, or the exported file's own structural
    /// content does not reflect an unaffected effect's expected timing. No file is ever written,
    /// overwritten, or backed up when this is thrown.
    /// </exception>
    /// <exception cref="XsqExportInvalidException">
    /// Same conditions as <see cref="IXsqExporter.ExportAsync"/> (blank/invalid output path, blank
    /// target xLights version, an effect whose target/recipe this exporter cannot translate). No file
    /// is ever written when this is thrown — PHASE-02 Step 013's required failure path, unchanged.
    /// </exception>
    Task<ScopedExportResult> ExportScopedAsync(
        DisplayGraph layout,
        SequenceDocument before,
        SequenceDocument after,
        EditScope scope,
        string outputPath,
        string targetXlightsVersion,
        XsqExportPolicy policy,
        CancellationToken ct);
}
