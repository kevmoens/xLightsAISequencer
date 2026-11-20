using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

internal static class TimelineEffectMask
{
    internal static List<EffectInstance> Remove(
        IEnumerable<EffectInstance> effects, DisplayGraph layout, TimeWindow window, IReadOnlySet<ModelId> targets)
    {
        var result = new List<EffectInstance>();
        foreach (var effect in effects)
        {
            var start = Math.Max(window.StartTicks, effect.StartTicks);
            var end = Math.Min(window.EndTicks, effect.EndTicks);
            if (start >= end)
            {
                result.Add(effect);
                continue;
            }

            var models = LookCompiler.TryResolveModels(effect.Target.Value, layout);
            if (!models.Any(targets.Contains))
            {
                result.Add(effect);
                continue;
            }

            Add(effect.Target, effect.StartTicks, start);
            Add(effect.Target, end, effect.EndTicks);
            foreach (var member in models.Where(m => !targets.Contains(m)))
            {
                Add(member, start, end);
            }

            void Add(ModelId target, long from, long to)
            {
                if (to - from >= FrameClock.FrameIntervalTicks)
                {
                    result.Add(effect with { Id = Guid.NewGuid(), Target = target, StartTicks = from, EndTicks = to });
                }
            }
        }

        return result;
    }
}
