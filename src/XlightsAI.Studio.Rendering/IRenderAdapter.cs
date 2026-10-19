namespace XlightsAI.Studio.Rendering;

/// <summary>
/// PHASE-13 Section 3 proposed contract, verbatim signature. Step 086's authoritative render tier: exports
/// are already on disk (PHASE-02 Step 013/015); this adapter probes the configured executable's real
/// capabilities, launches it only when supported, and reports a traceable <see cref="RenderArtifact"/> --
/// never the fast approximate tier owned by <see cref="ILocalPreviewRenderer"/> (Step 085).
/// </summary>
public interface IRenderAdapter
{
    /// <exception cref="RenderRequestInvalidException"><paramref name="request"/> targets a missing show directory, a missing exported .xsq, a missing output directory, or the configured executable could not be started.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled before or during the render.</exception>
    Task<RenderArtifact> RenderAsync(RenderRequest request, CancellationToken ct);
}
