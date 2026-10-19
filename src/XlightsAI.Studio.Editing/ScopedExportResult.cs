using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 023: the outcome of a successful <see cref="IScopedXsqExporter.ExportScopedAsync"/>
/// call. <see cref="Export"/> is the underlying <see cref="XlightsAI.Studio.Xlights.IXsqExporter"/>'s own
/// result for the file actually written. <see cref="OutOfScopeEffectHashes"/> is the verified-identical
/// content hash (keyed by <see cref="EffectInstance.Id"/>) for every effect this export confirmed was
/// <b>not</b> changed by the scoped edit — the literal evidence for this step's required acceptance
/// ("Hash unaffected effects before/after scoped edit; no changes outside scope"), not merely an
/// internal implementation detail.
/// </summary>
public sealed record ScopedExportResult(ExportResult Export, IReadOnlyDictionary<Guid, string> OutOfScopeEffectHashes);
