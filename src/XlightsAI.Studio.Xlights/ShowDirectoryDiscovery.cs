namespace XlightsAI.Studio.Xlights;

/// <summary>
/// Read-only, PHASE-02 Section 3 "Inputs" discovery result for a candidate show directory: which
/// of the two top-level xLights show files are present, and (if any) which required files are
/// missing or warnings about optional/escaped files. This step (009) never inspects file
/// *content* — parsing <c>xlights_rgbeffects.xml</c> into models/groups is Step 010's scope.
/// </summary>
public sealed record ShowDirectoryDiscovery(
    string ShowDirectory,
    bool HasRgbEffectsFile,
    bool HasNetworksFile,
    IReadOnlyList<string> MissingRequiredFiles,
    IReadOnlyList<string> Warnings)
{
    /// <summary>True when every required file was found (and not a boundary-escaping link).</summary>
    public bool IsComplete => MissingRequiredFiles.Count == 0;
}
