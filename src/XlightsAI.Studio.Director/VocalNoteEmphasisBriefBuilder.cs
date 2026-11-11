using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director;

/// <summary>
/// User-requested real investigation (2026-11-08): "is there processing to split out singing from the
/// rest of the song and notes?" -- yes: <c>AudioAnalysisRunner</c> already runs real YIN pitch
/// tracking against the isolated Spleeter vocals stem (never the full mix) and segments it into real,
/// absolute-tick <see cref="NoteEvent"/>s -- but that data, unlike transient hits/energy transitions/key
/// changes, never reached either prompt; only a per-section SUMMARY (<see cref="MelodicContourBriefBuilder"/>'s
/// contour shape/density) did. A real song's vocal line carries hundreds of notes (confirmed: a ~3-4 minute
/// song's real persisted melody revision has 350-480 notes) -- far too dense to paste every onset into a
/// per-section prompt -- so this type selects only the LOUDEST few per section (capped at
/// <see cref="MaxNotesPerSection"/>, by real measured <see cref="NoteEvent.Velocity"/>, the actual
/// onset-strength signal this pipeline already has), the same "real, measured, capped" discipline the
/// energy-transition/key-change/timed-cue lists already use.
/// </summary>
/// <param name="SectionId">The <see cref="SectionLabelCandidate.Id"/> this brief describes.</param>
/// <param name="SalientNotes">
/// This section's own loudest sung-note onsets (by real measured <see cref="NoteEvent.Velocity"/>), capped
/// at <see cref="VocalNoteEmphasisBriefBuilder.MaxNotesPerSection"/>, re-ordered back into time order for
/// display. Empty (never null) when this section has no melody data or no sounding notes.
/// </param>
public sealed record SectionVocalNoteEmphasis(Guid SectionId, IReadOnlyList<NoteEvent> SalientNotes);

public static class VocalNoteEmphasisBriefBuilder
{
    /// <summary>
    /// A fixed, defensible cap -- not a confirmed design decision, same disclosed-default discipline as
    /// <see cref="DirectorPromptBuilder.ExemplarCountForKind"/>'s own cap. Keeps even a melismatic,
    /// high-note-density section's own prompt line short and scannable rather than listing dozens of
    /// near-simultaneous onsets.
    /// </summary>
    public const int MaxNotesPerSection = 8;

    /// <summary>
    /// Computes one <see cref="SectionVocalNoteEmphasis"/> per <see cref="MusicModel.Sections"/> label
    /// that actually has salient notes -- a section with no melody data, an unresolvable bar range, or no
    /// sounding notes at all is simply absent from the result (never a fabricated empty entry), mirroring
    /// <see cref="MotifLinkedPredecessorBuilder"/>'s own "only sections that actually have the signal"
    /// convention rather than <see cref="MelodicContourBriefBuilder"/>'s "one entry per section, possibly
    /// a zero-value placeholder" convention -- there is no meaningful placeholder for "no emphasis notes."
    /// </summary>
    public static IReadOnlyList<SectionVocalNoteEmphasis> ComputeSectionBriefs(MusicModel music)
    {
        ArgumentNullException.ThrowIfNull(music);

        if (music.Melody is not { Notes.Count: > 0 } melody)
        {
            return [];
        }

        var barGrid = BarGrid.FromBeatGrid(music.BeatGrid, music.SongDurationTicks);
        var briefs = new List<SectionVocalNoteEmphasis>();

        foreach (var section in music.Sections.Labels.OrderBy(l => l.StartBarIndex))
        {
            var window = barGrid.WindowForBarRange(new BarIndex(section.StartBarIndex), new BarIndex(section.EndBarIndexExclusive));
            if (window is null)
            {
                continue;
            }

            var notesInWindow = melody.Notes
                .Where(n => n.StartTicks < window.Value.EndTicks && n.EndTicks > window.Value.StartTicks)
                .ToList();
            if (notesInWindow.Count == 0)
            {
                continue;
            }

            var salient = notesInWindow
                .OrderByDescending(n => n.Velocity)
                .Take(MaxNotesPerSection)
                .OrderBy(n => n.StartTicks)
                .ToList();

            briefs.Add(new SectionVocalNoteEmphasis(section.Id, salient));
        }

        return briefs;
    }
}
