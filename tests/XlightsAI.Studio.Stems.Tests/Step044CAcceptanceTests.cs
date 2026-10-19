using Microsoft.ML.OnnxRuntime.Tensors;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Stems.Tests;

/// <summary>
/// PHASE-06A Step 044C — "Define the dual-model spectrogram separator composition and prove its
/// masking/reconstruction math." Required acceptance (verbatim): "A synthetic end-to-end round-trip
/// proving the real masking/reconstruction math is correct -- e.g. a trivial, hand-computed two-stem
/// synthetic case (two known, analytically-separable synthetic spectrograms standing in for
/// 'vocals_estimate'/'accompaniment_estimate') where the expected separated output is analytically known
/// -- independent of the real model weights, so this step's own correctness does not depend on the real
/// model producing musically good output." Failure/edge case (verbatim): "A model output tensor of the
/// wrong rank/shape is rejected via StemSeparationOutputShapeException... before any mask arithmetic
/// runs; a digitally silent input never divides by zero in the soft-mask formula."
///
/// No real or fixture ONNX model is loaded by any test in this file: the "vocals_estimate"/
/// "accompaniment_estimate" tensors below are hand-computed directly from known synthetic tones (the
/// exact same real STFT each analytically-known tone alone would produce), standing in for a model's
/// output, so this step's own correctness never depends on any real model weights -- disclosed, exactly
/// per this step's own required acceptance text.
/// </summary>
public sealed class Step044CAcceptanceTests
{
    private static readonly StftConfig Cfg = StftConfig.SpleeterReference;

    private static SpectrogramSeparationRecipe FixtureRecipe() => new(
        RecipeVersion: "step044c-fixture-recipe/1.0.0",
        ExpectedSampleRate: 44100,
        ExpectedChannels: 2,
        Stft: Cfg,
        RetainedFrequencyBins: 1024,
        TimeSplitFrames: 512,
        InputTensorName: "x",
        OutputTensorName: "y");

    private static float[] DeterministicSineWave(int sampleCount, int sampleRate, double frequencyHz, float amplitude)
    {
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            samples[i] = amplitude * (float)Math.Sin(2 * Math.PI * frequencyHz * i / sampleRate);
        }

        return samples;
    }

    /// <summary>Crops a full <c>[frameCount, 2049]</c> magnitude spectrogram down to its first <paramref name="retainedBins"/> columns.</summary>
    private static float[,] CropToRetainedMagnitude((float[,] Real, float[,] Imag) stft, int retainedBins)
    {
        var frameCount = stft.Real.GetLength(0);
        var result = new float[frameCount, retainedBins];
        for (var f = 0; f < frameCount; f++)
        {
            for (var b = 0; b < retainedBins; b++)
            {
                var re = stft.Real[f, b];
                var im = stft.Imag[f, b];
                result[f, b] = MathF.Sqrt(re * re + im * im);
            }
        }

        return result;
    }

    private static double RmsRelativeErrorOverInterior(float[] expected, float[] actual, int edgeMargin)
    {
        var lo = edgeMargin;
        var hi = expected.Length - edgeMargin;
        double sumSquaredError = 0;
        double sumSquaredExpected = 0;
        for (var i = lo; i < hi; i++)
        {
            var diff = actual[i] - expected[i];
            sumSquaredError += diff * diff;
            sumSquaredExpected += expected[i] * (double)expected[i];
        }

        return Math.Sqrt(sumSquaredError / Math.Max(sumSquaredExpected, 1e-12));
    }

    [Fact]
    public void Step044C_Acceptance_Passes_TwoStemSyntheticSeparationMatchesAnalyticallyKnownExpectedOutput()
    {
        const int sampleRate = 44100;
        const int sampleCount = 3 * sampleRate; // 3 seconds -- several STFT frames of real overlap.
        var recipe = FixtureRecipe();

        // Two known, analytically-separable tones, both comfortably within the retained first-1024-bin
        // range (bin frequency ~= bin * 44100/4096 Hz; bin 1024 ~= 11025 Hz).
        var vocalsTone = DeterministicSineWave(sampleCount, sampleRate, frequencyHz: 300.0, amplitude: 0.6f);
        var accompanimentTone = DeterministicSineWave(sampleCount, sampleRate, frequencyHz: 3000.0, amplitude: 0.6f);
        var mix = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            mix[i] = vocalsTone[i] + accompanimentTone[i];
        }

        // The ORIGINAL complex STFT of the real mixed signal -- the mask is applied to this, never to a
        // model-estimated phase (required per this step's own implementation guidance).
        var mixStft = ShortTimeFourierTransform.Forward(mix, Cfg);

        // Hand-computed "vocals_estimate"/"accompaniment_estimate" tensors: the real STFT magnitude each
        // tone, in isolation, actually produces -- analytically known, never a trained model's output.
        var vocalsEstimate = CropToRetainedMagnitude(ShortTimeFourierTransform.Forward(vocalsTone, Cfg), recipe.RetainedFrequencyBins);
        var accompanimentEstimate = CropToRetainedMagnitude(ShortTimeFourierTransform.Forward(accompanimentTone, Cfg), recipe.RetainedFrequencyBins);

        var vocalsMask = SpectrogramStemMasking.ComputeSoftMask(vocalsEstimate, accompanimentEstimate, recipe.SoftMaskEpsilon);
        var accompanimentMask = SpectrogramStemMasking.ComputeSoftMask(accompanimentEstimate, vocalsEstimate, recipe.SoftMaskEpsilon);

        var maskedVocalsStft = SpectrogramStemMasking.ApplyMaskToOriginalStft(mixStft, vocalsMask, Cfg.FullFrequencyBinCount);
        var maskedAccompanimentStft = SpectrogramStemMasking.ApplyMaskToOriginalStft(mixStft, accompanimentMask, Cfg.FullFrequencyBinCount);

        var separatedVocals = ShortTimeFourierTransform.Inverse(maskedVocalsStft, Cfg, sampleCount);
        var separatedAccompaniment = ShortTimeFourierTransform.Inverse(maskedAccompanimentStft, Cfg, sampleCount);

        // Masks always sum to exactly 1 at every retained bin (the real published soft-mask formula's own
        // disclosed property: (a+eps/2)/(a+b+eps) + (b+eps/2)/(a+b+eps) == 1 for a=stemSquared, b=otherSquared).
        for (var f = 0; f < vocalsMask.GetLength(0); f++)
        {
            for (var b = 0; b < vocalsMask.GetLength(1); b++)
            {
                Assert.Equal(1.0, vocalsMask[f, b] + accompanimentMask[f, b], precision: 5);
            }
        }

        // Required acceptance, verbatim: the expected separated output is analytically known -- it is
        // each known tone, in isolation. Exclude Step 044B's own disclosed Hann-window edge-taper margin
        // (WindowSize - HopSize) from the tolerance check, exactly as Step 044B's own round-trip test does.
        var edgeMargin = Cfg.WindowSize - Cfg.HopSize;
        var vocalsError = RmsRelativeErrorOverInterior(vocalsTone, separatedVocals, edgeMargin);
        var accompanimentError = RmsRelativeErrorOverInterior(accompanimentTone, separatedAccompaniment, edgeMargin);

        Assert.True(vocalsError < 1e-3, $"vocals stem RMS-relative error {vocalsError} too high for analytically known separation");
        Assert.True(accompanimentError < 1e-3, $"accompaniment stem RMS-relative error {accompanimentError} too high for analytically known separation");

        foreach (var sample in separatedVocals)
        {
            Assert.True(float.IsFinite(sample));
        }

        foreach (var sample in separatedAccompaniment)
        {
            Assert.True(float.IsFinite(sample));
        }
    }

    [Fact]
    public void ComputeSoftMask_SilentInputBothZero_NeverDividesByZero_ProducesFiniteHalfMask_NegativePathEdgeCase()
    {
        var silentEstimateA = new float[4, 8]; // all zeros
        var silentEstimateB = new float[4, 8]; // all zeros

        var mask = SpectrogramStemMasking.ComputeSoftMask(silentEstimateA, silentEstimateB, epsilon: 1e-10f);

        for (var f = 0; f < 4; f++)
        {
            for (var b = 0; b < 8; b++)
            {
                Assert.True(float.IsFinite(mask[f, b]), "silent estimates must never produce a non-finite mask value");
                Assert.Equal(0.5f, mask[f, b]); // (0+eps/2)/(0+0+eps) == 0.5 exactly, regardless of epsilon value
            }
        }
    }

    [Fact]
    public void ComputeSoftMask_MismatchedEstimateShapes_ThrowsStemSeparationOutputShapeException_NegativePath()
    {
        var stemEstimate = new float[3, 1024];
        var otherStemEstimate = new float[3, 512]; // deliberately wrong bin count

        var ex = Assert.Throws<StemSeparationOutputShapeException>(
            () => SpectrogramStemMasking.ComputeSoftMask(stemEstimate, otherStemEstimate, epsilon: 1e-10f));

        Assert.Contains("mismatched shapes", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyMaskToOriginalStft_MaskFrameCountMismatch_ThrowsStemSeparationOutputShapeException_NegativePath()
    {
        var originalStft = (Real: new float[5, 2049], Imag: new float[5, 2049]);
        var mismatchedMask = new float[4, 1024]; // 4 frames, but originalStft has 5

        var ex = Assert.Throws<StemSeparationOutputShapeException>(
            () => SpectrogramStemMasking.ApplyMaskToOriginalStft(originalStft, mismatchedMask, fullFrequencyBinCount: 2049));

        Assert.Contains("frame count", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyMaskToOriginalStft_BinsBeyondRetainedRangeAreZeroed_PermanentQualityCeilingDisclosed()
    {
        const int frameCount = 1;
        const int fullBins = 2049;
        const int retainedBins = 1024;

        var originalStft = (Real: new float[frameCount, fullBins], Imag: new float[frameCount, fullBins]);
        for (var b = 0; b < fullBins; b++)
        {
            originalStft.Real[0, b] = 7f; // nonzero everywhere, including beyond the retained range
        }

        var mask = new float[frameCount, retainedBins];
        for (var b = 0; b < retainedBins; b++)
        {
            mask[0, b] = 1f; // pass-through mask within the retained range
        }

        var masked = SpectrogramStemMasking.ApplyMaskToOriginalStft(originalStft, mask, fullBins);

        for (var b = 0; b < retainedBins; b++)
        {
            Assert.Equal(7f, masked.Real[0, b]);
        }

        for (var b = retainedBins; b < fullBins; b++)
        {
            Assert.Equal(0f, masked.Real[0, b]); // disclosed, permanent quality ceiling: never recovered
            Assert.Equal(0f, masked.Imag[0, b]);
        }
    }

    [Fact]
    public void BuildInputTensor_ShapeMatchesRealDisclosedConvention()
    {
        var recipe = FixtureRecipe();
        const int frameCount = 600; // not an exact multiple of 512 -- exercises real zero-padding
        var leftStft = (Real: new float[frameCount, Cfg.FullFrequencyBinCount], Imag: new float[frameCount, Cfg.FullFrequencyBinCount]);
        var rightStft = (Real: new float[frameCount, Cfg.FullFrequencyBinCount], Imag: new float[frameCount, Cfg.FullFrequencyBinCount]);

        var tensor = SpectrogramTensorFraming.BuildInputTensor(leftStft, rightStft, recipe, out var originalFrameCount, out var numSplits);

        Assert.Equal(frameCount, originalFrameCount);
        Assert.Equal(2, numSplits); // 600 frames -> padding = 512 - 600%512 = 424 -> 1024 padded frames -> 2 splits of 512
        Assert.Equal(new[] { 2, numSplits, recipe.TimeSplitFrames, recipe.RetainedFrequencyBins }, tensor.Dimensions.ToArray());
    }

    [Fact]
    public void ExtractChannelEstimate_WrongRankModelOutput_ThrowsStemSeparationOutputShapeException_BeforeAnyMaskArithmetic_NegativePath()
    {
        var recipe = FixtureRecipe();
        var wrongRankTensor = new DenseTensor<float>(new[] { 2, 512, 1024 }); // rank 3, not the required rank 4

        var ex = Assert.Throws<StemSeparationOutputShapeException>(
            () => SpectrogramTensorFraming.ExtractChannelEstimate(wrongRankTensor, channel: 0, originalFrameCount: 100, recipe));

        Assert.Contains("rank", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractChannelEstimate_WrongShapeModelOutput_ThrowsStemSeparationOutputShapeException_NegativePath()
    {
        var recipe = FixtureRecipe();
        // Correct rank (4), but wrong channel-axis size (3 instead of the required 2).
        var wrongShapeTensor = new DenseTensor<float>(new[] { 3, 1, recipe.TimeSplitFrames, recipe.RetainedFrequencyBins });

        Assert.Throws<StemSeparationOutputShapeException>(
            () => SpectrogramTensorFraming.ExtractChannelEstimate(wrongShapeTensor, channel: 0, originalFrameCount: 10, recipe));
    }

    [Fact]
    public void ExtractChannelEstimate_RoundTripsKnownValues()
    {
        var recipe = FixtureRecipe();
        const int numSplits = 2;
        var tensor = new DenseTensor<float>(new[] { 2, numSplits, recipe.TimeSplitFrames, recipe.RetainedFrequencyBins });

        // Known, hand-set values at channel 1, frame 600 (split 1, local 88 for TimeSplitFrames=512), bin 10.
        tensor[1, 1, 88, 10] = 42f;

        var estimate = SpectrogramTensorFraming.ExtractChannelEstimate(tensor, channel: 1, originalFrameCount: 601, recipe);

        Assert.Equal(42f, estimate[600, 10]);
    }
}
