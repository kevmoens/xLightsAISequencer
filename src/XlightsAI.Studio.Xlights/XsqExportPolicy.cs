namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Step 015 ("Implement atomic export, filename safety, backups..."): how
/// <see cref="IXsqExporter.ExportAsync"/> should resolve an export request whose <c>outputPath</c>
/// already exists. <see cref="RejectIfExists"/> is the default and is Step 013's own original,
/// unmodified behavior ("Never overwrite source without explicit approval") — the other two members
/// are additive capabilities this step introduces, never a silent default change.
/// </summary>
public enum XsqExportPolicy
{
    /// <summary>Step 013's own original behavior: fail safely with <see cref="XsqExportInvalidException"/>, write nothing. The default for every existing call site.</summary>
    RejectIfExists = 0,

    /// <summary>"Filename safety": never overwrites anything; instead picks the next available non-colliding filename (<c>name (1).xsq</c>, <c>name (2).xsq</c>, ...) and writes there, reporting the actual path used via <see cref="ExportResult.Path"/>.</summary>
    AppendCollisionSuffix = 1,

    /// <summary>
    /// The user has explicitly approved overwriting the existing file at <c>outputPath</c>. The
    /// pre-existing file is first copied, byte-for-byte, to a timestamped backup path (reported via
    /// <see cref="ExportResult.BackupPath"/>) before the new file replaces it — this policy value
    /// itself IS the "expressly requests overwrite" approval named in this step's own implementation
    /// guidance; callers must never select it without a real, explicit user action.
    /// </summary>
    OverwriteWithBackup = 2,
}
