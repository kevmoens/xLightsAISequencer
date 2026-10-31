using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// Build-plan Phase 8 task 3 (singing-face support, deferred from Phase 3's stretch item). <b>This is a
/// heuristic amplitude/zero-crossing-rate proxy for mouth shape, NOT phoneme recognition.</b> No ASR or
/// forced-alignment infrastructure exists anywhere in this pipeline (no transcript, no lyrics text), so
/// there is nothing to align phonemes *to* -- what this produces is a plausible-looking, deterministic
/// mouth-shape guess from the vocals stem's own short-time loudness and noisiness, bucketed into one of
/// xLights' real Faces viseme codes. Treat it as a placeholder for a real phoneme aligner, never as
/// ground truth.
/// </summary>
public sealed class VisemeApproximator
{
    public const string Version = "XlightsAI.Studio.Stems.VisemeApproximator/1.0.0";

    /// <summary>Emitted for any window outside every <see cref="VocalActivityEnvelope.ActiveRegions"/> entry, or never emitted at all -- callers only ever see this inside a region when <see cref="ApproximateAsync"/> itself needs a placeholder, which it does not today; kept for callers that want an explicit "not singing" code.</summary>
    public const string RestCode = "rest";

    /// <summary>
    /// The real xLights Faces viseme codes (see <c>EffectVocabularyBuilder</c>'s own documented
    /// <c>Faces_MouthMovements</c> options), ordered from most closed/quiet (<c>MBP</c> -- a bilabial
    /// closure, acoustically the quietest shape) to most open/loud (<c>AI</c>) -- used purely as an
    /// openness ladder to bucket normalized short-time energy into a plausible mouth shape, not as a
    /// claim about which phoneme is actually present.
    /// </summary>
    private static readonly string[] OpennessLadder = ["MBP", "E", "L", "O", "U", "WQ", "AI"];

    /// <summary>Zero-crossing rate above this fraction of a window's own sample count marks a noisy/fricative-sounding window -- "FV" (lips-and-teeth) regardless of loudness, since a fricative can be quiet.</summary>
    private const double FricativeZeroCrossingRateThreshold = 0.20;

    private const long TicksPerSecond = 10_000_000L;

    private readonly IAudioDecoder _decoder;

    public VisemeApproximator(IAudioDecoder decoder)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
    }

    /// <summary>
    /// Produces one <see cref="VisemeMark"/> per <paramref name="windowSize"/> slice of every
    /// <paramref name="vocalActivity"/> active region -- empty when there is no vocals stem or no active
    /// region at all (an instrumental song gets no viseme marks, never a fabricated "always resting" track).
    /// </summary>
    public async Task<IReadOnlyList<VisemeMark>> ApproximateAsync(
        StemManifest stems, VocalActivityEnvelope vocalActivity, TimeSpan windowSize, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stems);
        ArgumentNullException.ThrowIfNull(vocalActivity);
        if (windowSize <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSize), "Window size must be positive.");
        }

        if (vocalActivity.ActiveRegions.Count == 0)
        {
            return [];
        }

        var vocalsStem = stems.Stems.FirstOrDefault(s => s.Kind == StemKind.Vocals && s.Id == vocalActivity.VocalsStemId);
        if (vocalsStem is null)
        {
            return [];
        }

        ct.ThrowIfCancellationRequested();
        var probed = await _decoder.ProbeAsync(vocalsStem.FilePath, ct).ConfigureAwait(false);
        var samples = new List<float>(vocalsStem.SampleCount is > 0 and <= int.MaxValue ? (int)vocalsStem.SampleCount : 0);
        await foreach (var block in _decoder.DecodeMonoAsync(probed, vocalsStem.SampleRate, ct).ConfigureAwait(false))
        {
            samples.AddRange(block.Span);
            ct.ThrowIfCancellationRequested();
        }

        var sampleRate = vocalsStem.SampleRate;
        var windowSamples = Math.Max(1, (int)Math.Round(windowSize.TotalSeconds * sampleRate));

        var raw = new List<(int Start, int Length, double Rms, double ZeroCrossingRate)>();
        foreach (var region in vocalActivity.ActiveRegions.OrderBy(r => r.StartTicks))
        {
            var startSample = (int)Math.Clamp(TicksToSamples(region.StartTicks, sampleRate), 0, samples.Count);
            var endSample = (int)Math.Clamp(TicksToSamples(region.EndTicks, sampleRate), 0, samples.Count);
            for (var s = startSample; s < endSample; s += windowSamples)
            {
                var length = Math.Min(windowSamples, endSample - s);
                if (length <= 0)
                {
                    break;
                }

                var (rms, zcr) = ComputeFeatures(samples, s, length);
                raw.Add((s, length, rms, zcr));
            }
        }

        if (raw.Count == 0)
        {
            return [];
        }

        var peakRms = raw.Max(w => w.Rms);
        var marks = new List<VisemeMark>(raw.Count);
        foreach (var window in raw)
        {
            var code = PickViseme(window.Rms, window.ZeroCrossingRate, peakRms, window.Length);
            marks.Add(new VisemeMark(code, SamplesToTicks(window.Start, sampleRate), SamplesToTicks(window.Start + window.Length, sampleRate)));
        }

        return CoalesceAdjacentSameCode(marks);
    }

    private static string PickViseme(double rms, double zeroCrossingRate, double peakRms, int windowLength)
    {
        var normalizedZcr = windowLength > 0 ? zeroCrossingRate / windowLength : 0.0;
        if (normalizedZcr >= FricativeZeroCrossingRateThreshold)
        {
            return "FV";
        }

        var normalizedRms = peakRms > 0 ? Math.Clamp(rms / peakRms, 0.0, 1.0) : 0.0;
        var index = Math.Clamp((int)(normalizedRms * OpennessLadder.Length), 0, OpennessLadder.Length - 1);
        return OpennessLadder[index];
    }

    private static (double Rms, double ZeroCrossingRate) ComputeFeatures(List<float> samples, int start, int length)
    {
        double sumSquares = 0;
        var crossings = 0;
        for (var i = start; i < start + length; i++)
        {
            sumSquares += (double)samples[i] * samples[i];
            if (i > start && Math.Sign(samples[i]) != Math.Sign(samples[i - 1]) && samples[i] != 0 && samples[i - 1] != 0)
            {
                crossings++;
            }
        }

        return (Math.Sqrt(sumSquares / length), crossings);
    }

    private static List<VisemeMark> CoalesceAdjacentSameCode(List<VisemeMark> marks)
    {
        var result = new List<VisemeMark> { marks[0] };
        for (var i = 1; i < marks.Count; i++)
        {
            var last = result[^1];
            if (string.Equals(last.Code, marks[i].Code, StringComparison.Ordinal) && last.EndTicks == marks[i].StartTicks)
            {
                result[^1] = last with { EndTicks = marks[i].EndTicks };
            }
            else
            {
                result.Add(marks[i]);
            }
        }

        return result;
    }

    private static long TicksToSamples(long ticks, int sampleRate) => (long)Math.Round(ticks * (sampleRate / (double)TicksPerSecond));

    private static long SamplesToTicks(long sampleIndex, int sampleRate) =>
        sampleRate <= 0 ? 0 : (long)Math.Round(sampleIndex * (TicksPerSecond / (double)sampleRate));
}
