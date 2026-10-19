using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Composition;

/// <summary>Default <see cref="IVisualBudgetResolver"/>. See the interface and <c>implementation-evidence/PHASE-10/STEP-068.md</c> for the full rationale.</summary>
public sealed class VisualBudgetResolver : IVisualBudgetResolver
{
    private const long TicksPerSecond = 10_000_000; // TimelineTick is 100 ns per PHASE-10.md's own shared contract.

    private readonly IRolePrecedenceResolver _precedenceResolver;

    public VisualBudgetResolver(IRolePrecedenceResolver precedenceResolver)
    {
        ArgumentNullException.ThrowIfNull(precedenceResolver);
        _precedenceResolver = precedenceResolver;
    }

    public VisualBudgetResolution Resolve(
        IReadOnlyList<VisualBudgetCandidate> candidates,
        VisualBudget budget,
        TimeWindow evaluationWindow)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(budget);

        budget.Validate();
        ValidateWindow(evaluationWindow, "evaluationWindow");

        foreach (var candidate in candidates)
        {
            ArgumentNullException.ThrowIfNull(candidate.Assignment);
            ValidateWindow(candidate.Window, $"candidate '{candidate.Assignment.SourceTrackId}'");
            if (!double.IsFinite(candidate.Brightness) || candidate.Brightness < 0.0 || candidate.Brightness > 1.0)
            {
                throw new CompositionInvalidException(
                    $"Candidate '{candidate.Assignment.SourceTrackId}' Brightness must be a finite number in [0,1] (was {candidate.Brightness}).");
            }
        }

        var durationSeconds = DurationSeconds(evaluationWindow);

        // Precompute precedence order via the already-accepted IRolePrecedenceResolver (also validates every
        // candidate's own PrecedenceTier, the same way RolePrecedenceResolver already does) -- matched back
        // to its owning candidate by reference, since MusicalRoleAssignment is a value-equal record and two
        // distinct candidates could otherwise carry equal-valued assignments.
        var byAssignment = new List<(MusicalRoleAssignment Assignment, VisualBudgetCandidate Candidate)>(candidates.Count);
        foreach (var candidate in candidates)
        {
            byAssignment.Add((candidate.Assignment, candidate));
        }

        var orderedAssignments = _precedenceResolver.ResolveOrder(candidates.Select(c => c.Assignment).ToList());
        var remaining = new List<(MusicalRoleAssignment Assignment, VisualBudgetCandidate Candidate)>(byAssignment);
        var orderedCandidates = new List<VisualBudgetCandidate>(candidates.Count);
        foreach (var assignment in orderedAssignments)
        {
            var index = remaining.FindIndex(r => ReferenceEquals(r.Assignment, assignment) || Equals(r.Assignment, assignment));
            orderedCandidates.Add(remaining[index].Candidate);
            remaining.RemoveAt(index);
        }

        var admittedFocalWindows = new List<TimeWindow>();
        var admittedCount = 0;
        var admittedBrightnessSum = 0.0;
        var results = new List<VisualBudgetCandidateResult>(orderedCandidates.Count);

        foreach (var candidate in orderedCandidates)
        {
            var admittedWindow = candidate.Window;
            var decision = VisualBudgetDecision.Accepted;
            string? reason = null;

            if (candidate.IsFocal)
            {
                var trimmed = TryFitUnderFocalCap(candidate.Window, admittedFocalWindows, budget.MaxConcurrentFocalProps);
                if (trimmed is null)
                {
                    results.Add(new VisualBudgetCandidateResult(candidate, VisualBudgetDecision.Dropped, null,
                        "Exceeds max concurrent focal props."));
                    continue;
                }

                if (trimmed.Value != candidate.Window)
                {
                    admittedWindow = trimmed.Value;
                    decision = VisualBudgetDecision.Trimmed;
                    reason = "Trimmed to fit max concurrent focal props.";
                }
            }

            var prospectiveDensity = (admittedCount + 1) / durationSeconds;
            if (prospectiveDensity > budget.MaxEventDensityPerSecond)
            {
                results.Add(new VisualBudgetCandidateResult(candidate, VisualBudgetDecision.Dropped, null,
                    "Exceeds max event density per second."));
                continue;
            }

            var prospectiveMeanBrightness = (admittedBrightnessSum + candidate.Brightness) / (admittedCount + 1);
            if (prospectiveMeanBrightness > budget.MaxMeanBrightness)
            {
                results.Add(new VisualBudgetCandidateResult(candidate, VisualBudgetDecision.Dropped, null,
                    "Exceeds max mean brightness."));
                continue;
            }

            admittedCount++;
            admittedBrightnessSum += candidate.Brightness;
            if (candidate.IsFocal)
            {
                admittedFocalWindows.Add(admittedWindow);
            }

            results.Add(new VisualBudgetCandidateResult(candidate, decision, admittedWindow, reason));
        }

        // Restore the caller's own original relative order for Results (ResolveOrder only governs admission
        // priority, not the shape of the returned report).
        var resultsByOriginalOrder = candidates
            .Select(c => results.Single(r => ReferenceEquals(r.Candidate, c)))
            .ToList();

        var observedMeanBrightness = admittedCount == 0 ? 0.0 : admittedBrightnessSum / admittedCount;
        var observedDensity = admittedCount / durationSeconds;
        var observedPeakFocal = PeakOverlap(admittedFocalWindows);
        var observedDarkness = 1.0 - CoverageFraction(AdmittedWindows(results), evaluationWindow);

        var warnings = new List<string>();
        if (observedDarkness < budget.DarknessFractionTarget)
        {
            warnings.Add(
                $"Observed darkness fraction {observedDarkness:0.###} is below the target {budget.DarknessFractionTarget:0.###}.");
        }

        return new VisualBudgetResolution(
            resultsByOriginalOrder,
            observedMeanBrightness,
            observedDensity,
            observedPeakFocal,
            observedDarkness,
            warnings);
    }

    private static IReadOnlyList<TimeWindow> AdmittedWindows(List<VisualBudgetCandidateResult> results) =>
        results.Where(r => r.AdmittedWindow is not null).Select(r => r.AdmittedWindow!.Value).ToList();

    private static void ValidateWindow(TimeWindow window, string label)
    {
        if (window.StartTicks >= window.EndTicks)
        {
            throw new CompositionInvalidException(
                $"{label} window must satisfy StartTicks < EndTicks (was {window.StartTicks}..{window.EndTicks}).");
        }
    }

    private static double DurationSeconds(TimeWindow window) => (window.EndTicks - window.StartTicks) / (double)TicksPerSecond;

    private static bool Overlaps(TimeWindow a, TimeWindow b) => a.StartTicks < b.EndTicks && b.StartTicks < a.EndTicks;

    /// <summary>
    /// Finds the longest sub-interval of <paramref name="candidateWindow"/> whose point-wise overlap count
    /// against <paramref name="admittedFocalWindows"/> stays strictly below <paramref name="maxConcurrent"/>,
    /// preferring the earliest such sub-interval on a tie. Returns <see langword="null"/> when no non-empty
    /// sub-interval qualifies (the entire candidate window is saturated).
    /// </summary>
    private static TimeWindow? TryFitUnderFocalCap(TimeWindow candidateWindow, IReadOnlyList<TimeWindow> admittedFocalWindows, int maxConcurrent)
    {
        var overlapping = admittedFocalWindows.Where(w => Overlaps(w, candidateWindow)).ToList();
        if (overlapping.Count == 0)
        {
            return maxConcurrent >= 1 ? candidateWindow : null;
        }

        var boundaries = new SortedSet<long> { candidateWindow.StartTicks, candidateWindow.EndTicks };
        foreach (var w in overlapping)
        {
            if (w.StartTicks > candidateWindow.StartTicks && w.StartTicks < candidateWindow.EndTicks)
            {
                boundaries.Add(w.StartTicks);
            }

            if (w.EndTicks > candidateWindow.StartTicks && w.EndTicks < candidateWindow.EndTicks)
            {
                boundaries.Add(w.EndTicks);
            }
        }

        var points = boundaries.ToList();
        TimeWindow? best = null;
        long bestLength = 0;

        for (var i = 0; i < points.Count - 1; i++)
        {
            var segmentStart = points[i];
            var segmentEnd = points[i + 1];
            if (segmentStart >= segmentEnd)
            {
                continue;
            }

            var segment = new TimeWindow(segmentStart, segmentEnd);
            var coverage = overlapping.Count(w => Overlaps(w, segment));
            if (coverage + 1 > maxConcurrent)
            {
                continue;
            }

            var length = segmentEnd - segmentStart;
            if (length > bestLength)
            {
                bestLength = length;
                best = segment;
            }
        }

        return best;
    }

    private static int PeakOverlap(IReadOnlyList<TimeWindow> windows)
    {
        if (windows.Count == 0)
        {
            return 0;
        }

        var events = new List<(long Tick, int Delta)>(windows.Count * 2);
        foreach (var w in windows)
        {
            events.Add((w.StartTicks, 1));
            events.Add((w.EndTicks, -1));
        }

        var peak = 0;
        var current = 0;
        foreach (var (_, delta) in events.OrderBy(e => e.Tick).ThenBy(e => e.Delta))
        {
            current += delta;
            peak = Math.Max(peak, current);
        }

        return peak;
    }

    private static double CoverageFraction(IReadOnlyList<TimeWindow> windows, TimeWindow evaluationWindow)
    {
        var duration = evaluationWindow.EndTicks - evaluationWindow.StartTicks;
        if (duration <= 0 || windows.Count == 0)
        {
            return 0.0;
        }

        var clipped = new List<TimeWindow>();
        foreach (var w in windows)
        {
            var start = Math.Max(w.StartTicks, evaluationWindow.StartTicks);
            var end = Math.Min(w.EndTicks, evaluationWindow.EndTicks);
            if (start < end)
            {
                clipped.Add(new TimeWindow(start, end));
            }
        }

        if (clipped.Count == 0)
        {
            return 0.0;
        }

        clipped.Sort((a, b) => a.StartTicks.CompareTo(b.StartTicks));
        long unionTicks = 0;
        var currentStart = clipped[0].StartTicks;
        var currentEnd = clipped[0].EndTicks;
        for (var i = 1; i < clipped.Count; i++)
        {
            var next = clipped[i];
            if (next.StartTicks <= currentEnd)
            {
                currentEnd = Math.Max(currentEnd, next.EndTicks);
            }
            else
            {
                unionTicks += currentEnd - currentStart;
                currentStart = next.StartTicks;
                currentEnd = next.EndTicks;
            }
        }

        unionTicks += currentEnd - currentStart;
        return unionTicks / (double)duration;
    }
}
