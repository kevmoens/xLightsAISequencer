namespace XlightsAI.Studio.Reference;

/// <summary>Outcome of an upstream reference audit: the parsed manifest (if any) plus validation errors.</summary>
public sealed record UpstreamReferenceAuditReport(
    UpstreamReferenceManifest? Manifest,
    UpstreamReferenceValidationResult Validation)
{
    public bool IsPinned => Validation.IsValid;

    /// <summary>Human-readable summary: the "public output" a Settings/Diagnostics screen can display verbatim.</summary>
    public string ToDisplayText()
    {
        if (!Validation.IsValid || Manifest is null)
        {
            return "Upstream reference manifest is INVALID:" + Environment.NewLine
                + string.Join(Environment.NewLine, Validation.Errors.Select(e => $"  - {e}"));
        }

        return $"""
            Upstream reference: {Manifest.Name}
              URL:      {Manifest.RepositoryUrl}
              Commit:   {Manifest.CommitSha}
              License:  {Manifest.License.SpdxId} (c) {Manifest.License.CopyrightYear} {Manifest.License.CopyrightHolder}
              Pinned:   {Manifest.PinnedUtc:u}
              Components audited: {Manifest.Components.Count}
            """;
    }
}

/// <summary>
/// Application-layer orchestration for Step 001: reads the pinned upstream reference manifest
/// and validates it against this Studio's contract rules. This is the narrow command a future
/// Settings/Diagnostics screen (Phase 01 Step 005) calls; it owns no UI and no persistence writes.
/// </summary>
public sealed class UpstreamReferenceService(IUpstreamReferenceManifestReader reader)
{
    public async Task<UpstreamReferenceAuditReport> AuditAsync(string manifestPath, CancellationToken ct)
    {
        UpstreamReferenceManifest manifest;
        try
        {
            manifest = await reader.ReadAsync(manifestPath, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or UpstreamReferenceManifestFormatException)
        {
            return new UpstreamReferenceAuditReport(
                Manifest: null,
                Validation: UpstreamReferenceValidationResult.Failure([ex.Message]));
        }

        var validation = UpstreamReferenceValidator.Validate(manifest);
        return new UpstreamReferenceAuditReport(manifest, validation);
    }
}
