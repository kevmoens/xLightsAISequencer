using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Step 081's own real <see cref="IChoreographyQualityEvaluator"/>: implements this step's own
/// named dimensions, verbatim --
/// <list type="bullet">
/// <item><description><b>legal effects</b> (hard gate): every <see cref="TransitionGesture"/> validates against PHASE-10's own <see cref="TransitionGesture.Validate"/> invariants, targets only props named in <see cref="LoopRun.Scope"/>, stays within <see cref="LoopRun.Scope"/>'s own time window, and never overlaps an already-locked effect (PHASE-03's own lock concept) when <see cref="XlightsAI.Studio.Editing.EditScope.RespectLocks"/> is <see langword="true"/>.</description></item>
/// <item><description><b>density</b> (soft): the fraction of the run's permitted props this candidate actually targets -- the concrete metric behind the phase gate's own "knowingly over-busy 8-bar phrase" scenario.</description></item>
/// <item><description><b>timing</b> (soft): how closely the gesture's own start/end ticks snap to real <see cref="BeatGrid"/> beat markers, when one is supplied.</description></item>
/// <item><description><b>melodic alignment</b> (soft): whether the gesture's own intensity ramp direction agrees with the melody's own pitch-contour direction across the gesture's window, when a <see cref="MelodyTrack"/> is supplied.</description></item>
/// <item><description><b>motif reuse</b> (soft): how closely this candidate's own gesture shape matches the best-matching prior, non-violating sibling attempt in the same run, when any are supplied.</description></item>
/// </list>
/// Every soft metric that needs an analysis input not supplied in <see cref="ChoreographyQualityContext"/>
/// reports <c>Weight = 0</c> and a fixed <c>Value = 0.5</c> with an explicit explanation -- this step's own
/// required failure/edge case, verbatim: "Silence, invalid timing boundaries and missing media must be
/// handled explicitly." Never throws for a malformed/empty candidate -- a hard-gate failure is reported as
/// a <c>0.0</c>-valued <see cref="MetricValue"/> plus a <see cref="QualityReport.Violations"/> entry, the
/// same shape Steps 079/080 already use for "must not auto-commit."
/// </summary>
public sealed class GestureQualityEvaluator : IChoreographyQualityEvaluator
{
    /// <summary>This evaluator's own scoring-recipe identity (PHASE-12 Section 12: "<c>BetterThan</c> must compare quality reports produced with the same evaluator version").</summary>
    public const string EvaluatorVersion = "gesture-quality.v1";

    private const double NoDataValue = 0.5;
    private const double FlatSlopeEpsilon = 1e-9;

    public Task<QualityReport> EvaluateAsync(LoopRun run, CandidateAttempt candidate, ChoreographyQualityContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();

        var gestures = candidate.Gestures ?? [];
        var violations = new List<string>(candidate.Violations);
        var metrics = new List<MetricValue>
        {
            EvaluateLegalEffects(run, candidate, gestures, context.LockedEffects, violations),
            EvaluateDensity(run, gestures),
            EvaluateTiming(gestures, context.BeatGrid),
            EvaluateMelodicAlignment(gestures, context.Melody),
            EvaluateMotifReuse(gestures, context.PriorAttempts),
        };

        var report = new QualityReport(candidate.Id, EvaluatorVersion, metrics, violations);
        report.Validate();
        return Task.FromResult(report);
    }

    /// <summary>
    /// This step's own "legal effects" hard gate: upstream <see cref="CandidateAttempt.Violations"/> (an
    /// already-rejected/malformed generation), per-gesture <see cref="TransitionGesture.Validate"/>
    /// failures, out-of-scope target props, an out-of-scope time window, and an overlap against an
    /// already-locked effect all fail this gate. An empty <paramref name="gestures"/> list (this step's own
    /// required failure/edge case, verbatim: "Silence... must be handled explicitly") also fails this gate
    /// -- there is nothing legal to evaluate.
    /// </summary>
    private static MetricValue EvaluateLegalEffects(
        LoopRun run,
        CandidateAttempt candidate,
        IReadOnlyList<TransitionGesture> gestures,
        IReadOnlyList<EffectInstance>? lockedEffects,
        List<string> violations)
    {
        var reasons = new List<string>();

        if (candidate.Violations.Count > 0)
        {
            reasons.Add($"candidate already carries {candidate.Violations.Count} upstream violation(s).");
        }

        if (gestures.Count == 0)
        {
            reasons.Add("candidate carries no gestures to evaluate (silence/empty generation).");
        }

        foreach (var gesture in gestures)
        {
            try
            {
                gesture.Validate();
            }
            catch (CompositionInvalidException ex)
            {
                reasons.Add(ex.Message);
            }

            var outOfScopeProps = gesture.TargetProps.Where(p => !run.Scope.Models.Contains(p)).ToArray();
            if (outOfScopeProps.Length > 0)
            {
                reasons.Add($"gesture '{gesture.Id}' targets prop(s) outside the run's permitted scope: {string.Join(", ", outOfScopeProps.Select(p => p.Value))}.");
            }

            if (gesture.Window.StartTicks < run.Scope.Time.StartTicks || gesture.Window.EndTicks > run.Scope.Time.EndTicks)
            {
                reasons.Add($"gesture '{gesture.Id}' window [{gesture.Window.StartTicks},{gesture.Window.EndTicks}) exceeds the run's permitted time scope [{run.Scope.Time.StartTicks},{run.Scope.Time.EndTicks}).");
            }

            if (run.Scope.RespectLocks && lockedEffects is { Count: > 0 })
            {
                foreach (var locked in lockedEffects.Where(e => e.Locked))
                {
                    var overlapsTime = locked.StartTicks < gesture.Window.EndTicks && gesture.Window.StartTicks < locked.EndTicks;
                    if (overlapsTime && gesture.TargetProps.Contains(locked.Target))
                    {
                        reasons.Add($"gesture '{gesture.Id}' overlaps locked effect '{locked.Id}' on prop '{locked.Target.Value}'.");
                    }
                }
            }
        }

        var legal = reasons.Count == 0;
        if (!legal)
        {
            violations.Add("Legal effects hard gate failed: " + string.Join(" ", reasons));
        }

        var explanation = legal
            ? $"{gestures.Count} gesture(s) validated, scoped to permitted props/time, with no upstream violations."
            : string.Join(" ", reasons);

        return new MetricValue("LegalEffects", legal ? 1.0 : 0.0, Weight: 1.0, HardGate: true, explanation);
    }

    /// <summary>
    /// The concrete metric behind the phase gate's own "knowingly over-busy 8-bar phrase" scenario: the
    /// fraction of the run's permitted props this candidate actually targets. Always computable -- never
    /// depends on an optional analysis input.
    /// </summary>
    private static MetricValue EvaluateDensity(LoopRun run, IReadOnlyList<TransitionGesture> gestures)
    {
        var permittedCount = run.Scope.Models.Count;
        if (permittedCount == 0)
        {
            return new MetricValue("Density", 0.0, Weight: 1.0, HardGate: false, "The run's permitted scope names no models -- density is undefined; scored as 0.");
        }

        var usedProps = gestures.SelectMany(g => g.TargetProps).Distinct().Count();
        var ratio = usedProps / (double)permittedCount;
        return new MetricValue(
            "Density",
            Math.Clamp(ratio, 0.0, 1.0),
            Weight: 1.0,
            HardGate: false,
            $"{usedProps}/{permittedCount} permitted prop(s) targeted ({ratio:P0}).");
    }

    /// <summary>
    /// How closely each gesture's own start/end ticks snap to a real <see cref="BeatGrid"/> beat marker.
    /// Reports <c>Weight = 0</c> when no beat grid (or fewer than two beats, e.g. silence/no tempo detected)
    /// is supplied -- this step's own required failure/edge case, verbatim: "invalid timing boundaries...
    /// must be handled explicitly."
    /// </summary>
    private static MetricValue EvaluateTiming(IReadOnlyList<TransitionGesture> gestures, BeatGrid? beatGrid)
    {
        if (gestures.Count == 0)
        {
            return NoData("Timing", "no gestures to evaluate timing against.");
        }

        if (beatGrid is null)
        {
            return NoData("Timing", "no BeatGrid supplied.");
        }

        if (beatGrid.Beats.Length < 2)
        {
            return NoData("Timing", "BeatGrid has fewer than two beat markers (silence or no tempo detected) -- no reliable beat period to snap against.");
        }

        var beatTicks = beatGrid.Beats.Select(b => b.TimeTicks).OrderBy(t => t).ToArray();
        var beatPeriod = (double)(beatTicks[^1] - beatTicks[0]) / (beatTicks.Length - 1);
        if (beatPeriod <= 0)
        {
            return NoData("Timing", "BeatGrid's own beat markers carry no positive spacing to measure against.");
        }

        var offsets = new List<double>();
        foreach (var gesture in gestures)
        {
            offsets.Add(NearestBeatOffsetTicks(gesture.Window.StartTicks, beatTicks));
            offsets.Add(NearestBeatOffsetTicks(gesture.Window.EndTicks, beatTicks));
        }

        var avgOffsetRatio = offsets.Average() / beatPeriod;
        var value = 1.0 - Math.Clamp(avgOffsetRatio, 0.0, 1.0);
        return new MetricValue(
            "Timing",
            value,
            Weight: 1.0,
            HardGate: false,
            $"Average gesture boundary offset from the nearest beat is {avgOffsetRatio:P0} of one beat period ({beatPeriod:F0} ticks).");
    }

    private static double NearestBeatOffsetTicks(long tick, long[] sortedBeatTicks)
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

    /// <summary>
    /// Whether the gesture's own intensity ramp direction (<see cref="TransitionGesture.StartIntensity"/>
    /// -&gt; <see cref="TransitionGesture.EndIntensity"/>) agrees with the melody's own pitch-contour
    /// direction (first overlapping note's <see cref="NoteEvent.MidiNote"/> -&gt; last overlapping note's)
    /// across the gesture's own window. Reports <c>Weight = 0</c> when no melody (or no notes at all, or no
    /// notes overlapping the gesture's window) is supplied -- this step's own required failure/edge case,
    /// verbatim: "missing media must be handled explicitly."
    /// </summary>
    private static MetricValue EvaluateMelodicAlignment(IReadOnlyList<TransitionGesture> gestures, MelodyTrack? melody)
    {
        if (gestures.Count == 0)
        {
            return NoData("MelodicAlignment", "no gestures to evaluate melodic alignment against.");
        }

        if (melody is null)
        {
            return NoData("MelodicAlignment", "no MelodyTrack supplied.");
        }

        if (melody.Notes.Count == 0)
        {
            return NoData("MelodicAlignment", "MelodyTrack has no notes (silent source).");
        }

        var scores = new List<double>();
        var consideredGestures = 0;
        foreach (var gesture in gestures)
        {
            var overlapping = melody.Notes
                .Where(n => n.StartTicks < gesture.Window.EndTicks && gesture.Window.StartTicks < n.EndTicks)
                .OrderBy(n => n.StartTicks)
                .ToArray();

            if (overlapping.Length == 0)
            {
                continue;
            }

            consideredGestures++;
            var pitchSlope = overlapping[^1].MidiNote - overlapping[0].MidiNote;
            var intensitySlope = gesture.EndIntensity - gesture.StartIntensity;
            scores.Add(DirectionAgreementScore(pitchSlope, intensitySlope));
        }

        if (consideredGestures == 0)
        {
            return NoData("MelodicAlignment", "no melody notes overlap any gesture's own window.");
        }

        var value = scores.Average();
        return new MetricValue(
            "MelodicAlignment",
            value,
            Weight: 1.0,
            HardGate: false,
            $"{consideredGestures} gesture(s) compared against overlapping melody notes; average direction-agreement score {value:F2}.");
    }

    private static double DirectionAgreementScore(double pitchSlope, double intensitySlope)
    {
        var pitchFlat = Math.Abs(pitchSlope) < FlatSlopeEpsilon;
        var intensityFlat = Math.Abs(intensitySlope) < FlatSlopeEpsilon;

        if (pitchFlat && intensityFlat)
        {
            return 1.0;
        }

        if (pitchFlat || intensityFlat)
        {
            return 0.5;
        }

        return Math.Sign(pitchSlope) == Math.Sign(intensitySlope) ? 1.0 : 0.0;
    }

    /// <summary>
    /// How closely this candidate's own gesture shape matches the best-matching prior, non-violating
    /// sibling attempt recorded so far in the same run. Reports <c>Weight = 0</c> when no prior attempts
    /// are supplied (e.g. this is the run's first attempt).
    /// </summary>
    private static MetricValue EvaluateMotifReuse(IReadOnlyList<TransitionGesture> gestures, IReadOnlyList<CandidateAttempt>? priorAttempts)
    {
        if (gestures.Count == 0)
        {
            return NoData("MotifReuse", "no gestures to compare against prior attempts.");
        }

        var eligiblePriors = (priorAttempts ?? [])
            .Where(a => a.Violations.Count == 0 && a.Gestures is { Count: > 0 })
            .ToArray();

        if (eligiblePriors.Length == 0)
        {
            return NoData("MotifReuse", "no prior, non-violating sibling attempts supplied to compare motif reuse against.");
        }

        var gesture = gestures[0];
        var best = 0.0;
        foreach (var prior in eligiblePriors)
        {
            foreach (var priorGesture in prior.Gestures!)
            {
                var similarity = GestureShapeSimilarity(gesture, priorGesture);
                if (similarity > best)
                {
                    best = similarity;
                }
            }
        }

        return new MetricValue(
            "MotifReuse",
            best,
            Weight: 1.0,
            HardGate: false,
            $"Best-matching similarity to {eligiblePriors.Length} prior sibling attempt(s): {best:F2}.");
    }

    private static double GestureShapeSimilarity(TransitionGesture a, TransitionGesture b)
    {
        if (a.Kind != b.Kind)
        {
            return 0.0;
        }

        var startDelta = Math.Abs(a.StartIntensity - b.StartIntensity);
        var endDelta = Math.Abs(a.EndIntensity - b.EndIntensity);
        return 1.0 - Math.Clamp((startDelta + endDelta) / 2.0, 0.0, 1.0);
    }

    private static MetricValue NoData(string name, string reason) =>
        new(name, NoDataValue, Weight: 0.0, HardGate: false, reason);
}
