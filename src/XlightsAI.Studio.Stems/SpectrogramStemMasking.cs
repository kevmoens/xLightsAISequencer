namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06A Step 044C. The real soft-mask/reconstruction math, verbatim from the sherpa-onnx Spleeter
/// reference algorithm, as a pure, independently-testable composition -- deliberately separated from any
/// ONNX session call so its own correctness can be proven against hand-computed, analytically-known
/// synthetic tensors (standing in for a real model's "vocals_estimate"/"accompaniment_estimate" output),
/// never depending on the real downloaded model weights (PHASE-06A.md Section 6, Step 044C's own required
/// acceptance, verbatim: "independent of the real model weights").
/// </summary>
public static class SpectrogramStemMasking
{
    /// <summary>
    /// The real published soft-mask formula, verbatim:
    /// <c>mask = (stem_estimate^2 + epsilon/2) / (vocals_estimate^2 + accompaniment_estimate^2 + epsilon)</c>.
    /// <paramref name="stemEstimate"/> and <paramref name="otherStemEstimate"/> must be the same shape
    /// (both <c>[frameCount, retainedBins]</c>) -- validated and rejected via
    /// <see cref="StemSeparationOutputShapeException"/> before any mask arithmetic runs (PHASE-06A.md
    /// Section 6, Step 044C's own named failure/edge case, verbatim). <paramref name="epsilon"/> is
    /// load-bearing, not decorative: with both estimates exactly zero (digital silence), every mask value
    /// still evaluates to a finite <c>0.5</c> (never a NaN from a literal zero-over-zero division).
    /// </summary>
    public static float[,] ComputeSoftMask(float[,] stemEstimate, float[,] otherStemEstimate, float epsilon)
    {
        ArgumentNullException.ThrowIfNull(stemEstimate);
        ArgumentNullException.ThrowIfNull(otherStemEstimate);

        var frameCount = stemEstimate.GetLength(0);
        var binCount = stemEstimate.GetLength(1);

        if (otherStemEstimate.GetLength(0) != frameCount || otherStemEstimate.GetLength(1) != binCount)
        {
            throw new StemSeparationOutputShapeException(
                $"Stem estimate tensors have mismatched shapes: [{frameCount},{binCount}] vs " +
                $"[{otherStemEstimate.GetLength(0)},{otherStemEstimate.GetLength(1)}]; both single-stem " +
                "model sessions must produce the same-shaped estimate before any mask arithmetic runs.");
        }

        var mask = new float[frameCount, binCount];
        for (var frame = 0; frame < frameCount; frame++)
        {
            for (var bin = 0; bin < binCount; bin++)
            {
                var stem = stemEstimate[frame, bin];
                var other = otherStemEstimate[frame, bin];
                var stemSquared = stem * stem;
                var otherSquared = other * other;
                mask[frame, bin] = (stemSquared + epsilon / 2f) / (stemSquared + otherSquared + epsilon);
            }
        }

        return mask;
    }

    /// <summary>
    /// Pads <paramref name="retainedMask"/> (shaped <c>[frameCount, retainedBins]</c>) back to
    /// <paramref name="fullFrequencyBinCount"/> bins with an implicit zero mask for every bin at or beyond
    /// <c>retainedBins</c> -- the real model's own disclosed, permanent frequency-domain truncation (the
    /// real model is never fed, and never recovers, anything above its retained bin count) -- then applies
    /// the padded mask to the <b>original</b> complex STFT's real/imaginary parts, never to a
    /// model-estimated phase (the real published algorithm's own disclosed convention).
    /// </summary>
    public static (float[,] Real, float[,] Imag) ApplyMaskToOriginalStft(
        (float[,] Real, float[,] Imag) originalStft, float[,] retainedMask, int fullFrequencyBinCount)
    {
        ArgumentNullException.ThrowIfNull(retainedMask);

        var originalReal = originalStft.Real ?? throw new ArgumentNullException(nameof(originalStft));
        var originalImag = originalStft.Imag ?? throw new ArgumentNullException(nameof(originalStft));

        var frameCount = originalReal.GetLength(0);
        if (originalImag.GetLength(0) != frameCount || originalImag.GetLength(1) != originalReal.GetLength(1))
        {
            throw new StemSeparationOutputShapeException(
                "Original STFT real/imaginary component arrays must have identical shapes.");
        }

        if (originalReal.GetLength(1) != fullFrequencyBinCount)
        {
            throw new StemSeparationOutputShapeException(
                $"Original STFT bin count {originalReal.GetLength(1)} does not match the configured full " +
                $"frequency bin count {fullFrequencyBinCount}.");
        }

        if (retainedMask.GetLength(0) != frameCount)
        {
            throw new StemSeparationOutputShapeException(
                $"Mask frame count {retainedMask.GetLength(0)} does not match the original STFT frame count " +
                $"{frameCount}; a model output tensor covering a different number of frames must be rejected " +
                "before any mask arithmetic is applied.");
        }

        var retainedBins = retainedMask.GetLength(1);
        if (retainedBins > fullFrequencyBinCount)
        {
            throw new StemSeparationOutputShapeException(
                $"Mask retained bin count {retainedBins} exceeds the full frequency bin count {fullFrequencyBinCount}.");
        }

        var real = new float[frameCount, fullFrequencyBinCount];
        var imag = new float[frameCount, fullFrequencyBinCount];
        for (var frame = 0; frame < frameCount; frame++)
        {
            for (var bin = 0; bin < fullFrequencyBinCount; bin++)
            {
                var mask = bin < retainedBins ? retainedMask[frame, bin] : 0f;
                real[frame, bin] = originalReal[frame, bin] * mask;
                imag[frame, bin] = originalImag[frame, bin] * mask;
            }
        }

        return (real, imag);
    }
}
