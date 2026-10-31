using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director;

/// <summary>Build-plan Phase 10 §2c/§3b: the coarse, closed vocabulary a section's own melodic shape is reduced to -- deliberately not a continuous curve the LLM would have to interpret unguided.</summary>
public enum MelodicContourShape
{
    Rising,
    Falling,
    Mixed,
    StaticSparse,
}

/// <summary>One section's own compact melodic digest, joined from the real <see cref="MelodyTrack"/> against that section's own bar range -- never raw frame-by-frame pitch data, which would bloat the prompt.</summary>
/// <param name="SectionId">The <see cref="SectionLabelCandidate.Id"/> this digest describes.</param>
/// <param name="ContourShape">This section's own coarse melodic direction (§2c).</param>
/// <param name="NoteDensity">Notes per beat across this section's own window -- <c>0</c> when there is no melody data or no notes sound in this section.</param>
/// <param name="PeakNoteBeatOffset">Zero-based beat offset (from this section's own first beat) of the section's highest-pitched note's own onset -- <c>0</c> when the section has no notes.</param>
public sealed record SectionMelodicBrief(Guid SectionId, MelodicContourShape ContourShape, double NoteDensity, int PeakNoteBeatOffset);

/// <summary>
/// Build-plan Phase 10 §3b: joins the real <see cref="MelodyTrack"/> (already computed by
/// <c>XlightsAI.Studio.Melody</c>, previously only consumed by the bar-fingerprint/section-detection path
/// and the Piano Roll UI -- never reaching either prompt) against each real section's own bar range, so the
/// Choreographer prompt can finally answer "which way should something move?" (§2c) instead of only "how
/// much energy does this section have."
/// </summary>
public static class MelodicContourBriefBuilder
{
    /// <summary>
    /// Computes one <see cref="SectionMelodicBrief"/> per <see cref="MusicModel.Sections"/> label, ordered
    /// by <see cref="SectionLabelCandidate.StartBarIndex"/>. Every brief is <see cref="MelodicContourShape.StaticSparse"/>
    /// with <c>0</c> density/offset (the same "silence is a valid, inspectable result" convention
    /// <see cref="Rhythm.BandEnergyEnvelope"/> already uses) when <see cref="MusicModel.Melody"/> is
    /// <see langword="null"/>, a section's bar range falls outside the song's own beat grid, or the section
    /// has no sounding notes at all.
    /// </summary>
    public static IReadOnlyList<SectionMelodicBrief> ComputeSectionBriefs(MusicModel music)
    {
        ArgumentNullException.ThrowIfNull(music);

        var sections = music.Sections.Labels.OrderBy(l => l.StartBarIndex).ToList();
        if (music.Melody is null || sections.Count == 0)
        {
            return sections.Select(s => new SectionMelodicBrief(s.Id, MelodicContourShape.StaticSparse, 0, 0)).ToList();
        }

        var barGrid = BarGrid.FromBeatGrid(music.BeatGrid, music.SongDurationTicks);
        var melody = music.Melody;
        var gesturesByNoteId = melody.Gestures.ToDictionary(g => g.NoteId);

        return sections.Select(section =>
        {
            var window = barGrid.WindowForBarRange(new BarIndex(section.StartBarIndex), new BarIndex(section.EndBarIndexExclusive));
            if (window is null)
            {
                return new SectionMelodicBrief(section.Id, MelodicContourShape.StaticSparse, 0, 0);
            }

            var notesInWindow = melody.Notes
                .Where(n => n.StartTicks < window.Value.EndTicks && n.EndTicks > window.Value.StartTicks)
                .OrderBy(n => n.StartTicks)
                .ToList();

            if (notesInWindow.Count == 0)
            {
                return new SectionMelodicBrief(section.Id, MelodicContourShape.StaticSparse, 0, 0);
            }

            var sectionBeats = CountBeatsInWindow(barGrid, window.Value);
            var density = sectionBeats > 0 ? notesInWindow.Count / (double)sectionBeats : 0;

            var upCount = 0;
            var downCount = 0;
            foreach (var note in notesInWindow)
            {
                if (!gesturesByNoteId.TryGetValue(note.Id, out var gesture))
                {
                    continue;
                }

                switch (gesture.Motion)
                {
                    case PitchMotion.StepUp or PitchMotion.LeapUp:
                        upCount++;
                        break;
                    case PitchMotion.StepDown or PitchMotion.LeapDown:
                        downCount++;
                        break;
                }
            }

            var shape = (upCount + downCount) switch
            {
                < 2 => MelodicContourShape.StaticSparse, // long held note / too little real pitch movement to call a direction (§2c)
                _ when upCount > downCount => MelodicContourShape.Rising,
                _ when downCount > upCount => MelodicContourShape.Falling,
                _ => MelodicContourShape.Mixed,
            };

            var peakNote = notesInWindow.OrderByDescending(n => n.MidiNote).ThenBy(n => n.StartTicks).First();
            var peakOffset = BeatOffsetInWindow(barGrid, window.Value, peakNote.StartTicks);

            return new SectionMelodicBrief(section.Id, shape, density, peakOffset);
        }).ToList();
    }

    /// <summary>How many of <paramref name="barGrid"/>'s own real beats fall inside <paramref name="window"/> -- the real per-section beat count the §3b digest's <c>NoteDensity</c>/"beat N of M" text is grounded in, never an assumed fixed beats-per-bar count.</summary>
    public static int CountBeatsInWindow(BarGrid barGrid, TimeWindow window)
    {
        ArgumentNullException.ThrowIfNull(barGrid);

        return barGrid.Beats.Count(b => b.TimeTicks >= window.StartTicks && b.TimeTicks < window.EndTicks);
    }

    /// <summary>Zero-based index, among <paramref name="window"/>'s own real beats, of the beat closest to (but never after) <paramref name="tick"/> -- clamped into range so a note landing exactly on or after the section's final beat still reports that final beat.</summary>
    private static int BeatOffsetInWindow(BarGrid barGrid, TimeWindow window, long tick)
    {
        var beatsInWindow = barGrid.Beats
            .Where(b => b.TimeTicks >= window.StartTicks && b.TimeTicks < window.EndTicks)
            .OrderBy(b => b.TimeTicks)
            .ToList();

        if (beatsInWindow.Count == 0)
        {
            return 0;
        }

        var offset = beatsInWindow.Count(b => b.TimeTicks <= tick) - 1;
        return Math.Clamp(offset, 0, beatsInWindow.Count - 1);
    }
}
