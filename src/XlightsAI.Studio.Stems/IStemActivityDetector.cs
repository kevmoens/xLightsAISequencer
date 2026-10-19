namespace XlightsAI.Studio.Stems;

/// <summary>PHASE-06 Section 3's own proposed signature, implemented for real by Step 043.</summary>
public interface IStemActivityDetector
{
    /// <summary>
    /// Detects contiguous regions of audible activity in <paramref name="stem"/> using RMS energy with
    /// hysteresis (Step 043's own implementation guidance, verbatim: "Calculate activity using stem
    /// RMS/onset features with hysteresis and confidence; avoid treating bleed as precise instrument
    /// classification"). Returns an empty list for a fully silent stem -- never an error.
    /// </summary>
    /// <exception cref="StemActivityDetectionInvalidException">This detector's configured <see cref="StemActivityDetectionSettings"/> is invalid, or <paramref name="stem"/>'s on-disk file no longer matches its own declared manifest metadata (stale/corrupt stem).</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
    Task<IReadOnlyList<StemActivity>> DetectAsync(StemAsset stem, CancellationToken ct);
}
