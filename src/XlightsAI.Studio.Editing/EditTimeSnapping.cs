namespace XlightsAI.Studio.Editing;

/// <summary>
/// Step 019's own "optional snap" half: rounds a candidate drag position to the nearest multiple of
/// an interval (e.g. the xLights frame grid), or returns it unchanged when no interval is supplied.
/// Deliberately a pure, stateless helper a caller applies before constructing a <see cref="MoveEffect"/>
/// (Section 3 "Architectural design notes": snapping is a UI-gesture concern), not something
/// <see cref="IEditTransactionService.ApplyAsync"/> does implicitly.
/// </summary>
public static class EditTimeSnapping
{
    /// <summary>
    /// Rounds <paramref name="ticks"/> to the nearest multiple of <paramref name="snapIntervalTicks"/>
    /// (ties round away from zero). A <see langword="null"/> or non-positive interval means "no
    /// snapping" and returns <paramref name="ticks"/> unchanged — snap is optional, never forced.
    /// </summary>
    public static long SnapTicks(long ticks, long? snapIntervalTicks)
    {
        if (snapIntervalTicks is not { } interval || interval <= 0)
        {
            return ticks;
        }

        var quotient = (double)ticks / interval;
        return (long)Math.Round(quotient, MidpointRounding.AwayFromZero) * interval;
    }
}
