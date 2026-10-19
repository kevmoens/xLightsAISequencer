using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Thrown when an <see cref="IRhythmGesturePlanner"/> request cannot proceed because its inputs are
/// invalid: no target props selected, a blank/unknown effect recipe id, or a malformed/empty
/// <see cref="TimeWindow"/>. Mirrors <see cref="BeatGridEditInvalidException"/>'s own "never silently
/// apply as if correct" rule (and PHASE-02's own <c>EffectPlanInvalidException</c> precedent) — no
/// partial or fabricated <see cref="XlightsAI.Studio.Xlights.EffectInstance"/> list is ever returned
/// for an invalid request (Section 2 / quality-gate matrix: "Malformed proposal... must not
/// auto-commit").
/// </summary>
public sealed class RhythmGesturePlanInvalidException : Exception
{
    public RhythmGesturePlanInvalidException(SongId songId, string reason)
        : base($"Cannot build rhythm gesture pulses for song '{songId.Value}': {reason}")
    {
        SongId = songId;
        Reason = reason;
    }

    public SongId SongId { get; }

    public string Reason { get; }
}
