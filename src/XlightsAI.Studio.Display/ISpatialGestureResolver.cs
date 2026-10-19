using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 064 ("Map pitch intervals/movement to jumps, holds and path traversal"). Section 3
/// proposes a bare <c>Resolve(MelodicGesture, SpatialPath, SpatialDisplayGraph, ResolutionSettings)</c>
/// signature; see <see cref="SpatialGestureResolver"/> remarks for the disclosed, deliberate deviation to a
/// whole-<see cref="MelodyTrack"/> signature instead -- the same "walk a whole ordered sequence in one call,
/// not one gesture at a time" shape <see cref="IMelodicGestureMapper.MapToEffects"/> already established for
/// Phase 07's own mapper, for the identical reason: resolving relative motion against path position requires
/// a cursor that persists across the sequence, which a bare per-gesture call cannot carry without either an
/// impure mutable resolver or external cursor-threading neither Section 3 nor any existing convention in
/// this repository uses.
/// </summary>
public interface ISpatialGestureResolver
{
    /// <exception cref="SpatialDisplayGraphInvalidException">
    /// <paramref name="path"/> has no ordered models, <paramref name="window"/> is not a valid non-empty
    /// <c>[start, end)</c> range, <paramref name="settings"/> names an unknown
    /// <see cref="ResolutionSettings.BoundaryPolicy"/>, or <paramref name="path"/> references a
    /// <paramref name="display"/> node that does not exist and <see cref="ResolutionSettings.SkipUnavailable"/>
    /// is <see langword="false"/>. Thrown before any <see cref="ResolvedGesture"/> is constructed.
    /// </exception>
    IReadOnlyList<ResolvedGesture> Resolve(
        MelodyTrack track,
        TimeWindow window,
        SpatialPath path,
        SpatialDisplayGraph display,
        ResolutionSettings settings);
}
