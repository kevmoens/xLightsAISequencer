namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// Thrown by <see cref="IWholeSongExportAuditor.ExportAndAuditAsync"/> when
/// <see cref="WholeSongContinuityReport.Passed"/> is <see langword="false"/> -- at least one
/// <see cref="ContinuitySeverity.Error"/> finding exists. Thrown before <see cref="XlightsAI.Studio.Xlights.IXsqExporter"/>
/// is ever called; no file is written. This step's own required failure/edge case, verbatim: "Invalid
/// model/format/version must fail safely without rewriting source files" -- extended here to a second,
/// disclosed gate: an invalid *continuity* state must fail exactly as safely as an invalid model/format/
/// version does, never producing a partial or silently-accepted export.
/// </summary>
public sealed class SongContinuityBlockedException : Exception
{
    public SongContinuityBlockedException(IReadOnlyList<ContinuityFinding> blockingFindings)
        : base(BuildMessage(blockingFindings))
    {
        BlockingFindings = blockingFindings;
    }

    /// <summary>Every <see cref="ContinuitySeverity.Error"/> finding that caused this export to be blocked.</summary>
    public IReadOnlyList<ContinuityFinding> BlockingFindings { get; }

    private static string BuildMessage(IReadOnlyList<ContinuityFinding> blockingFindings) =>
        $"Export blocked: {blockingFindings.Count} continuity finding(s) with Error severity must be " +
        $"resolved first: {string.Join("; ", blockingFindings.Select(f => $"[{f.Code}] {f.Explanation}"))}";
}
