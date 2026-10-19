using System.Security.Cryptography;
using System.Text;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Step 040 — "Integrate selected ONNX/native inference via .NET with deterministic
/// preprocessing/postprocessing." Real <see cref="IStemSeparator"/>: a genuine
/// <see cref="Microsoft.ML.OnnxRuntime.InferenceSession"/> CPU run (never mocked) against whatever
/// model <see cref="IModelAssetManager"/> resolves/verifies, driven by one versioned
/// <see cref="StemSeparationRecipe"/>.
///
/// PHASE-06 Step 041 — "Add chunking/overlap, caching, progress, cancellation and bounded resource
/// usage": the whole signal is now split into <see cref="StemSeparationRecipe.ChunkSamples"/>-sized
/// (optionally overlapping) chunks, each run through the ONNX session independently and bounded in
/// size regardless of total song length; each completed chunk's raw output is persisted to
/// <see cref="IStemSeparationChunkCache"/> under a cache key derived deterministically from
/// source/model/recipe identity, so a cancelled-and-retried run against the same inputs resumes rather
/// than recomputing already-finished chunks. Overlapping chunk boundaries are merged back together with
/// a weighted overlap-add (crossfade) so no audible seam is introduced. Final stem files are written to
/// a temp path and atomically moved into place only once fully written, so a cancellation can never
/// leave a half-written stem file sitting at its final path (the required acceptance's own "partial
/// output never marked complete").
///
/// Mono-only: preprocessing reads <see cref="IAudioDecoder.DecodeMonoAsync"/> (the only decode path
/// Step 024's <see cref="WavAudioDecoder"/> implements), so every recipe actually used by this
/// separator must declare <see cref="StemSeparationRecipe.ExpectedChannels"/> == 1. A multi-channel
/// source is still decoded (and separated) via its mono downmix, with a disclosed
/// <see cref="StemManifest.Warnings"/> entry — true per-channel stereo separation is deferred,
/// disclosed, pending a stereo-capable decoder.
/// </summary>
public sealed class OnnxStemSeparator : IStemSeparator
{
    private readonly IModelAssetManager _modelAssetManager;
    private readonly IAudioDecoder _audioDecoder;
    private readonly StemSeparationRecipe _recipe;
    private readonly string _outputDirectory;
    private readonly IStemSeparationChunkCache _chunkCache;

    public OnnxStemSeparator(
        IModelAssetManager modelAssetManager,
        IAudioDecoder audioDecoder,
        StemSeparationRecipe recipe,
        string outputDirectory,
        IStemSeparationChunkCache? chunkCache = null)
    {
        ArgumentNullException.ThrowIfNull(modelAssetManager);
        ArgumentNullException.ThrowIfNull(audioDecoder);
        StemSeparationRecipeValidator.Validate(recipe);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        if (recipe.ExpectedChannels != 1)
        {
            throw new StemSeparationRecipeInvalidException(
                $"{nameof(OnnxStemSeparator)} only supports recipes with ExpectedChannels == 1 (mono); got {recipe.ExpectedChannels}. " +
                "Stereo/multi-channel separation requires a decoder that preserves channels, which Step 024's WavAudioDecoder does not yet provide (disclosed gap).");
        }

        _modelAssetManager = modelAssetManager;
        _audioDecoder = audioDecoder;
        _recipe = recipe;
        _outputDirectory = outputDirectory;
        _chunkCache = chunkCache ?? new FileSystemStemSeparationChunkCache(Path.Combine(outputDirectory, ".chunk-cache"));
    }

    public async Task<StemManifest> SeparateAsync(
        SongAsset source, ModelAsset model, IProgress<SeparationProgress>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(model);
        ct.ThrowIfCancellationRequested();

        if (source.SampleRate != _recipe.ExpectedSampleRate)
        {
            throw new StemSeparationIncompatibleSourceException(
                $"Source song sample rate {source.SampleRate} Hz does not match recipe '{_recipe.RecipeVersion}' expected " +
                $"{_recipe.ExpectedSampleRate} Hz; resampling is not implemented by this recipe/separator.");
        }

        progress?.Report(new SeparationProgress(0.0, "ResolvingModel", 0));
        var modelPath = await _modelAssetManager.EnsureVerifiedAsync(model, userConsent: false, ct).ConfigureAwait(false);

        progress?.Report(new SeparationProgress(0.05, "Preprocessing", 0));
        var input = await DecodeFullMonoAsync(source, ct).ConfigureAwait(false);

        var peak = 0f;
        foreach (var sample in input)
        {
            var abs = MathF.Abs(sample);
            if (abs > peak)
            {
                peak = abs;
            }
        }

        var normalizationFactor = Math.Max(peak, _recipe.NormalizationEpsilon);
        var normalized = new float[input.Length];
        for (var i = 0; i < input.Length; i++)
        {
            normalized[i] = input[i] / normalizationFactor;
        }

        ct.ThrowIfCancellationRequested();
        progress?.Report(new SeparationProgress(0.3, "Inference", 0));

        var runId = Guid.NewGuid();
        var cacheKey = ComputeCacheKey(source, model, _recipe);
        var warnings = new List<string>();

        var (stems, outSamples) = await RunChunkedInferenceAndWriteStemsAsync(
            modelPath, normalized, normalizationFactor, source, runId, cacheKey, warnings, progress, ct).ConfigureAwait(false);

        progress?.Report(new SeparationProgress(1.0, "Complete", outSamples));

        if (source.Channels != 1)
        {
            warnings.Add(
                $"Source song had {source.Channels} channels; this separator operates on a mono downmix only (Step 040 disclosed scope boundary).");
        }

        return new StemManifest(
            RunId: runId,
            OriginalSongHash: source.Sha256,
            ModelId: model.Id,
            Stems: stems,
            Warnings: warnings);
    }

    private async Task<float[]> DecodeFullMonoAsync(SongAsset source, CancellationToken ct)
    {
        var samples = new List<float>(source.SampleCount is > 0 and < int.MaxValue ? (int)source.SampleCount : 0);
        await foreach (var block in _audioDecoder.DecodeMonoAsync(source, source.SampleRate, ct).ConfigureAwait(false))
        {
            samples.AddRange(block.Span.ToArray());
        }

        return samples.ToArray();
    }

    private async Task<(List<StemAsset> Stems, int OutSamples)> RunChunkedInferenceAndWriteStemsAsync(
        string modelPath,
        float[] normalized,
        float normalizationFactor,
        SongAsset source,
        Guid runId,
        string cacheKey,
        List<string> warnings,
        IProgress<SeparationProgress>? progress,
        CancellationToken ct)
    {
        var total = normalized.Length;
        var stemCount = _recipe.StemOrder.Count;

        var chunkSamples = Math.Min(_recipe.ChunkSamples, Math.Max(total, 1));
        var overlap = Math.Min(_recipe.OverlapSamples, chunkSamples - 1);
        var stride = chunkSamples - overlap;

        var chunkStarts = new List<int>();
        if (total == 0)
        {
            chunkStarts.Add(0);
        }
        else
        {
            for (var start = 0; start < total; start += stride)
            {
                chunkStarts.Add(start);
                if (start + chunkSamples >= total)
                {
                    break;
                }
            }
        }

        var stemAccumulators = new float[stemCount * Math.Max(total, 1)];
        var weightAccumulator = new float[Math.Max(total, 1)];

        InferenceSession? session = null;
        try
        {
            for (var chunkIndex = 0; chunkIndex < chunkStarts.Count; chunkIndex++)
            {
                ct.ThrowIfCancellationRequested();

                var start = chunkStarts[chunkIndex];
                var length = total == 0 ? 0 : Math.Min(chunkSamples, total - start);

                float[,]? chunkStems;
                if (!_chunkCache.TryGet(cacheKey, chunkIndex, stemCount, length, out chunkStems, out var discardedCorrupt))
                {
                    if (discardedCorrupt)
                    {
                        warnings.Add(
                            $"Chunk cache entry for chunk {chunkIndex} was corrupt/stale and was discarded; recomputed from " +
                            "the source audio instead (Step 041 disclosed resilience guarantee).");
                    }

                    session ??= new InferenceSession(modelPath, new SessionOptions());

                    var chunkInput = new float[length];
                    if (length > 0)
                    {
                        Array.Copy(normalized, start, chunkInput, 0, length);
                    }

                    chunkStems = RunOneChunk(session, chunkInput, length, stemCount, chunkIndex);
                    _chunkCache.Put(cacheKey, chunkIndex, chunkStems);
                }

                var chunkStemsNonNull = chunkStems!;
                var leftOverlap = chunkIndex > 0 ? Math.Min(overlap, length) : 0;
                var rightOverlap = chunkIndex < chunkStarts.Count - 1 ? Math.Min(overlap, length) : 0;

                for (var local = 0; local < length; local++)
                {
                    var weight = ComputeWindow(local, length, leftOverlap, rightOverlap);
                    var globalIndex = start + local;
                    weightAccumulator[globalIndex] += weight;
                    for (var s = 0; s < stemCount; s++)
                    {
                        stemAccumulators[s * total + globalIndex] += chunkStemsNonNull[s, local] * weight;
                    }
                }

                var fraction = chunkStarts.Count == 0 ? 0.9 : 0.3 + 0.6 * ((chunkIndex + 1) / (double)chunkStarts.Count);
                progress?.Report(new SeparationProgress(fraction, "Inference", Math.Min(start + length, total)));
            }
        }
        finally
        {
            session?.Dispose();
        }

        ct.ThrowIfCancellationRequested();
        progress?.Report(new SeparationProgress(0.95, "Writing", total));

        Directory.CreateDirectory(_outputDirectory);
        var stems = new List<StemAsset>(stemCount);
        var pendingTempPaths = new List<string>();
        try
        {
            for (var s = 0; s < stemCount; s++)
            {
                ct.ThrowIfCancellationRequested();

                var merged = new float[total];
                for (var i = 0; i < total; i++)
                {
                    var weight = weightAccumulator[i];
                    merged[i] = weight > 0f
                        ? stemAccumulators[s * total + i] / weight * normalizationFactor
                        : 0f;
                }

                var kind = _recipe.StemOrder[s];
                var finalPath = Path.Combine(_outputDirectory, $"{runId:N}_{kind}.wav");
                var tempPath = finalPath + ".tmp";
                pendingTempPaths.Add(tempPath);

                await StemWavWriter.WriteMonoFloatAsync(tempPath, merged, source.SampleRate, ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();

                File.Move(tempPath, finalPath, overwrite: true);
                pendingTempPaths.Remove(tempPath);

                var sha256 = await ComputeSha256Async(finalPath, ct).ConfigureAwait(false);

                stems.Add(new StemAsset(
                    Id: Guid.NewGuid(),
                    Kind: kind,
                    FilePath: finalPath,
                    SampleRate: source.SampleRate,
                    SampleCount: total,
                    SourceOriginTicks: source.OriginTicks,
                    Sha256: sha256));
            }
        }
        finally
        {
            // A cancellation/failure mid-write must never leave a half-written stem file behind at
            // either the temp or final path -- this step's own required acceptance, verbatim: "partial
            // output never marked complete."
            foreach (var tempPath in pendingTempPaths)
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        return (stems, total);
    }

    private float[,] RunOneChunk(InferenceSession session, float[] chunkInput, int length, int stemCount, int chunkIndex)
    {
        var inputTensor = new DenseTensor<float>(chunkInput, new[] { 1, 1, length });
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_recipe.InputTensorName, inputTensor) };

        using var results = session.Run(inputs, new[] { _recipe.OutputTensorName });

        DisposableNamedOnnxValue? outputValue = null;
        foreach (var result in results)
        {
            if (result.Name == _recipe.OutputTensorName)
            {
                outputValue = result;
                break;
            }
        }

        if (outputValue is null)
        {
            throw new StemSeparationOutputShapeException(
                $"Model did not produce an output tensor named '{_recipe.OutputTensorName}' (chunk {chunkIndex}).");
        }

        var outputTensor = outputValue.AsTensor<float>();
        var dims = outputTensor.Dimensions;

        if (dims.Length != 4)
        {
            throw new StemSeparationOutputShapeException(
                $"Expected a 4-D output tensor [1, stemCount, channels, samples], got rank {dims.Length} (chunk {chunkIndex}).");
        }

        var batch = dims[0];
        var outStemCount = dims[1];
        var outChannels = dims[2];
        var outSamples = dims[3];

        if (batch != 1 || outChannels != 1 || outSamples != length)
        {
            throw new StemSeparationOutputShapeException(
                $"Output tensor shape [{batch},{outStemCount},{outChannels},{outSamples}] is incompatible with expected " +
                $"[1, stemCount, 1, {length}] (chunk {chunkIndex}).");
        }

        if (outStemCount != stemCount)
        {
            throw new StemSeparationOutputShapeException(
                $"Model produced {outStemCount} stems but recipe '{_recipe.RecipeVersion}' declares {stemCount} " +
                $"({string.Join(", ", _recipe.StemOrder)}) (chunk {chunkIndex}).");
        }

        var result2 = new float[stemCount, length];
        for (var s = 0; s < stemCount; s++)
        {
            for (var i = 0; i < length; i++)
            {
                result2[s, i] = outputTensor[0, s, 0, i];
            }
        }

        return result2;
    }

    /// <summary>
    /// Weighted overlap-add crossfade window for one chunk's local sample index: ramps up from a small
    /// positive value to <c>1</c> across a <paramref name="leftOverlap"/>-sample region shared with the
    /// previous chunk, stays at <c>1</c> in the chunk's non-overlapping core, and ramps back down across
    /// a <paramref name="rightOverlap"/>-sample region shared with the next chunk. Never reaches exactly
    /// zero, so every sample contributes a nonzero weight and the final per-sample weight sum used to
    /// normalize the merge is always strictly positive.
    /// </summary>
    private static float ComputeWindow(int local, int length, int leftOverlap, int rightOverlap)
    {
        var w = 1f;
        if (leftOverlap > 0 && local < leftOverlap)
        {
            w = Math.Min(w, (local + 1f) / (leftOverlap + 1f));
        }

        if (rightOverlap > 0 && local >= length - rightOverlap)
        {
            var distanceFromEnd = length - 1 - local;
            w = Math.Min(w, (distanceFromEnd + 1f) / (rightOverlap + 1f));
        }

        return w;
    }

    private static string ComputeCacheKey(SongAsset source, ModelAsset model, StemSeparationRecipe recipe)
    {
        var raw = $"{source.Sha256}|{model.Sha256}|{model.Id}|{recipe.RecipeVersion}|{recipe.ChunkSamples}|{recipe.OverlapSamples}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
