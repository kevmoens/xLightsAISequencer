using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;

namespace XlightsAI.Studio.Display;

/// <summary>
/// Default <see cref="ISpatialGestureResolver"/>. Walks <see cref="MelodyTrack.Notes"/> in order (already
/// guaranteed sorted/non-overlapping by <see cref="MelodyTrackValidator"/>), tracking one "current index"
/// into the effective path's own position list -- starting at index <c>0</c>, the same convention
/// <see cref="MelodicGestureMapper"/> already established for Phase 07's own mapper -- and moving that
/// index per <see cref="MelodicGesture.Motion"/>: <see cref="PitchMotion.StepUp"/>/<see cref="PitchMotion.StepDown"/>
/// move by <see cref="StepIndexDelta"/>; <see cref="PitchMotion.LeapUp"/>/<see cref="PitchMotion.LeapDown"/>
/// move by <see cref="LeapIndexDelta"/> (identical index distances to <see cref="MelodicGestureMapper"/>, so
/// resolving the same gesture sequence against the same path produces the same walk, independent of which
/// recipe -- if any -- eventually renders it). <see cref="PitchMotion.Repeat"/> and <see cref="PitchMotion.Rest"/>
/// both pulse the current index unchanged, tagged <see cref="PathTraversalKind.Hold"/> -- this resolver
/// never suppresses a <see cref="PitchMotion.Rest"/> (Step 064's own implementation guidance: "separate
/// mapping from effect recipe rendering" -- whether a rest should emit no light is a recipe-rendering
/// decision, not a geometry-resolution one). <see cref="PitchMotion.Hold"/> never starts a new
/// <see cref="ResolvedGesture"/> once at least one has been produced -- it extends the immediately
/// preceding entry's own <see cref="ResolvedGesture.EndTicks"/>.
///
/// <para>
/// <b>Verified directly against Phase 07's own already-accepted worked example:</b> <c>C4,D4,E4,G4,E4,D4,C4</c>
/// against a 5-position path with <see cref="GestureBoundaryPolicy.Clamp"/> produces the identical index
/// sequence <see cref="MelodicGestureMapper"/> already proved correct: <c>0,1,2,4,2,1,0</c>.
/// </para>
///
/// <para>
/// <b>Disclosed architecture deviation from Section 3's bare per-gesture
/// <c>Resolve(MelodicGesture, SpatialPath, SpatialDisplayGraph, ResolutionSettings)</c>:</b> see
/// <see cref="ISpatialGestureResolver"/> remarks.
/// </para>
///
/// <para>
/// <b>Disclosed interpretation of "sweep and reverse" (Step 064's own implementation guidance, verbatim:
/// "Resolve step, leap, hold, sweep and reverse against path length"):</b> Section 3 does not define these
/// two words further. They are read here as the two non-default <see cref="GestureBoundaryPolicy"/> values a
/// caller may opt into for a move whose raw target exceeds the path's own bounds --
/// <see cref="GestureBoundaryPolicy.Sweep"/> (visit every intermediate node up to the edge, dividing the
/// gesture's own duration across them) and <see cref="GestureBoundaryPolicy.Reverse"/> (reflect the
/// overshoot back into the path, reversing the walk's own direction, via the standard triangle-wave/
/// "unfolded billiard path" reflection so a single large overshoot can bounce more than once). The resulting
/// <see cref="ResolvedGesture.GestureKind"/> is tagged <see cref="PathTraversalKind.Sweep"/> for every entry
/// a <see cref="GestureBoundaryPolicy.Sweep"/> move produces, and <see cref="PathTraversalKind.Reverse"/> for
/// as long as the <see cref="GestureBoundaryPolicy.Reverse"/> walk's own unbounded cumulative position
/// remains outside the path's first, unreflected pass (<c>[0, length - 1]</c>) -- a move that never leaves
/// that first pass is always tagged plain <see cref="PathTraversalKind.Step"/>/<see cref="PathTraversalKind.Leap"/>,
/// so a reader can tell from <see cref="ResolvedGesture.GestureKind"/> alone whether a boundary was ever
/// reached, under either boundary policy.
/// </para>
///
/// <para>
/// <b>Disclosed <see cref="ResolvedGesture.Intensity"/> derivation (an additive field Section 3 declares
/// but does not define):</b> <c>|IntervalSemitones| / 12</c> clamped to <c>[0, 1]</c> for a moving gesture --
/// directly derived from <see cref="MelodicGesture.IntervalSemitones"/>, already available, never invented --
/// and a fixed <see cref="HoldIntensity"/> for a non-moving one, since <see cref="PitchMotion.Repeat"/>/
/// <see cref="PitchMotion.Hold"/>/<see cref="PitchMotion.Rest"/> all carry <c>IntervalSemitones == 0</c> by
/// <see cref="MelodicGesture"/>'s own contract and a literal <c>0</c> intensity would read as "no gesture at
/// all" rather than "a sustained/pulsed one."
/// </para>
/// </summary>
public sealed class SpatialGestureResolver : ISpatialGestureResolver
{
    /// <summary>Index distance <see cref="PitchMotion.StepUp"/>/<see cref="PitchMotion.StepDown"/> move the walk -- matches <see cref="MelodicGestureMapper.StepIndexDelta"/>.</summary>
    public const int StepIndexDelta = 1;

    /// <summary>Index distance <see cref="PitchMotion.LeapUp"/>/<see cref="PitchMotion.LeapDown"/> move the walk -- matches <see cref="MelodicGestureMapper.LeapIndexDelta"/>.</summary>
    public const int LeapIndexDelta = 2;

    /// <summary>Fixed <see cref="ResolvedGesture.Intensity"/> for a <see cref="PathTraversalKind.Hold"/> entry (see remarks above).</summary>
    public const double HoldIntensity = 0.25;

    public IReadOnlyList<ResolvedGesture> Resolve(
        MelodyTrack track,
        TimeWindow window,
        SpatialPath path,
        SpatialDisplayGraph display,
        ResolutionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(settings);

        if (path.OrderedModels.Count == 0)
        {
            throw new SpatialDisplayGraphInvalidException(
                $"Resolve requires path '{path.Name}' to have at least one ordered model; the supplied path was empty.");
        }

        if (window.EndTicks <= window.StartTicks)
        {
            throw new SpatialDisplayGraphInvalidException(
                $"Resolve requires a valid, non-empty [start, end) window (received [{window.StartTicks}, {window.EndTicks})).");
        }

        if (!GestureBoundaryPolicy.All.Contains(settings.BoundaryPolicy))
        {
            throw new SpatialDisplayGraphInvalidException(
                $"Unknown gesture boundary policy '{settings.BoundaryPolicy}'. Known policies: {string.Join(", ", GestureBoundaryPolicy.All)}.");
        }

        var knownNodeIds = new HashSet<ModelId>(display.Nodes.Select(n => n.Id));
        var effectivePath = new List<ModelId>(path.OrderedModels.Count);
        foreach (var id in path.OrderedModels)
        {
            if (knownNodeIds.Contains(id))
            {
                effectivePath.Add(id);
                continue;
            }

            if (!settings.SkipUnavailable)
            {
                throw new SpatialDisplayGraphInvalidException(
                    $"Path '{path.Name}' references node '{id.Value}', which does not exist in the supplied display graph.");
            }
        }

        if (effectivePath.Count == 0)
        {
            throw new SpatialDisplayGraphInvalidException(
                $"Path '{path.Name}' has no nodes remaining in the display graph once unavailable nodes are excluded.");
        }

        var length = effectivePath.Count;
        var gesturesByNoteId = track.Gestures.ToDictionary(g => g.NoteId);
        var results = new List<ResolvedGesture>();
        var currentIndex = 0;
        var virtualIndex = 0;

        foreach (var note in track.Notes)
        {
            if (note.StartTicks < window.StartTicks || note.StartTicks >= window.EndTicks)
            {
                continue; // outside the requested preview window -- silently excluded, not an error.
            }

            if (!gesturesByNoteId.TryGetValue(note.Id, out var gesture))
            {
                continue; // defensive: MelodyTrackValidator/MelodicContourClassifier already guarantee one gesture per note.
            }

            if (gesture.Motion is PitchMotion.Hold && results.Count > 0)
            {
                var last = results[^1];
                results[^1] = last with { EndTicks = note.EndTicks };
                continue;
            }

            var (kind, baseDelta) = gesture.Motion switch
            {
                PitchMotion.StepUp => (PathTraversalKind.Step, StepIndexDelta),
                PitchMotion.StepDown => (PathTraversalKind.Step, -StepIndexDelta),
                PitchMotion.LeapUp => (PathTraversalKind.Leap, LeapIndexDelta),
                PitchMotion.LeapDown => (PathTraversalKind.Leap, -LeapIndexDelta),
                _ => (PathTraversalKind.Hold, 0), // Repeat, Rest, or an unextendable leading Hold.
            };

            if (baseDelta == 0)
            {
                results.Add(new ResolvedGesture(effectivePath[currentIndex], note.StartTicks, note.EndTicks, PathTraversalKind.Hold, HoldIntensity));
                continue;
            }

            var intensity = Math.Clamp(Math.Abs(gesture.IntervalSemitones) / 12.0, 0.0, 1.0);

            if (settings.BoundaryPolicy == GestureBoundaryPolicy.Reverse)
            {
                virtualIndex += baseDelta;
                currentIndex = Reflect(virtualIndex, length);
                // Still within the walk's first, unreflected pass through the path -> a plain Step/Leap.
                // Once the unbounded cumulative walk has gone past either edge, every further position is a
                // reflection of that overshoot, tagged Reverse for as long as it remains outside that range.
                var reflectedKind = virtualIndex >= 0 && virtualIndex <= length - 1 ? kind : PathTraversalKind.Reverse;
                results.Add(new ResolvedGesture(effectivePath[currentIndex], note.StartTicks, note.EndTicks, reflectedKind, intensity));
                continue;
            }

            var raw = currentIndex + baseDelta;
            if (raw >= 0 && raw < length)
            {
                currentIndex = raw;
                results.Add(new ResolvedGesture(effectivePath[currentIndex], note.StartTicks, note.EndTicks, kind, intensity));
                continue;
            }

            var boundary = raw >= length ? length - 1 : 0;

            if (settings.BoundaryPolicy == GestureBoundaryPolicy.Sweep)
            {
                var step = boundary >= currentIndex ? 1 : -1;
                var sweptIndices = new List<int>();
                for (var i = currentIndex + step; step > 0 ? i <= boundary : i >= boundary; i += step)
                {
                    sweptIndices.Add(i);
                }

                if (sweptIndices.Count == 0)
                {
                    sweptIndices.Add(currentIndex);
                }

                var totalTicks = note.EndTicks - note.StartTicks;
                var sliceTicks = Math.Max(1, totalTicks / sweptIndices.Count);
                var sliceStart = note.StartTicks;
                for (var i = 0; i < sweptIndices.Count; i++)
                {
                    var sliceEnd = i == sweptIndices.Count - 1 ? note.EndTicks : sliceStart + sliceTicks;
                    results.Add(new ResolvedGesture(effectivePath[sweptIndices[i]], sliceStart, sliceEnd, PathTraversalKind.Sweep, intensity));
                    sliceStart = sliceEnd;
                }

                currentIndex = boundary;
                continue;
            }

            // Clamp (default): hold at the edge, same semantics as MelodicGestureMapper.RelativeStepClampPolicy.
            currentIndex = boundary;
            results.Add(new ResolvedGesture(effectivePath[currentIndex], note.StartTicks, note.EndTicks, kind, intensity));
        }

        return results;
    }

    /// <summary>
    /// Standard triangle-wave ("unfolded billiard path") reflection of an unbounded cumulative index into
    /// <c>[0, length - 1]</c> -- lets <see cref="GestureBoundaryPolicy.Reverse"/> accumulate deltas in a
    /// straight, unbounded line and only fold the result back into range at read time, so a single overshoot
    /// larger than one path length still bounces correctly more than once without any explicit direction
    /// flag to track.
    /// </summary>
    private static int Reflect(int virtualIndex, int length)
    {
        if (length == 1)
        {
            return 0;
        }

        var period = 2 * (length - 1);
        var m = virtualIndex % period;
        if (m < 0)
        {
            m += period;
        }

        return m <= length - 1 ? m : period - m;
    }
}
