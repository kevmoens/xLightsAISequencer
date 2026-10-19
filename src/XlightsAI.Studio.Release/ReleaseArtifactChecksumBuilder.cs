using System.Security.Cryptography;
using System.Text;

namespace XlightsAI.Studio.Release;

/// <summary>
/// Real <see cref="IReleaseArtifactChecksumBuilder"/>. Walks every real file under
/// <c>bundleDirectory</c>, hashes its actual bytes, and sorts entries by relative path (ordinal) before
/// computing <see cref="ReleaseArtifactManifest.ManifestSha256"/> — sorting first is what makes
/// <see cref="ReleaseArtifactManifest.ManifestSha256"/> independent of the real filesystem's own
/// (unordered-by-contract) directory enumeration order, satisfying this step's own required acceptance,
/// verbatim: "Reproducible artifacts..." Two separate builds of a byte-identical directory tree always
/// produce the identical manifest hash; a single changed byte in any one file always changes it.
/// </summary>
public sealed class ReleaseArtifactChecksumBuilder : IReleaseArtifactChecksumBuilder
{
    public async Task<ReleaseArtifactManifest> BuildAsync(string bundleDirectory, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
        ct.ThrowIfCancellationRequested();

        if (!Directory.Exists(bundleDirectory))
        {
            throw new DirectoryNotFoundException($"Release bundle directory '{bundleDirectory}' does not exist.");
        }

        var root = Path.GetFullPath(bundleDirectory);
        var entries = new List<ReleaseArtifactManifestEntry>();

        foreach (var filePath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(root, filePath).Replace(Path.DirectorySeparatorChar, '/');
            await using var stream = File.OpenRead(filePath);
            var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
            entries.Add(new ReleaseArtifactManifestEntry(relativePath, Convert.ToHexString(hash).ToLowerInvariant(), stream.Length));
        }

        entries.Sort((a, b) => string.CompareOrdinal(a.RelativePath, b.RelativePath));

        var manifestPayload = new StringBuilder();
        foreach (var entry in entries)
        {
            manifestPayload.Append(entry.RelativePath).Append('\0').Append(entry.Sha256).Append('\n');
        }

        var manifestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifestPayload.ToString()))).ToLowerInvariant();

        return new ReleaseArtifactManifest(entries, manifestHash);
    }
}
