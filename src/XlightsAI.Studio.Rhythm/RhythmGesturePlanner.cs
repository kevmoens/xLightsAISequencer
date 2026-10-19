using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Rhythm;

/// <summary>PHASE-05 Step 036 — real implementation of <see cref="IRhythmGesturePlanner"/>. See the interface's own remarks for the overall design.</summary>
public sealed class RhythmGesturePlanner : IRhythmGesturePlanner
{
    private readonly IEffectRecipeRegistry _recipeRegistry;
    private readonly RhythmGestureSettings _settings;

    public RhythmGesturePlanner(IEffectRecipeRegistry recipeRegistry, RhythmGestureSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(recipeRegistry);

        _settings = settings ?? RhythmGestureSettings.Default;
        if (_settings.MinPulseDurationTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), _settings.MinPulseDurationTicks, "RhythmGestureSettings.MinPulseDurationTicks must be positive.");
        }

        if (_settings.MaxPulseDurationTicks < _settings.MinPulseDurationTicks)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), _settings.MaxPulseDurationTicks, "RhythmGestureSettings.MaxPulseDurationTicks must be >= MinPulseDurationTicks.");
        }

        _recipeRegistry = recipeRegistry;
    }

    public IReadOnlyList<EffectInstance> BuildPulses(
        BeatGrid grid,
        IReadOnlySet<ModelId> targets,
        string verifiedRecipeId,
        TimeWindow range)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(targets);

        if (targets.Count == 0)
        {
            throw new RhythmGesturePlanInvalidException(grid.SongId, "At least one target prop must be selected.");
        }

        if (string.IsNullOrWhiteSpace(verifiedRecipeId))
        {
            throw new RhythmGesturePlanInvalidException(grid.SongId, "A verified effect recipe id must be supplied.");
        }

        if (range.StartTicks < 0)
        {
            throw new RhythmGesturePlanInvalidException(grid.SongId, $"range.StartTicks ({range.StartTicks}) must not be negative.");
        }

        if (range.EndTicks <= range.StartTicks)
        {
            throw new RhythmGesturePlanInvalidException(
                grid.SongId,
                $"range [{range.StartTicks}, {range.EndTicks}) must be a non-empty, half-open window (StartTicks < EndTicks).");
        }

        // Deterministic alternating-chase order: a stable, repeatable rotation across the caller's own
        // target set — never the undefined enumeration order of an IReadOnlySet<T>. Sorted by the
        // underlying xLights model/group name so the same target set always produces the same rotation.
        var orderedTargets = targets
            .Select(t => t.Value)
            .OrderBy(v => v, StringComparer.Ordinal)
            .Select(v => new ModelId(v))
            .ToArray();

        var frameTicks = XsqExporter.FrameIntervalMs * TimeSpan.TicksPerMillisecond;

        // Beats outside [range.StartTicks, range.EndTicks) are simply not iterated — a grid with no
        // beats in range (including true silence, this step's own "Silence... must be handled
        // explicitly" edge case) produces an empty, non-exceptional plan, not a fabricated pulse.
        var beatsInRange = grid.Beats
            .Where(b => b.TimeTicks >= range.StartTicks && b.TimeTicks < range.EndTicks)
            .OrderBy(b => b.TimeTicks)
            .ToArray();

        var effects = new List<EffectInstance>(beatsInRange.Length);
        for (var i = 0; i < beatsInRange.Length; i++)
        {
            var beat = beatsInRange[i];
            var target = orderedTargets[i % orderedTargets.Length];

            // "Translate selected beat strength into finite pulse duration" (implementation guidance,
            // verbatim): linear interpolation between the restrained minimum and maximum, never a
            // uniform duration that ignores confidence.
            var confidence = Math.Clamp(beat.Confidence, 0.0, 1.0);
            var rawDurationTicks = _settings.MinPulseDurationTicks
                + (long)((_settings.MaxPulseDurationTicks - _settings.MinPulseDurationTicks) * confidence);

            // Output frame snapping: never a sub-frame duration xLights' own 25 ms sequence grid could
            // not actually represent (at least one whole frame).
            var snappedFrames = Math.Max(1L, (long)Math.Round((double)rawDurationTicks / frameTicks, MidpointRounding.AwayFromZero));
            var snappedDurationTicks = snappedFrames * frameTicks;

            var startTicks = beat.TimeTicks;
            var endTicks = Math.Min(startTicks + snappedDurationTicks, range.EndTicks);

            // Confined to the caller's own time scope (quality-gate matrix row 7, verbatim: "remain
            // confined to time scope"): a pulse clipped down to a zero/negative window is skipped
            // rather than ever producing an invalid effect — defensive, since the beatsInRange filter
            // above already guarantees startTicks < range.EndTicks for every iterated beat.
            if (endTicks <= startTicks)
            {
                continue;
            }

            effects.Add(_recipeRegistry.CreateVerified(verifiedRecipeId, target, startTicks, endTicks));
        }

        return effects;
    }
}
