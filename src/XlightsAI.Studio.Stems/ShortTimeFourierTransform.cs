using System.Numerics;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// Real (no third-party DSP NuGet package), dependency-free STFT/ISTFT — the same "own and test a
/// well-defined ~200-line algorithm" precedent Phase 07 Step 048 already established for its MIDI reader,
/// and <see cref="SeparationQualityMetricCalculator"/>'s own "no 3rd-party DSP dependency" precedent from
/// Phase 06 Step 044. FFT is computed via a recursive radix-2 Cooley-Tukey (4096 is a power of two).
/// </summary>
public static class ShortTimeFourierTransform
{
    /// <summary>
    /// Real-valued forward STFT, <c>center=False</c>. Returns the real and imaginary (Cartesian) component
    /// arrays of the complex STFT — not a magnitude/polar representation — each shaped
    /// <c>[frameCount, config.FullFrequencyBinCount]</c> — the full 2049 bins; truncation to the first
    /// 1024 bins the real model actually consumes is the caller's own disclosed, explicit step
    /// (<see cref="SpectrogramSeparationRecipe.RetainedFrequencyBins"/>), never silently applied here.
    ///
    /// A signal shorter than one full <c>WindowSize</c> frame is explicitly zero-padded to exactly one
    /// frame — never silently truncated to zero frames. An all-zero (silent) input produces a finite,
    /// all-zero STFT with no NaNs (the Hann window and FFT are purely linear operators on the signal).
    /// </summary>
    public static (float[,] Real, float[,] Imag) Forward(ReadOnlySpan<float> signal, StftConfig config)
    {
        var window = HannWindow(config.WindowSize);
        int frameCount;
        if (signal.Length < config.WindowSize)
        {
            // Explicit zero-pad to exactly one full frame — never silently zero frames.
            frameCount = 1;
        }
        else
        {
            frameCount = 1 + (signal.Length - config.WindowSize) / config.HopSize;
        }

        var binCount = config.FullFrequencyBinCount;
        var real = new float[frameCount, binCount];
        var imag = new float[frameCount, binCount];

        var frameBuffer = new Complex[config.FftSize];
        for (var frame = 0; frame < frameCount; frame++)
        {
            var start = frame * config.HopSize;
            for (var n = 0; n < config.FftSize; n++)
            {
                float sample;
                if (n < config.WindowSize)
                {
                    var sourceIndex = start + n;
                    sample = sourceIndex < signal.Length ? signal[sourceIndex] * window[n] : 0f;
                }
                else
                {
                    sample = 0f;
                }

                frameBuffer[n] = new Complex(sample, 0.0);
            }

            var spectrum = Fft(frameBuffer, inverse: false);
            for (var bin = 0; bin < binCount; bin++)
            {
                real[frame, bin] = (float)spectrum[bin].Real;
                imag[frame, bin] = (float)spectrum[bin].Imaginary;
            }
        }

        return (real, imag);
    }

    /// <summary>
    /// Real-valued inverse STFT (overlap-add reconstruction), <c>center=False</c>, matching
    /// <see cref="Forward"/>'s exact frame convention. <paramref name="outputLength"/> is the original
    /// signal's own sample count, so trailing partial-frame padding introduced by <see cref="Forward"/>
    /// is trimmed away rather than left in the reconstructed signal. Overlap-add is normalized by the
    /// local sum of squared analysis-window weight so overlapping frames recombine to the correct
    /// amplitude; positions with zero accumulated weight (only ever the very first/last sample, where the
    /// Hann window itself is exactly zero) are explicitly guarded to 0 rather than dividing by zero.
    /// </summary>
    public static float[] Inverse((float[,] Real, float[,] Imag) stft, StftConfig config, int outputLength)
    {
        var (real, imag) = stft;
        var frameCount = real.GetLength(0);
        var binCount = real.GetLength(1);
        if (binCount != config.FullFrequencyBinCount)
        {
            throw new ArgumentException(
                $"STFT frequency-bin count {binCount} does not match config.FullFrequencyBinCount {config.FullFrequencyBinCount}.",
                nameof(stft));
        }

        var window = HannWindow(config.WindowSize);
        var paddedLength = frameCount == 0 ? 0 : (frameCount - 1) * config.HopSize + config.WindowSize;

        var output = new float[paddedLength];
        var weight = new float[paddedLength];

        var spectrum = new Complex[config.FftSize];
        for (var frame = 0; frame < frameCount; frame++)
        {
            for (var bin = 0; bin < binCount; bin++)
            {
                spectrum[bin] = new Complex(real[frame, bin], imag[frame, bin]);
            }

            // Reconstruct the full-length conjugate-symmetric spectrum from the retained half.
            for (var bin = binCount; bin < config.FftSize; bin++)
            {
                var mirror = config.FftSize - bin;
                spectrum[bin] = Complex.Conjugate(spectrum[mirror]);
            }

            var timeDomain = Fft(spectrum, inverse: true);
            var start = frame * config.HopSize;
            for (var n = 0; n < config.WindowSize; n++)
            {
                output[start + n] += (float)timeDomain[n].Real * window[n];
                weight[start + n] += window[n] * window[n];
            }
        }

        for (var i = 0; i < paddedLength; i++)
        {
            output[i] = weight[i] > 1e-12f ? output[i] / weight[i] : 0f;
        }

        if (outputLength == paddedLength)
        {
            return output;
        }

        var result = new float[outputLength];
        var copyLength = Math.Min(outputLength, paddedLength);
        Array.Copy(output, result, copyLength);
        return result;
    }

    /// <summary>
    /// The real reference algorithm's own time-axis zero-padding formula: <c>padding = splitFrames -
    /// frameCount % splitFrames</c>. Deliberately never special-cased for "already an exact multiple" —
    /// when <c>frameCount % splitFrames == 0</c> this still evaluates to <c>splitFrames</c> (one full
    /// extra zero-padded block), not <c>0</c>, matching the real model's own disclosed expected input
    /// shape exactly.
    /// </summary>
    public static int ComputeTimeAxisZeroPadding(int frameCount, int splitFrames)
    {
        if (frameCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameCount), frameCount, "Frame count must be non-negative.");
        }

        if (splitFrames <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(splitFrames), splitFrames, "Split size must be positive.");
        }

        return splitFrames - (frameCount % splitFrames);
    }

    private static float[] HannWindow(int size)
    {
        var window = new float[size];
        for (var n = 0; n < size; n++)
        {
            window[n] = (float)(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * n / size));
        }

        return window;
    }

    /// <summary>Recursive radix-2 Cooley-Tukey FFT/IFFT. <paramref name="input"/>.Length must be a power of two.</summary>
    private static Complex[] Fft(Complex[] input, bool inverse)
    {
        var n = input.Length;
        if (n == 1)
        {
            return new[] { input[0] };
        }

        if ((n & (n - 1)) != 0)
        {
            throw new ArgumentException($"FFT size {n} is not a power of two.", nameof(input));
        }

        var even = new Complex[n / 2];
        var odd = new Complex[n / 2];
        for (var i = 0; i < n / 2; i++)
        {
            even[i] = input[2 * i];
            odd[i] = input[2 * i + 1];
        }

        var evenTransformed = Fft(even, inverse);
        var oddTransformed = Fft(odd, inverse);

        var result = new Complex[n];
        var sign = inverse ? 1.0 : -1.0;
        for (var k = 0; k < n / 2; k++)
        {
            var angle = sign * 2.0 * Math.PI * k / n;
            var twiddle = new Complex(Math.Cos(angle), Math.Sin(angle)) * oddTransformed[k];
            result[k] = evenTransformed[k] + twiddle;
            result[k + n / 2] = evenTransformed[k] - twiddle;
        }

        if (inverse)
        {
            for (var k = 0; k < n; k++)
            {
                result[k] /= 2;
            }
        }

        return result;
    }
}
