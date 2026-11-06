namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan "richer analysis tools" Phase 1 (2026-11-01): one user-stated, approximate timed
/// instruction extracted from a <see cref="Contracts.CustomGenerationPrompt.Text"/> -- e.g. "hit the
/// word bang" near <c>00:00:24-00:00:25</c>. Deliberately carries a <see cref="Tolerance"/> rather than
/// treating <see cref="Start"/>/<see cref="End"/> as exact: a hand-typed mm:ss timestamp is never going
/// to line up with a beat-grid-quantized effect to the millisecond, and the guard rail this feeds
/// (<see cref="SectionScriptPromptBuilder"/>'s per-section scoping, and the post-compile compliance
/// check) must reason in terms of "approximately here," never "exactly here."
/// </summary>
/// <param name="Start">The stated (or range-start) time, as literally typed by the user.</param>
/// <param name="End">The stated range-end time, or <see langword="null"/> for a single-instant cue (in which case <see cref="EffectiveEnd"/> equals <see cref="Start"/>).</param>
/// <param name="Instruction">The free text from the same line as this cue's timestamp(s), with the timestamp token(s) themselves stripped out -- what the user actually wants to happen near this moment.</param>
/// <param name="Tolerance">How far (on either side) a real effect may land and still count as satisfying this cue -- see <see cref="CustomPromptTimedCueParser.DefaultTolerance"/> for the default and its own disclosed rationale.</param>
public sealed record TimedCue(TimeSpan Start, TimeSpan? End, string Instruction, TimeSpan Tolerance)
{
    public TimeSpan EffectiveEnd => End ?? Start;

    /// <summary>The tolerance-expanded window this cue is considered to cover, clamped to never go negative.</summary>
    public TimeSpan WindowStart => Start - Tolerance < TimeSpan.Zero ? TimeSpan.Zero : Start - Tolerance;

    public TimeSpan WindowEnd => EffectiveEnd + Tolerance;

    /// <summary>Whether this cue's tolerance-expanded window overlaps a <c>[sectionStart, sectionEnd)</c> real-time half-open interval, in seconds.</summary>
    public bool OverlapsSeconds(double sectionStartSeconds, double sectionEndSeconds) =>
        WindowStart.TotalSeconds < sectionEndSeconds && WindowEnd.TotalSeconds > sectionStartSeconds;
}
