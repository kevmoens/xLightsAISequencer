using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rendering;

/// <summary>
/// PHASE-13 Section 3 proposed contract, verbatim fields. One request to render an already-exported
/// <c>.xsq</c> (PHASE-02 Step 013/015) using <see cref="IRenderAdapter"/>.
/// </summary>
/// <param name="SequenceId">Identifies which <see cref="Xlights.SequenceDocument"/> this render evidences, for correlation only -- never re-derived from the exported file.</param>
/// <param name="ShowDirectory">The real, on-disk xLights show directory (<c>-s</c> argument) the sequence belongs to.</param>
/// <param name="ExportedXsqPath">The real, already-exported <c>.xsq</c> file to render; never generated or rewritten by the adapter itself.</param>
/// <param name="OutputDirectory">An isolated, app-owned folder for this render's own output -- never the show directory itself (Section 2: "Never overwrite source show files silently").</param>
/// <param name="Selection">
/// An optional sub-range of interest. No verified upstream xLights CLI flag exists for a scoped headless
/// render, so this is recorded as evidence/intent only and never translated into an invented command-line
/// argument (Section 2: "do not invent xLights effect parameters").
/// </param>
public sealed record RenderRequest(
    Guid SequenceId,
    string ShowDirectory,
    string ExportedXsqPath,
    string OutputDirectory,
    TimeWindow? Selection,
    RenderAuthority RequestedAuthority);
