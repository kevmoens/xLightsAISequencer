using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

internal sealed record SectionCueWindow(
    SectionCue Cue, int Index, TimeWindow Window, IReadOnlyList<ModelId> Models, IReadOnlyList<ModelId> HandoffModels)
{
    internal bool IsHeld => Cue.Kind is CueKind.Hold or CueKind.Takeover && Cue.Intensity != IntensityTier.Off;

    internal static IReadOnlyList<SectionCueWindow> Resolve(
        SectionTimeline timeline, IReadOnlyDictionary<string, long> marks, TimeWindow section, DisplayGraph layout,
        long? allowedHoldEnd = null)
    {
        var candidates = timeline.CuesOrEmpty.Select((cue, index) => (cue, index))
            .Where(item => !string.IsNullOrWhiteSpace(item.cue.AnchorId) && marks.ContainsKey(item.cue.AnchorId) &&
                (string.IsNullOrEmpty(item.cue.HoldUntilAnchorId) || marks.ContainsKey(item.cue.HoldUntilAnchorId)))
            .OrderBy(item => marks[item.cue.AnchorId]).ThenBy(item => item.index).ToArray();
        var windows = new List<SectionCueWindow>();
        foreach (var (cue, index) in candidates)
        {
            var start = marks[cue.AnchorId];
            var end = cue.HoldUntilAnchorId is { Length: > 0 } id
                ? marks[id]
                : cue.Kind == CueKind.Accent
                    ? candidates.Where(x => x.cue.TargetReferences.SequenceEqual(cue.TargetReferences) && x.index != index && marks[x.cue.AnchorId] >= start &&
                            (marks[x.cue.AnchorId] > start || x.index > index))
                        .Select(x => marks[x.cue.AnchorId]).DefaultIfEmpty(section.EndTicks).First()
                    : section.EndTicks;
            end = Math.Min(end, cue.Kind is CueKind.Hold or CueKind.Takeover ? allowedHoldEnd ?? section.EndTicks : section.EndTicks);
            if (start >= section.StartTicks && end > start)
            {
                windows.Add(new SectionCueWindow(cue, index, new TimeWindow(start, end),
                    cue.TargetReferences.SelectMany(p => LookCompiler.TryResolveModels(p, layout)).Distinct().ToArray(),
                    cue.HandoffFromProps is { Length: > 0 } from ? LookCompiler.TryResolveModels(from, layout) : []));
            }
        }

        return windows;
    }

    internal IReadOnlyList<ModelId> Replaces => Cue.Kind == CueKind.Takeover
        ? Models.Concat(HandoffModels).Distinct().ToArray()
        : [];

    internal long HeldEnd(IReadOnlyList<SectionCueWindow> windows, ModelId model) =>
        Math.Min(Window.EndTicks, windows.Where(other => other.Window.StartTicks > Window.StartTicks && other.Replaces.Contains(model))
            .Select(other => other.Window.StartTicks).DefaultIfEmpty(Window.EndTicks).Min());

    internal bool OverlapsHeld(SectionCueWindow hold, IReadOnlyList<SectionCueWindow> windows) =>
        hold.Window.StartTicks < Window.EndTicks &&
        Models.Concat(HandoffModels).Intersect(hold.Models).Any(model => Window.StartTicks < hold.HeldEnd(windows, model));

    internal bool ReplacesRunningHold(IReadOnlyList<SectionCueWindow> existing) =>
        existing.Any(hold => hold.IsHeld && hold.Window.StartTicks < Window.StartTicks &&
            hold.Models.Intersect(Replaces).Any(model => hold.HeldEnd(existing, model) > Window.StartTicks));
}
