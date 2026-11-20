using System.Text.Json;
using System.Xml.Linq;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Director;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Xlights;

internal static class BaselineMeasurement
{
    private sealed record Placement(string Target, string Effect, int Layer, long Start, long End, IReadOnlyList<ModelId> Models);
    private sealed record Cue(long Start, long? End, IReadOnlyList<ModelId> Models, Placement? Placement, bool Handoff, bool IsHold = true);
    private sealed record Span(long StartTicks, long EndTicks, string[] HoldTargets, int UnrelatedSwitches, int UndeclaredInterruptions);

    public static void Write(string run, string path, DisplayGraph layout, MusicModel music, IReadOnlyList<SectionCueAudit>? finalCues)
    {
        var exported = new XsqStructuralReader().Read(path);
        var frame = TimeSpan.FromMilliseconds(exported.FrameIntervalMs).Ticks;
        var timelines = JsonSerializer.Deserialize<Dictionary<Guid, SectionTimeline>>(
            File.ReadAllText(Path.Combine(run, "timelines.json"))) ?? [];

        var placements = XDocument.Load(path).Root!.Element("ElementEffects")!.Elements("Element")
            .Where(e => (string?)e.Attribute("type") != "timing")
            .SelectMany(e =>
            {
                var target = (string)e.Attribute("name")!;
                var models = LookCompiler.TryResolveModels(target, layout);
                return e.Elements("EffectLayer").SelectMany((layer, i) => layer.Elements("Effect").Select(effect =>
                    new Placement(target, (string)effect.Attribute("name")!, i,
                        (long)effect.Attribute("startTime")! * TimeSpan.TicksPerMillisecond,
                        (long)effect.Attribute("endTime")! * TimeSpan.TicksPerMillisecond, models)));
            }).ToArray();

        var grid = BarGrid.FromBeatGrid(music.BeatGrid, music.SongDurationTicks);
        var tracks = new Dictionary<string, TimingTrack>
        {
            ["Beats"] = TimingTrackEmitter.Beats(music.BeatGrid, music.SongDurationTicks),
            ["Bars"] = TimingTrackEmitter.Bars(grid),
        };
        var cues = new List<Cue>();
        if (finalCues is not null)
        {
            foreach (var decision in finalCues)
            {
                foreach (var effect in decision.Placements)
                {
                    var placement = placements.FirstOrDefault(p => p.Target == effect.Target.Value &&
                        Math.Abs(p.Start - effect.StartTicks) <= frame && Math.Abs(p.End - effect.EndTicks) <= frame &&
                        p.Effect == decision.Cue.Effect);
                    if (placement is not null)
                    {
                        cues.Add(new Cue(placement.Start, placement.End, placement.Models, placement,
                            decision.Cue.Kind == CueKind.Takeover || decision.Cue.HandoffFromProps is not null,
                            decision.Cue.Kind == CueKind.Hold));
                    }
                }
            }
        }
        foreach (var section in finalCues is null ? music.Sections.Labels : [])
        {
            if (!timelines.TryGetValue(section.Id, out var timeline) ||
                grid.WindowForBarRange(new BarIndex(section.StartBarIndex), new BarIndex(section.EndBarIndexExclusive)) is not { } window)
            {
                continue;
            }

            var marks = SectionTimingMarks.Resolve(new SectionMusicContext(window, section.Kind, 1, grid, tracks, false),
                music.Phrases.Phrases.Where(p => p.SectionLabelId == section.Id).ToArray(),
                music.TransientHits, music.Melody?.Notes, music.LeadInstrumentWindows);
            foreach (var cue in timeline.CuesOrEmpty)
            {
                if (!marks.TryGetValue(cue.AnchorId, out var start))
                {
                    continue;
                }

                long? end = cue.HoldUntilAnchorId is { } id && marks.TryGetValue(id, out var resolved) ? resolved : null;
                var models = LookCompiler.TryResolveModels(cue.Props, layout);
                var name = cue.Props.StartsWith("group:", StringComparison.Ordinal) ? cue.Props[6..] : cue.Props;
                var placement = placements.FirstOrDefault(p =>
                    p.Target == name && p.Effect == cue.Effect && Math.Abs(p.Start - start) <= frame);
                cues.Add(new Cue(start, end, models, placement, !string.IsNullOrEmpty(cue.HandoffFromProps)));
                if (cue.HandoffFromProps is { Length: > 0 } source)
                {
                    cues.Add(new Cue(start, null, LookCompiler.TryResolveModels(source, layout), null, true));
                }
            }
        }

        var spans = new List<Span>();
        foreach (var note in music.Melody?.Notes ?? [])
        {
            if (note.EndTicks - note.StartTicks < Math.Max(TimeSpan.FromMilliseconds(750).Ticks, LocalBeat(note, grid)))
            {
                continue;
            }

            // Before cue kinds existed, an explicit endpoint plus a matching exported placement is the
            // evidence of a declared hold. Measure the intended interval, not its duty-trimmed export.
            var holds = cues.Where(c => c.IsHold && c.Placement is not null && !c.Handoff &&
                c.Start <= note.StartTicks + frame && c.End >= note.EndTicks - frame).ToArray();
            var holdModels = holds.SelectMany(h => h.Models).ToHashSet();
            var unrelated = 0;
            var interruptions = 0;
            foreach (var placement in placements.Where(p => p.Start >= note.StartTicks && p.Start < note.EndTicks))
            {
                var matchingCues = cues.Where(c => finalCues is null
                    ? Math.Abs(c.Start - placement.Start) <= frame : ReferenceEquals(c.Placement, placement)).ToArray();
                var declared = matchingCues.SelectMany(c => c.Models).ToHashSet();
                unrelated += placement.Models.Count(m => !holdModels.Contains(m) && !declared.Contains(m));
                var handoff = matchingCues.Where(c => c.Handoff).SelectMany(c => c.Models).ToHashSet();
                interruptions += placement.Models.Count(m => !handoff.Contains(m) &&
                    holds.Any(h => h.Placement!.Layer == placement.Layer && h.Models.Contains(m) && !ReferenceEquals(h.Placement, placement)));
            }
            spans.Add(new Span(note.StartTicks, note.EndTicks,
                holdModels.Select(m => m.Value).Order(StringComparer.Ordinal).ToArray(), unrelated, interruptions));
        }

        var summary = new
        {
            ExportPath = path,
            layout.SourceLayoutHash,
            music.SongDurationTicks,
            PlacementCount = placements.Length,
            SustainCount = spans.Count,
            SpansWithDeclaredHolds = spans.Count(s => s.HoldTargets.Length > 0),
            AverageUnrelatedSwitches = spans.Count > 0 ? (double?)spans.Average(s => s.UnrelatedSwitches) : null,
            UndeclaredInterruptions = spans.Sum(s => s.UndeclaredInterruptions),
            Spans = spans,
        };
        var evidence = Path.Combine(run, "metrics.json");
        File.WriteAllText(evidence, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Measurement: {spans.Count} spans; average switches {summary.AverageUnrelatedSwitches}; {summary.UndeclaredInterruptions} interruptions. {evidence}");
    }

    private static long LocalBeat(NoteEvent note, BarGrid grid)
    {
        for (var i = 1; i < grid.Beats.Count; i++)
        {
            if (note.StartTicks < grid.Beats[i].TimeTicks || i == grid.Beats.Count - 1)
            {
                return grid.Beats[i].TimeTicks - grid.Beats[i - 1].TimeTicks;
            }
        }
        return 0;
    }
}
