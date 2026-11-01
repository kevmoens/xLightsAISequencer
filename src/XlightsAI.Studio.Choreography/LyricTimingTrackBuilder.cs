using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 8 task 3 (singing-face support). Architecture §6: "Lyric tracks are the same
/// structure with *three* EffectLayer children: phrases, words, phonemes." <c>XsqExporter</c> emits
/// exactly one <c>EffectLayer</c> per timing-track <c>Element</c> (confirmed against real output), so
/// -- rather than touching the exporter's single-layer timing-track path -- this emits three separate,
/// ordinary single-layer timing tracks (<c>Lyrics_Phrases</c>/<c>Lyrics_Words</c>/<c>Lyrics_Phonemes</c>)
/// instead of one three-layer track. Gated entirely on real vocal activity: an instrumental song (no
/// <see cref="VocalActivityEnvelope.ActiveRegions"/>) gets none of these tracks, never an empty/useless one.
/// </summary>
public static class LyricTimingTrackBuilder
{
    public const string PhrasesTrackName = "Lyrics_Phrases";
    public const string WordsTrackName = "Lyrics_Words";
    public const string PhonemesTrackName = "Lyrics_Phonemes";

    /// <summary>Empty when <paramref name="vocalActivity"/> has no active regions at all -- no lyric tracks are produced for an instrumental song.</summary>
    public static IReadOnlyDictionary<string, TimingTrack> BuildAll(
        PhraseSegmentationAnalysis phrases, VocalActivityEnvelope? vocalActivity, IReadOnlyList<VisemeMark>? visemeMarks)
    {
        ArgumentNullException.ThrowIfNull(phrases);

        if (vocalActivity is not { ActiveRegions.Count: > 0 })
        {
            return new Dictionary<string, TimingTrack>(StringComparer.Ordinal);
        }

        var marks = visemeMarks ?? [];
        var tracks = new Dictionary<string, TimingTrack>(StringComparer.Ordinal)
        {
            [PhrasesTrackName] = Phrases(phrases),
            [PhonemesTrackName] = Phonemes(marks),
        };

        var words = Words(marks);
        if (words.Marks.Count > 0)
        {
            tracks[WordsTrackName] = words;
        }

        return tracks;
    }

    /// <summary>Reuses Phase 3/8's own measured phrase segmentation verbatim, under the lyric-specific track name <see cref="PhrasesTrackName"/> karaoke-style text would key off at the phrase level.</summary>
    public static TimingTrack Phrases(PhraseSegmentationAnalysis phrases) => TimingTrackEmitter.Phrases(phrases) with { Name = PhrasesTrackName };

    /// <summary>
    /// Approximate word boundaries, heuristically: a word is one maximal run of non-resting, non-<c>MBP</c>
    /// viseme marks -- <c>MBP</c> (a bilabial closure, the quietest/most-closed code on
    /// <see cref="VisemeApproximator"/>'s own openness ladder) and <see cref="VisemeApproximator.RestCode"/>
    /// both act as the "local energy valley" that splits one word from the next, since they are literally
    /// the lowest points of the same amplitude signal the viseme codes were already bucketed from. This is
    /// a heuristic subdivision, not real word-boundary detection -- no lyrics transcript exists to align to.
    /// </summary>
    public static TimingTrack Words(IReadOnlyList<VisemeMark> visemeMarks)
    {
        ArgumentNullException.ThrowIfNull(visemeMarks);

        var marks = new List<TimingMark>();
        long? runStart = null;
        var runEnd = 0L;

        foreach (var mark in visemeMarks.OrderBy(m => m.StartTicks))
        {
            var isValley = mark.Code is VisemeApproximator.RestCode or "MBP";
            if (isValley)
            {
                if (runStart is { } start && runEnd > start)
                {
                    marks.Add(new TimingMark("word", start, runEnd));
                }

                runStart = null;
                continue;
            }

            runStart ??= mark.StartTicks;
            runEnd = mark.EndTicks;
        }

        if (runStart is { } finalStart && runEnd > finalStart)
        {
            marks.Add(new TimingMark("word", finalStart, runEnd));
        }

        return new TimingTrack(WordsTrackName, EnforceExportableMarkDurations(marks));
    }

    /// <summary>The raw <see cref="VisemeApproximator"/> output, one mark per window, labelled with its own viseme code.</summary>
    public static TimingTrack Phonemes(IReadOnlyList<VisemeMark> visemeMarks)
    {
        ArgumentNullException.ThrowIfNull(visemeMarks);
        var marks = visemeMarks.OrderBy(m => m.StartTicks).Select(m => new TimingMark(m.Code, m.StartTicks, m.EndTicks)).ToList();
        return new TimingTrack(PhonemesTrackName, EnforceExportableMarkDurations(marks));
    }

    /// <summary>
    /// Real-song export failure fixed here: "Timing track 'Lyrics_Phonemes' mark
    /// (489986395-490000000 ticks) rounds to a non-positive duration at the 25ms xLights frame grid."
    /// A single <see cref="VisemeMark"/> window (or, for <see cref="Words"/>, a short word run) can
    /// legitimately be well under the 25ms xLights frame grid -- real speech regularly has phonemes
    /// shorter than that. Reuses <see cref="TimingTrackEmitter"/>'s own <c>Percussion</c>-track fix for
    /// exactly this failure mode (a too-short trailing mark, absorbed into its predecessor rather than
    /// emitted as its own unexportable mark) instead of re-deriving the same safety net here.
    /// </summary>
    private static List<TimingMark> EnforceExportableMarkDurations(List<TimingMark> marks) =>
        TimingTrackEmitter.CoalesceAdjacentSameLabel(TimingTrackEmitter.EnforceMinimumMarkDuration(marks, FrameClock.FrameIntervalTicks));
}
