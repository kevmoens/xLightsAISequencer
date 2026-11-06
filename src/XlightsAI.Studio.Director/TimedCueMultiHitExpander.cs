using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan "richer analysis tools" multi-hit phase (2026-11-02): user feedback -- a range like
/// <c>"00:00:37-00:00:39"</c> meant to cover a cluster of repeated hits ("BANG BANG BANG") previously
/// became exactly ONE <see cref="TimedCue"/>, and <see cref="TimedCueComplianceChecker"/> is satisfied the
/// instant ANY single qualifying effect lands anywhere in that window -- one flash anywhere in a 2-second
/// range already "counted," even though the user meant three separate hits.
///
/// <para>
/// This expands a single ranged cue into one instant cue PER real repeated hit actually found inside it,
/// using the real detectors this build plan already shipped -- never inventing a hit count from the
/// instruction text alone (which would mean parsing "BANG BANG BANG" as "three," a fragile word-counting
/// heuristic this design deliberately avoids). Two independent signals are tried, in order:
/// </para>
/// <list type="number">
/// <item>A real word the instruction itself names (e.g. "bang") occurring more than once in
/// <see cref="MusicModel.Transcript"/> within the cue's own range -- the strongest, most specific signal
/// (the user's own word, literally repeated).</item>
/// <item>Falling back to real detected <see cref="TransientHit"/>s within the range when no repeated-word
/// match exists (no transcript, or the instruction names no word that recurs) -- a loud moment repeating
/// is still real evidence of "more than one hit here," even without knowing what word (if any) was sung.</item>
/// </list>
/// <para>
/// A cue with fewer than two matches on either signal is left completely unchanged (single-instant cues,
/// and ranges with only one real hit, are untouched -- this is purely additive, never a regression for the
/// cues this mechanism does not apply to). Every expanded sub-cue carries the SAME instruction text, so the
/// Choreographer sees "do this" once per real occurrence, and <see cref="TimedCueComplianceChecker"/>
/// (completely unchanged) now independently verifies each one.
/// </para>
/// </summary>
public static class TimedCueMultiHitExpander
{
    public static IReadOnlyList<TimedCue> Expand(
        IReadOnlyList<TimedCue> cues, IReadOnlyList<TranscribedWord>? transcript, IReadOnlyList<TransientHit>? transientHits)
    {
        ArgumentNullException.ThrowIfNull(cues);
        if (cues.Count == 0)
        {
            return cues;
        }

        var result = new List<TimedCue>();
        foreach (var cue in cues)
        {
            // Only a genuine range (an explicit, distinct End) can plausibly cover a cluster of repeated
            // hits -- a bare instant already names one specific moment, nothing to split.
            if (cue.End is not { } end || end <= cue.Start)
            {
                result.Add(cue);
                continue;
            }

            var expanded = TryExpandByRepeatedWord(cue, end, transcript) ?? TryExpandByMultipleTransients(cue, end, transientHits);
            result.AddRange(expanded ?? [cue]);
        }

        return result;
    }

    private static IReadOnlyList<TimedCue>? TryExpandByRepeatedWord(TimedCue cue, TimeSpan end, IReadOnlyList<TranscribedWord>? transcript)
    {
        if (transcript is null || transcript.Count == 0)
        {
            return null;
        }

        var instructionWords = TimedCueTranscriptResolver.Tokenize(cue.Instruction);
        if (instructionWords.Count == 0)
        {
            return null;
        }

        var startTicks = cue.Start.Ticks;
        var endTicks = end.Ticks;

        var matches = transcript
            .Where(w => w.StartTicks >= startTicks && w.EndTicks <= endTicks)
            .Where(w => instructionWords.Contains(TimedCueTranscriptResolver.Clean(w.Text)))
            .OrderBy(w => w.StartTicks)
            .ToList();

        if (matches.Count < 2)
        {
            return null;
        }

        return matches
            .Select(w => new TimedCue(TimeSpan.FromTicks(w.StartTicks), TimeSpan.FromTicks(w.EndTicks), cue.Instruction, TimedCueTranscriptResolver.SnappedTolerance))
            .ToList();
    }

    private static IReadOnlyList<TimedCue>? TryExpandByMultipleTransients(TimedCue cue, TimeSpan end, IReadOnlyList<TransientHit>? transientHits)
    {
        if (transientHits is null || transientHits.Count == 0)
        {
            return null;
        }

        var startTicks = cue.Start.Ticks;
        var endTicks = end.Ticks;

        var matches = transientHits
            .Where(h => h.TimeTicks >= startTicks && h.TimeTicks <= endTicks)
            .OrderBy(h => h.TimeTicks)
            .ToList();

        if (matches.Count < 2)
        {
            return null;
        }

        return matches
            .Select(h => new TimedCue(TimeSpan.FromTicks(h.TimeTicks), null, cue.Instruction, TimedCueTransientResolver.SnappedTolerance))
            .ToList();
    }
}
