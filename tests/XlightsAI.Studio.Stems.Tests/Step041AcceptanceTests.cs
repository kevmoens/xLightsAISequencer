using System.Security.Cryptography;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Stems.Tests;

/// <summary>
/// PHASE-06 Step 041 — "Add chunking/overlap, caching, progress, cancellation and bounded resource
/// usage." Required acceptance (verbatim): "Long fixture cancels cleanly and partial output never
/// marked complete." Failure/edge case (verbatim): "Invalid input, cancellation or stale persisted
/// state must not corrupt the project." Implementation guidance (verbatim): "Add overlapping chunks,
/// merge normalization, cancellation and resumable complete-chunk cache; enforce upper bound on working
/// buffers."
///
/// Reuses the exact same honestly-synthetic, analytically-invertible "stem = input * known_gain" ONNX
/// fixture Step 040 already committed (<c>fixtures/onnx/synthetic-stem-separator-v1.onnx</c>) -- no new
/// model fixture is needed because this step's own scope is the chunking/caching/cancellation
/// *mechanism*, not separation quality (Step 044's own later scope, unchanged).
/// </summary>
public sealed class Step041AcceptanceTests : IDisposable
{
    private const string GoodFixtureBase64 =
        "CAcSM3hsaWdodHMtYWktc3R1ZGlvLXBoYXNlMDYtc3RlcDA0MC1maXh0dXJlLWdlbmVyYXRvcjrzAQorCgVpbnB1dBIKaW5wdXRfdW5zcSIJVW5zcXVlZXplKgsKBGF4ZXNAAaABBwogCgppbnB1dF91bnNxCgVnYWlucxIGb3V0cHV0IgNNdWwSGFN5bnRoZXRpY1N0ZW1TZXBhcmF0b3JWMSojCAEIBAgBCAEQAUIFZ2FpbnNKEM3MzD0AAIA+AAAAPwAAQD9aLgoFaW5wdXQSJQojCAESHwoCCAEKChIIY2hhbm5lbHMKDRILbnVtX3NhbXBsZXNiMwoGb3V0cHV0EikKJwgBEiMKAggBCgIIBAoKEghjaGFubmVscwoNEgtudW1fc2FtcGxlc0IECgAQCw==";

    private static readonly float[] GoodFixtureGains = { 0.10f, 0.25f, 0.50f, 0.75f };

    private static readonly IReadOnlyList<StemKind> FixtureStemOrder =
        new[] { StemKind.Drums, StemKind.Bass, StemKind.Vocals, StemKind.Other };

    private readonly string _rootDirectory;
    private readonly string _modelCacheDirectory;
    private readonly string _audioDirectory;
    private readonly string _stemOutputDirectory;

    public Step041AcceptanceTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step041-{Guid.NewGuid():N}");
        _modelCacheDirectory = Path.Combine(_rootDirectory, "models");
        _audioDirectory = Path.Combine(_rootDirectory, "audio");
        _stemOutputDirectory = Path.Combine(_rootDirectory, "stems");
        Directory.CreateDirectory(_modelCacheDirectory);
        Directory.CreateDirectory(_audioDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private static StemSeparationRecipe ChunkedFixtureRecipe(int chunkSamples, int overlapSamples) => new(
        RecipeVersion: "step041-fixture-recipe/1.0.0",
        ExpectedSampleRate: 44100,
        ExpectedChannels: 1,
        StemOrder: FixtureStemOrder,
        InputTensorName: "input",
        OutputTensorName: "output",
        ChunkSamples: chunkSamples,
        OverlapSamples: overlapSamples);

    private static ModelAsset FixtureModelAsset(byte[] modelBytes, string id = "synthetic-stem-separator") => new(
        Id: id,
        Version: "1.0.0-fixture",
        Source: new Uri("https://example.invalid/models/synthetic-stem-separator.onnx"),
        Sha256: Convert.ToHexString(SHA256.HashData(modelBytes)).ToLowerInvariant(),
        LicenseId: "fixture-only, no third-party rights",
        InferenceBackend: "Microsoft.ML.OnnxRuntime (CPU)");

    private async Task<string> SeedVerifiedModelAsync(ModelAssetManager manager, ModelAsset asset, byte[] modelBytes)
    {
        var verifiedPath = manager.GetVerifiedPath(asset);
        await File.WriteAllBytesAsync(verifiedPath, modelBytes);
        return verifiedPath;
    }

    private async Task<SongAsset> WriteFixtureSongAsync(string fileName, float[] samples, int sampleRate)
    {
        var path = Path.Combine(_audioDirectory, fileName);
        await StemWavWriter.WriteMonoFloatAsync(path, samples, sampleRate, CancellationToken.None);
        return await new WavAudioDecoder().ProbeAsync(path, CancellationToken.None);
    }

    private static float[] DeterministicSineWave(int sampleCount, int sampleRate, double frequencyHz, float amplitude)
    {
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            samples[i] = amplitude * (float)Math.Sin(2 * Math.PI * frequencyHz * i / sampleRate);
        }

        return samples;
    }

    private sealed class NeverCalledDownloader : IModelAssetDownloader
    {
        public Task DownloadAsync(Uri source, string destinationPath, CancellationToken ct) =>
            throw new InvalidOperationException("Downloader must not be invoked; Step 041 tests always pre-provision the verified model file.");
    }

    /// <summary>
    /// Instrumented decorator over the real <see cref="FileSystemStemSeparationChunkCache"/> that counts
    /// hits/misses/puts per chunk index -- used to prove a resumed run genuinely skips recomputing
    /// already-cached chunks, not merely that the final output happens to be correct.
    /// </summary>
    private sealed class CountingChunkCache : IStemSeparationChunkCache
    {
        private readonly IStemSeparationChunkCache _inner;
        public readonly List<int> Hits = new();
        public readonly List<int> Puts = new();

        /// <summary>Invoked after a successful Put for the given chunk index -- lets a test cancel mid-run deterministically.</summary>
        public Action<int>? OnPutCompleted { get; set; }

        public CountingChunkCache(IStemSeparationChunkCache inner) => _inner = inner;

        public bool TryGet(string cacheKey, int chunkIndex, int stemCount, int sampleCount, out float[,]? stemSamples, out bool discardedCorruptEntry)
        {
            var hit = _inner.TryGet(cacheKey, chunkIndex, stemCount, sampleCount, out stemSamples, out discardedCorruptEntry);
            if (hit)
            {
                Hits.Add(chunkIndex);
            }

            return hit;
        }

        public void Put(string cacheKey, int chunkIndex, float[,] stemSamples)
        {
            _inner.Put(cacheKey, chunkIndex, stemSamples);
            Puts.Add(chunkIndex);
            OnPutCompleted?.Invoke(chunkIndex);
        }
    }

    [Fact]
    public async Task Step041_Acceptance_Passes_ChunkedOverlapAddMatchesGroundTruth()
    {
        const int sampleRate = 44100;
        // "Long fixture": many chunks (sample count >> chunk size), proving the chunking/overlap-add
        // mechanism itself, not merely a single-chunk pass-through.
        var input = DeterministicSineWave(sampleCount: 2600, sampleRate, frequencyHz: 220.0, amplitude: 0.6f);
        var song = await WriteFixtureSongAsync("source.wav", input, sampleRate);

        var modelBytes = Convert.FromBase64String(GoodFixtureBase64);
        var asset = FixtureModelAsset(modelBytes);
        var manager = new ModelAssetManager(_modelCacheDirectory, new NeverCalledDownloader());
        await SeedVerifiedModelAsync(manager, asset, modelBytes);

        // 9 chunks of 320 with 64-sample overlap for a 2600-sample source -- deliberately not an exact
        // multiple, so the final partial chunk and overlap arithmetic are both exercised for real.
        var recipe = ChunkedFixtureRecipe(chunkSamples: 320, overlapSamples: 64);
        var separator = new OnnxStemSeparator(manager, new WavAudioDecoder(), recipe, _stemOutputDirectory);

        var manifest = await separator.SeparateAsync(song, asset, progress: null, CancellationToken.None);

        Assert.Equal(FixtureStemOrder.Count, manifest.Stems.Count);
        for (var i = 0; i < manifest.Stems.Count; i++)
        {
            var stem = manifest.Stems[i];
            Assert.Equal(song.SampleCount, stem.SampleCount);
            Assert.Equal(song.OriginTicks, stem.SourceOriginTicks);
            Assert.True(File.Exists(stem.FilePath));

            var recoveredSong = await new WavAudioDecoder().ProbeAsync(stem.FilePath, CancellationToken.None);
            var recovered = new List<float>();
            await foreach (var block in new WavAudioDecoder().DecodeMonoAsync(recoveredSong, sampleRate, CancellationToken.None))
            {
                recovered.AddRange(block.Span.ToArray());
            }

            Assert.Equal(input.Length, recovered.Count);
            for (var n = 0; n < input.Length; n++)
            {
                Assert.True(
                    Math.Abs((recovered[n] / GoodFixtureGains[i]) - input[n]) < 1e-3f,
                    $"stem {i} sample {n}: chunked overlap-add must still exactly recover the known-gain ground truth; " +
                    $"expected ~{input[n]}, got {recovered[n] / GoodFixtureGains[i]}");
            }
        }
    }

    [Fact]
    public async Task CancellationMidRun_LeavesNoPartialOutputAndNoFinalManifest_NegativePath()
    {
        const int sampleRate = 44100;
        var input = DeterministicSineWave(sampleCount: 2000, sampleRate, frequencyHz: 220.0, amplitude: 0.6f);
        var song = await WriteFixtureSongAsync("source-cancel-midrun.wav", input, sampleRate);

        var modelBytes = Convert.FromBase64String(GoodFixtureBase64);
        var asset = FixtureModelAsset(modelBytes);
        var manager = new ModelAssetManager(_modelCacheDirectory, new NeverCalledDownloader());
        await SeedVerifiedModelAsync(manager, asset, modelBytes);

        var recipe = ChunkedFixtureRecipe(chunkSamples: 200, overlapSamples: 20); // 10+ chunks
        var cacheDir = Path.Combine(_stemOutputDirectory, ".chunk-cache");
        var cache = new CountingChunkCache(new FileSystemStemSeparationChunkCache(cacheDir));

        using var cts = new CancellationTokenSource();
        cache.OnPutCompleted = chunkIndex =>
        {
            if (chunkIndex == 1) // let chunk 0 and 1 complete, then cancel before chunk 2 starts
            {
                cts.Cancel();
            }
        };

        var separator = new OnnxStemSeparator(manager, new WavAudioDecoder(), recipe, _stemOutputDirectory, cache);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => separator.SeparateAsync(song, asset, progress: null, cts.Token));

        // Required acceptance, verbatim: "partial output never marked complete" -- no final stem .wav
        // file (and no leftover .tmp file) may exist anywhere under the output directory.
        if (Directory.Exists(_stemOutputDirectory))
        {
            var leftoverFiles = Directory.GetFiles(_stemOutputDirectory, "*", SearchOption.TopDirectoryOnly);
            Assert.Empty(leftoverFiles);
        }

        // The already-completed chunks (0 and 1) remain in the resumable cache -- proving cancellation
        // discarded no already-finished work, only stopped starting new work.
        Assert.Contains(0, cache.Puts);
        Assert.Contains(1, cache.Puts);
        Assert.DoesNotContain(2, cache.Puts);
    }

    [Fact]
    public async Task ResumeAfterCancellation_SkipsRecomputingAlreadyCachedChunks()
    {
        const int sampleRate = 44100;
        var input = DeterministicSineWave(sampleCount: 2000, sampleRate, frequencyHz: 220.0, amplitude: 0.6f);
        var song = await WriteFixtureSongAsync("source-resume.wav", input, sampleRate);

        var modelBytes = Convert.FromBase64String(GoodFixtureBase64);
        var asset = FixtureModelAsset(modelBytes);
        var manager = new ModelAssetManager(_modelCacheDirectory, new NeverCalledDownloader());
        await SeedVerifiedModelAsync(manager, asset, modelBytes);

        var recipe = ChunkedFixtureRecipe(chunkSamples: 200, overlapSamples: 20);
        var cacheDir = Path.Combine(_stemOutputDirectory, ".chunk-cache");
        var sharedCache = new FileSystemStemSeparationChunkCache(cacheDir);
        var countingCache = new CountingChunkCache(sharedCache);

        using (var cts = new CancellationTokenSource())
        {
            countingCache.OnPutCompleted = chunkIndex =>
            {
                if (chunkIndex == 1)
                {
                    cts.Cancel();
                }
            };

            var firstRunSeparator = new OnnxStemSeparator(manager, new WavAudioDecoder(), recipe, _stemOutputDirectory, countingCache);
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => firstRunSeparator.SeparateAsync(song, asset, progress: null, cts.Token));
        }

        Assert.Equal(new[] { 0, 1 }, countingCache.Puts);
        countingCache.Puts.Clear();
        countingCache.OnPutCompleted = null;

        var secondRunSeparator = new OnnxStemSeparator(manager, new WavAudioDecoder(), recipe, _stemOutputDirectory, countingCache);
        var manifest = await secondRunSeparator.SeparateAsync(song, asset, progress: null, CancellationToken.None);

        // Resumability, proven directly: chunks 0 and 1 were served from cache (a hit, never re-Put)
        // on the second run; only chunks from index 2 onward were freshly computed.
        Assert.Contains(0, countingCache.Hits);
        Assert.Contains(1, countingCache.Hits);
        Assert.DoesNotContain(0, countingCache.Puts);
        Assert.DoesNotContain(1, countingCache.Puts);

        Assert.Equal(FixtureStemOrder.Count, manifest.Stems.Count);
        foreach (var stem in manifest.Stems)
        {
            Assert.Equal(song.SampleCount, stem.SampleCount);
            Assert.True(File.Exists(stem.FilePath));
        }
    }

    [Fact]
    public async Task CorruptStaleChunkCacheEntry_IsDiscardedAndRecomputed_NegativePath()
    {
        const int sampleRate = 44100;
        var input = DeterministicSineWave(sampleCount: 900, sampleRate, frequencyHz: 220.0, amplitude: 0.6f);
        var song = await WriteFixtureSongAsync("source-corrupt-cache.wav", input, sampleRate);

        var modelBytes = Convert.FromBase64String(GoodFixtureBase64);
        var asset = FixtureModelAsset(modelBytes);
        var manager = new ModelAssetManager(_modelCacheDirectory, new NeverCalledDownloader());
        await SeedVerifiedModelAsync(manager, asset, modelBytes);

        var recipe = ChunkedFixtureRecipe(chunkSamples: 300, overlapSamples: 30);
        var cacheDir = Path.Combine(_stemOutputDirectory, ".chunk-cache");
        var cache = new FileSystemStemSeparationChunkCache(cacheDir);

        // Corrupt the chunk-0 cache entry *before* the separator ever runs, simulating "stale persisted
        // state" left behind by, e.g., a prior crash mid-write (this step's own failure/edge case,
        // verbatim: "stale persisted state must not corrupt the project").
        cache.Put("deliberately-wrong-cache-key-never-used", 0, new float[4, 300]);
        // Directly plant a truncated/garbage file at the real cache key's chunk-0 path.
        var realCacheKey = ComputeExpectedCacheKeyForTest(song, asset, recipe);
        var chunkDir = Path.Combine(cacheDir, realCacheKey);
        Directory.CreateDirectory(chunkDir);
        var chunk0Path = Path.Combine(chunkDir, "chunk-00000000.bin");
        File.WriteAllBytes(chunk0Path, new byte[] { 1, 2, 3 }); // garbage, far too short to be valid

        var separator = new OnnxStemSeparator(manager, new WavAudioDecoder(), recipe, _stemOutputDirectory, cache);
        var manifest = await separator.SeparateAsync(song, asset, progress: null, CancellationToken.None);

        // Must not crash, must not silently return corrupt/garbage stem data -- it must recompute
        // chunk 0 cleanly and disclose that the stale entry was discarded.
        Assert.Equal(FixtureStemOrder.Count, manifest.Stems.Count);
        Assert.Contains(manifest.Warnings, w => w.Contains("corrupt", StringComparison.OrdinalIgnoreCase));

        foreach (var stem in manifest.Stems)
        {
            Assert.Equal(song.SampleCount, stem.SampleCount);
        }
    }

    /// <summary>Mirrors <c>OnnxStemSeparator</c>'s own private cache-key derivation exactly, so this test can plant a cache file at the real key without needing that method to be made public.</summary>
    private static string ComputeExpectedCacheKeyForTest(SongAsset source, ModelAsset model, StemSeparationRecipe recipe)
    {
        var raw = $"{source.Sha256}|{model.Sha256}|{model.Id}|{recipe.RecipeVersion}|{recipe.ChunkSamples}|{recipe.OverlapSamples}";
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        var buffer = hex.ToCharArray();
        for (var i = 0; i < buffer.Length; i++)
        {
            if (!char.IsLetterOrDigit(buffer[i]))
            {
                buffer[i] = '_';
            }
        }

        return new string(buffer);
    }

    [Fact]
    public void Constructor_InvalidChunkOverlapRecipe_ThrowsStemSeparationRecipeInvalidException_NegativePath()
    {
        var manager = new ModelAssetManager(_modelCacheDirectory, new NeverCalledDownloader());
        var invalidRecipe = ChunkedFixtureRecipe(chunkSamples: 100, overlapSamples: 100); // overlap must be < chunk size

        Assert.Throws<StemSeparationRecipeInvalidException>(
            () => new OnnxStemSeparator(manager, new WavAudioDecoder(), invalidRecipe, _stemOutputDirectory));
    }

    [Fact]
    public async Task ChunkedAndWholeClip_SameSourceAndRecipeShape_ProduceSameStemLengthsAndSync()
    {
        const int sampleRate = 44100;
        var input = DeterministicSineWave(sampleCount: 1500, sampleRate, frequencyHz: 330.0, amplitude: 0.4f);
        var song = await WriteFixtureSongAsync("source-compare.wav", input, sampleRate);

        var modelBytes = Convert.FromBase64String(GoodFixtureBase64);
        var asset = FixtureModelAsset(modelBytes);
        var manager = new ModelAssetManager(_modelCacheDirectory, new NeverCalledDownloader());
        await SeedVerifiedModelAsync(manager, asset, modelBytes);

        var wholeClipRecipe = ChunkedFixtureRecipe(chunkSamples: int.MaxValue, overlapSamples: 0);
        var wholeClipSeparator = new OnnxStemSeparator(
            manager, new WavAudioDecoder(), wholeClipRecipe, Path.Combine(_stemOutputDirectory, "whole"));
        var wholeManifest = await wholeClipSeparator.SeparateAsync(song, asset, progress: null, CancellationToken.None);

        var chunkedRecipe = ChunkedFixtureRecipe(chunkSamples: 256, overlapSamples: 48);
        var chunkedSeparator = new OnnxStemSeparator(
            manager, new WavAudioDecoder(), chunkedRecipe, Path.Combine(_stemOutputDirectory, "chunked"));
        var chunkedManifest = await chunkedSeparator.SeparateAsync(song, asset, progress: null, CancellationToken.None);

        Assert.Equal(wholeManifest.Stems.Count, chunkedManifest.Stems.Count);
        for (var i = 0; i < wholeManifest.Stems.Count; i++)
        {
            Assert.Equal(wholeManifest.Stems[i].SampleCount, chunkedManifest.Stems[i].SampleCount);
            Assert.Equal(wholeManifest.Stems[i].SourceOriginTicks, chunkedManifest.Stems[i].SourceOriginTicks);
        }
    }
}
