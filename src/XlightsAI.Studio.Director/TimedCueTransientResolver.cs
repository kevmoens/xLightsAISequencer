using XlightsAI.Studio.Rhythm;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan "richer analysis tools" Phase 2 (2026-11-02): a user's hand-typed <see cref="TimedCue"/>
/// timestamp is a guess, not ground truth. When a real, loud <see cref="TransientHit"/> falls within the
/// cue's own tolerance window, this snaps the cue onto that real moment and collapses its tolerance to
/// <see cref="SnappedTolerance"/> -- the guard rail (<see cref="TimedCueComplianceChecker"/>) then checks
/// compliance against the verified real event instead of the raw typed estimate. A cue with no qualifying
/// transient nearby is returned unchanged; the original, wider, typed tolerance still applies to it.
/// </summary>
public static class TimedCueTransientResolver
{
    public static readonly TimeSpan SnappedTolerance = TimeSpan.FromMilliseconds(300);

    public static IReadOnlyList<TimedCue> Resolve(IReadOnlyList<TimedCue> cues, IReadOnlyList<TransientHit>? hits)
    {
        ArgumentNullException.ThrowIfNull(cues);
        if (hits is null || hits.Count == 0 || cues.Count == 0)
        {
            return cues;
        }

        var resolved = new List<TimedCue>(cues.Count);
        foreach (var cue in cues)
        {
            var windowStartTicks = cue.WindowStart.Ticks;
            var windowEndTicks = cue.WindowEnd.Ticks;

            TransientHit? best = null;
            var bestDistance = long.MaxValue;
            foreach (var hit in hits)
            {
                if (hit.TimeTicks < windowStartTicks || hit.TimeTicks > windowEndTicks)
                {
                    continue;
                }

                var distance = Math.Abs(hit.TimeTicks - cue.Start.Ticks);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = hit;
                }
            }

            if (best is null)
            {
                resolved.Add(cue);
                continue;
            }

            var snappedStart = TimeSpan.FromTicks(best.TimeTicks);
            var snappedEnd = cue.End is { } end ? snappedStart + (end - cue.Start) : (TimeSpan?)null;
            resolved.Add(cue with { Start = snappedStart, End = snappedEnd, Tolerance = SnappedTolerance });
        }

        return resolved;
    }
}
