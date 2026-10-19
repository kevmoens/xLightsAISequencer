namespace XlightsAI.Studio.Release;

/// <summary>
/// One real, observed file inside a published release bundle: its path relative to the bundle root
/// (forward-slash-normalized so the manifest is identical across Windows/macOS/Linux builders) and its
/// actual SHA-256, computed fresh from the real bytes on disk every time — never trusted from a prior
/// run or a caller assertion (same convention as <see cref="XlightsAI.Studio.Distribution.DownloadableAsset"/>
/// and <see cref="XlightsAI.Studio.Distribution.StartupHealthChecks"/>).
/// </summary>
/// <param name="RelativePath">Forward-slash path relative to the bundle root.</param>
/// <param name="Sha256">Actual, freshly computed SHA-256 of the file's bytes, lower-case hex.</param>
/// <param name="SizeBytes">Actual file size in bytes.</param>
public sealed record ReleaseArtifactManifestEntry(string RelativePath, string Sha256, long SizeBytes);
