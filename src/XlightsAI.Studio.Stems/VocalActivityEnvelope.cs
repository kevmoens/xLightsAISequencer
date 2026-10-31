namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-03 (Analysis extensions) Task 2's own persisted output: a <c>vocalActive</c> envelope derived
/// from the vocals half of the existing two-stem separation (vocals + accompaniment) -- despite the
/// six-member <see cref="StemKind"/> enum only populating two members in practice today, this type is
/// deliberately tolerant of that: a <see cref="StemManifest"/> with no <see cref="StemKind.Vocals"/>
/// entry (an instrumental track, or a separator that never populated it) produces an explicit, empty,
/// non-throwing envelope rather than an error. Feeds hold-vs-chase gating in the compiler (architecture
/// §12 / build-plan Phase 4 item 2): a vocal-active window favors a steady full-house hold, a
/// vocal-inactive + high-energy window keeps the frequency-reactive split.
/// </summary>
/// <param name="VersionId">Identity of this particular computed run.</param>
/// <param name="VocalsStemId">The <see cref="StemAsset.Id"/> of the vocals stem this envelope was computed from, or <see langword="null"/> when the source <see cref="StemManifest"/> had no vocals stem at all.</param>
/// <param name="SourceHash">The vocals stem's own <see cref="StemAsset.Sha256"/> at detection time, or <see langword="null"/> alongside a <see langword="null"/> <see cref="VocalsStemId"/>.</param>
/// <param name="DetectorVersion">Identifies which analyzer (and its version) produced this envelope.</param>
/// <param name="Settings">The exact <see cref="StemActivityDetectionSettings"/> this run used to detect activity in the vocals stem.</param>
/// <param name="ActiveRegions">Contiguous regions where the vocals stem is judged active, ordered by <see cref="StemActivity.StartTicks"/>. Every entry's <see cref="StemActivity.Kind"/> is <see cref="StemKind.Vocals"/>. Empty when no vocals stem was available, or when the vocals stem itself has no active regions (an instrumental passage/track).</param>
public sealed record VocalActivityEnvelope(
    Guid VersionId,
    Guid? VocalsStemId,
    string? SourceHash,
    string DetectorVersion,
    StemActivityDetectionSettings Settings,
    IReadOnlyList<StemActivity> ActiveRegions)
{
    /// <summary>Whether <paramref name="tick"/> (canonical 100&#8201;ns ticks from song origin) falls inside any detected vocal-active region.</summary>
    public bool IsActiveAt(long tick) =>
        ActiveRegions.Any(r => tick >= r.StartTicks && tick < r.EndTicks);
}
