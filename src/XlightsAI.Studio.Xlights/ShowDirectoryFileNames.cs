namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Section 2's named top-level xLights show files. Only the two filenames relevant to
/// Step 009's read-only discovery are declared here; the editable <c>.xsq</c> sequence and
/// rendered <c>.fseq</c> are not show-directory inputs and are out of this step's scope.
/// </summary>
public static class ShowDirectoryFileNames
{
    /// <summary>Required: models/groups/layout and other show configuration.</summary>
    public const string RgbEffects = "xlights_rgbeffects.xml";

    /// <summary>Optional: controller/output configuration.</summary>
    public const string Networks = "xlights_networks.xml";
}
