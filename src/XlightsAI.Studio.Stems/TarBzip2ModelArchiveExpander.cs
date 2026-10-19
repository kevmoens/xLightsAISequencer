using SharpCompress.Readers;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06A Step 044D. Real, deterministic <see cref="IModelArchiveExpander"/> for a <c>.tar.bz2</c>
/// archive. .NET has no built-in bzip2 decompression (<c>System.IO.Compression</c> covers GZip/Deflate/
/// Brotli/ZLib only, not bzip2/tar), so this step takes a disclosed, deliberate dependency on
/// <c>SharpCompress</c> (MIT-licensed, actively maintained; see <c>implementation-evidence/PHASE-06A/STEP-044D.md</c>
/// §3 for the exact version/license evidence) rather than hand-rolling a bzip2 decompressor -- a
/// categorically larger undertaking than this repository's own "~200-line algorithm" precedent
/// (archive decompression is not DSP, unlike the STFT this repository already owns outright).
///
/// Streams the already hash-verified archive through <see cref="ReaderFactory.OpenReader(Stream, ReaderOptions?)"/>
/// (SharpCompress's own format-auto-detecting streaming reader, which recognizes the combined tar+bzip2
/// container and exposes it as a flat sequence of tar entries) and extracts every regular-file entry to
/// <paramref name="destinationDirectory"/> using the entry's own relative path (preserving the archive's
/// own <c>sherpa-onnx-spleeter-2stems/</c> folder prefix, which
/// <see cref="SpectrogramModelBundle.StemArchiveEntryNames"/> declares verbatim). Writes each entry to a
/// temp path first, then atomically moves it into place, so a cancelled or failed extraction never leaves
/// a partially-written file at its final path.
/// </summary>
public sealed class TarBzip2ModelArchiveExpander : IModelArchiveExpander
{
    public async Task<IReadOnlyList<ExtractedModelFile>> ExpandAsync(
        string verifiedArchivePath, string destinationDirectory, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verifiedArchivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(destinationDirectory);
        var extracted = new List<ExtractedModelFile>();

        await using var fileStream = File.OpenRead(verifiedArchivePath);
        using var reader = ReaderFactory.OpenReader(fileStream);

        while (reader.MoveToNextEntry())
        {
            ct.ThrowIfCancellationRequested();

            if (reader.Entry.IsDirectory)
            {
                continue;
            }

            var entryName = reader.Entry.Key!.Replace('\\', '/');
            var fullDestinationRoot = Path.GetFullPath(destinationDirectory);
            if (!fullDestinationRoot.EndsWith(Path.DirectorySeparatorChar))
            {
                fullDestinationRoot += Path.DirectorySeparatorChar;
            }

            var finalPath = Path.GetFullPath(Path.Combine(fullDestinationRoot, entryName.Replace('/', Path.DirectorySeparatorChar)));
            if (!finalPath.StartsWith(fullDestinationRoot, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Archive entry '{entryName}' would extract outside the destination directory (disclosed zip-slip/tar-slip guard, independent of SharpCompress's own GHSA-6c8g-7p36-r338 fix).");
            }

            var finalDirectory = Path.GetDirectoryName(finalPath)!;
            Directory.CreateDirectory(finalDirectory);

            var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";
            try
            {
                await using (var entryStream = reader.OpenEntryStream())
                await using (var outStream = new FileStream(
                    tempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
                {
                    await entryStream.CopyToAsync(outStream, ct).ConfigureAwait(false);
                }

                ct.ThrowIfCancellationRequested();
                File.Move(tempPath, finalPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }

            extracted.Add(new ExtractedModelFile(entryName, finalPath, new FileInfo(finalPath).Length));
        }

        return extracted;
    }
}
