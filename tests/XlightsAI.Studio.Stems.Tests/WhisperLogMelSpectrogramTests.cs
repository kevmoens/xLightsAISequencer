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

        Assert.Equal(WhisperLogMelSpectrogram.MelBins, mel.GetLength(0));
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
}
