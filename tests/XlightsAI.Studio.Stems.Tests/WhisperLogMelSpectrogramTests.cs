namespace XlightsAI.Studio.Stems.Tests;

/// <summary>
/// Build-plan "richer analysis tools" Phase 6 (2026-11-02): <see cref="WhisperLogMelSpectrogram"/>.
/// Regression coverage for a real bug caught during implementation: a static field initialization order
/// mistake (a non-const field referenced by the mel filterbank's own static initializer, but declared
/// later in the source file) silently divided by zero, producing an all-NaN spectrogram with no exception
/// anywhere -- these tests assert finiteness explicitly, not just "doesn't throw."
/// </summary>
public sealed class WhisperLogMelSpectrogramTests
{
    [Fact]
    public void Compute_SilentInput_ProducesFiniteValuesOfTheExpectedShape_NoNaN()
    {
        var silence = new float[WhisperLogMelSpectrogram.ChunkSamples];

        var mel = WhisperLogMelSpectrogram.Compute(silence);

        Assert.Equal(WhisperLogMelSpectrogram.DefaultMelBins, mel.GetLength(0));
        Assert.Equal(3000, mel.GetLength(1));
        foreach (var value in mel)
        {
            Assert.False(float.IsNaN(value), "mel spectrogram must never contain NaN");
            Assert.False(float.IsInfinity(value), "mel spectrogram must never contain Infinity");
        }
    }

    [Fact]
    public void Compute_ToneInput_ProducesFiniteNonConstantValues()
    {
        const int sampleRate = WhisperLogMelSpectrogram.SampleRate;
        var samples = new float[sampleRate * 2]; // 2 real seconds -- the rest is implicitly silence-padded.
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = 0.3f * MathF.Sin(2f * MathF.PI * 440f * i / sampleRate);
        }

        var mel = WhisperLogMelSpectrogram.Compute(samples);

        var distinctValues = new HashSet<float>();
        foreach (var value in mel)
        {
            Assert.False(float.IsNaN(value));
            Assert.False(float.IsInfinity(value));
            distinctValues.Add(value);
        }

        Assert.True(distinctValues.Count > 10, "a real tone must produce real spectral variation, not a flat/degenerate output");
    }

    [Fact]
    public void Compute_ShorterThanOneChunk_ZeroPadsRatherThanThrowing()
    {
        var shortClip = new float[16000]; // 1 second, far under the 30-second chunk.

        var mel = WhisperLogMelSpectrogram.Compute(shortClip);

        Assert.Equal(3000, mel.GetLength(1));
    }

    [Fact]
    public void Compute_LongerThanOneChunk_TruncatesRatherThanThrowing()
    {
        var longClip = new float[WhisperLogMelSpectrogram.ChunkSamples * 2];

        var mel = WhisperLogMelSpectrogram.Compute(longClip);

        Assert.Equal(3000, mel.GetLength(1));
    }

    /// <summary>
    /// User-requested real investigation (2026-11-08): large-v3's own real ONNX encoder rejected an
    /// 80-bin mel tensor outright ("Got: 80 Expected: 128") -- confirming OpenAI's large-v3 release uses
    /// a 128-bin filterbank, not 80. <see cref="WhisperLogMelSpectrogram.Compute"/>'s own
    /// <c>melBins</c> parameter must produce a real, distinct, non-degenerate 128-row filterbank output,
    /// not merely resize the array.
    /// </summary>
    [Fact]
    public void Compute_With128MelBins_ProducesCorrectlyShapedFiniteNonConstantOutput()
    {
        const int sampleRate = WhisperLogMelSpectrogram.SampleRate;
        var samples = new float[sampleRate * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = 0.3f * MathF.Sin(2f * MathF.PI * 440f * i / sampleRate);
        }

        var mel80 = WhisperLogMelSpectrogram.Compute(samples, melBins: 80);
        var mel128 = WhisperLogMelSpectrogram.Compute(samples, melBins: 128);

        Assert.Equal(80, mel80.GetLength(0));
        Assert.Equal(128, mel128.GetLength(0));
        Assert.Equal(mel80.GetLength(1), mel128.GetLength(1));

        var distinctValues = new HashSet<float>();
        foreach (var value in mel128)
        {
            Assert.False(float.IsNaN(value));
            Assert.False(float.IsInfinity(value));
            distinctValues.Add(value);
        }

        Assert.True(distinctValues.Count > 10, "a real tone must produce real spectral variation at 128 bins too, not a flat/degenerate output");

        // The two bin counts must produce genuinely DIFFERENT filterbanks (not, say, one silently
        // reusing the cached 80-bin table for both) -- compare the first row where both exist.
        var differsSomewhere = false;
        for (var t = 0; t < mel80.GetLength(1); t++)
        {
            if (Math.Abs(mel80[0, t] - mel128[0, t]) > 1e-4f)
            {
                differsSomewhere = true;
                break;
            }
        }

        Assert.True(differsSomewhere, "80-bin and 128-bin filterbanks must be genuinely distinct, not accidentally shared/cached under one key");
    }
}
