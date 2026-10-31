using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rendering;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Refinement.Harness;

/// <summary>
/// Build-plan Phase 7b: the full numeric metric battery from architecture §9 of
/// <c>docs/AI-CHOREOGRAPHY-ARCHITECTURE.md</c>, computed from a <see cref="HarnessSequenceContext"/> into
/// one <see cref="QualityReport"/>. Mirrors <see cref="GestureQualityEvaluator"/>'s own established
/// conventions: every optional-input metric reports <see cref="NoData"/> (Weight 0, Value 0.5, an
/// explicit reason) rather than fabricating a score, and only genuine invariant breaks (an undeclared
/// black frame, a breached energy ceiling) are hard gates -- every other row is a soft, weighted metric.
///
/// <para>
/// Architecture §9 is explicit that this battery is "guardrails, not a quality score" -- it reliably
/// detects breakage, not goodness. <see cref="Evaluate"/> never claims otherwise: every soft
/// <see cref="MetricValue"/> is one dimension of the diff a Phase 7d convergence check compares between
/// iterations, not a single number to maximize.
/// </para>
///
/// <para>
/// <b>Hue computation is deliberately duplicated, not referenced, from
/// <c>XlightsAI.Studio.Choreography.HueBrightnessNormalizer</c>.</b> Pulling in a project reference to
/// Choreography (which does not reference Refinement, so it would not be cyclic) purely to reuse one
/// ~15-line RGB-to-hue conversion was judged not worth the added coupling for this phase; the duplicate
/// is small, self-contained, and independently tested here.
/// </para>
/// </summary>
public static class SequenceMetricBattery
{
    /// <summary>This battery's own scoring-recipe identity (<see cref="QualityReport.AreComparable"/>).</summary>
    public const string EvaluatorVersion = "sequence-harness.v1";

    private const double NoDataValue = 0.5;
    private const double HighSaturationThreshold = 0.15;
    private const int HueFamilyBucketCount = 12;

    public static QualityReport Evaluate(Guid candidateId, HarnessSequenceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var frames = context.Frames ?? [];
        var orderedFrames = frames.OrderBy(f => f.TimeTicks).ToArray();
        var violations = new List<string>();

        var metrics = new List<MetricValue>
        {
            EvaluateRmsCorrelation(orderedFrames, context.TotalDisplayPointCount, context.RmsEnvelope),
            EvaluateLongestNoChangeStretch(orderedFrames, context.BarDurationTicks),
            EvaluateUndeclaredBlackFrames(orderedFrames, context.DeclaredBlackoutWindows, violations),
            EvaluateLargestPixelPlateau(orderedFrames),
            EvaluateMeanHueFamilies(orderedFrames),
            EvaluateActivePaletteSlots(context.Sections),
            EvaluateChangeRateWithinStyleBand(orderedFrames, context.Sections),
            EvaluatePixelHeavyDutyCycle(orderedFrames, context.CategoryByModel),
            EvaluateRepeatedCompositionShare(orderedFrames),
            EvaluateEffectBeatDeviation(orderedFrames, context.EffectBoundaryTicks, context.BeatGrid),
            EvaluateBeatHitRate(context.EffectBoundaryTicks, context.BeatGrid),
            EvaluatePeakSimilarity(orderedFrames, context.PeakTimestampTicks),
            EvaluateSectionEnergyCeiling(orderedFrames, context.TotalDisplayPointCount, context.Sections, violations),
        };

        var report = new QualityReport(candidateId, EvaluatorVersion, metrics, violations);
        report.Validate();
        return report;
    }

    // ---- 1. Lit-pixel curve vs. smoothed RMS correlation (target r >= 0.90) ----
    private static MetricValue EvaluateRmsCorrelation(
        IReadOnlyList<PreviewFrame> frames, int totalPoints, IReadOnlyList<(long TimeTicks, double NormalizedRms)>? rms)
    {
        const string name = "RmsCorrelation";
        if (frames.Count < 2)
        {
            return NoData(name, "fewer than two rendered frames -- no curve to correlate.");
        }

        if (rms is not { Count: > 1 })
        {
            return NoData(name, "no RMS envelope (or fewer than two samples) supplied.");
        }

        if (totalPoints <= 0)
        {
            return NoData(name, "total display point count is not positive.");
        }

        var sortedRms = rms.OrderBy(s => s.TimeTicks).ToArray();
        var activity = new double[frames.Count];
        var matchedRms = new double[frames.Count];
        for (var i = 0; i < frames.Count; i++)
        {
            activity[i] = FrameActivity(frames[i], totalPoints);
            matchedRms[i] = NearestValue(frames[i].TimeTicks, sortedRms);
        }

        var r = PearsonCorrelation(activity, matchedRms);
        if (r is null)
        {
            return NoData(name, "activity or RMS series has zero variance -- correlation is undefined.");
        }

        var value = r.Value <= 0 ? 0.0 : Math.Clamp(r.Value, 0.0, 1.0); // r in [-1,1]; negative/weak correlation scores 0.
        return new MetricValue(name, value, Weight: 1.0, HardGate: false,
            $"Pearson r between duty-rendered lit-pixel activity and smoothed RMS is {r.Value:F2} (target >= 0.90).");
    }

    // ---- 2. Longest stretch with no model changing (target <= 1 bar) ----
    private static MetricValue EvaluateLongestNoChangeStretch(IReadOnlyList<PreviewFrame> frames, long? barDurationTicks)
    {
        const string name = "LongestNoChangeStretch";
        if (frames.Count == 0)
        {
            return NoData(name, "no rendered frames.");
        }

        if (barDurationTicks is not > 0)
        {
            return NoData(name, "no positive bar duration supplied.");
        }

        var longest = 0L;
        var runStartTicks = frames[0].TimeTicks;
        var runSet = IlluminatedModelSet(frames[0]);
        for (var i = 1; i < frames.Count; i++)
        {
            var set = IlluminatedModelSet(frames[i]);
            if (!set.SetEquals(runSet))
            {
                longest = Math.Max(longest, frames[i - 1].TimeTicks - runStartTicks);
                runStartTicks = frames[i].TimeTicks;
                runSet = set;
            }
        }

        longest = Math.Max(longest, frames[^1].TimeTicks - runStartTicks);

        var bars = longest / (double)barDurationTicks.Value;
        var value = bars <= 1.0 ? 1.0 : Math.Clamp(1.0 - ((bars - 1.0) / 7.0), 0.0, 1.0);
        return new MetricValue(name, value, Weight: 1.0, HardGate: false,
            $"Longest stretch with no illuminated-model-set change is {bars:F2} bar(s) (target <= 1).");
    }

    // ---- 3. Undeclared fully-black frames (target 0 -- HARD GATE) ----
    private static MetricValue EvaluateUndeclaredBlackFrames(
        IReadOnlyList<PreviewFrame> frames, IReadOnlyList<TimeWindow>? declaredBlackoutWindows, List<string> violations)
    {
        const string name = "UndeclaredBlackFrames";
        var windows = declaredBlackoutWindows ?? [];
        var undeclared = frames.Count(f => f.Illuminated.Count == 0 && !windows.Any(w => f.TimeTicks >= w.StartTicks && f.TimeTicks < w.EndTicks));

        if (undeclared > 0)
        {
            violations.Add($"{undeclared} fully-black frame(s) occur outside every declared blackout window.");
        }

        return new MetricValue(name, undeclared == 0 ? 1.0 : 0.0, Weight: 1.0, HardGate: true,
            undeclared == 0
                ? $"No undeclared fully-black frames across {frames.Count} rendered frame(s)."
                : $"{undeclared} undeclared fully-black frame(s) found.");
    }

    // ---- 4. Largest single pixel-level plateau, % of runtime (target <= 20%) ----
    private static MetricValue EvaluateLargestPixelPlateau(IReadOnlyList<PreviewFrame> frames)
    {
        const string name = "LargestPixelPlateau";
        if (frames.Count == 0)
        {
            return NoData(name, "no rendered frames.");
        }

        var modeCount = frames.GroupBy(f => f.Illuminated.Count).Max(g => g.Count());
        var fraction = modeCount / (double)frames.Count;
        var value = fraction <= 0.20 ? 1.0 : Math.Clamp(1.0 - ((fraction - 0.20) / 0.80), 0.0, 1.0);
        return new MetricValue(name, value, Weight: 1.0, HardGate: false,
            $"The most common single lit-pixel-count value accounts for {fraction:P0} of runtime (target <= 20%).");
    }

    // ---- 5. Mean hue families per frame (target <= 2.5) ----
    private static MetricValue EvaluateMeanHueFamilies(IReadOnlyList<PreviewFrame> frames)
    {
        const string name = "MeanHueFamilies";
        if (frames.Count == 0)
        {
            return NoData(name, "no rendered frames.");
        }

        var totalFamilies = 0;
        foreach (var frame in frames)
        {
            var families = new HashSet<int>();
            foreach (var point in frame.Illuminated)
            {
                var (hue, saturation, _) = ToHsv(point.R, point.G, point.B);
                if (saturation < HighSaturationThreshold)
                {
                    continue; // Near-grey/white carries no meaningful hue family.
                }

                families.Add((int)(hue / (360.0 / HueFamilyBucketCount)));
            }

            totalFamilies += families.Count;
        }

        var mean = totalFamilies / (double)frames.Count;
        var value = mean <= 2.5 ? 1.0 : Math.Clamp(1.0 - ((mean - 2.5) / (HueFamilyBucketCount - 2.5)), 0.0, 1.0);
        return new MetricValue(name, value, Weight: 1.0, HardGate: false,
            $"Mean distinct hue families per frame is {mean:F2} (target <= 2.5).");
    }

    // ---- 6. Active palette slots per section, within style band ----
    private static MetricValue EvaluateActivePaletteSlots(IReadOnlyList<HarnessSection>? sections)
    {
        const string name = "ActivePaletteSlots";
        if (sections is not { Count: > 0 })
        {
            return NoData(name, "no section palette data supplied.");
        }

        var inBand = sections.Count(s => s.DeclaredPaletteSlotCount >= s.StyleMinPaletteSlots && s.DeclaredPaletteSlotCount <= s.StyleMaxPaletteSlots);
        var value = inBand / (double)sections.Count;
        return new MetricValue(name, value, Weight: 1.0, HardGate: false,
            $"{inBand}/{sections.Count} section(s) declare a palette slot count within their style's own band.");
    }

    // ---- 6b. Composition-change rate per section, within the configured style's own density band (build-plan Phase 8 task 1) ----
    private static MetricValue EvaluateChangeRateWithinStyleBand(IReadOnlyList<PreviewFrame> frames, IReadOnlyList<HarnessSection>? sections)
    {
        const string name = "ChangeRateWithinStyleBand";
        var checkedSections = (sections ?? []).Where(s => s.StyleMinChangeRatePerSecond is not null && s.StyleMaxChangeRatePerSecond is not null).ToList();
        if (checkedSections.Count == 0)
        {
            return NoData(name, "no section declares a style density (change-rate) band.");
        }

        if (frames.Count < 2)
        {
            return NoData(name, "fewer than two rendered frames -- no change rate to measure.");
        }

        const long ticksPerSecond = 10_000_000L;
        var inBand = 0;
        foreach (var section in checkedSections)
        {
            var inWindow = frames.Where(f => f.TimeTicks >= section.Window.StartTicks && f.TimeTicks < section.Window.EndTicks).ToArray();
            var durationSeconds = (section.Window.EndTicks - section.Window.StartTicks) / (double)ticksPerSecond;
            if (inWindow.Length < 2 || durationSeconds <= 0)
            {
                continue;
            }

            var changes = 0;
            for (var i = 1; i < inWindow.Length; i++)
            {
                if (!IlluminatedModelSet(inWindow[i]).SetEquals(IlluminatedModelSet(inWindow[i - 1])))
                {
                    changes++;
                }
            }

            var rate = changes / durationSeconds;
            if (rate >= section.StyleMinChangeRatePerSecond!.Value && rate <= section.StyleMaxChangeRatePerSecond!.Value)
            {
                inBand++;
            }
        }

        var value = inBand / (double)checkedSections.Count;
        return new MetricValue(name, value, Weight: 1.0, HardGate: false,
            $"{inBand}/{checkedSections.Count} section(s) with a declared style density band measured a composition-change rate within it.");
    }

    // ---- 7. Pixel-heavy prop duty cycle (target >= 65% matrices, >= 40% trees/focal) ----
    private static MetricValue EvaluatePixelHeavyDutyCycle(IReadOnlyList<PreviewFrame> frames, IReadOnlyDictionary<ModelId, ModelCategory>? categoryByModel)
    {
        const string name = "PixelHeavyDutyCycle";
        if (frames.Count == 0)
        {
            return NoData(name, "no rendered frames.");
        }

        if (categoryByModel is not { Count: > 0 })
        {
            return NoData(name, "no per-model category data supplied.");
        }

        var litFrameCountByModel = new Dictionary<ModelId, int>();
        foreach (var frame in frames)
        {
            foreach (var model in IlluminatedModelSet(frame))
            {
                litFrameCountByModel[model] = litFrameCountByModel.GetValueOrDefault(model) + 1;
            }
        }

        var checkedTargets = new List<(ModelId Model, double Duty, double Target)>();
        foreach (var (model, category) in categoryByModel)
        {
            var duty = litFrameCountByModel.GetValueOrDefault(model) / (double)frames.Count;
            if (category == ModelCategory.Matrix)
            {
                checkedTargets.Add((model, duty, 0.65));
            }
            else if (category == ModelCategory.Tree)
            {
                checkedTargets.Add((model, duty, 0.40));
            }
        }

        if (checkedTargets.Count == 0)
        {
            return NoData(name, "layout has no Matrix or Tree category models to check duty cycle against.");
        }

        var met = checkedTargets.Count(t => t.Duty >= t.Target);
        var value = met / (double)checkedTargets.Count;
        return new MetricValue(name, value, Weight: 1.0, HardGate: false,
            $"{met}/{checkedTargets.Count} pixel-heavy prop(s) meet their own duty-cycle target (Matrix >= 65%, Tree >= 40%).");
    }

    // ---- 8. Max share of runtime on one repeated composition (target <= 2%) ----
    private static MetricValue EvaluateRepeatedCompositionShare(IReadOnlyList<PreviewFrame> frames)
    {
        const string name = "RepeatedCompositionShare";
        if (frames.Count == 0)
        {
            return NoData(name, "no rendered frames.");
        }

        var modeCount = frames.GroupBy(CompositionSignature).Max(g => g.Count());
        var fraction = modeCount / (double)frames.Count;
        var value = fraction <= 0.02 ? 1.0 : Math.Clamp(1.0 - ((fraction - 0.02) / 0.98), 0.0, 1.0);
        return new MetricValue(name, value, Weight: 1.0, HardGate: false,
            $"The most-repeated exact illuminated-model composition accounts for {fraction:P1} of runtime (target <= 2%).");
    }

    // ---- 9. Effect-boundary deviation from nearest beat (target <= 1 frame) ----
    private static MetricValue EvaluateEffectBeatDeviation(IReadOnlyList<PreviewFrame> frames, IReadOnlyList<long>? effectBoundaryTicks, BeatGrid? beatGrid)
    {
        const string name = "EffectBeatDeviation";
        if (effectBoundaryTicks is not { Count: > 0 })
        {
            return NoData(name, "no effect boundary ticks supplied.");
        }

        if (beatGrid is null || beatGrid.Beats.Length < 2)
        {
            return NoData(name, "no usable BeatGrid supplied (missing, or fewer than two beats).");
        }

        var frameDuration = MedianFrameDuration(frames);
        if (frameDuration is not > 0)
        {
            return NoData(name, "cannot determine a frame duration from the rendered frames (fewer than two distinct timestamps).");
        }

        var beatTicks = beatGrid.Beats.Select(b => b.TimeTicks).OrderBy(t => t).ToArray();
        var avgDeviation = effectBoundaryTicks.Select(t => NearestBeatOffset(t, beatTicks)).Average();
        var avgDeviationFrames = avgDeviation / frameDuration.Value;
        var value = avgDeviationFrames <= 1.0 ? 1.0 : Math.Clamp(1.0 - ((avgDeviationFrames - 1.0) / 9.0), 0.0, 1.0);
        return new MetricValue(name, value, Weight: 1.0, HardGate: false,
            $"Average effect-boundary deviation from the nearest beat is {avgDeviationFrames:F2} frame(s) (target <= 1).");
    }

    // ---- 10. Beat-hit rate, within corpus-derived band ----
    private static MetricValue EvaluateBeatHitRate(IReadOnlyList<long>? effectBoundaryTicks, BeatGrid? beatGrid)
    {
        const string name = "BeatHitRate";
        // Disclosed placeholder band: architecture §9 names "within corpus-derived band" but no concrete
        // corpus-measured numeric band is recorded anywhere in this repo's docs yet (same disclosed-gap
        // convention as DutyCycleTable's unmeasured-default case) -- [0.30, 0.95] is a deliberately wide
        // sanity band pending real corpus calibration, not a claimed measured target.
        const double bandMin = 0.30;
        const double bandMax = 0.95;

        if (effectBoundaryTicks is not { Count: > 0 })
        {
            return NoData(name, "no effect boundary ticks supplied.");
        }

        if (beatGrid is null || beatGrid.Beats.Length < 2)
        {
            return NoData(name, "no usable BeatGrid supplied (missing, or fewer than two beats).");
        }

        var beatTicks = beatGrid.Beats.Select(b => b.TimeTicks).OrderBy(t => t).ToArray();
        var period = (double)(beatTicks[^1] - beatTicks[0]) / (beatTicks.Length - 1);
        if (period <= 0)
        {
            return NoData(name, "BeatGrid's own beat markers carry no positive spacing.");
        }

        var tolerance = period / 4.0;
        var hits = beatTicks.Count(b => effectBoundaryTicks.Any(e => Math.Abs(e - b) <= tolerance));
        var rate = hits / (double)beatTicks.Length;
        var inBand = rate >= bandMin && rate <= bandMax;
        var value = inBand ? 1.0 : Math.Clamp(1.0 - (Math.Min(Math.Abs(rate - bandMin), Math.Abs(rate - bandMax)) / 0.30), 0.0, 1.0);
        return new MetricValue(name, value, Weight: 1.0, HardGate: false,
            $"{hits}/{beatTicks.Length} beats ({rate:P0}) have an effect boundary within one quarter-beat (placeholder sanity band [{bandMin:P0},{bandMax:P0}], not corpus-calibrated).");
    }

    // ---- 11. Pairwise visual similarity between designated peaks (below threshold) ----
    private static MetricValue EvaluatePeakSimilarity(IReadOnlyList<PreviewFrame> frames, IReadOnlyList<long>? peakTimestampTicks)
    {
        const string name = "PeakSimilarity";
        const double similarityThreshold = 0.60;

        if (frames.Count == 0)
        {
            return NoData(name, "no rendered frames.");
        }

        if (peakTimestampTicks is not { Count: > 1 })
        {
            return NoData(name, "fewer than two designated peak timestamps supplied.");
        }

        var peakSets = peakTimestampTicks.Select(t => IlluminatedModelSet(NearestFrame(t, frames))).ToArray();
        var maxSimilarity = 0.0;
        for (var i = 0; i < peakSets.Length; i++)
        {
            for (var j = i + 1; j < peakSets.Length; j++)
            {
                maxSimilarity = Math.Max(maxSimilarity, JaccardSimilarity(peakSets[i], peakSets[j]));
            }
        }

        var value = maxSimilarity <= similarityThreshold ? 1.0 : Math.Clamp(1.0 - ((maxSimilarity - similarityThreshold) / (1.0 - similarityThreshold)), 0.0, 1.0);
        return new MetricValue(name, value, Weight: 1.0, HardGate: false,
            $"The most similar pair of designated peaks shares {maxSimilarity:P0} of their illuminated models (target below {similarityThreshold:P0}).");
    }

    // ---- 12. Per-section energy vs. declared ceiling (never exceeded -- HARD GATE) ----
    private static MetricValue EvaluateSectionEnergyCeiling(
        IReadOnlyList<PreviewFrame> frames, int totalPoints, IReadOnlyList<HarnessSection>? sections, List<string> violations)
    {
        const string name = "SectionEnergyCeiling";
        const double tolerance = 0.01;

        if (sections is not { Count: > 0 })
        {
            return NoData(name, "no section ceiling data supplied.");
        }

        if (frames.Count == 0 || totalPoints <= 0)
        {
            return NoData(name, "no rendered frames (or no positive total point count) to measure section energy against.");
        }

        var breaches = new List<string>();
        foreach (var section in sections)
        {
            var inWindow = frames.Where(f => f.TimeTicks >= section.Window.StartTicks && f.TimeTicks < section.Window.EndTicks).ToArray();
            if (inWindow.Length == 0)
            {
                continue;
            }

            var measured = inWindow.Max(f => FrameActivity(f, totalPoints));
            if (measured > section.EnergyCeiling + tolerance)
            {
                breaches.Add($"[{section.Window.StartTicks},{section.Window.EndTicks}) measured {measured:P0} > declared ceiling {section.EnergyCeiling:P0}");
            }
        }

        if (breaches.Count > 0)
        {
            violations.Add($"{breaches.Count} section(s) exceeded their declared energy ceiling: {string.Join("; ", breaches)}");
        }

        return new MetricValue(name, breaches.Count == 0 ? 1.0 : 0.0, Weight: 1.0, HardGate: true,
            breaches.Count == 0
                ? $"All {sections.Count} section(s) stayed within their own declared energy ceiling."
                : string.Join("; ", breaches));
    }

    // ---- shared helpers ----

    private static double FrameActivity(PreviewFrame frame, int totalPoints)
    {
        if (totalPoints <= 0)
        {
            return 0.0;
        }

        var sum = 0.0;
        foreach (var point in frame.Illuminated)
        {
            sum += ((0.299 * point.R) + (0.587 * point.G) + (0.114 * point.B)) / 255.0;
        }

        return sum / totalPoints;
    }

    private static HashSet<ModelId> IlluminatedModelSet(PreviewFrame frame) => frame.Illuminated.Select(p => p.Model).ToHashSet();

    private static string CompositionSignature(PreviewFrame frame) =>
        string.Join(",", IlluminatedModelSet(frame).Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal));

    private static double JaccardSimilarity(HashSet<ModelId> a, HashSet<ModelId> b)
    {
        if (a.Count == 0 && b.Count == 0)
        {
            return 1.0;
        }

        var intersection = a.Count(b.Contains);
        var union = a.Count + b.Count - intersection;
        return union == 0 ? 0.0 : intersection / (double)union;
    }

    private static PreviewFrame NearestFrame(long timeTicks, IReadOnlyList<PreviewFrame> frames) =>
        frames.OrderBy(f => Math.Abs(f.TimeTicks - timeTicks)).First();

    private static double NearestValue(long timeTicks, IReadOnlyList<(long TimeTicks, double NormalizedRms)> sorted)
    {
        var nearest = sorted[0];
        var bestDistance = Math.Abs(nearest.TimeTicks - timeTicks);
        foreach (var sample in sorted)
        {
            var distance = Math.Abs(sample.TimeTicks - timeTicks);
            if (distance < bestDistance)
            {
                nearest = sample;
                bestDistance = distance;
            }
        }

        return nearest.NormalizedRms;
    }

    private static double NearestBeatOffset(long tick, long[] sortedBeatTicks)
    {
        var index = Array.BinarySearch(sortedBeatTicks, tick);
        if (index >= 0)
        {
            return 0.0;
        }

        var insertionPoint = ~index;
        var distances = new List<long>();
        if (insertionPoint > 0)
        {
            distances.Add(Math.Abs(tick - sortedBeatTicks[insertionPoint - 1]));
        }

        if (insertionPoint < sortedBeatTicks.Length)
        {
            distances.Add(Math.Abs(sortedBeatTicks[insertionPoint] - tick));
        }

        return distances.Min();
    }

    private static long? MedianFrameDuration(IReadOnlyList<PreviewFrame> frames)
    {
        var distinctTicks = frames.Select(f => f.TimeTicks).Distinct().OrderBy(t => t).ToArray();
        if (distinctTicks.Length < 2)
        {
            return null;
        }

        var deltas = new long[distinctTicks.Length - 1];
        for (var i = 1; i < distinctTicks.Length; i++)
        {
            deltas[i - 1] = distinctTicks[i] - distinctTicks[i - 1];
        }

        Array.Sort(deltas);
        return deltas[deltas.Length / 2];
    }

    private static double? PearsonCorrelation(double[] a, double[] b)
    {
        var n = a.Length;
        var meanA = a.Average();
        var meanB = b.Average();

        var covariance = 0.0;
        var varianceA = 0.0;
        var varianceB = 0.0;
        for (var i = 0; i < n; i++)
        {
            var da = a[i] - meanA;
            var db = b[i] - meanB;
            covariance += da * db;
            varianceA += da * da;
            varianceB += db * db;
        }

        if (varianceA <= 0 || varianceB <= 0)
        {
            return null;
        }

        return covariance / Math.Sqrt(varianceA * varianceB);
    }

    /// <summary>Small, self-contained RGB-&gt;HSV conversion (hue degrees, saturation, value) -- see the type-level remarks on why this duplicates <c>HueBrightnessNormalizer.ToHsv</c> rather than referencing it.</summary>
    private static (double Hue, double Saturation, double Value) ToHsv(byte r, byte g, byte b)
    {
        double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var delta = max - min;

        double hue;
        if (delta <= 0)
        {
            hue = 0;
        }
        else if (max == rf)
        {
            hue = 60 * (((gf - bf) / delta) % 6);
        }
        else if (max == gf)
        {
            hue = 60 * (((bf - rf) / delta) + 2);
        }
        else
        {
            hue = 60 * (((rf - gf) / delta) + 4);
        }

        if (hue < 0)
        {
            hue += 360;
        }

        var saturation = max <= 0 ? 0 : delta / max;
        return (hue, saturation, max);
    }

    private static MetricValue NoData(string name, string reason) => new(name, NoDataValue, Weight: 0.0, HardGate: false, reason);
}
