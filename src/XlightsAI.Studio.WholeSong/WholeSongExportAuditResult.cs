using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14 Step 093's own full outcome: the continuity report that gated the export, the real
/// <see cref="ExportResult"/> the export actually produced, and the real <see cref="XlightsLaunchResult"/>
/// if an installed xLights executable was supplied for audition.
/// </summary>
/// <param name="ContinuityReport">Always <see cref="WholeSongContinuityReport.Passed"/> -- a failed report never reaches this result (see <see cref="SongContinuityBlockedException"/>).</param>
/// <param name="Export">The real, written <c>.xsq</c> file's own result.</param>
/// <param name="Audition"><see langword="null"/> when no xLights executable path was supplied for audition (PHASE-02 Section 2: "An XML parse pass does not prove xLights renders an effect" -- disclosed as NOT VERIFIED, never faked, when this is <see langword="null"/>).</param>
public sealed record WholeSongExportAuditResult(
    WholeSongContinuityReport ContinuityReport,
    ExportResult Export,
    XlightsLaunchResult? Audition);
