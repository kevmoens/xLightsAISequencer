using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 4 task 5 ("This includes emitting the timing tracks themselves -- Beats, Bars,
/// Phrases, Sections, and labelled Percussion once Phase 3's per-band energy lands"). Architecture §6:
/// "timing tracks are first-class output, not metadata... labelling those marks is what unlocks the
/// whole filter mechanism." Every emitted <see cref="TimingTrack"/> is contiguous and half-open across
/// <c>[0, songEndTicks)</c> -- the real-file shape <c>XsqExporter</c> already requires (non-overlapping,
/// sorted marks).
/// </summary>
public static class TimingTrackEmitter
{
    private const long TicksPerSecond = 10_000_000L;

    /// <summary>One mark per beat, labelled <c>"downbeat"</c> on <see cref="BeatMarker.BeatWithinBar"/> <c>== 0</c>, empty otherwise.</summary>
    public static TimingTrack Beats(BeatGrid beatGrid, long songEndTicks)
    {
        ArgumentNullException.ThrowIfNull(beatGrid);

        var ordered = beatGrid.Beats.OrderBy(b => b.TimeTicks).ToList();
        var marks = new List<TimingMark>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var end = i + 1 < ordered.Count ? ordered[i + 1].TimeTicks : Math.Max(songEndTicks, ordered[i].TimeTicks + 1);
            if (end <= ordered[i].TimeTicks)
            {
                continue;
            }

            marks.Add(new TimingMark(ordered[i].BeatWithinBar == 0 ? "downbeat" : "", ordered[i].TimeTicks, end));
        }

        return new TimingTrack("Beats", marks);
    }

    /// <summary>One mark per measured bar (see <see cref="BarGrid"/> -- beat-index bar definition, never a fixed tempo period).</summary>
    public static TimingTrack Bars(BarGrid barGrid)
    {
        ArgumentNullException.ThrowIfNull(barGrid);
        return new TimingTrack("Bars", barGrid.Bars.Select(b => new TimingMark("", b.Window.StartTicks, b.Window.EndTicks)).ToList());
    }

    /// <summary>One mark per measured phrase (Phase 3's <c>PhrasePeriodEstimator</c>-fit segmentation), labelled with the phrase's own boundary source.</summary>
    public static TimingTrack Phrases(PhraseSegmentationAnalysis phrases)
    {
        ArgumentNullException.ThrowIfNull(phrases);
        return new TimingTrack(
            "Phrases",
            phrases.Phrases.OrderBy(p => p.StartTicks).Select(p => new TimingMark(p.Source.ToString(), p.StartTicks, p.EndTicks)).ToList());
    }

    /// <summary>One mark per classified section, labelled with its <see cref="SectionKind"/>. Section bar ranges are resolved to ticks via <paramref name="barGrid"/> (a section is bar-indexed, never tick-indexed, at the source).</summary>
    public static TimingTrack Sections(SectionLabelAnalysis labels, BarGrid barGrid)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(barGrid);

        var marks = new List<TimingMark>();
        foreach (var label in labels.Labels.OrderBy(l => l.StartBarIndex))
        {
            var window = barGrid.WindowForBarRange(new BarIndex(label.StartBarIndex), new BarIndex(label.EndBarIndexExclusive));
            if (window is { } w)
            {
                marks.Add(new TimingMark(label.Kind.ToString(), w.StartTicks, w.EndTicks));
            }
        }

        return new TimingTrack("Sections", marks);
    }

    /// <summary>
    /// Build-plan Phase 4 task 5's own "labelled Percussion once Phase 3's per-band energy lands": one
    /// mark per frame of <paramref name="bandEnergy"/>, labelled <c>"kick"</c>/<c>"snare"</c>/<c>"hat"</c>
    /// when that frame is a local peak in the bass/mid/high band respectively (an adaptive
    /// mean+1.5&#963; threshold over a local window, the same shape <c>OnsetDetector</c>'s own
    /// peak-picking already uses), empty otherwise -- a disclosed, simple heuristic (real drum-hit
    /// classification is a separate, harder problem this build does not attempt), not a verified
    /// transcription.
    /// </summary>
    public static TimingTrack Percussion(BandEnergyEnvelope bandEnergy, double sensitivityMultiplier = 1.5, int localWindowRadiusFrames = 8)
    {
        ArgumentNullException.ThrowIfNull(bandEnergy);

        var frames = bandEnergy.Frames;
        var sampleRate = bandEnergy.Settings.Rhythm.AnalysisSampleRate;

        // Each BandEnergyFrame's own analysis window ([StartSample, EndSample)) is FftSize samples wide
        // but frames are only HopSamples apart -- adjacent frames' own windows genuinely overlap in raw
        // sample space whenever HopSamples < FftSize (the normal case). A timing-track mark must be
        // half-open and non-overlapping (XsqExporter's own hard requirement), so each mark here spans
        // from its own frame's start to the *next* frame's start (its own hop), never its own analysis
        // window's end -- the same "mark boundary is the next event's own start" convention every other
        // emitter in this type already uses.
        var marks = new List<TimingMark>();
        for (var i = 0; i < frames.Length; i++)
        {
            var label = PickLabel(frames, i, sensitivityMultiplier, localWindowRadiusFrames);
            var startTicks = SamplesToTicks(frames[i].StartSample, sampleRate);
            var nextStartSample = i + 1 < frames.Length ? frames[i + 1].StartSample : frames[i].EndSample;
            var endTicks = SamplesToTicks(nextStartSample, sampleRate);
            if (endTicks <= startTicks)
            {
                continue;
            }

            marks.Add(new TimingMark(label, startTicks, endTicks));
        }

        // The raw per-hop mark width (~21ms at a typical real-audio sample rate/FFT size) is routinely
        // narrower than the exported frame grid (25ms) -- XsqExporter cannot represent a mark that
        // narrow at all ("rounds to a non-positive duration" / adjacent marks collide after rounding).
        // Coalesce forward into at least one frame interval before the same-label merge below.
        return new TimingTrack("Percussion", CoalesceAdjacentSameLabel(EnforceMinimumMarkDuration(marks, FrameClock.FrameIntervalTicks)));
    }

    private static string PickLabel(BandEnergyFrame[] frames, int index, double sensitivityMultiplier, int radius)
    {
        var (bassMean, bassStdDev) = LocalStatistics(frames, index, radius, f => f.Bass);
        var (midMean, midStdDev) = LocalStatistics(frames, index, radius, f => f.Mid);
        var (highMean, highStdDev) = LocalStatistics(frames, index, radius, f => f.High);

        var bassScore = frames[index].Bass - (bassMean + (sensitivityMultiplier * bassStdDev));
        var midScore = frames[index].Mid - (midMean + (sensitivityMultiplier * midStdDev));
        var highScore = frames[index].High - (highMean + (sensitivityMultiplier * highStdDev));

        if (bassScore <= 0 && midScore <= 0 && highScore <= 0)
        {
            return "";
        }

        // Strongest surviving band wins this frame's label.
        if (bassScore >= midScore && bassScore >= highScore)
        {
            return "kick";
        }

        return midScore >= highScore ? "snare" : "hat";
    }

    private static (double Mean, double StdDev) LocalStatistics(BandEnergyFrame[] frames, int centerIndex, int radius, Func<BandEnergyFrame, double> select)
    {
        var lo = Math.Max(0, centerIndex - radius);
        var hi = Math.Min(frames.Length - 1, centerIndex + radius);

        double sum = 0;
        var count = 0;
        for (var i = lo; i <= hi; i++)
        {
            sum += select(frames[i]);
            count++;
        }

        var mean = count > 0 ? sum / count : 0.0;

        double sumSquaredDeviation = 0;
        for (var i = lo; i <= hi; i++)
        {
            var deviation = select(frames[i]) - mean;
            sumSquaredDeviation += deviation * deviation;
        }

        var stdDev = count > 0 ? Math.Sqrt(sumSquaredDeviation / count) : 0.0;
        return (mean, stdDev);
    }

    /// <summary>
    /// Merges consecutive marks forward (keeping the first non-empty label encountered, since that is
    /// the earliest real event in the merged span) until each emitted mark's own duration is at least
    /// <paramref name="minimumDurationTicks"/> -- never splits a mark, only ever grows one by absorbing
    /// its immediate successors. A final too-short remainder is absorbed into the last emitted mark
    /// rather than dropped, so total coverage of <paramref name="marks"/>' own span is preserved exactly.
    ///
    /// Bug fixed here (found against a real song export -- "Timing track 'Percussion' mark
    /// (2309921088-2309921315 ticks) rounds to a non-positive duration"): the loop below used to fall
    /// through and emit the final run as its own brand-new <see cref="TimingMark"/> even when that run
    /// was still shorter than <paramref name="minimumDurationTicks"/> -- a 227-tick (0.0227ms) remainder
    /// at the very end of the track, nowhere near the 25ms xLights frame grid, exactly contradicting
    /// this method's own doc comment above ("absorbed into the last emitted mark rather than dropped").
    /// Now a too-short final run is genuinely absorbed: its span extends whichever mark <see cref="result"/>
    /// already holds last, rather than becoming a new, separately-emitted short mark -- and if that
    /// trailing run itself carries a real (non-empty) label that the already-emitted mark lacks, the
    /// merged mark keeps that real label rather than silently discarding it (same "a real detected
    /// label must never be masked" precedence the ordinary per-run label selection below already uses).
    /// Only when <paramref name="marks"/> has exactly one entry (nothing yet in <c>result</c> to extend)
    /// does the short run still get emitted as-is -- there is no other mark to absorb it into.
    /// </summary>
    /// <summary>
    /// Reused by <see cref="LyricTimingTrackBuilder"/>'s own <c>Phonemes</c>/<c>Words</c> tracks (same
    /// real-song export failure, different track: "Timing track 'Lyrics_Phonemes' mark
    /// (489986395-490000000 ticks) rounds to a non-positive duration" -- a single viseme window can be
    /// well under the 25ms xLights frame grid, exactly like a single band-energy hop here). Internal
    /// rather than private so that one real fix lives in one place instead of being re-derived per
    /// track type.
    /// </summary>
    internal static List<TimingMark> EnforceMinimumMarkDuration(List<TimingMark> marks, long minimumDurationTicks)
    {
        if (marks.Count == 0)
        {
            return marks;
        }

        var result = new List<TimingMark>();
        var runStartIndex = 0;

        for (var i = 0; i < marks.Count; i++)
        {
            var runStart = marks[runStartIndex].StartTicks;
            var runEnd = marks[i].EndTicks;
            var isLastMark = i == marks.Count - 1;

            if (runEnd - runStart < minimumDurationTicks && !isLastMark)
            {
                continue; // Keep absorbing the next mark into this run.
            }

            // Prefer the first real (non-empty) label within the run -- an unlabeled "nothing of
            // interest" default must never mask a detected kick/snare/hat elsewhere in the same run.
            var label = "";
            for (var j = runStartIndex; j <= i; j++)
            {
                if (!string.IsNullOrEmpty(marks[j].Label))
                {
                    label = marks[j].Label;
                    break;
                }
            }

            if (runEnd - runStart < minimumDurationTicks && result.Count > 0)
            {
                // Final run is still too short: genuinely absorb it into the already-emitted previous
                // mark (extend its end) instead of emitting a new, separately too-short mark. Keep the
                // previous mark's own label if it already had a real one; otherwise promote this run's
                // own real label (if any) onto the merged mark.
                var previous = result[^1];
                var mergedLabel = string.IsNullOrEmpty(previous.Label) ? label : previous.Label;
                result[^1] = previous with { Label = mergedLabel, EndTicks = runEnd };
                break;
            }

            result.Add(new TimingMark(label, runStart, runEnd));
            runStartIndex = i + 1;
        }

        return result;
    }

    /// <summary>Internal rather than private -- reused by <see cref="LyricTimingTrackBuilder"/>, same reason as <see cref="EnforceMinimumMarkDuration"/> above.</summary>
    internal static List<TimingMark> CoalesceAdjacentSameLabel(List<TimingMark> marks)
    {
        if (marks.Count == 0)
        {
            return marks;
        }

        var result = new List<TimingMark> { marks[0] };
        for (var i = 1; i < marks.Count; i++)
        {
            var last = result[^1];
            if (string.Equals(last.Label, marks[i].Label, StringComparison.Ordinal) && last.EndTicks == marks[i].StartTicks)
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

    private static long SamplesToTicks(long sampleIndex, int sampleRate) =>
        sampleRate <= 0 ? 0 : (long)Math.Round(sampleIndex * (TicksPerSecond / (double)sampleRate));
}
