using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Step 082's own real <see cref="ITargetedRepairStrategy"/>: selects the single weakest reported
/// dimension from a Step 081 <see cref="QualityReport"/> and applies exactly one narrow, deterministic
/// mutation addressing only that dimension -- this step's own implementation guidance, verbatim: "Select a
/// targeted mutation for failed metric (shorten holds, reduce density, repair direction)."
/// <list type="bullet">
/// <item><description><b>LegalEffects</b> (always repaired first, ahead of any soft metric, when it is failing): clips every gesture's own window to <see cref="LoopRun.Scope"/>'s own time window, drops any target prop outside <see cref="XlightsAI.Studio.Editing.EditScope.Models"/>, and -- this step's own required acceptance, verbatim, "respects locks" -- drops any target prop that would still overlap an already-locked <see cref="XlightsAI.Studio.Xlights.EffectInstance"/> when <see cref="XlightsAI.Studio.Editing.EditScope.RespectLocks"/> is <see langword="true"/>.</description></item>
/// <item><description><b>Timing</b> ("shorten holds"): moves each gesture's own window boundary to the nearest real <see cref="XlightsAI.Studio.Rhythm.BeatGrid"/> beat marker, clipped back into the run's own permitted time scope -- never touches <see cref="TransitionGesture.TargetProps"/> or intensities.</description></item>
/// <item><description><b>Density</b> ("reduce density"): drops exactly one target prop (the lexicographically last, for a deterministic tie-break) from every gesture -- never touches the window or intensities.</description></item>
/// <item><description><b>MelodicAlignment</b> ("repair direction"): swaps each gesture's own <see cref="TransitionGesture.StartIntensity"/>/<see cref="TransitionGesture.EndIntensity"/> -- never touches the window or target props.</description></item>
/// </list>
/// <b>Disclosed scope decision:</b> <c>MotifReuse</c> is deliberately excluded from this strategy's own
/// repairable-metric set. PHASE-12 Step 081's own disclosed ADR (STEP-081.md Section 3.5) already narrowed
/// "motif reuse" to mean "similarity to this run's own prior sibling attempts," not a property any single
/// gesture's own shape can be locally mutated to fix without first knowing which prior shape to converge
/// toward -- none of this step's own three named example mutations ("shorten holds, reduce density, repair
/// direction") describes a motif-reuse mutation, and inventing one here would be exactly the kind of
/// unrequested, speculative generalization <c>CLAUDE.md</c>'s own "no designing for hypothetical future
/// requirements" rule warns against.
/// </summary>
public sealed class GestureTargetedRepairStrategy : ITargetedRepairStrategy
{
    private static readonly string[] RepairableSoftMetricNames = ["Timing", "MelodicAlignment", "Density"];

    public Task<RepairOutcome> RepairAsync(LoopRun run, CandidateAttempt candidate, QualityReport report, ChoreographyQualityContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();

        if (report.CandidateId != candidate.Id)
        {
            throw new RefinementRunInvalidException(
                $"QualityReport '{report.CandidateId}' does not describe CandidateAttempt '{candidate.Id}'; a repair strategy must be given the candidate's own report.");
        }

        var gestures = candidate.Gestures;
        if (gestures is not { Count: > 0 })
        {
            // This step's own required failure/edge case, verbatim: "Malformed proposal... must not
            // auto-commit." There is no gesture content to mutate -- returning the same instance, untouched,
            // is itself the "must not auto-commit" behavior; no gesture is fabricated from nothing.
            return Task.FromResult(new RepairOutcome(
                candidate,
                TargetedMetricName: null,
                "candidate carries no gestures to repair (malformed/empty generation); returned unchanged."));
        }

        var legalEffects = report.Metrics.FirstOrDefault(m => m.Name == "LegalEffects");
        if (legalEffects is { HardGate: true, Value: 0.0 })
        {
            return Task.FromResult(RepairLegalEffects(run, candidate, gestures, context));
        }

        var target = SelectWeakestRepairableSoftMetric(report, gestures);
        if (target is null)
        {
            return Task.FromResult(new RepairOutcome(
                candidate,
                TargetedMetricName: null,
                "no repairable soft metric is reported with non-zero weight (every weighted repairable dimension is already perfect/at its minimum, or only MotifReuse carries weight -- not independently repairable); returned unchanged."));
        }

        var outcome = target.Name switch
        {
            "Density" => RepairDensity(run, candidate, gestures),
            "Timing" => RepairTiming(run, candidate, gestures, context),
            "MelodicAlignment" => RepairMelodicAlignmentDirection(run, candidate, gestures),
            _ => throw new RefinementRunInvalidException($"No repair mutation is defined for metric '{target.Name}'."),
        };

        return Task.FromResult(outcome);
    }

    /// <summary>
    /// Two-tier selection, disclosed below (both tiers only ever consider a metric with positive
    /// <see cref="MetricValue.Weight"/> -- a <c>Weight == 0</c> "no data" metric is never a repair target):
    /// <list type="number">
    /// <item><description><b>Timing</b>/<b>MelodicAlignment</b> ("failing," literally -- PHASE-12 Section 3's own architectural design notes, verbatim: "targeted repair strategy for weakest failing metric"): both already follow <see cref="MetricValue.Value"/>'s own documented "higher is always better" contract, so a real failure is any <see cref="MetricValue.Value"/> below the maximum of <c>1.0</c>; the lower of the two is addressed first.</description></item>
    /// <item><description><b>Density</b> (disclosed deviation from that same "higher is always better" reading): PHASE-12 Section 1's own user story names a "knowingly over-busy 8-bar phrase" as the scenario this phase exists to address, and this step's own implementation guidance names the mutation "reduce density," literally -- never "increase density." A maximal Density score (every permitted prop targeted) is exactly the over-busy case the phase gate's own demonstration exercises, not a success to leave alone. Density is therefore considered for repair whenever any gesture still has more than one target prop to drop, regardless of its own numeric <see cref="MetricValue.Value"/> -- but only after Timing/MelodicAlignment, so a candidate that is both over-busy and badly mistimed addresses timing first (this step's own required acceptance, verbatim, read together with PHASE-12 Section 3: "weakest failing metric" -- timing/melody failures are failures in the metric's own documented sense; density is not, so it yields priority).</description></item>
    /// </list>
    /// </summary>
    private static MetricValue? SelectWeakestRepairableSoftMetric(QualityReport report, IReadOnlyList<TransitionGesture> gestures)
    {
        var failing = report.Metrics
            .Where(m => !m.HardGate && m.Weight > 0.0 && m.Value < 1.0 && (m.Name == "Timing" || m.Name == "MelodicAlignment"))
            .OrderBy(m => m.Value)
            .ThenBy(m => Array.IndexOf(RepairableSoftMetricNames, m.Name))
            .FirstOrDefault();
        if (failing is not null)
        {
            return failing;
        }

        var density = report.Metrics.FirstOrDefault(m => m.Name == "Density" && !m.HardGate && m.Weight > 0.0);
        return density is not null && gestures.Any(g => g.TargetProps.Count > 1) ? density : null;
    }

    /// <summary>
    /// Clips every gesture to the run's own permitted time/model scope and drops any target prop that
    /// would still overlap an already-locked effect -- this step's own required acceptance, verbatim:
    /// "respects locks." Never widens a window or adds a prop; only ever removes/clips.
    /// </summary>
    private static RepairOutcome RepairLegalEffects(
        LoopRun run,
        CandidateAttempt source,
        IReadOnlyList<TransitionGesture> gestures,
        ChoreographyQualityContext context)
    {
        var scopeStart = run.Scope.Time.StartTicks;
        var scopeEnd = run.Scope.Time.EndTicks;
        var lockedEffects = (context.LockedEffects ?? []).Where(e => e.Locked).ToArray();

        var repairedGestures = new List<TransitionGesture>();
        var violations = new List<string>();

        foreach (var gesture in gestures)
        {
            var clippedStart = Math.Max(gesture.Window.StartTicks, scopeStart);
            var clippedEnd = Math.Min(gesture.Window.EndTicks, scopeEnd);

            if (clippedStart >= clippedEnd)
            {
                violations.Add($"gesture '{gesture.Id}' could not be repaired: clipping to the run's permitted time scope [{scopeStart},{scopeEnd}) collapsed its window to zero length.");
                continue;
            }

            var inScopeProps = gesture.TargetProps.Where(p => run.Scope.Models.Contains(p)).ToArray();

            var allowedProps = run.Scope.RespectLocks && lockedEffects.Length > 0
                ? inScopeProps
                    .Where(p => !lockedEffects.Any(locked =>
                        locked.Target.Equals(p) && locked.StartTicks < clippedEnd && clippedStart < locked.EndTicks))
                    .ToArray()
                : inScopeProps;

            if (allowedProps.Length == 0)
            {
                violations.Add($"gesture '{gesture.Id}' could not be repaired: no permitted, un-locked prop remains after clipping to the run's own scope.");
                continue;
            }

            var repairedGesture = gesture with
            {
                Window = new TimeWindow(clippedStart, clippedEnd),
                TargetProps = allowedProps,
            };

            try
            {
                repairedGesture.Validate();
            }
            catch (CompositionInvalidException ex)
            {
                violations.Add(ex.Message);
                continue;
            }

            repairedGestures.Add(repairedGesture);
        }

        if (repairedGestures.Count == 0)
        {
            // Never fabricate a legal gesture out of nothing -- this step's own required failure/edge case,
            // verbatim: "Malformed proposal... must not auto-commit."
            var unrepaired = BuildRepairedAttempt(run, source, "LegalEffects", [],
                violations.Count > 0 ? violations : ["LegalEffects repair removed every gesture; no legal content remains."]);
            return new RepairOutcome(unrepaired, "LegalEffects",
                "every gesture was out-of-scope/locked beyond repair; the repaired candidate carries no gestures and remains rejected.");
        }

        var repaired = BuildRepairedAttempt(run, source, "LegalEffects", repairedGestures, violations);
        return new RepairOutcome(repaired, "LegalEffects",
            $"clipped {gestures.Count} gesture(s) to the run's permitted scope/time and removed any locked-effect overlap; {repairedGestures.Count} gesture(s) remain legal.");
    }

    /// <summary>"reduce density" (this step's own implementation guidance, verbatim): drops exactly one target prop -- the lexicographically last, for a deterministic tie-break -- from every gesture. Never touches a gesture's own window or intensities.</summary>
    private static RepairOutcome RepairDensity(LoopRun run, CandidateAttempt source, IReadOnlyList<TransitionGesture> gestures)
    {
        var violations = new List<string>();
        var repairedGestures = new List<TransitionGesture>();

        foreach (var gesture in gestures)
        {
            if (gesture.TargetProps.Count <= 1)
            {
                violations.Add($"gesture '{gesture.Id}' could not be density-repaired: it already targets the minimum of one prop.");
                continue;
            }

            var reducedProps = gesture.TargetProps
                .Select(p => p.Value)
                .OrderBy(v => v, StringComparer.Ordinal)
                .SkipLast(1)
                .Select(v => new ModelId(v))
                .ToArray();

            var repairedGesture = gesture with { TargetProps = reducedProps };
            repairedGesture.Validate();
            repairedGestures.Add(repairedGesture);
        }

        if (repairedGestures.Count == 0)
        {
            return new RepairOutcome(source, "Density",
                "every gesture already targets the minimum of one prop; density cannot be reduced further -- returned unchanged.");
        }

        var repaired = BuildRepairedAttempt(run, source, "Density", repairedGestures, violations);
        return new RepairOutcome(repaired, "Density",
            $"dropped one target prop from {repairedGestures.Count} gesture(s) to reduce density; window and intensities are unchanged.");
    }

    /// <summary>"shorten holds" (this step's own implementation guidance, verbatim): moves each window boundary to the nearest real beat marker, clipped back into the run's own permitted time scope. Never touches a gesture's own target props or intensities.</summary>
    private static RepairOutcome RepairTiming(LoopRun run, CandidateAttempt source, IReadOnlyList<TransitionGesture> gestures, ChoreographyQualityContext context)
    {
        var beatGrid = context.BeatGrid;
        if (beatGrid is null || beatGrid.Beats.Length < 2)
        {
            return new RepairOutcome(source, "Timing",
                "no usable BeatGrid supplied (none, or fewer than two beat markers); timing cannot be repaired against an absent/unreliable beat grid -- returned unchanged.");
        }

        var beatTicks = beatGrid.Beats.Select(b => b.TimeTicks).OrderBy(t => t).ToArray();
        var scopeStart = run.Scope.Time.StartTicks;
        var scopeEnd = run.Scope.Time.EndTicks;

        var repairedGestures = new List<TransitionGesture>();
        var violations = new List<string>();

        foreach (var gesture in gestures)
        {
            var snappedStart = Math.Clamp(NearestBeatTick(gesture.Window.StartTicks, beatTicks), scopeStart, scopeEnd);
            var snappedEnd = Math.Clamp(NearestBeatTick(gesture.Window.EndTicks, beatTicks), scopeStart, scopeEnd);

            if (snappedStart >= snappedEnd)
            {
                violations.Add($"gesture '{gesture.Id}' could not be timing-repaired: snapping both boundaries to the nearest beat collapsed its window to zero length.");
                continue;
            }

            var repairedGesture = gesture with { Window = new TimeWindow(snappedStart, snappedEnd) };
            repairedGesture.Validate();
            repairedGestures.Add(repairedGesture);
        }

        if (repairedGestures.Count == 0)
        {
            return new RepairOutcome(source, "Timing",
                "snapping every gesture's own window to the nearest beat collapsed it to zero length; timing cannot be repaired -- returned unchanged.");
        }

        var repaired = BuildRepairedAttempt(run, source, "Timing", repairedGestures, violations);
        return new RepairOutcome(repaired, "Timing",
            $"snapped {repairedGestures.Count} gesture(s) own window boundaries to the nearest beat marker; target props and intensities are unchanged.");
    }

    private static long NearestBeatTick(long tick, long[] sortedBeatTicks)
    {
        var index = Array.BinarySearch(sortedBeatTicks, tick);
        if (index >= 0)
        {
            return sortedBeatTicks[index];
        }

        var insertionPoint = ~index;
        if (insertionPoint == 0)
        {
            return sortedBeatTicks[0];
        }

        if (insertionPoint == sortedBeatTicks.Length)
        {
            return sortedBeatTicks[^1];
        }

        var before = sortedBeatTicks[insertionPoint - 1];
        var after = sortedBeatTicks[insertionPoint];
        return (tick - before) <= (after - tick) ? before : after;
    }

    /// <summary>"repair direction" (this step's own implementation guidance, verbatim): swaps each gesture's own start/end intensity so its ramp direction reverses. Never touches a gesture's own window or target props. A <see cref="TransitionKind.Blackout"/> gesture's own intensities are always <c>0.0</c>/<c>0.0</c> -- swapping is a no-op and never violates its own invariant.</summary>
    private static RepairOutcome RepairMelodicAlignmentDirection(LoopRun run, CandidateAttempt source, IReadOnlyList<TransitionGesture> gestures)
    {
        var repairedGestures = new List<TransitionGesture>();
        foreach (var gesture in gestures)
        {
            var repairedGesture = gesture with { StartIntensity = gesture.EndIntensity, EndIntensity = gesture.StartIntensity };
            repairedGesture.Validate();
            repairedGestures.Add(repairedGesture);
        }

        var repaired = BuildRepairedAttempt(run, source, "MelodicAlignment", repairedGestures, []);
        return new RepairOutcome(repaired, "MelodicAlignment",
            $"reversed the intensity ramp direction of {repairedGestures.Count} gesture(s); window and target props are unchanged.");
    }

    private static CandidateAttempt BuildRepairedAttempt(
        LoopRun run,
        CandidateAttempt source,
        string targetMetricName,
        IReadOnlyList<TransitionGesture> repairedGestures,
        IReadOnlyList<string> violations)
    {
        var nextOrdinal = source.Attempt + 1;
        var identityKey = $"{run.Id:N}|repair|{source.Id:N}|{targetMetricName}|{nextOrdinal}";
        var contentKey = string.Join(
            ';',
            repairedGestures.Select(g => string.Join(
                ':',
                g.Kind,
                g.Window.StartTicks.ToString(System.Globalization.CultureInfo.InvariantCulture),
                g.Window.EndTicks.ToString(System.Globalization.CultureInfo.InvariantCulture),
                string.Join(',', g.TargetProps.Select(p => p.Value)),
                g.StartIntensity.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                g.EndIntensity.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));

        var snapshotHash = DeterministicContent.Hash(string.Join('|', "repair-plan", identityKey, contentKey));

        var attempt = new CandidateAttempt(
            Id: DeterministicContent.GuidFrom($"attempt|{identityKey}"),
            RunId: run.Id,
            Attempt: nextOrdinal,
            Seed: source.Seed,
            SnapshotHash: snapshotHash,
            Status: violations.Count == 0 ? RefinementStatus.PreviewReady : RefinementStatus.Rejected,
            Violations: violations,
            RecordedAtUtc: DateTimeOffset.UtcNow,
            Gestures: repairedGestures);

        attempt.Validate();
        return attempt;
    }
}
