namespace XlightsAI.Studio.Reference.Fixtures;

/// <summary>
/// Pure, deterministic synthesis of this Studio's baseline audio fixtures. No randomness, no
/// wall-clock, no I/O: the same recipe parameters always produce byte-identical PCM samples, which
/// is what lets Step 003's "repeatable invocation" requirement be proven by a unit test rather than
/// taken on faith. All fixtures here are fully synthetic sine/harmonic tones — no copyrighted
/// recordings are read, mixed or redistributed.
/// </summary>
public static class DeterministicSyntheticAudioGenerator
{
    /// <summary>A plain, single-frequency note sequence ("synthetic notes" fixture).</summary>
    public static float[] BuildNoteSequence(IReadOnlyList<double> frequenciesHz, double noteDurationSeconds, int sampleRate)
    {
        var samplesPerNote = (int)(noteDurationSeconds * sampleRate);
        var samples = new float[samplesPerNote * frequenciesHz.Count];

        for (var noteIndex = 0; noteIndex < frequenciesHz.Count; noteIndex++)
        {
            var frequency = frequenciesHz[noteIndex];
            for (var i = 0; i < samplesPerNote; i++)
            {
                var t = i / (double)sampleRate;
                samples[(noteIndex * samplesPerNote) + i] = (float)(0.6 * Math.Sin(2 * Math.PI * frequency * t));
            }
        }

        return samples;
    }

    /// <summary>A fixed-tempo click track ("drum pulse" fixture): short tone bursts at every beat.</summary>
    public static float[] BuildClickTrack(int bpm, int clickCount, double clickDurationSeconds, double toneHz, int sampleRate)
    {
        var beatIntervalSeconds = 60.0 / bpm;
        var totalSamples = (int)(beatIntervalSeconds * clickCount * sampleRate);
        var samples = new float[totalSamples];
        var clickSamples = (int)(clickDurationSeconds * sampleRate);

        for (var click = 0; click < clickCount; click++)
        {
            var startSample = (int)(click * beatIntervalSeconds * sampleRate);
            for (var i = 0; i < clickSamples && startSample + i < totalSamples; i++)
            {
                var t = i / (double)sampleRate;
                // Linear decay envelope so each click is a short percussive burst, not a sustained tone.
                var envelope = 1.0 - (i / (double)clickSamples);
                samples[startSample + i] = (float)(0.8 * envelope * Math.Sin(2 * Math.PI * toneHz * t));
            }
        }

        return samples;
    }

    /// <summary>An additive-harmonic ("isolated piano phrase") note sequence: fundamental + decaying overtones.</summary>
    public static float[] BuildHarmonicPhrase(IReadOnlyList<double> frequenciesHz, double noteDurationSeconds, int sampleRate)
    {
        var samplesPerNote = (int)(noteDurationSeconds * sampleRate);
        var samples = new float[samplesPerNote * frequenciesHz.Count];
        double[] harmonicWeights = [1.0, 0.45, 0.2];

        for (var noteIndex = 0; noteIndex < frequenciesHz.Count; noteIndex++)
        {
            var frequency = frequenciesHz[noteIndex];
            for (var i = 0; i < samplesPerNote; i++)
            {
                var t = i / (double)sampleRate;
                var envelope = Math.Exp(-2.5 * t); // percussive-ish decay, like a struck string
                double value = 0;
                for (var h = 0; h < harmonicWeights.Length; h++)
                {
                    value += harmonicWeights[h] * Math.Sin(2 * Math.PI * frequency * (h + 1) * t);
                }

                samples[(noteIndex * samplesPerNote) + i] = (float)(0.5 * envelope * value);
            }
        }

        return samples;
    }

    /// <summary>Sums multiple synthesized sources sample-for-sample and clamps to a safe PCM range.</summary>
    public static float[] MixDown(params float[][] sources)
    {
        var length = sources.Max(s => s.Length);
        var mixed = new float[length];

        foreach (var source in sources)
        {
            for (var i = 0; i < source.Length; i++)
            {
                mixed[i] += source[i];
            }
        }

        for (var i = 0; i < mixed.Length; i++)
        {
            mixed[i] = Math.Clamp(mixed[i], -1f, 1f);
        }

        return mixed;
    }

    /// <summary>
    /// PHASE-06A Step 044A: encodes independent left/right 32-bit float channels as an interleaved
    /// 16-bit PCM stereo WAV file, byte-identical for identical input. Additive sibling to
    /// <see cref="EncodeWav"/> (mono) -- does not change that method's own behavior or signature.
    /// <paramref name="left"/>/<paramref name="right"/> must have equal length.
    /// </summary>
    public static byte[] EncodeStereoWav(float[] left, float[] right, int sampleRate)
    {
        if (left.Length != right.Length)
        {
            throw new ArgumentException("Left and right channels must have equal sample counts.", nameof(right));
        }

        const short bitsPerSample = 16;
        const short channels = 2;
        var byteRate = sampleRate * channels * (bitsPerSample / 8);
        var blockAlign = (short)(channels * (bitsPerSample / 8));
        var dataSize = left.Length * blockAlign;

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write("RIFF"u8.ToArray());
            writer.Write(36 + dataSize);
            writer.Write("WAVE"u8.ToArray());

            writer.Write("fmt "u8.ToArray());
            writer.Write(16); // PCM fmt chunk size
            writer.Write((short)1); // PCM format
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);

            writer.Write("data"u8.ToArray());
            writer.Write(dataSize);
            for (var i = 0; i < left.Length; i++)
            {
                writer.Write((short)(Math.Clamp(left[i], -1f, 1f) * short.MaxValue));
                writer.Write((short)(Math.Clamp(right[i], -1f, 1f) * short.MaxValue));
            }
        }

        return stream.ToArray();
    }

    /// <summary>Encodes mono 32-bit float samples as a 16-bit PCM WAV file, byte-identical for identical input.</summary>
    public static byte[] EncodeWav(float[] samples, int sampleRate)
    {
        const short bitsPerSample = 16;
        const short channels = 1;
        var byteRate = sampleRate * channels * (bitsPerSample / 8);
        var blockAlign = (short)(channels * (bitsPerSample / 8));
        var dataSize = samples.Length * (bitsPerSample / 8);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write("RIFF"u8.ToArray());
            writer.Write(36 + dataSize);
            writer.Write("WAVE"u8.ToArray());

            writer.Write("fmt "u8.ToArray());
            writer.Write(16); // PCM fmt chunk size
            writer.Write((short)1); // PCM format
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);

            writer.Write("data"u8.ToArray());
            writer.Write(dataSize);
            foreach (var sample in samples)
            {
                var clamped = Math.Clamp(sample, -1f, 1f);
                writer.Write((short)(clamped * short.MaxValue));
            }
        }

        return stream.ToArray();
    }
}
