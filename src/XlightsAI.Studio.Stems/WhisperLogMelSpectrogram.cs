namespace XlightsAI.Studio.Stems;

/// <summary>
/// Build-plan "richer analysis tools" Phase 6 (2026-11-02): OpenAI Whisper's own documented audio front
/// end (<c>whisper/audio.py</c>'s <c>log_mel_spectrogram</c>) -- 16 kHz mono input, a 400-sample Hann-
/// windowed STFT (hop 160, reflect-padded, <c>center=True</c>), an 80-band Slaney-style mel filterbank,
/// log10 magnitude clipped to the top 8 dB, then rescaled to roughly <c>[-1, 1]</c>
/// (<c>(log_spec + 4) / 4</c>). Every constant here is Whisper's own fixed, published preprocessing
/// contract, not a tunable choice -- the encoder was trained against exactly this transform and will not
/// produce sensible output against any other. Implemented as a direct (non-power-of-two) DFT rather than
/// <see cref="ShortTimeFourierTransform"/> (that type requires a power-of-two FFT size; Whisper's own
/// window size, 400, is not one). Verified end-to-end (this extractor plus the real encoder/decoder ONNX
/// graphs) against <see cref="SherpaOnnxWhisperTinyEnModelAsset"/>'s own bundled ground-truth test clips
/// before this feature shipped -- see that type's own doc comment.
/// </summary>
public static class WhisperLogMelSpectrogram
{
    public const int SampleRate = 16000;
    public const int FftSize = 400;
    public const int HopSize = 160;
    public const int MelBins = 80;
    public const int ChunkSamples = 30 * SampleRate;

    // Declared before MelFilterbank below: C# initializes static fields in textual declaration order, and
    // BuildMelFilterbank() (via HzToMel/MelToHz) depends on LogStep -- a static readonly field, not a
    // const, so it must already be initialized by the time BuildMelFilterbank() runs, or every mel-scale
    // conversion above 1000 Hz silently divides by zero.
    private const double MelBreakFrequencyHz = 1000.0;
    private const double MelHighFrequencyQ = 1000.0 / (200.0 / 3.0);
    private static readonly double LogStep = Math.Log(6.4) / 27.0; // Slaney mel-scale log-region step.

    private static readonly int FrequencyBinCount = FftSize / 2 + 1;
    private static readonly float[] HannWindow = BuildHannWindow(FftSize);
    private static readonly double[,] CosTable = BuildTrigTable(isSine: false);
    private static readonly double[,] SinTable = BuildTrigTable(isSine: true);
    private static readonly float[,] MelFilterbank = BuildMelFilterbank();

    /// <summary>
    /// Computes the log-mel spectrogram for one chunk of already-16kHz-mono samples, zero-padded or
    /// truncated to exactly <see cref="ChunkSamples"/> (30 seconds) first -- Whisper's encoder always
    /// consumes a fixed-length 30-second window, silence-padding a shorter real chunk rather than varying
    /// its own input shape.
    /// </summary>
    /// <returns><c>[MelBins, 3000]</c> row-major (mel bin, then time frame).</returns>
    public static float[,] Compute(ReadOnlySpan<float> samples)
    {
        var chunk = new float[ChunkSamples];
        samples[..Math.Min(samples.Length, ChunkSamples)].CopyTo(chunk);

        var padded = ReflectPad(chunk, FftSize / 2);
        var frameCount = 1 + (padded.Length - FftSize) / HopSize;

        var power = new float[FrequencyBinCount, frameCount];
        var frame = new float[FftSize];
        for (var t = 0; t < frameCount; t++)
        {
            var start = t * HopSize;
            for (var n = 0; n < FftSize; n++)
            {
                frame[n] = padded[start + n] * HannWindow[n];
            }

            for (var k = 0; k < FrequencyBinCount; k++)
            {
                double real = 0, imag = 0;
                for (var n = 0; n < FftSize; n++)
                {
                    real += frame[n] * CosTable[k, n];
                    imag -= frame[n] * SinTable[k, n];
                }

                power[k, t] = (float)(real * real + imag * imag);
            }
        }

        // Whisper's own reference implementation drops the STFT's final frame (`stft[..., :-1]`) --
        // center=True padding produces one extra trailing frame beyond what the mel filterbank expects.
        var usedFrames = frameCount - 1;
        var mel = new float[MelBins, usedFrames];
        var maxLog = float.NegativeInfinity;
        for (var m = 0; m < MelBins; m++)
        {
            for (var t = 0; t < usedFrames; t++)
            {
                double sum = 0;
                for (var k = 0; k < FrequencyBinCount; k++)
                {
                    sum += MelFilterbank[m, k] * power[k, t];
                }

                var logValue = (float)Math.Log10(Math.Max(sum, 1e-10));
                mel[m, t] = logValue;
                if (logValue > maxLog)
                {
                    maxLog = logValue;
                }
            }
        }

        var floor = maxLog - 8f;
        for (var m = 0; m < MelBins; m++)
        {
            for (var t = 0; t < usedFrames; t++)
            {
                var clipped = Math.Max(mel[m, t], floor);
                mel[m, t] = (clipped + 4f) / 4f;
            }
        }

        return mel;
    }

    private static float[] ReflectPad(float[] signal, int padWidth)
    {
        var result = new float[signal.Length + 2 * padWidth];
        Array.Copy(signal, 0, result, padWidth, signal.Length);
        for (var i = 0; i < padWidth; i++)
        {
            result[padWidth - 1 - i] = signal[Math.Min(i + 1, signal.Length - 1)];
            result[padWidth + signal.Length + i] = signal[Math.Max(signal.Length - 2 - i, 0)];
        }

        return result;
    }

    private static float[] BuildHannWindow(int size)
    {
        var window = new float[size];
        for (var n = 0; n < size; n++)
        {
            window[n] = (float)(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * n / size));
        }

        return window;
    }

    private static double[,] BuildTrigTable(bool isSine)
    {
        var table = new double[FrequencyBinCount, FftSize];
        for (var k = 0; k < FrequencyBinCount; k++)
        {
            for (var n = 0; n < FftSize; n++)
            {
                var angle = 2.0 * Math.PI * k * n / FftSize;
                table[k, n] = isSine ? Math.Sin(angle) : Math.Cos(angle);
            }
        }

        return table;
    }

    /// <summary>
    /// Slaney-style mel filterbank (the same scale/normalization <c>librosa.filters.mel</c>'s default --
    /// and Whisper's own precomputed <c>mel_filters.npz</c> -- uses): triangular filters spaced evenly on
    /// the mel scale between 0 Hz and Nyquist, each individually area-normalized so a flat-spectrum input
    /// produces roughly equal energy per band.
    /// </summary>
    private static float[,] BuildMelFilterbank()
    {
        var fftFreqs = new double[FrequencyBinCount];
        for (var k = 0; k < FrequencyBinCount; k++)
        {
            fftFreqs[k] = k * (SampleRate / 2.0) / (FrequencyBinCount - 1);
        }

        var minMel = HzToMel(0.0);
        var maxMel = HzToMel(SampleRate / 2.0);
        var melPoints = new double[MelBins + 2];
        for (var i = 0; i < melPoints.Length; i++)
        {
            melPoints[i] = minMel + (maxMel - minMel) * i / (MelBins + 1);
        }

        var hzPoints = melPoints.Select(MelToHz).ToArray();

        var weights = new float[MelBins, FrequencyBinCount];
        for (var m = 0; m < MelBins; m++)
        {
            var lowerHz = hzPoints[m];
            var centerHz = hzPoints[m + 1];
            var upperHz = hzPoints[m + 2];
            var enorm = 2.0 / (upperHz - lowerHz);

            for (var k = 0; k < FrequencyBinCount; k++)
            {
                var lower = (fftFreqs[k] - lowerHz) / (centerHz - lowerHz);
                var upper = (upperHz - fftFreqs[k]) / (upperHz - centerHz);
                var weight = Math.Max(0.0, Math.Min(lower, upper));
                weights[m, k] = (float)(weight * enorm);
            }
        }

        return weights;
    }

    private static double HzToMel(double hz)
    {
        const double fSp = 200.0 / 3.0;
        if (hz < MelBreakFrequencyHz)
        {
            return hz / fSp;
        }

        return MelHighFrequencyQ + Math.Log(hz / MelBreakFrequencyHz) / LogStep;
    }

    private static double MelToHz(double mel)
    {
        const double fSp = 200.0 / 3.0;
        if (mel < MelHighFrequencyQ)
        {
            return fSp * mel;
        }

        return MelBreakFrequencyHz * Math.Exp(LogStep * (mel - MelHighFrequencyQ));
    }
}
