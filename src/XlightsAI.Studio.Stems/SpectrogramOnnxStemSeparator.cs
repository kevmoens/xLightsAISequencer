using System.Security.Cryptography;
using Microsoft.ML.OnnxRuntime;
using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06A Step 044C. Real <see cref="ISpectrogramStemSeparator"/>: real stereo decode (via
/// <see cref="IStereoAudioDecoder"/>), real STFT (via <see cref="ShortTimeFourierTransform"/>), real
/// dual-session ONNX inference (one session per declared stem in <see cref="SpectrogramModelBundle"/>),
/// the real soft mask (<see cref="SpectrogramStemMasking"/>), real ISTFT, real stereo WAV write (via
/// <see cref="StemWavWriter.WriteStereoFloatAsync"/>). Generic over any 2-stem, dual-single-stem-session
/// spectrogram model matching <see cref="SpectrogramSeparationRecipe"/>'s own declared tensor contract --
/// this step proves the composition's own masking/reconstruction math against synthetic estimates
/// (<see cref="SpectrogramStemMasking"/>'s own acceptance tests), independent of any real model weights;
/// Step 044D's own later scope is only to supply the real, downloaded <c>sherpa-onnx-spleeter-2stems</c>
/// archive/<see cref="ModelAsset"/> and the real <see cref="IModelArchiveExpander"/> this class already
/// depends on.
/// </summary>
public sealed class SpectrogramOnnxStemSeparator : ISpectrogramStemSeparator
{
    private readonly IModelAssetManager _modelAssetManager;
    private readonly IModelArchiveExpander _archiveExpander;
    private readonly IStereoAudioDecoder _stereoAudioDecoder;
    private readonly string _outputDirectory;

    public SpectrogramOnnxStemSeparator(
        IModelAssetManager modelAssetManager,
        IModelArchiveExpander archiveExpander,
        IStereoAudioDecoder stereoAudioDecoder,
        string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(modelAssetManager);
        ArgumentNullException.ThrowIfNull(archiveExpander);
        ArgumentNullException.ThrowIfNull(stereoAudioDecoder);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        _modelAssetManager = modelAssetManager;
        _archiveExpander = archiveExpander;
        _stereoAudioDecoder = stereoAudioDecoder;
        _outputDirectory = outputDirectory;
    }

    public async Task<StemManifest> SeparateAsync(
        SongAsset source,
        SpectrogramModelBundle modelBundle,
        SpectrogramSeparationRecipe recipe,
        IProgress<SeparationProgress>? progress,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(modelBundle);
        ArgumentNullException.ThrowIfNull(recipe);
        ct.ThrowIfCancellationRequested();

        if (source.SampleRate != recipe.ExpectedSampleRate || source.Channels != recipe.ExpectedChannels)
        {
            throw new StemSeparationIncompatibleSourceException(
                $"Source song ({source.SampleRate} Hz, {source.Channels}ch) does not match recipe " +
                $"'{recipe.RecipeVersion}' expected {recipe.ExpectedSampleRate} Hz, {recipe.ExpectedChannels}ch stereo.");
        }

        if (modelBundle.StemArchiveEntryNames.Count != 2)
        {
            throw new StemSeparationOutputShapeException(
                $"{nameof(SpectrogramOnnxStemSeparator)} requires exactly 2 declared single-stem sessions " +
                $"(the real sherpa-onnx-spleeter-2stems model's own vocals/accompaniment shape); got " +
                $"{modelBundle.StemArchiveEntryNames.Count}.");
        }

        progress?.Report(new SeparationProgress(0.0, "ResolvingModel", 0));
        var archivePath = await _modelAssetManager.EnsureVerifiedAsync(modelBundle.Archive, userConsent: false, ct).ConfigureAwait(false);

        var extractDirectory = Path.Combine(_outputDirectory, ".extracted", SanitizeForPath(modelBundle.Archive.Id));
        var extracted = await _archiveExpander.ExpandAsync(archivePath, extractDirectory, ct).ConfigureAwait(false);
        var modelPathsByKind = ResolveModelPaths(modelBundle, extracted);

        ct.ThrowIfCancellationRequested();
        progress?.Report(new SeparationProgress(0.1, "Preprocessing", 0));
        var (left, right) = await DecodeFullStereoAsync(source, ct).ConfigureAwait(false);

        var leftStft = ShortTimeFourierTransform.Forward(left, recipe.Stft);
        var rightStft = ShortTimeFourierTransform.Forward(right, recipe.Stft);

        var inputTensor = SpectrogramTensorFraming.BuildInputTensor(
            leftStft, rightStft, recipe, out var originalFrameCount, out _);

        progress?.Report(new SeparationProgress(0.3, "Inference", 0));

        var estimatesByKind = new Dictionary<StemKind, (float[,] Left, float[,] Right)>();
        foreach (var (kind, modelPath) in modelPathsByKind)
        {
            ct.ThrowIfCancellationRequested();
            using var session = new InferenceSession(modelPath, new SessionOptions());
            using var results = session.Run(
                new[] { NamedOnnxValue.CreateFromTensor(recipe.InputTensorName, inputTensor) },
                new[] { recipe.OutputTensorName });

            var outputValue = results.FirstOrDefault(r => r.Name == recipe.OutputTensorName)
                ?? throw new StemSeparationOutputShapeException(
                    $"Model for stem '{kind}' did not produce an output tensor named '{recipe.OutputTensorName}'.");

            var outputTensor = outputValue.AsTensor<float>();
            var leftEstimate = SpectrogramTensorFraming.ExtractChannelEstimate(outputTensor, channel: 0, originalFrameCount, recipe);
            var rightEstimate = SpectrogramTensorFraming.ExtractChannelEstimate(outputTensor, channel: 1, originalFrameCount, recipe);
            estimatesByKind[kind] = (leftEstimate, rightEstimate);
        }

        progress?.Report(new SeparationProgress(0.7, "Masking", 0));

        Directory.CreateDirectory(_outputDirectory);
        var runId = Guid.NewGuid();
        var stems = new List<StemAsset>();

        foreach (var kind in modelBundle.StemArchiveEntryNames.Keys)
        {
            ct.ThrowIfCancellationRequested();
            var otherKind = modelBundle.StemArchiveEntryNames.Keys.First(k => k != kind);

            var leftMask = SpectrogramStemMasking.ComputeSoftMask(
                estimatesByKind[kind].Left, estimatesByKind[otherKind].Left, recipe.SoftMaskEpsilon);
            var rightMask = SpectrogramStemMasking.ComputeSoftMask(
                estimatesByKind[kind].Right, estimatesByKind[otherKind].Right, recipe.SoftMaskEpsilon);

            var maskedLeft = SpectrogramStemMasking.ApplyMaskToOriginalStft(leftStft, leftMask, recipe.Stft.FullFrequencyBinCount);
            var maskedRight = SpectrogramStemMasking.ApplyMaskToOriginalStft(rightStft, rightMask, recipe.Stft.FullFrequencyBinCount);

            var leftOut = ShortTimeFourierTransform.Inverse(maskedLeft, recipe.Stft, left.Length);
            var rightOut = ShortTimeFourierTransform.Inverse(maskedRight, recipe.Stft, right.Length);

            var finalPath = Path.Combine(_outputDirectory, $"{runId:N}_{kind}.wav");
            var tempPath = finalPath + ".tmp";
            try
            {
                await StemWavWriter.WriteStereoFloatAsync(tempPath, leftOut, rightOut, source.SampleRate, ct).ConfigureAwait(false);
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

            var sha256 = await ComputeSha256Async(finalPath, ct).ConfigureAwait(false);
            stems.Add(new StemAsset(
                Id: Guid.NewGuid(),
                Kind: kind,
                FilePath: finalPath,
                SampleRate: source.SampleRate,
                SampleCount: left.Length,
                SourceOriginTicks: source.OriginTicks,
                Sha256: sha256));
        }

        progress?.Report(new SeparationProgress(1.0, "Complete", left.Length));

        return new StemManifest(
            RunId: runId,
            OriginalSongHash: source.Sha256,
            ModelId: modelBundle.Archive.Id,
            Stems: stems,
            Warnings: Array.Empty<string>());
    }

    private static Dictionary<StemKind, string> ResolveModelPaths(
        SpectrogramModelBundle modelBundle, IReadOnlyList<ExtractedModelFile> extracted)
    {
        var paths = new Dictionary<StemKind, string>();
        foreach (var (kind, entryName) in modelBundle.StemArchiveEntryNames)
        {
            var match = extracted.FirstOrDefault(f => f.ArchiveEntryName == entryName);
            if (match is null)
            {
                throw new StemSeparationOutputShapeException(
                    $"Declared archive entry '{entryName}' for stem '{kind}' was not found among the " +
                    $"{extracted.Count} extracted file(s) from archive '{modelBundle.Archive.Id}'.");
            }

            paths[kind] = match.LocalPath;
        }

        return paths;
    }

    private async Task<(float[] Left, float[] Right)> DecodeFullStereoAsync(SongAsset source, CancellationToken ct)
    {
        var left = new List<float>(source.SampleCount is > 0 and < int.MaxValue ? (int)source.SampleCount : 0);
        var right = new List<float>(left.Capacity);
        await foreach (var block in _stereoAudioDecoder.DecodeStereoAsync(source, ct).ConfigureAwait(false))
        {
            left.AddRange(block.Left.Span.ToArray());
            right.AddRange(block.Right.Span.ToArray());
        }

        return (left.ToArray(), right.ToArray());
    }

    private static string SanitizeForPath(string id)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = id.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
