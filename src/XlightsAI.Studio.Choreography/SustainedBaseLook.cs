using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

internal static class SustainedBaseLook
{
    internal static IReadOnlyList<EffectInstance> KeepSteady(
        IReadOnlyList<EffectInstance> effects, TimeWindow section, IReadOnlyList<SustainTiming> sustains)
    {
        var spans = new List<TimeWindow>();
        foreach (var note in sustains.Select(s => s.Note).OrderBy(n => n.StartTicks))
        {
            var start = Math.Max(section.StartTicks, note.StartTicks);
            var end = Math.Min(section.EndTicks, note.EndTicks);
            if (end <= start)
            {
                continue;
            }
            if (spans.Count > 0 && start <= spans[^1].EndTicks)
            {
                spans[^1] = spans[^1] with { EndTicks = Math.Max(end, spans[^1].EndTicks) };
            }
            else
            {
                spans.Add(new TimeWindow(start, end));
            }
        }
        if (spans.Count == 0)
        {
            return effects;
        }

        return effects.GroupBy(e => (e.Target, e.Layer)).SelectMany(lane =>
        {
            var current = lane.OrderBy(e => e.StartTicks).ToList();
            foreach (var span in spans)
            {
                var continuing = current.FindLastIndex(e => e.StartTicks < span.StartTicks && e.EndTicks >= span.StartTicks);
                var firstInside = current.FindIndex(e => e.StartTicks >= span.StartTicks && e.StartTicks < span.EndTicks);
                var keep = continuing >= 0 ? continuing : firstInside;
                if (keep < 0)
                {
                    continue;
                }
                var held = current[keep];
                var end = Math.Max(held.EndTicks, span.EndTicks);
                var next = new List<EffectInstance>(current.Count);
                for (var i = 0; i < current.Count; i++)
                {
                    var effect = current[i];
                    if (i == keep)
                    {
                        next.Add(held with { EndTicks = end });
                    }
                    else if (effect.StartTicks >= span.StartTicks && effect.StartTicks < end)
                    {
                        if (effect.EndTicks > end)
                        {
                            next.Add(effect with { StartTicks = end });
                        }
                    }
                    else
                    {
                        next.Add(effect);
                    }
                }
                current = next.OrderBy(e => e.StartTicks).ToList();
            }
            // A sustain can consume all the visible frames of a following effect without reaching its raw end.
            return current.Where(e => FrameClock.RoundToFrameTicks(e.EndTicks) > FrameClock.RoundToFrameTicks(e.StartTicks));
        }).ToArray();
    }
}
