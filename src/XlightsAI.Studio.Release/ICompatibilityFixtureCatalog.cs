namespace XlightsAI.Studio.Release;

/// <summary>
/// Phase-local abstraction (not in the Section 3 proposed signature list; disclosed, mirrors
/// PHASE-15 Step 097's own <c>IApphostLauncher</c> precedent of isolating one genuinely
/// environment-specific lookup from the otherwise pure matrix-running logic). Resolves a
/// <see cref="CompatibilityCase"/>'s own fixture ids to real, on-disk content —
/// <see cref="ICompatibilityMatrixRunner"/> never guesses or invents a fixture path itself.
/// </summary>
public interface ICompatibilityFixtureCatalog
{
    /// <summary>
    /// Resolves <paramref name="layoutFixtureId"/> to a real show directory containing a real,
    /// safely-parseable <c>xlights_rgbeffects.xml</c>.
    /// </summary>
    /// <exception cref="CompatibilityFixtureNotFoundException"><paramref name="layoutFixtureId"/> is not a known fixture id.</exception>
    string ResolveLayoutShowDirectory(string layoutFixtureId);

    /// <summary>Resolves <paramref name="audioFixtureId"/> to a real, on-disk audio file path.</summary>
    /// <exception cref="CompatibilityFixtureNotFoundException"><paramref name="audioFixtureId"/> is not a known fixture id.</exception>
    string ResolveAudioFilePath(string audioFixtureId);
}
