namespace XlightsAI.Studio.Stems;

/// <summary>
/// Real (no third-party dependency) <see cref="IStemSeparationChunkCache"/> backed by one small binary
/// file per chunk under <c><paramref name="baseDirectory"/>/&lt;cacheKey&gt;/chunk-&lt;index&gt;.bin</c>.
/// File layout: <c>int32 Magic, int32 StemCount, int32 SampleCount</c>, followed by
/// <c>StemCount * SampleCount</c> little-endian float32 samples (row-major by stem). A file whose
/// length does not exactly match its own declared header, or whose magic does not match, is treated as
/// corrupt: deleted and reported as a miss rather than ever being deserialized into bad data -- this is
/// the concrete mechanism behind this step's own disclosed "stale persisted state must not corrupt the
/// project" guarantee.
/// </summary>
public sealed class FileSystemStemSeparationChunkCache : IStemSeparationChunkCache
{
    private const int Magic = 0x53434348; // "SCCH"
    private const int HeaderBytes = 3 * sizeof(int);

    private readonly string _baseDirectory;

    public FileSystemStemSeparationChunkCache(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        _baseDirectory = baseDirectory;
    }

    public bool TryGet(string cacheKey, int chunkIndex, int stemCount, int sampleCount, out float[,]? stemSamples, out bool discardedCorruptEntry)
    {
        stemSamples = null;
        discardedCorruptEntry = false;

        var path = ChunkPath(cacheKey, chunkIndex);
        if (!File.Exists(path))
        {
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            // A file that exists but cannot be read (e.g. truncated mid-write by a prior crash) is
            // exactly the "stale persisted state" this step's failure/edge case names -- treat as a
            // discarded-corrupt miss rather than propagating the I/O failure.
            discardedCorruptEntry = true;
            TryDelete(path);
            return false;
        }

        var expectedBytes = HeaderBytes + (long)stemCount * sampleCount * sizeof(float);
        if (bytes.Length < HeaderBytes || bytes.Length != expectedBytes)
        {
            discardedCorruptEntry = true;
            TryDelete(path);
            return false;
        }

        var magic = BitConverter.ToInt32(bytes, 0);
        var storedStemCount = BitConverter.ToInt32(bytes, 4);
        var storedSampleCount = BitConverter.ToInt32(bytes, 8);
        if (magic != Magic || storedStemCount != stemCount || storedSampleCount != sampleCount)
        {
            discardedCorruptEntry = true;
            TryDelete(path);
            return false;
        }

        var result = new float[stemCount, sampleCount];
        var offset = HeaderBytes;
        for (var s = 0; s < stemCount; s++)
        {
            for (var i = 0; i < sampleCount; i++)
            {
                result[s, i] = BitConverter.ToSingle(bytes, offset);
                offset += sizeof(float);
            }
        }

        stemSamples = result;
        return true;
    }

    public void Put(string cacheKey, int chunkIndex, float[,] stemSamples)
    {
        ArgumentNullException.ThrowIfNull(stemSamples);

        var stemCount = stemSamples.GetLength(0);
        var sampleCount = stemSamples.GetLength(1);
        var dir = Path.Combine(_baseDirectory, SanitizeKey(cacheKey));
        Directory.CreateDirectory(dir);

        var bytes = new byte[HeaderBytes + (long)stemCount * sampleCount * sizeof(float)];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 4), Magic);
        BitConverter.TryWriteBytes(bytes.AsSpan(4, 4), stemCount);
        BitConverter.TryWriteBytes(bytes.AsSpan(8, 4), sampleCount);

        var offset = HeaderBytes;
        for (var s = 0; s < stemCount; s++)
        {
            for (var i = 0; i < sampleCount; i++)
            {
                BitConverter.TryWriteBytes(bytes.AsSpan(offset, 4), stemSamples[s, i]);
                offset += sizeof(float);
            }
        }

        // Write-to-temp-then-move so a crash/cancellation mid-write can never leave a half-written
        // chunk cache file sitting at the real path for a later TryGet to misread as complete.
        var finalPath = ChunkPath(cacheKey, chunkIndex);
        var tempPath = finalPath + ".tmp";
        File.WriteAllBytes(tempPath, bytes);
        File.Move(tempPath, finalPath, overwrite: true);
    }

    private string ChunkPath(string cacheKey, int chunkIndex) =>
        Path.Combine(_baseDirectory, SanitizeKey(cacheKey), $"chunk-{chunkIndex:D8}.bin");

    private static string SanitizeKey(string cacheKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        Span<char> buffer = stackalloc char[cacheKey.Length];
        for (var i = 0; i < cacheKey.Length; i++)
        {
            var c = cacheKey[i];
            buffer[i] = char.IsLetterOrDigit(c) ? c : '_';
        }

        return new string(buffer);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Best-effort: a future Put() for the same chunk will overwrite it anyway via temp+move.
        }
    }
}
