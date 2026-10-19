namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 023 ("Scope-aware export and regeneration contract"): thrown by
/// <see cref="IScopedXsqExporter"/> when a requested scoped export would change, add, or remove any
/// effect outside the supplied <see cref="EditScope"/> — this step's own required acceptance ("Hash
/// unaffected effects before/after scoped edit; no changes outside scope") made into an enforced gate,
/// not just a test assertion. Always thrown before <see cref="XlightsAI.Studio.Xlights.IXsqExporter"/>
/// is ever invoked, so no file is created, overwritten, or backed up when this is thrown — the same
/// "fail safely without rewriting source files" rule every prior export step already enforces.
/// </summary>
public sealed class ScopedExportContractViolatedException : Exception
{
    public ScopedExportContractViolatedException(string message)
        : base(message)
    {
    }
}
