namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14 Step 093: how strongly <see cref="ISongContinuityEvaluator"/> treats a given
/// <see cref="ContinuityFinding"/>. Deliberately string constants, not an enum -- the same convention
/// <c>XlightsAI.Studio.Display.RouteIssueSeverity</c> already established, and matches PHASE-14.md
/// Section 3's own proposed <c>ContinuityFinding(... string Severity ...)</c> shape exactly (unlike
/// <c>SeamStrategy</c>, this one is kept as the spec's own literal <c>string</c> type rather than
/// promoted to an enum, since <see cref="ContinuityFinding"/> itself is not redefined here).
/// </summary>
public static class ContinuitySeverity
{
    /// <summary>
    /// Blocks export -- <see cref="WholeSongExportAuditor"/> never writes a file while any
    /// <see cref="ContinuityFinding"/> carries this severity (this step's own required acceptance,
    /// verbatim: "Export opens in xLights and passes continuity report").
    /// </summary>
    public const string Error = "Error";

    /// <summary>Surfaced for user review but does not block export -- e.g. a seam overlap that was already safely auto-resolved, or a motif occurrence a human may want to re-approve.</summary>
    public const string Warning = "Warning";

    /// <summary>Every severity this evaluator currently recognizes.</summary>
    public static readonly IReadOnlyList<string> All = [Error, Warning];
}
