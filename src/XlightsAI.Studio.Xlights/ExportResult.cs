namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Section 3 proposed contract: the outcome of a successful <see cref="IXsqExporter.ExportNewAsync"/>/
/// <see cref="IXsqExporter.ExportAsync"/> call. <see cref="Sha256"/> lets a caller/evidence record
/// identify exactly which on-disk bytes were produced without re-reading the file; <see cref="Warnings"/>
/// carries every disclosed, non-fatal limitation of the export (e.g. an effect recipe whose xLights
/// settings have not yet been verified against a real xLights-authored sample) — never a silent gap.
/// </summary>
/// <param name="Path">
/// The actual path written to. Equal to the caller-supplied <c>outputPath</c> for
/// <see cref="XsqExportPolicy.RejectIfExists"/>/<see cref="XsqExportPolicy.OverwriteWithBackup"/>; may
/// differ (PHASE-02 Step 015's own "filename safety") when <see cref="XsqExportPolicy.AppendCollisionSuffix"/>
/// picked a non-colliding alternate filename.
/// </param>
/// <param name="BackupPath">
/// PHASE-02 Step 015: the real on-disk path the pre-existing file at <c>outputPath</c> was copied to
/// before being overwritten, when <see cref="XsqExportPolicy.OverwriteWithBackup"/> was used;
/// <see langword="null"/> for every other policy (nothing was ever overwritten, so nothing was backed up).
/// </param>
public sealed record ExportResult(string Path, string Sha256, IReadOnlyList<string> Warnings, string? BackupPath = null);
