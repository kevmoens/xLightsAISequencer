using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 4 task 2 ("The always-moving rule: gap-splitting pass that loops until no gap
/// survives... the original inserted one beat per gap and declared victory, leaving a 17-second frozen
/// block past the last detected beat"). Architecture §7: "No model holds unchanged longer than one bar...
/// Calm sections get gentler motion and a slower grid, never no motion." User feedback (2026-11-13)
/// scoped that one-bar-max guarantee to real negative space, exactly the section(s) a target was
/// actually named by; user feedback (2026-11-16), after that shipped and most of a real display went
/// dark for most of a real song, widened the scope back out -- see <see cref="FillGaps"/>'s own
/// <c>activeWindows</c> parameter, now every section the caller (<see cref="ChoreographyEngine"/>) does
/// not itself mark as declared negative space, not only the ones that happened to name the target
/// directly.
/// </summary>
public static class AlwaysMovingGapSplitter
{
    /// <summary>
    /// Inserts filler <see cref="EffectInstance"/>s from <paramref name="fillerPool"/> into every gap (a
    /// span with no existing effect) in <paramref name="existingEffects"/> on <paramref name="target"/>
    /// across <paramref name="coverageWindow"/>, looping bar-by-bar through a gap until none remains --
    /// this is the loop the original one-shot pass never performed. Each filler's own duration is
    /// <see cref="ComputeFillerDurationTicks"/>'s own choice, always strictly positive and never
    /// exceeding the containing bar's own span, so the gap after filling is guaranteed to hold no span
    /// longer than one bar. Returns the full merged, time-ordered effect list for <paramref name="target"/>
    /// (existing effects unchanged, byte-for-byte, interleaved with new filler effects) -- never mutates
    /// or reorders an existing effect.
    /// </summary>
    /// <param name="fillerPoolResolver">
    /// Build-plan Phase 15 §3: resolves, for any tick position, which section's own declared
    /// <see cref="FillerPool"/> applies there -- replaces a single fixed pool for the whole song now that
    /// filler is a per-section (and per-role) Choreographer declaration (<see cref="SectionFillerSchedule"/>
    /// is the real caller; a test or hand-authored-Look caller may pass a constant lambda for the old
    /// single-pool behavior). Each resolved pool keeps its own rotation cursor (keyed by pool identity,
    /// not shared globally), so switching sections mid-song never skips or repeats states in a pool this
    /// gap has not actually used yet.
    /// </param>
    /// <param name="activeWindows">
    /// The real windows (sorted, non-overlapping) where the always-moving guarantee holds for
    /// <paramref name="target"/>. User feedback (2026-11-13) scoped this to exactly the sections that
    /// named <paramref name="target"/> directly (measured against a real reference show: the prior
    /// "entire rest of the song" scoping averaged 0.0% idle time per prop vs. a real hand-made
    /// reference's 82.1%); user feedback (2026-11-16) widened it back out after that left most of a real
    /// display dark for most of a real song -- the caller (<see cref="ChoreographyEngine"/>) now passes
    /// every section's window except ones its own Look marks <c>DeclaredDarkness</c> (a deliberate
    /// negative-space moment is still exempt; an ordinary section that simply didn't happen to name this
    /// target is not). A gap entirely outside every active window is left fully dark (no filler, no
    /// one-bar cap); a gap inside an active window keeps the exact one-bar-max behavior this rule always
    /// had. An empty list means "never in play anywhere" (nothing to fill, matching today's own silent
    /// handling of a target this song's own Look library never addresses at all).
    /// </param>
    /// <param name="ceilingAtTick">
    /// User feedback (2026-11-13): replaces a single flat whole-song-average energy ceiling (the
    /// disclosed simplification <see cref="ChoreographyEngine"/> used to carry: "a per-tick-aware
    /// cadence would need this pass to re-enter each section's own declared ceiling") with the real
    /// per-tick section ceiling -- a quiet section now reliably gets the full one-bar-fraction
    /// <see cref="ComputeFillerDurationTicks"/> already allows it, instead of sometimes getting a
    /// tighter cap because some OTHER section elsewhere in the song happened to be louder.
    /// </param>
    public static IReadOnlyList<EffectInstance> FillGaps(
        ModelId target,
        IReadOnlyList<EffectInstance> existingEffects,
        TimeWindow coverageWindow,
        BarGrid barGrid,
        Func<long, FillerPool> fillerPoolResolver,
        IReadOnlyList<TimeWindow> activeWindows,
        Func<long, double> ceilingAtTick,
        IEffectRecipeRegistry recipeRegistry)
    {
        ArgumentNullException.ThrowIfNull(existingEffects);
        ArgumentNullException.ThrowIfNull(barGrid);
        ArgumentNullException.ThrowIfNull(fillerPoolResolver);
        ArgumentNullException.ThrowIfNull(activeWindows);
        ArgumentNullException.ThrowIfNull(ceilingAtTick);
        ArgumentNullException.ThrowIfNull(recipeRegistry);

        var result = new List<EffectInstance>();
        var cursorByPool = new Dictionary<FillerPool, int>();

        var sorted = existingEffects
            .Where(e => e.Target == target)
            .OrderBy(e => e.StartTicks)
            .ToList();

        var sortedActiveWindows = activeWindows.OrderBy(w => w.StartTicks).ToList();

        void FillGap(long gapStart, long gapEnd)
        {
            // Only fill the sub-ranges of [gapStart, gapEnd) that genuinely overlap a real active
            // window -- a sub-range this target is never "in play" for (no section's own Look
            // addresses it there) is left fully dark, never forced into motion.
            foreach (var active in sortedActiveWindows)
            {
                var segmentStart = Math.Max(gapStart, active.StartTicks);
                var segmentEnd = Math.Min(gapEnd, active.EndTicks);
                if (segmentEnd > segmentStart)
                {
                    FillActiveSubGap(segmentStart, segmentEnd);
                }
            }
        }

        void FillActiveSubGap(long gapStart, long gapEnd)
        {
            // Built as a local segment list first, then emitted, so a too-short trailing remainder (an
            // effect shorter than one exported frame -- XsqExporter cannot represent it at all, "rounds
            // to a non-positive duration") can be absorbed into its own preceding segment rather than
            // becoming its own doomed-to-fail effect.
            var segments = new List<(long Start, long End, string RecipeId)>();
            var position = gapStart;
            while (position < gapEnd)
            {
                // Confirmed as a real bug against a real export: BarAt(position) returns null for any
                // position before the first detected bar or after the last one (a real, common case --
                // a song's own lead-in/pre-roll before its beat grid locks in, or a trailing tail after
                // it ends). The old fallback, "the rest of the gap itself" as the bar window, silently
                // broke this method's own documented "never exceeding the containing bar's own span"
                // guarantee -- producing one giant filler (observed: a full 60s "On" effect) instead of
                // looping bar-by-bar, exactly the frozen-block defect this splitter exists to prevent.
                // FallbackBarTicks (a real bar's own measured duration from elsewhere in this same song)
                // is what "one bar" should mean here too, not an unbounded span.
                var barWindow = barGrid.BarAt(position)?.Window;
                var maxGapTicks = barWindow is { } w
                    ? Math.Max(1, w.EndTicks - w.StartTicks)
                    : Math.Min(FallbackBarTicks(barGrid), gapEnd - position);
                var fillerDurationTicks = ComputeFillerDurationTicks(maxGapTicks, ceilingAtTick(position));

                var fillerEnd = Math.Min(position + fillerDurationTicks, gapEnd);
                if (fillerEnd <= position)
                {
                    // Pathological zero/negative duration (shouldn't happen given the floor below) --
                    // never loop forever; place one frame-sized filler and stop.
                    fillerEnd = gapEnd;
                }

                var remainder = gapEnd - fillerEnd;
                if (remainder > 0 && remainder < FrameClock.FrameIntervalTicks)
                {
                    // The remainder after this segment would itself round to zero at the exported frame
                    // grid -- absorb it into this segment now rather than emit an unrepresentable one.
                    fillerEnd = gapEnd;
                }

                var pool = fillerPoolResolver(position);
                var poolCursor = cursorByPool.GetValueOrDefault(pool);
                segments.Add((position, fillerEnd, pool.RecipeIdAt(poolCursor)));
                cursorByPool[pool] = poolCursor + 1;
                position = fillerEnd;
            }

            foreach (var (start, end, recipeId) in segments)
            {
                if (end - start < FrameClock.FrameIntervalTicks)
                {
                    // The gap as a whole (or, pathologically, a lone remaining segment) is itself
                    // shorter than one exported frame -- below the exporter's own representable
                    // resolution; the surrounding real effects' own frame-rounding already covers it, so
                    // this is not a real always-moving violation, just dropped.
                    continue;
                }

                result.Add(recipeRegistry.CreateVerified(recipeId, target, start, end));
            }
        }

        var cursor = coverageWindow.StartTicks;
        foreach (var effect in sorted)
        {
            if (effect.StartTicks > cursor)
            {
                FillGap(cursor, effect.StartTicks);
            }

            result.Add(effect);
            cursor = Math.Max(cursor, effect.EndTicks);
        }

        if (cursor < coverageWindow.EndTicks)
        {
            FillGap(cursor, coverageWindow.EndTicks);
        }

        return result.OrderBy(e => e.StartTicks).ToList();
    }

    /// <summary>
    /// A calm section (low <paramref name="sectionEnergyCeiling"/>) gets a slower grid -- fewer, longer
    /// filler segments, up to the full bar -- while a high-energy section gets a faster grid -- more,
    /// shorter segments -- but <paramref name="sectionEnergyCeiling"/> never reaches zero duration: "never
    /// no motion" (architecture §7) holds because every branch below returns a strictly positive,
    /// at-most-one-bar duration.
    /// </summary>
    public static long ComputeFillerDurationTicks(long maxGapTicks, double sectionEnergyCeiling)
    {
        var fraction = sectionEnergyCeiling switch
        {
            <= 0.3 => 1.0,
            <= 0.6 => 0.5,
            _ => 0.25,
        };

        return Math.Max(1, (long)Math.Round(maxGapTicks * fraction));
    }

    /// <summary>
    /// A real bar's own duration from elsewhere in this same song's measured grid, for a gap position
    /// outside the known bar range (<see cref="BarGrid.BarAt"/> returns <see langword="null"/> there) --
    /// the first bar's own span when any bar exists at all (bars are close enough to uniform within one
    /// song that this is a reasonable stand-in), or a conservative 2-second fallback in the fully
    /// degenerate case of a bar grid with no bars at all.
    /// </summary>
    private static long FallbackBarTicks(BarGrid barGrid)
    {
        if (barGrid.Bars.Count > 0)
        {
            var first = barGrid.Bars[0].Window;
            return Math.Max(1, first.EndTicks - first.StartTicks);
        }

        return 2 * TimeSpan.TicksPerSecond; // A plausible single-bar duration order of magnitude, same canonical 100ns tick unit this codebase uses throughout.
    }

    /// <summary>
    /// The longest gap (no existing effect) on <paramref name="target"/> within <paramref name="coverageWindow"/>,
    /// in ticks -- 0 when fully covered. Used to verify the always-moving rule against *compiled*
    /// output (the real acceptance check, not just this splitter's own internal invariant).
    /// </summary>
    /// <remarks>
    /// Bug fix (2026-11-13): <paramref name="effects"/> is now filtered (and clamped) to only the span
    /// overlapping <paramref name="coverageWindow"/> before measuring gaps -- previously every one of
    /// <paramref name="target"/>'s effects across its ENTIRE input list was considered regardless of
    /// <paramref name="coverageWindow"/>'s own bounds, which was harmless while every real caller always
    /// passed the whole song as the window (every effect trivially fell inside it), but produces a
    /// nonsensical "gap" far exceeding the window's own span once a caller legitimately asks about a
    /// narrower window (e.g. one target's own real "in play" window) -- a real effect from a much
    /// earlier or later unrelated active window would otherwise be read as part of THIS window's own
    /// timeline.
    /// </remarks>
    public static long LargestGapTicks(ModelId target, IReadOnlyList<EffectInstance> effects, TimeWindow coverageWindow)
    {
        var sorted = effects
            .Where(e => e.Target == target && e.StartTicks < coverageWindow.EndTicks && e.EndTicks > coverageWindow.StartTicks)
            .OrderBy(e => e.StartTicks)
            .ToList();

        long largest = 0;
        var cursor = coverageWindow.StartTicks;

        foreach (var effect in sorted)
        {
            var start = Math.Max(effect.StartTicks, coverageWindow.StartTicks);
            var end = Math.Min(effect.EndTicks, coverageWindow.EndTicks);
            if (start > cursor)
            {
                largest = Math.Max(largest, start - cursor);
            }

            cursor = Math.Max(cursor, end);
        }

        if (cursor < coverageWindow.EndTicks)
        {
            largest = Math.Max(largest, coverageWindow.EndTicks - cursor);
        }

        return largest;
    }
}
