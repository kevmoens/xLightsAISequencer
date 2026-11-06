using System.Text.RegularExpressions;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan "richer analysis tools" Phase 1 (2026-11-01): extracts <see cref="TimedCue"/>s from a
/// user's free-text custom generation prompt. Deliberately NARROW, mechanical extraction -- only a
/// literal <c>mm:ss</c> or <c>hh:mm:ss</c> token (optionally a <c>start-end</c> range) is ever treated as
/// a timed cue. This is not general NLP: a prose time reference like "the first 11 seconds" is NOT
/// extracted (recognizing arbitrary prose durations reliably is open-ended and exactly the fragile
/// free-text-intent-matching this design deliberately avoids) -- that text stays exactly where it
/// already was, in the unfiltered custom-prompt block every section's prompt already receives.
///
/// One line of the prompt is treated as one potential cue source: every timestamp token found on that
/// line shares that line's own text (with the timestamp tokens themselves stripped out) as its
/// <see cref="TimedCue.Instruction"/> -- matching the natural way a user writes "do X at these N
/// moments: t1 t2 t3" on one line/wrapped paragraph, confirmed against the real prompt that motivated
/// this feature.
/// </summary>
public static class CustomPromptTimedCueParser
{
    /// <summary>
    /// How far a stated timestamp is trusted to be from where a compiled effect should actually land.
    /// 1.5 seconds is a disclosed, deliberately modest default -- generous enough to absorb normal
    /// hand-typed imprecision and beat-grid quantization, small enough that "satisfies the cue" still
    /// means something close to what the user actually asked for, not "anywhere in this section."
    /// </summary>
    public static readonly TimeSpan DefaultTolerance = TimeSpan.FromSeconds(1.5);

    private static readonly Regex TimestampRegex = new(
        @"(?<start>\d{1,2}:\d{2}(?::\d{2})?)(?:\s*-\s*(?<end>\d{1,2}:\d{2}(?::\d{2})?))?",
        RegexOptions.Compiled);

    public static IReadOnlyList<TimedCue> Parse(string? text, TimeSpan? tolerance = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var effectiveTolerance = tolerance ?? DefaultTolerance;
        var cues = new List<TimedCue>();

        foreach (var rawLine in text.Split('\n'))
        {
            var matches = TimestampRegex.Matches(rawLine);
            if (matches.Count == 0)
            {
                continue;
            }

            var instruction = Regex.Replace(TimestampRegex.Replace(rawLine, " "), @"\s+", " ").Trim();
            if (instruction.Length == 0)
            {
                // A line that is ONLY timestamps, with no surrounding instruction text, has nothing
                // actionable to carry -- never fabricate an instruction for it.
                continue;
            }

            foreach (Match match in matches)
            {
                if (!TryParseTimestamp(match.Groups["start"].Value, out var start))
                {
                    continue;
                }

                TimeSpan? end = null;
                if (match.Groups["end"].Success && TryParseTimestamp(match.Groups["end"].Value, out var parsedEnd))
                {
                    end = parsedEnd;
                }

                cues.Add(new TimedCue(start, end, instruction, effectiveTolerance));
            }
        }

        return cues;
    }

    /// <summary>Parses "mm:ss" (2 colon-separated parts) or "hh:mm:ss" (3 parts) -- never ambiguous once the part count is known, so no locale/format guessing is needed.</summary>
    private static bool TryParseTimestamp(string token, out TimeSpan value)
    {
        var parts = token.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out var minutes) && int.TryParse(parts[1], out var seconds))
        {
            value = new TimeSpan(0, 0, minutes, seconds);
            return true;
        }

        if (parts.Length == 3 && int.TryParse(parts[0], out var hours) && int.TryParse(parts[1], out var minutes3) && int.TryParse(parts[2], out var seconds3))
        {
            value = new TimeSpan(0, hours, minutes3, seconds3);
            return true;
        }

        value = default;
        return false;
    }
}
