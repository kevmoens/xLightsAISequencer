namespace XlightsAI.Studio.Stems;

/// <summary>PHASE-03 (Analysis extensions) Task 2's own analysis boundary: derives a <see cref="VocalActivityEnvelope"/> from a separated <see cref="StemManifest"/>.</summary>
public interface IVocalActivityAnalyzer
{
    /// <summary>
    /// Finds the <see cref="StemKind.Vocals"/> entry (if any) in <paramref name="stems"/> and detects its
    /// active regions. Returns an explicit, empty <see cref="VocalActivityEnvelope"/> (never throws) when
    /// <paramref name="stems"/> has no vocals stem at all.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="stems"/> is <see langword="null"/>.</exception>
    Task<VocalActivityEnvelope> AnalyzeAsync(StemManifest stems, CancellationToken ct);
}
