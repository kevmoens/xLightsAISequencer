using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Rendering;

/// <summary>One full real-render attempt's outcome: the raw render evidence plus, when it succeeded, real decoded/projected frames ready for the Phase 7b metric battery.</summary>
/// <param name="Artifact">The underlying real render attempt's own evidence (exit code/hash/warnings) -- the authoritative success/failure gate, exactly as <see cref="IRenderAdapter"/> already reports it.</param>
/// <param name="Frames">Real per-tick <see cref="PreviewFrame"/>s when <see cref="Artifact"/>.<see cref="RenderArtifact.Success"/> is <see langword="true"/>; otherwise <see langword="null"/> -- there is nothing to decode/project for a render that never produced an <c>.fseq</c>.</param>
/// <param name="ProjectionWarnings">Every model <see cref="RenderedFrameProjector"/> could not resolve, when <see cref="Frames"/> is non-null; empty when every model projected cleanly.</param>
public sealed record RenderPipelineResult(RenderArtifact Artifact, IReadOnlyList<PreviewFrame>? Frames, IReadOnlyList<string> ProjectionWarnings);

/// <summary>
/// PHASE-07 (build-plan "The harness: render, measure, critique, revise") 7a orchestrator: chains the
/// already-proven real render pipeline (<see cref="IRenderAdapter"/>, which itself probes via
/// <see cref="IRenderCapabilityProbe"/> before ever launching a process) with the real decode
/// (<see cref="IRenderedFrameDecoder"/>) and the real channel-to-model projection
/// (<see cref="RenderedFrameProjector"/>, via <see cref="SequenceChannelLayoutResolver"/>) into one call a
/// Phase 7 iteration loop can use directly. Makes no render-success/failure judgment of its own -- a
/// failed/unsupported render is passed through unchanged, with <see cref="RenderPipelineResult.Frames"/>
/// left <see langword="null"/> rather than fabricating empty frames for a render that never happened.
///
/// <para>
/// <b>Channel layout is resolved from the actual rendered <c>.xsq</c> itself</b> (its own
/// <c>&lt;DisplayElements&gt;</c>, via <see cref="IXsqStructuralReader"/>), not supplied by the caller --
/// <see cref="SequenceChannelLayoutResolver"/>'s own confirmed finding is that a render's FSEQ channel
/// buffer is locally compacted per-sequence, so the only correct channel layout for a given <c>.fseq</c> is
/// the one derived from the exact <c>.xsq</c> that produced it, never a precomputed/cached mapping that
/// could silently go stale against a different export.
/// </para>
/// </summary>
public sealed class RenderPipeline
{
    private readonly IRenderAdapter _renderAdapter;
    private readonly IRenderedFrameDecoder _frameDecoder;
    private readonly IXsqStructuralReader _structuralReader;

    public RenderPipeline(IRenderAdapter renderAdapter, IRenderedFrameDecoder frameDecoder, IXsqStructuralReader structuralReader)
    {
        _renderAdapter = renderAdapter ?? throw new ArgumentNullException(nameof(renderAdapter));
        _frameDecoder = frameDecoder ?? throw new ArgumentNullException(nameof(frameDecoder));
        _structuralReader = structuralReader ?? throw new ArgumentNullException(nameof(structuralReader));
    }

    /// <param name="request">The real render request (already-exported .xsq, show/output directories).</param>
    /// <param name="layout">The same show directory's imported layout (model node counts, group membership) -- for channel/geometry projection.</param>
    /// <exception cref="RenderRequestInvalidException">Propagated unchanged from <see cref="IRenderAdapter.RenderAsync"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled before or during the render.</exception>
    public async Task<RenderPipelineResult> RunAsync(RenderRequest request, DisplayGraph layout, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(layout);

        var artifact = await _renderAdapter.RenderAsync(request, ct).ConfigureAwait(false);
        if (!artifact.Success || artifact.FseqPath is null)
        {
            return new RenderPipelineResult(artifact, Frames: null, ProjectionWarnings: []);
        }

        var structuralDocument = _structuralReader.Read(request.ExportedXsqPath);
        var channelSpansByModel = SequenceChannelLayoutResolver.Resolve(structuralDocument.DisplayElements, layout);

        var decoded = _frameDecoder.Decode(artifact.FseqPath);
        var projection = RenderedFrameProjector.Project(decoded, layout, channelSpansByModel);

        return new RenderPipelineResult(artifact, projection.Frames, projection.SkippedModels);
    }
}
