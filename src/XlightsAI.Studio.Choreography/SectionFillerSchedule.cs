using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 15 §3: one section's own name-based filler plan, resolved from its own
/// <see cref="SectionScript.FillerEffects"/>/<see cref="SectionScript.FillerEffectsByRole"/> into real
/// <see cref="FillerPool"/>s (recipe ids, not bare names) -- kept name-based at the
/// <see cref="SectionScriptCompiler"/> level (see that type's own filler-plan output) and only resolved
/// into actual <see cref="FillerPool"/>s once a caller (ordinarily <c>XlightsAI.Studio.Director.ChoreographyGenerationPipeline</c>)
/// has a real <c>EffectRecipeCatalog</c> to resolve names against.
/// </summary>
/// <param name="Window">This entry's own section bar-range window, in ticks.</param>
/// <param name="Flat">The section-wide default pool, used for any target with no matching <paramref name="ByRoleKey"/> entry (or when none is declared).</param>
/// <param name="ByRoleKey">Optional per-role narrowing, keyed by the same plain role-key strings <see cref="SectionScript.FillerEffectsByRole"/> uses (e.g. <c>"Foundation"</c>) -- <see langword="null"/>/empty when the section declared no role-specific lists.</param>
public sealed record SectionFillerWindow(TimeWindow Window, FillerPool Flat, IReadOnlyDictionary<string, FillerPool>? ByRoleKey = null);

/// <summary>
/// Build-plan Phase 15 §3: resolves, for any tick position and target, which section's own declared
/// filler plan applies and (when that target classifies into a role the section narrowed) which of that
/// section's pools to use -- the structural consequence of moving filler from one whole-song pool
/// (<c>AiChoreographyWholeSongGenerator.BuildFillerPool</c>, deleted by this phase) to a per-section
/// declaration, now that <see cref="AlwaysMovingGapSplitter.FillGaps"/>'s own gap-filling pass still runs
/// once per target across the WHOLE SONG (a gap can span a section boundary -- see
/// <see cref="ChoreographyEngine.Compile"/>'s own doc comment), not per section.
///
/// Deliberately lives in <c>Choreography</c>, not <c>Director</c> (which owns the real
/// <c>BandRole</c> enum and <c>BandEnergyGroupRoleMapper</c> that produces it): <paramref name="roleKeyOf"/>
/// is supplied by the Director-layer caller as a plain string-returning delegate, so this type itself
/// never needs to know what a "role" even is -- the same reason <see cref="SectionScript.FillerEffectsByRole"/>'s
/// own keys are kept as plain strings in this project (Director depends on Choreography, never the reverse).
/// </summary>
public sealed class SectionFillerSchedule
{
    private readonly IReadOnlyList<SectionFillerWindow> _windows;
    private readonly Func<ModelId, string?> _roleKeyOf;

    /// <param name="windows">This song's own per-section filler windows -- at least one is required (the always-moving rule must never have nothing to fall back to). Sorted by start tick internally; callers may supply them in any order.</param>
    /// <param name="roleKeyOf">Resolves a target to its own role key (e.g. <c>"Foundation"</c>), or <see langword="null"/> when the target classifies into no known role -- defaults to "no role for anyone" when omitted.</param>
    /// <exception cref="ArgumentException"><paramref name="windows"/> is empty.</exception>
    public SectionFillerSchedule(IReadOnlyList<SectionFillerWindow> windows, Func<ModelId, string?>? roleKeyOf = null)
    {
        ArgumentNullException.ThrowIfNull(windows);
        if (windows.Count == 0)
        {
            throw new ArgumentException("At least one filler window is required -- the always-moving rule must always have a real pool to fall back to.", nameof(windows));
        }

        _windows = windows.OrderBy(w => w.Window.StartTicks).ToList();
        _roleKeyOf = roleKeyOf ?? (_ => null);
    }

    /// <summary>A trivial single-window schedule spanning the whole song with no role narrowing -- the pre-Phase-15 "one global pool" shape, for callers (hand-authored-Look tests, mainly) that have no real <see cref="SectionScript"/> to derive a per-section plan from.</summary>
    public static SectionFillerSchedule Flat(FillerPool pool, long songEndTicks) =>
        new([new SectionFillerWindow(new TimeWindow(0, songEndTicks), pool)]);

    /// <summary>
    /// The pool to draw the next filler recipe id from at <paramref name="tick"/> for <paramref name="target"/>
    /// -- the containing section's own role-specific pool when <paramref name="target"/> classifies into a
    /// role that section narrowed, else that section's own flat pool. A tick outside every declared window
    /// (a song's own lead-in/pre-roll before its beat grid locks in, or a trailing tail after the last
    /// section -- both real, documented cases, see <see cref="AlwaysMovingGapSplitter.FillGaps"/>'s own doc
    /// comment) falls back to the nearest window in time, never an invented pool.
    /// </summary>
    public FillerPool Resolve(long tick, ModelId target)
    {
        var window = _windows.FirstOrDefault(w => tick >= w.Window.StartTicks && tick < w.Window.EndTicks)
            ?? (tick < _windows[0].Window.StartTicks ? _windows[0] : _windows[^1]);

        var roleKey = _roleKeyOf(target);
        if (roleKey is not null && window.ByRoleKey is { } byRole && byRole.TryGetValue(roleKey, out var rolePool))
        {
            return rolePool;
        }

        return window.Flat;
    }
}
