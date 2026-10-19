using Microsoft.ML.OnnxRuntime.Tensors;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06A Step 044C. The real <c>(2, num_splits, 512, 1024)</c> tensor-framing contract: per-channel
/// magnitude spectrogram, time axis zero-padded to a multiple of <see cref="SpectrogramSeparationRecipe.TimeSplitFrames"/>
/// (reusing Step 044B's own <see cref="ShortTimeFourierTransform.ComputeTimeAxisZeroPadding"/> formula,
/// never re-derived), channels stacked on axis 0. Deliberately separated from any real ONNX session call
/// so the tensor shape contract itself -- both building the model's input and rejecting an
/// incompatible model output shape -- can be proven directly against hand-built tensors, independent of
/// any real or even fixture ONNX model (Step 044D's own later scope).
/// </summary>
public static class SpectrogramTensorFraming
{
    /// <summary>
    /// Builds the real <c>(2, num_splits, TimeSplitFrames, RetainedFrequencyBins)</c> input tensor from
    /// both channels' full-bin complex STFT, retaining only the first <see cref="SpectrogramSeparationRecipe.RetainedFrequencyBins"/>
    /// bins' magnitude and zero-padding every frame beyond the original frame count (the <c>DenseTensor&lt;float&gt;</c>'s
    /// own default-zero-initialized backing array already supplies this padding with no copy needed).
    /// </summary>
    public static DenseTensor<float> BuildInputTensor(
        (float[,] Real, float[,] Imag) leftStft,
        (float[,] Real, float[,] Imag) rightStft,
        SpectrogramSeparationRecipe recipe,
        out int originalFrameCount,
        out int numSplits)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        originalFrameCount = leftStft.Real.GetLength(0);
        if (rightStft.Real.GetLength(0) != originalFrameCount)
        {
            throw new StemSeparationOutputShapeException(
                $"Left/right channel STFT frame counts differ ({originalFrameCount} vs {rightStft.Real.GetLength(0)}); " +
                "both stereo channels must cover the same number of frames.");
        }

        var padding = ShortTimeFourierTransform.ComputeTimeAxisZeroPadding(originalFrameCount, recipe.TimeSplitFrames);
        var paddedFrameCount = originalFrameCount + padding;
        numSplits = paddedFrameCount / recipe.TimeSplitFrames;

        var tensor = new DenseTensor<float>(new[] { 2, numSplits, recipe.TimeSplitFrames, recipe.RetainedFrequencyBins });
        FillChannelMagnitude(tensor, channel: 0, leftStft, originalFrameCount, recipe);
        FillChannelMagnitude(tensor, channel: 1, rightStft, originalFrameCount, recipe);
        return tensor;
    }

    /// <summary>
    /// Extracts one channel's real-frame-range magnitude estimate (cropped back to
    /// <paramref name="originalFrameCount"/> frames, discarding the zero-padded tail) from a model's
    /// output tensor, after validating its rank/shape against <paramref name="recipe"/>'s own declared
    /// contract. Rejected via <see cref="StemSeparationOutputShapeException"/> (reusing the existing
    /// exception type, never inventing a parallel one) before any mask arithmetic runs -- this step's own
    /// named failure/edge case, verbatim.
    /// </summary>
    public static float[,] ExtractChannelEstimate(
        Tensor<float> modelOutput, int channel, int originalFrameCount, SpectrogramSeparationRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(modelOutput);
        ArgumentNullException.ThrowIfNull(recipe);

        var dims = modelOutput.Dimensions;
        if (dims.Length != 4)
        {
            throw new StemSeparationOutputShapeException(
                $"Expected a 4-D model output tensor [2, numSplits, {recipe.TimeSplitFrames}, {recipe.RetainedFrequencyBins}], " +
                $"got rank {dims.Length}.");
        }

        if (dims[0] != 2 || dims[2] != recipe.TimeSplitFrames || dims[3] != recipe.RetainedFrequencyBins)
        {
            throw new StemSeparationOutputShapeException(
                $"Model output tensor shape [{dims[0]},{dims[1]},{dims[2]},{dims[3]}] is incompatible with the " +
                $"configured recipe's expected [2,numSplits,{recipe.TimeSplitFrames},{recipe.RetainedFrequencyBins}].");
        }

        if (channel is not (0 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "Channel must be 0 (left) or 1 (right).");
        }

        var numSplits = dims[1];
        var estimate = new float[originalFrameCount, recipe.RetainedFrequencyBins];
        for (var frame = 0; frame < originalFrameCount; frame++)
        {
            var split = frame / recipe.TimeSplitFrames;
            var local = frame % recipe.TimeSplitFrames;
            if (split >= numSplits)
            {
                throw new StemSeparationOutputShapeException(
                    $"Model output tensor covers {numSplits} time splits ({numSplits * recipe.TimeSplitFrames} frames), " +
                    $"insufficient for {originalFrameCount} original frames.");
            }

            for (var bin = 0; bin < recipe.RetainedFrequencyBins; bin++)
            {
                estimate[frame, bin] = modelOutput[channel, split, local, bin];
            }
        }

        return estimate;
    }

    private static void FillChannelMagnitude(
        DenseTensor<float> tensor, int channel, (float[,] Real, float[,] Imag) stft, int frameCount, SpectrogramSeparationRecipe recipe)
    {
        for (var frame = 0; frame < frameCount; frame++)
        {
            var split = frame / recipe.TimeSplitFrames;
            var local = frame % recipe.TimeSplitFrames;
            for (var bin = 0; bin < recipe.RetainedFrequencyBins; bin++)
            {
                var real = stft.Real[frame, bin];
                var imag = stft.Imag[frame, bin];
                tensor[channel, split, local, bin] = MathF.Sqrt(real * real + imag * imag);
            }
        }
    }
}
