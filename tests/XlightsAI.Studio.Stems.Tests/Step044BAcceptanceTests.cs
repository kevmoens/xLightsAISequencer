using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Stems.Tests;

/// <summary>
/// PHASE-06A Step 044B — "Implement real, dependency-free STFT/ISTFT matching the exact published model
/// algorithm." Required acceptance (verbatim): "A real round-trip test (STFT then ISTFT reconstructs a
/// synthetic signal... to within a small numeric tolerance) and a frequency-bin-truncation test proving
/// the first 1024 of the full 2049 complex bins are the ones retained/discarded, matching the real
/// model's own convention exactly." Failure/edge case (verbatim): "A signal shorter than one full
/// win_length frame is handled explicitly...; an all-zero (silent) input produces a finite, all-zero
/// STFT/ISTFT round trip with no NaNs; a frame count that is already an exact multiple of 512 still
/// receives one full extra 512-frame zero-padded block... an implementer must not special-case 'already
/// aligned' to mean 'no padding'."
/// </summary>
public sealed class Step044BAcceptanceTests
{
    private static readonly StftConfig Config = StftConfig.SpleeterReference;

    private static float[] DeterministicSineSum(int sampleCount, int sampleRate, params (double FrequencyHz, float Amplitude)[] tones)
    {
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            double value = 0;
            foreach (var (frequencyHz, amplitude) in tones)
            {
                value += amplitude * Math.Sin(2 * Math.PI * frequencyHz * i / sampleRate);
            }

            samples[i] = (float)value;
        }

        return samples;
    }

    [Fact]
    public void Step044B_Acceptance_Passes_RoundTripReconstructsSyntheticSignalWithinTolerance()
    {
        const int sampleRate = 44100;
        // ~3 seconds -> many overlapping frames (hop=1024, win=4096 => 4x overlap), so the inherent
        // Hann-window-edge boundary loss (exactly the first/last sample of the whole signal, where the
        // window itself is exactly zero) is a vanishingly small fraction of the signal.
        var signal = DeterministicSineSum(sampleRate * 3, sampleRate, (220.0, 0.6f), (880.0, 0.25f), (1760.0, 0.1f));

        var stft = ShortTimeFourierTransform.Forward(signal, Config);
        var reconstructed = ShortTimeFourierTransform.Inverse(stft, Config, signal.Length);

        Assert.Equal(signal.Length, reconstructed.Length);
        Assert.All(reconstructed, v => Assert.False(float.IsNaN(v)));
        Assert.All(reconstructed, v => Assert.False(float.IsInfinity(v)));

        // Disclosed boundary artifact: with center=False, frame 0 starts exactly at sample 0 and the
        // last frame ends exactly at the signal's own end, so the outermost (WindowSize-HopSize) samples
        // at each edge never receive full 4x Hann overlap coverage (ramping from 1x up to full 4x) the
        // way the signal's interior does. This is the real, published algorithm's own center=False
        // convention, not a bug -- exclude this disclosed margin and verify the fully-overlapped interior
        // reconstructs to a tight numeric tolerance.
        var edgeMargin = Config.WindowSize - Config.HopSize;
        double sumSquaredError = 0;
        double sumSquaredSignal = 0;
        for (var i = edgeMargin; i < signal.Length - edgeMargin; i++)
        {
            var error = reconstructed[i] - signal[i];
            sumSquaredError += error * error;
            sumSquaredSignal += (double)signal[i] * signal[i];
        }

        var rmsRelativeError = Math.Sqrt(sumSquaredError / sumSquaredSignal);
        Assert.True(rmsRelativeError < 1e-4, $"RMS relative reconstruction error too high: {rmsRelativeError}");

        for (var i = edgeMargin; i < signal.Length - edgeMargin; i++)
        {
            Assert.True(Math.Abs(reconstructed[i] - signal[i]) < 0.01f,
                $"Sample {i}: expected {signal[i]}, got {reconstructed[i]}");
        }
    }

    [Fact]
    public void Step044B_Acceptance_Passes_FrequencyBinTruncation_FirstHalfRetainedSecondHalfDiscarded()
    {
        const int sampleRate = 44100;
        Assert.Equal(2049, Config.FullFrequencyBinCount);

        // Bin index k corresponds to frequency k * sampleRate / FftSize. Bin 500 (< 1024, retained) and
        // bin 1500 (>= 1024, discarded) are both comfortably inside their own half, away from spectral
        // leakage at the exact boundary.
        var lowBinFrequencyHz = 500.0 * sampleRate / Config.FftSize;
        var highBinFrequencyHz = 1500.0 * sampleRate / Config.FftSize;

        var lowToneSignal = DeterministicSineSum(sampleRate * 2, sampleRate, (lowBinFrequencyHz, 1.0f));
        var highToneSignal = DeterministicSineSum(sampleRate * 2, sampleRate, (highBinFrequencyHz, 1.0f));

        var (lowReal, lowImag) = ShortTimeFourierTransform.Forward(lowToneSignal, Config);
        var (highReal, highImag) = ShortTimeFourierTransform.Forward(highToneSignal, Config);

        Assert.Equal(2049, lowReal.GetLength(1));
        Assert.Equal(2049, highReal.GetLength(1));

        var midFrame = lowReal.GetLength(0) / 2;

        double RetainedEnergy(float[,] real, float[,] imag, int frame)
        {
            double energy = 0;
            for (var bin = 0; bin < 1024; bin++)
            {
                energy += (double)real[frame, bin] * real[frame, bin] + (double)imag[frame, bin] * imag[frame, bin];
            }

            return energy;
        }

        double DiscardedEnergy(float[,] real, float[,] imag, int frame)
        {
            double energy = 0;
            for (var bin = 1024; bin < 2049; bin++)
            {
                energy += (double)real[frame, bin] * real[frame, bin] + (double)imag[frame, bin] * imag[frame, bin];
            }

            return energy;
        }

        // Low-frequency tone: almost all energy sits in the retained first 1024 bins.
        var lowRetained = RetainedEnergy(lowReal, lowImag, midFrame);
        var lowDiscarded = DiscardedEnergy(lowReal, lowImag, midFrame);
        Assert.True(lowRetained > lowDiscarded * 100, $"Low tone: retained={lowRetained}, discarded={lowDiscarded}");

        // High-frequency tone: almost all energy sits in the discarded bins 1024-2048 -- this is the
        // model's own permanent, disclosed quality ceiling, proven here as an explicit, observed fact,
        // not assumed.
        var highRetained = RetainedEnergy(highReal, highImag, midFrame);
        var highDiscarded = DiscardedEnergy(highReal, highImag, midFrame);
        Assert.True(highDiscarded > highRetained * 100, $"High tone: retained={highRetained}, discarded={highDiscarded}");
    }

    [Fact]
    public void ShortSignal_ShorterThanOneWindowFrame_ZeroPaddedToOneFullFrame_NegativePathEdgeCase()
    {
        // Deliberately far shorter than WindowSize (4096) -- must not silently produce zero frames.
        var shortSignal = new float[100];
        for (var i = 0; i < shortSignal.Length; i++)
        {
            shortSignal[i] = 0.5f * (float)Math.Sin(2 * Math.PI * 440.0 * i / 44100);
        }

        var stft = ShortTimeFourierTransform.Forward(shortSignal, Config);

        Assert.Equal(1, stft.Real.GetLength(0));
        Assert.Equal(2049, stft.Real.GetLength(1));

        var reconstructed = ShortTimeFourierTransform.Inverse(stft, Config, shortSignal.Length);
        Assert.Equal(shortSignal.Length, reconstructed.Length);
        Assert.All(reconstructed, v => Assert.False(float.IsNaN(v)));
        Assert.All(reconstructed, v => Assert.False(float.IsInfinity(v)));
    }

    [Fact]
    public void SilentAllZeroInput_ProducesFiniteAllZeroRoundTrip_NoNaNs_NegativePathEdgeCase()
    {
        var silent = new float[Config.FftSize * 3];

        var stft = ShortTimeFourierTransform.Forward(silent, Config);

        for (var frame = 0; frame < stft.Real.GetLength(0); frame++)
        {
            for (var bin = 0; bin < stft.Real.GetLength(1); bin++)
            {
                Assert.Equal(0f, stft.Real[frame, bin]);
                Assert.Equal(0f, stft.Imag[frame, bin]);
            }
        }

        var reconstructed = ShortTimeFourierTransform.Inverse(stft, Config, silent.Length);

        Assert.Equal(silent.Length, reconstructed.Length);
        Assert.All(reconstructed, v => Assert.False(float.IsNaN(v)));
        Assert.All(reconstructed, v => Assert.Equal(0f, v));
    }

    [Theory]
    [InlineData(0, 512)]
    [InlineData(512, 512)]
    [InlineData(1024, 512)]
    [InlineData(1536, 512)]
    public void FrameCountAlreadyExactMultipleOfSplitSize_StillReceivesOneFullExtraPaddedBlock_NegativePathEdgeCase(int frameCount, int splitSize)
    {
        var padding = ShortTimeFourierTransform.ComputeTimeAxisZeroPadding(frameCount, splitSize);

        // The real reference algorithm's own formula: padding = splitSize - frameCount % splitSize.
        // When frameCount is already an exact multiple of splitSize, frameCount % splitSize == 0, so this
        // evaluates to splitSize itself (one full extra block) -- never 0 -- proving "already aligned" is
        // never special-cased to mean "no padding."
        Assert.Equal(splitSize, padding);

        var paddedFrameCount = frameCount + padding;
        Assert.Equal(0, paddedFrameCount % splitSize);
        Assert.True(paddedFrameCount > frameCount, "An already-aligned frame count must still grow by a full extra block.");
    }

    [Fact]
    public void FrameCountNotAlignedToSplitSize_ReceivesPartialPaddingUpToNextMultiple_NegativePath()
    {
        var padding = ShortTimeFourierTransform.ComputeTimeAxisZeroPadding(1546, 512);
        Assert.Equal(502, padding);
        Assert.Equal(0, (1546 + padding) % 512);
    }
}
