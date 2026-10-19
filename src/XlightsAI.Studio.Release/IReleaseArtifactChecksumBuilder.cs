namespace XlightsAI.Studio.Release;

/// <summary>
/// New, phase-local abstraction (disclosed; not in Section 3's proposed signature list — mirrors Step
/// 098's own <c>ICompatibilityFixtureCatalog</c> precedent of isolating a genuinely new concern Section 3
/// did not name). Builds a real, reproducible <see cref="ReleaseArtifactManifest"/> from an actual
/// on-disk published bundle directory.
/// </summary>
public interface IReleaseArtifactChecksumBuilder
{
    Task<ReleaseArtifactManifest> BuildAsync(string bundleDirectory, CancellationToken ct);
}
