using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// Build-plan "richer analysis tools" Phase 6 (2026-11-02): real <see cref="ILyricTranscriber"/> --
/// OpenAI Whisper's own encoder/decoder ONNX graphs (<see cref="SherpaOnnxWhisperTinyEnModelAsset"/>), run
/// directly against <c>Microsoft.ML.OnnxRuntime</c> (never mocked), same consent-gated model-resolution
/// flow as <see cref="OnnxStemSeparator"/>/<see cref="SpectrogramOnnxStemSeparator"/>
/// (<c>EnsureVerifiedAsync(..., userConsent: false, ...)</c> -- a not-yet-installed model surfaces as
/// <see cref="ModelAssetConsentRequiredException"/>, never a hidden download).
///
/// <para>
/// <b>Algorithm.</b> The song's vocal stem is decoded to 16 kHz mono (<see cref="IAudioDecoder.DecodeMonoAsync"/>'s
/// own built-in resampling) and processed in independent, non-overlapping 30-second chunks (Whisper's own
/// fixed encoder window -- <see cref="WhisperLogMelSpectrogram.ChunkSamples"/>). Each chunk: log-mel
/// spectrogram -&gt; one encoder forward pass (cross-attention key/value tensors) -&gt; a greedy,
/// KV-cached autoregressive decode loop, with the very first generated token forced away from
/// <see cref="WhisperSpecialTokens.NoTimestamps"/> so the model emits real timestamp tokens bounding each
/// recognized phrase (Whisper's own native segment-timing mechanism -- no separate forced-alignment model
/// needed). A segment's own words are recovered by splitting its decoded text on whitespace and
/// distributing its measured timestamp-pair span across them proportionally to character length -- a
/// disclosed approximation (see <see cref="TranscribedWord"/>'s own doc comment), not a true per-word
/// forced alignment.
/// </para>
/// <para>
/// Verified end-to-end (this exact algorithm, ported from a Python prototype run against the real ONNX
/// graphs) against <see cref="SherpaOnnxWhisperTinyEnModelAsset"/>'s own bundled ground-truth test clips
/// before this feature shipped -- ground-truth transcripts were reproduced essentially verbatim.
/// </para>
/// </summary>
public sealed class WhisperOnnxTranscriber : ILyricTranscriber
{
    private const int MaxSelfAttentionCacheLength = 448;
    private const int MaxDecodeStepsPerChunk = MaxSelfAttentionCacheLength - 1;
    private const long TicksPerSecond = 10_000_000L;

    private readonly IModelAssetManager _modelAssetManager;
    private readonly IModelArchiveExpander _archiveExpander;
    private readonly IAudioDecoder _audioDecoder;
    private readonly WhisperModelBundle _modelBundle;
    private readonly string _workingDirectory;

    public WhisperOnnxTranscriber(
        IModelAssetManager modelAssetManager,
        IModelArchiveExpander archiveExpander,
        IAudioDecoder audioDecoder,
        WhisperModelBundle modelBundle,
        string workingDirectory)
    {
        _modelAssetManager = modelAssetManager ?? throw new ArgumentNullException(nameof(modelAssetManager));
        _archiveExpander = archiveExpander ?? throw new ArgumentNullException(nameof(archiveExpander));
        _audioDecoder = audioDecoder ?? throw new ArgumentNullException(nameof(audioDecoder));
        _modelBundle = modelBundle ?? throw new ArgumentNullException(nameof(modelBundle));
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        _workingDirectory = workingDirectory;
    }

    public async Task<IReadOnlyList<TranscribedWord>> TranscribeAsync(SongAsset vocalsStem, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(vocalsStem);
        ct.ThrowIfCancellationRequested();

        var archivePath = await _modelAssetManager.EnsureVerifiedAsync(_modelBundle.Archive, userConsent: false, ct).ConfigureAwait(false);
        var extractDirectory = Path.Combine(_workingDirectory, ".extracted", SanitizeForPath(_modelBundle.Archive.Id));
        var extracted = await _archiveExpander.ExpandAsync(archivePath, extractDirectory, ct).ConfigureAwait(false);

        var encoderPath = ResolveEntry(extracted, _modelBundle.EncoderEntryName);
        var decoderPath = ResolveEntry(extracted, _modelBundle.DecoderEntryName);
        var tokensPath = ResolveEntry(extracted, _modelBundle.TokensEntryName);

        var vocabulary = await WhisperTokenVocabulary.LoadAsync(tokensPath, ct).ConfigureAwait(false);
        var samples = await DecodeAllMono16kAsync(vocalsStem, ct).ConfigureAwait(false);

        using var encoderSession = new InferenceSession(encoderPath, new SessionOptions());
        using var decoderSession = new InferenceSession(decoderPath, new SessionOptions());

        var words = new List<TranscribedWord>();
        for (var chunkStart = 0; chunkStart < samples.Length; chunkStart += WhisperLogMelSpectrogram.ChunkSamples)
        {
            ct.ThrowIfCancellationRequested();

            var chunkLength = Math.Min(WhisperLogMelSpectrogram.ChunkSamples, samples.Length - chunkStart);
            var chunkStartTicks = (long)chunkStart * TicksPerSecond / WhisperLogMelSpectrogram.SampleRate;
            var chunkDurationTicks = (long)chunkLength * TicksPerSecond / WhisperLogMelSpectrogram.SampleRate;

            var segments = TranscribeChunk(encoderSession, decoderSession, samples.AsSpan(chunkStart, chunkLength), chunkDurationTicks, ct);
            foreach (var segment in segments)
            {
                var text = vocabulary.Decode(segment.TextTokenIds);
                words.AddRange(SplitIntoWords(text, segment.StartTicks + chunkStartTicks, segment.EndTicks + chunkStartTicks, segment.Confidence));
            }
        }

        return words;
    }

    private sealed record ChunkSegment(long StartTicks, long EndTicks, IReadOnlyList<int> TextTokenIds, float Confidence);

    private static List<ChunkSegment> TranscribeChunk(
        InferenceSession encoderSession, InferenceSession decoderSession, ReadOnlySpan<float> chunkSamples, long chunkDurationTicks, CancellationToken ct)
    {
        var mel = WhisperLogMelSpectrogram.Compute(chunkSamples);
        var melFrames = mel.GetLength(1);
        var melTensor = new DenseTensor<float>(new[] { 1, WhisperLogMelSpectrogram.MelBins, melFrames });
        for (var m = 0; m < WhisperLogMelSpectrogram.MelBins; m++)
        {
            for (var t = 0; t < melFrames; t++)
            {
                melTensor[0, m, t] = mel[m, t];
            }
        }

        DenseTensor<float> crossK, crossV;
        using (var encoderOutputs = encoderSession.Run([NamedOnnxValue.CreateFromTensor("mel", melTensor)]))
        {
            crossK = CopyTensor(RequireOutput(encoderOutputs, "n_layer_cross_k"));
            crossV = CopyTensor(RequireOutput(encoderOutputs, "n_layer_cross_v"));
        }

        var nLayer = crossK.Dimensions[0];
        var nState = crossK.Dimensions[3];

        var selfK = new DenseTensor<float>(new[] { nLayer, 1, MaxSelfAttentionCacheLength, nState });
        var selfV = new DenseTensor<float>(new[] { nLayer, 1, MaxSelfAttentionCacheLength, nState });

        var tokens = new List<int> { WhisperSpecialTokens.StartOfTranscript };
        var segments = new List<ChunkSegment>();
        long? openSegmentStartTicks = null;
        var openSegmentTokens = new List<int>();
        var openSegmentConfidences = new List<float>();
        long offset = 0;

        for (var step = 0; step < MaxDecodeStepsPerChunk; step++)
        {
            ct.ThrowIfCancellationRequested();

            var tokenInputIds = step == 0
                ? tokens.Select(t => (long)t).ToArray()
                : [(long)tokens[^1]];
            var tokenTensor = new DenseTensor<long>(tokenInputIds, new[] { 1, tokenInputIds.Length });
            var offsetTensor = new DenseTensor<long>(new[] { offset }, new[] { 1 });

            float[] logitsRow;
            int vocabSize;
            using (var decoderOutputs = decoderSession.Run([
                NamedOnnxValue.CreateFromTensor("tokens", tokenTensor),
                NamedOnnxValue.CreateFromTensor("in_n_layer_self_k_cache", selfK),
                NamedOnnxValue.CreateFromTensor("in_n_layer_self_v_cache", selfV),
                NamedOnnxValue.CreateFromTensor("n_layer_cross_k", crossK),
                NamedOnnxValue.CreateFromTensor("n_layer_cross_v", crossV),
                NamedOnnxValue.CreateFromTensor("offset", offsetTensor),
            ]))
            {
                var logits = RequireOutput(decoderOutputs, "logits");
                var lastPosition = logits.Dimensions[1] - 1;
                vocabSize = logits.Dimensions[2];
                logitsRow = new float[vocabSize];
                for (var v = 0; v < vocabSize; v++)
                {
                    logitsRow[v] = logits[0, lastPosition, v];
                }

                selfK = CopyTensor(RequireOutput(decoderOutputs, "out_n_layer_self_k_cache"));
                selfV = CopyTensor(RequireOutput(decoderOutputs, "out_n_layer_self_v_cache"));
            }

            offset += tokenInputIds.Length;

            var bestId = 0;
            var bestLogit = float.NegativeInfinity;
            for (var v = 0; v < vocabSize; v++)
            {
                // The very first generated token is forced away from NoTimestamps (step == 0 is always a
                // single-token-context first call) so this model emits a real segment-timing anchor
                // instead of opting out of timestamps entirely -- see this type's own doc comment.
                if (step == 0 && v == WhisperSpecialTokens.NoTimestamps)
                {
                    continue;
                }

                if (logitsRow[v] > bestLogit)
                {
                    bestLogit = logitsRow[v];
                    bestId = v;
                }
            }

            double sumExp = 0;
            for (var v = 0; v < vocabSize; v++)
            {
                sumExp += Math.Exp(logitsRow[v] - bestLogit);
            }

            var confidence = (float)(1.0 / sumExp);

            if (bestId == WhisperSpecialTokens.EndOfText)
            {
                break;
            }

            tokens.Add(bestId);

            if (bestId >= WhisperSpecialTokens.TimestampBegin)
            {
                var ticks = (long)((bestId - WhisperSpecialTokens.TimestampBegin) * WhisperSpecialTokens.TimestampSecondsPerToken * TicksPerSecond);
                if (openSegmentStartTicks is { } startTicks)
                {
                    if (openSegmentTokens.Count > 0)
                    {
                        segments.Add(new ChunkSegment(startTicks, ticks, openSegmentTokens, Average(openSegmentConfidences)));
                    }
                }

                openSegmentStartTicks = ticks;
                openSegmentTokens = [];
                openSegmentConfidences = [];
            }
            else
            {
                openSegmentTokens.Add(bestId);
                openSegmentConfidences.Add(confidence);
            }
        }

        // A segment the chunk ran out of decode budget on (no closing timestamp token ever emitted) is
        // still real recognized text -- close it at the chunk's own end rather than discarding it.
        if (openSegmentStartTicks is { } finalStart && openSegmentTokens.Count > 0)
        {
            segments.Add(new ChunkSegment(finalStart, chunkDurationTicks, openSegmentTokens, Average(openSegmentConfidences)));
        }

        return segments;
    }

    /// <summary>
    /// Distributes one segment's own measured timestamp-pair span across its words, proportionally to
    /// each word's character length -- a disclosed approximation (see <see cref="TranscribedWord"/>'s own
    /// doc comment), not a true per-word forced alignment.
    /// </summary>
    private static IEnumerable<TranscribedWord> SplitIntoWords(string text, long startTicks, long endTicks, float confidence)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            yield break;
        }

        var totalChars = words.Sum(w => w.Length);
        if (totalChars == 0 || endTicks <= startTicks)
        {
            yield break;
        }

        var span = endTicks - startTicks;
        var cursor = startTicks;
        for (var i = 0; i < words.Length; i++)
        {
            var wordSpan = i == words.Length - 1
                ? endTicks - cursor // last word absorbs any rounding remainder
                : span * words[i].Length / totalChars;
            var wordEnd = cursor + wordSpan;
            yield return new TranscribedWord(words[i], cursor, wordEnd, confidence);
            cursor = wordEnd;
        }
    }

    private async Task<float[]> DecodeAllMono16kAsync(SongAsset vocalsStem, CancellationToken ct)
    {
        var samples = new List<float>(vocalsStem.SampleCount is > 0 and < int.MaxValue ? (int)vocalsStem.SampleCount : 0);
        await foreach (var block in _audioDecoder.DecodeMonoAsync(vocalsStem, WhisperLogMelSpectrogram.SampleRate, ct).ConfigureAwait(false))
        {
            samples.AddRange(block.Span.ToArray());
        }

        return samples.ToArray();
    }

    private static float Average(IReadOnlyList<float> values) => values.Count == 0 ? 0f : values.Average();

    private static Tensor<float> RequireOutput(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs, string name)
    {
        foreach (var output in outputs)
        {
            if (output.Name == name)
            {
                return output.AsTensor<float>();
            }
        }

        throw new InvalidOperationException($"Whisper ONNX graph did not produce an output tensor named '{name}'.");
    }

    private static DenseTensor<float> CopyTensor(Tensor<float> tensor) =>
        new(tensor.ToArray(), tensor.Dimensions.ToArray());

    private static string ResolveEntry(IReadOnlyList<ExtractedModelFile> extracted, string entryName)
    {
        var match = extracted.FirstOrDefault(f => f.ArchiveEntryName == entryName);
        if (match is null)
        {
            throw new InvalidOperationException(
                $"Declared archive entry '{entryName}' was not found among the {extracted.Count} extracted file(s).");
        }

        return match.LocalPath;
    }

    private static string SanitizeForPath(string id)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(id.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
