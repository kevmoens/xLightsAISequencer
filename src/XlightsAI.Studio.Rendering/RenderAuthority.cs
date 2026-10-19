namespace XlightsAI.Studio.Rendering;

/// <summary>
/// PHASE-13 Section 3 proposed contract, verbatim. Distinguishes Step 085's fast, inherently approximate
/// preview tier from Step 086's authoritative, real-installed-executable render tier -- never a claim that
/// either produces output identical to the other (Section 2: "no assumption every installed release
/// supports --headless").
/// </summary>
public enum RenderAuthority
{
    ApproximateStudioPreview,
    ActualXlights,
}
