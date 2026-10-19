namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Section 3 proposed contract. PHASE-02 Step 010: read-only parse of
/// <c>xlights_rgbeffects.xml</c> under a show directory into a normalized <see cref="DisplayGraph"/>.
/// Only ever reads the one file; never writes/moves/deletes anything under the show directory.
/// </summary>
public interface IShowLayoutImporter
{
    /// <summary>
    /// Imports the <c>xlights_rgbeffects.xml</c> models/groups/geometry found under
    /// <paramref name="showDirectory"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="showDirectory"/> is null/blank.</exception>
    /// <exception cref="DirectoryNotFoundException"><paramref name="showDirectory"/> does not exist.</exception>
    /// <exception cref="FileNotFoundException">The required rgbeffects file is not present.</exception>
    /// <exception cref="ShowLayoutMalformedException">The file is not a safely parseable xLights rgbeffects document.</exception>
    Task<DisplayGraph> ImportReadOnlyAsync(string showDirectory, CancellationToken ct);
}
