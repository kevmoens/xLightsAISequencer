using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 28a: the single "real marks for this section" enumeration both
/// <see cref="SectionTimelineValidator"/> and <see cref="SectionTimelineCompiler"/> resolve a
/// <see cref="SectionCue.AnchorId"/> against, and the same data <c>XlightsAI.Studio.Director.SectionScriptPromptBuilder</c>'s
/// own <c>AppendTimingMarks</c> renders into the prompt -- one enumeration, never duplicated logic, so an
/// id the prompt offers always matches an id the compiler can resolve. Every mark is a real, already-known
/// moment (a beat, a bar, a transient hit, a vocal-note onset, a lead-instrument-window start, a phrase
/// start) overlapping this section's own window, plus the one always-available <see cref="AnchorKind.SectionStart"/>
/// anchor -- never an invented timestamp.
/// </summary>
public static class SectionTimingMarks
{
    /// <summary>
    /// Resolves every real timing mark available for <paramref name="context"/>'s own section into a
    /// stable, short id -&gt; absolute tick lookup. Every optional parameter mirrors the identical signal
    /// this project's Choreographer prompt already threads per-section
    /// (<c>XlightsAI.Studio.Director.SectionScriptPromptBuilder.BuildVolatileSuffix</c>'s own
    /// <c>phrases</c>/<c>transientHits</c>/<c>salientVocalNotes</c> parameters, plus the new
    /// <c>leadInstrumentWindows</c>) -- <see langword="null"/>/empty for any one of them simply omits
    /// that anchor kind from the result, never fabricating a mark the caller has no real data for.
    /// </summary>
    public static IReadOnlyDictionary<string, long> Resolve(
        SectionMusicContext context,
        IReadOnlyList<PhraseCandidate>? phrases = null,
        IReadOnlyList<TransientHit>? transientHits = null,
        IReadOnlyList<NoteEvent>? vocalNotes = null,
        IReadOnlyList<LeadInstrumentWindow>? leadInstrumentWindows = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var start = context.Window.StartTicks;
        var end = context.Window.EndTicks;
        var marks = new Dictionary<string, long>(StringComparer.Ordinal)
        {
            [SectionStartAnchorId] = start,
        };

        if (context.TimingTracksByName.TryGetValue("Beats", out var beats))
        {
            AddTrackMarks(marks, beats, "beat", start, end);
        }

        if (context.TimingTracksByName.TryGetValue("Bars", out var bars))
        {
            AddTrackMarks(marks, bars, "bar", start, end);
        }

        if (transientHits is { Count: > 0 })
        {
            var ordered = transientHits.Where(h => h.TimeTicks >= start && h.TimeTicks < end).OrderBy(h => h.TimeTicks).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                marks[$"transient-{i + 1}"] = ordered[i].TimeTicks;
            }
        }

        if (vocalNotes is { Count: > 0 })
        {
            var ordered = vocalNotes.Where(n => n.StartTicks >= start && n.StartTicks < end).OrderBy(n => n.StartTicks).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                marks[$"vocalnote-{i + 1}"] = ordered[i].StartTicks;
            }
        }

        if (leadInstrumentWindows is { Count: > 0 })
        {
            var ordered = leadInstrumentWindows.Where(w => w.StartTicks < end && w.EndTicks > start).OrderBy(w => w.StartTicks).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                marks[$"leadinstrumentwindowstart-{i + 1}"] = Math.Max(ordered[i].StartTicks, start);
            }
        }

        if (phrases is { Count: > 0 })
        {
            var ordered = phrases.OrderBy(p => p.StartBarIndex).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                marks[$"phrase-{i + 1}-start"] = ordered[i].StartTicks;
            }
        }

        return marks;
    }

    /// <summary>The one anchor id always available for every section, regardless of what other real data exists -- resolves to this section's own window start.</summary>
    public const string SectionStartAnchorId = "section-start";

    private static void AddTrackMarks(Dictionary<string, long> marks, TimingTrack track, string idPrefix, long start, long end)
    {
        var ordered = track.Marks.Where(m => m.StartTicks < end && m.EndTicks > start).OrderBy(m => m.StartTicks).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            marks[$"{idPrefix}-{i + 1}"] = Math.Max(ordered[i].StartTicks, start);
        }
    }
}
