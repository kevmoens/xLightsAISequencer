using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Choreography;

public sealed record SustainTiming(
    string StartId, string EndId, NoteEvent Note, string? EstimatedWord, IReadOnlyList<string> ConcurrentHits,
    string? ShortStartId = null, string? ShortEndId = null);

public sealed record SectionTimingCatalog(IReadOnlyDictionary<string, long> Marks, IReadOnlyList<SustainTiming> Sustains)
{
    public static SectionTimingCatalog Build(
        SectionMusicContext context, IReadOnlyList<PhraseCandidate>? phrases = null,
        IReadOnlyList<TransientHit>? transientHits = null, IReadOnlyList<NoteEvent>? salientNotes = null,
        IReadOnlyList<LeadInstrumentWindow>? leadWindows = null, IReadOnlyList<NoteEvent>? allNotes = null,
        IReadOnlyList<TranscribedWord>? words = null, IReadOnlyList<EnergyTransition>? shifts = null)
    {
        var marks = new Dictionary<string, long>(SectionTimingMarks.Resolve(context, phrases, transientHits, salientNotes, leadWindows), StringComparer.Ordinal);
        marks["section-end"] = context.Window.EndTicks;
        var sustains = new List<SustainTiming>();
        var beatTimes = context.BarGrid.Beats.Select(b => b.TimeTicks).ToArray();
        var hits = (transientHits ?? []).Where(h => h.TimeTicks >= context.Window.StartTicks && h.TimeTicks < context.Window.EndTicks)
            .OrderBy(h => h.TimeTicks).Select((hit, i) => (Hit: hit, Id: $"transient-{i + 1}")).ToArray();

        var notes = (allNotes ?? salientNotes ?? []).OrderBy(n => n.StartTicks).ToArray();
        var ambiguousPrefixes = notes.GroupBy(n => n.Id.ToString("N")[..8], StringComparer.Ordinal)
            .Where(g => g.Select(n => n.Id).Distinct().Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var note in notes)
        {
            var duration = note.EndTicks - note.StartTicks;
            if (note.StartTicks >= context.Window.EndTicks || note.EndTicks <= context.Window.StartTicks ||
                duration < Math.Max(TimeSpan.FromMilliseconds(750).Ticks, BeatTicksAt(note.StartTicks, beatTimes)))
            {
                continue;
            }

            var startId = $"sustain-{note.Id:N}-start";
            var endId = $"sustain-{note.Id:N}-end";
            marks[startId] = note.StartTicks;
            marks[endId] = note.EndTicks;
            var prefix = note.Id.ToString("N")[..8];
            var shortStart = ambiguousPrefixes.Contains(prefix) ? startId : $"hold-{prefix}-start";
            var shortEnd = ambiguousPrefixes.Contains(prefix) ? endId : $"hold-{prefix}-end";
            marks[shortStart] = note.StartTicks;
            marks[shortEnd] = note.EndTicks;
            var word = (words ?? []).Select(w => (Word: w, Overlap: Math.Min(w.EndTicks, note.EndTicks) - Math.Max(w.StartTicks, note.StartTicks)))
                .Where(x => x.Overlap >= (duration + 1) / 2)
                .OrderByDescending(x => x.Overlap).ThenByDescending(x => x.Word.Confidence).FirstOrDefault().Word;
            var concurrent = hits.Where(x => x.Hit.TimeTicks >= note.StartTicks && x.Hit.TimeTicks < note.EndTicks)
                .OrderByDescending(x => x.Hit.Strength).Take(4).OrderBy(x => x.Hit.TimeTicks).Select(x => x.Id).ToArray();
            sustains.Add(new SustainTiming(startId, endId, note, word?.Text, concurrent, shortStart, shortEnd));
        }

        var shiftNumber = 0;
        foreach (var shift in (shifts ?? []).OrderBy(s => s.TimeTicks))
        {
            if (shift.TimeTicks >= context.Window.StartTicks && shift.TimeTicks < context.Window.EndTicks)
            {
                marks[$"energy-{shift.Kind.ToString().ToLowerInvariant()}-{++shiftNumber}"] = shift.TimeTicks;
            }
        }
        return new SectionTimingCatalog(marks, sustains);
    }

    private static long BeatTicksAt(long tick, long[] beats)
    {
        if (beats.Length < 2)
        {
            return 0;
        }
        var i = Array.BinarySearch(beats, tick);
        var before = Math.Clamp(i >= 0 ? i : ~i - 1, 0, beats.Length - 2);
        return beats[before + 1] - beats[before];
    }
}
