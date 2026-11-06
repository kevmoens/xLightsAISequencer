using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan "richer analysis tools" Phase 6 (2026-11-02): answers the user's own original ask ("can you
/// solve how to sync to the actual word?") -- when a <see cref="TimedCue.Instruction"/> names a real word
/// that also appears in <see cref="MusicModel.Transcript"/> within the cue's own tolerance window, this
/// snaps the cue onto that word's real (approximate -- see <see cref="TranscribedWord"/>'s own doc
/// comment) timing and tightens its tolerance, the same "verified real moment beats the user's raw typed
/// guess" pattern <see cref="TimedCueTransientResolver"/> already established for loud transients. Applied
/// AFTER <see cref="TimedCueTransientResolver"/> in the pipeline (word match is a stronger, more specific
/// signal than "a loud moment happened nearby") -- a cue already snapped to a transient is still eligible
/// to snap again to a matching word if one exists in its own (by-then-tightened) window.
/// </summary>
public static class TimedCueTranscriptResolver
{
    /// <summary>
    /// Wider than <see cref="TimedCueTransientResolver.SnappedTolerance"/>: a transcribed word's own
    /// timing is itself a disclosed approximation (segment-level timestamps, words evenly distributed
    /// within), not as precise as a verified loud transient's own instant.
    /// </summary>
    public static readonly TimeSpan SnappedTolerance = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// A matched instruction word must be at least this many characters -- shorter words ("a", "to", "in")
    /// match too many transcript words to be a meaningful anchor. Internal (not private): reused by
    /// <see cref="TimedCueMultiHitExpander"/>'s own word-repeat detection, the same matching rule applied
    /// to a different question ("does this word occur more than once in this range" rather than "is there
    /// one nearby").
    /// </summary>
    internal const int MinimumMatchableWordLength = 3;

    public static IReadOnlyList<TimedCue> Resolve(IReadOnlyList<TimedCue> cues, IReadOnlyList<TranscribedWord>? transcript)
    {
        ArgumentNullException.ThrowIfNull(cues);
        if (transcript is null || transcript.Count == 0 || cues.Count == 0)
        {
            return cues;
        }

        var resolved = new List<TimedCue>(cues.Count);
        foreach (var cue in cues)
        {
            var instructionWords = Tokenize(cue.Instruction);
            if (instructionWords.Count == 0)
            {
                resolved.Add(cue);
                continue;
            }

            var windowStartTicks = cue.WindowStart.Ticks;
            var windowEndTicks = cue.WindowEnd.Ticks;

            TranscribedWord? best = null;
            var bestDistance = long.MaxValue;
            foreach (var word in transcript)
            {
                if (word.StartTicks > windowEndTicks || word.EndTicks < windowStartTicks)
                {
                    continue;
                }

                var cleaned = Clean(word.Text);
                if (cleaned.Length < MinimumMatchableWordLength || !instructionWords.Contains(cleaned))
                {
                    continue;
                }

                var distance = Math.Abs(word.StartTicks - cue.Start.Ticks);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = word;
                }
            }

            if (best is null)
            {
                resolved.Add(cue);
                continue;
            }

            resolved.Add(cue with
            {
                Start = TimeSpan.FromTicks(best.StartTicks),
                End = TimeSpan.FromTicks(best.EndTicks),
                Tolerance = SnappedTolerance,
            });
        }

        return resolved;
    }

    internal static HashSet<string> Tokenize(string instruction) =>
        instruction
            .Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(Clean)
            .Where(w => w.Length >= MinimumMatchableWordLength)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    internal static string Clean(string word) => word.Trim().Trim(',', '.', '!', '?', ';', ':', '"', '\'', '(', ')');
}
