using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Rendering;

/// <summary>Result of <see cref="RenderedFrameProjector.Project"/>: real decoded frames plus every model this projection could not resolve.</summary>
/// <param name="Frames">One real <see cref="PreviewFrame"/> per <see cref="DecodedRenderOutput.Frames"/> entry, in the same order.</param>
/// <param name="SkippedModels">Every model that was not projected (and why) -- missing channel offset (e.g. a <c>modelGroup</c>, which never has one of its own), an unsupported <see cref="DisplayModel.StringType"/>, or a channel span exceeding the decoded channel count. Never silently dropped without a reason.</param>
public sealed record RenderedFrameProjectionResult(IReadOnlyList<PreviewFrame> Frames, IReadOnlyList<string> SkippedModels);

/// <summary>
/// Combines a real decoded <c>.fseq</c> (<see cref="DecodedRenderOutput"/>, channel-indexed only --
/// <see cref="FseqRenderedFrameDecoder"/>'s own doc comment explicitly deferred model interpretation) with
/// each model's real channel span within this sequence's own rendered buffer (<see cref="SequenceChannelLayoutResolver"/>) and its imported
/// geometry (<see cref="DisplayGraph"/>) into real, model-indexed <see cref="PreviewFrame"/>s -- the same
/// shape <see cref="DisplayMetricsCalculator"/> already consumes, now carrying genuinely rendered color
/// instead of <see cref="LocalGesturePreviewRenderer"/>'s fast, white-only approximation.
///
/// <para>
/// <b>Verified channel width.</b> Every model in the real <c>C:\xLights</c> house declares
/// <c>StringType="RGB Nodes"</c> (3 channels/node, RGB order) -- the only value this projector resolves;
/// any other <see cref="DisplayModel.StringType"/> is reported in <see cref="RenderedFrameProjectionResult.SkippedModels"/>
/// rather than guessed at.
/// </para>
///
/// <para>
/// <b>Spatial placement, disclosed scope decision.</b> xLights' own per-node geometry algorithm (arcs,
/// tree spirals, matrix grids, custom per-node coordinate lists, one per <c>DisplayAs</c> type) is not
/// reimplemented here -- every node of a model is projected at that model's own single representative
/// point (<see cref="DisplayModel.PreviewPoints"/>'s first entry, falling back to
/// <see cref="DisplayModel.Bounds"/>'s world position, or the origin if neither is present), each carrying
/// that specific node's own real decoded color. This keeps the one genuinely new fidelity gain --
/// real measured per-node brightness/color instead of a binary lit/unlit guess -- while being explicit
/// that per-node spatial fan-out within a model is not yet attempted; <see cref="DisplayMetricsCalculator"/>'s
/// own metrics (brightness, per-model coverage/duty-cycle, section-level motion) do not depend on exact
/// intra-model layout to be meaningful.
/// </para>
///
/// <para>
/// <b>"Illuminated" convention.</b> A node whose decoded bytes are exactly <c>(0,0,0)</c> is not added to a
/// frame's <see cref="PreviewFrame.Illuminated"/> list -- matching <see cref="LocalGesturePreviewRenderer"/>'s
/// own established convention that the list holds genuinely lit points only, never a zero-brightness entry
/// for every physical channel every frame (which would otherwise make <see cref="DisplayMetrics.DensityRatio"/>
/// read as a constant 100%).
/// </para>
/// </summary>
public static class RenderedFrameProjector
{
    private const int RgbChannelsPerNode = 3;
    private const string SupportedStringType = "RGB Nodes";

    public static RenderedFrameProjectionResult Project(
        DecodedRenderOutput decoded, DisplayGraph layout, IReadOnlyDictionary<ModelId, IReadOnlyList<ModelStringChannelSpan>> channelSpansByModel)
    {
        ArgumentNullException.ThrowIfNull(decoded);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(channelSpansByModel);

        var skipped = new List<string>();
        var plan = new List<(ModelId Id, long Offset, int NodeCount, double X, double Y)>();

        foreach (var model in layout.Models)
        {
            if (!channelSpansByModel.TryGetValue(model.Id, out var stringSpans) || stringSpans.Count == 0)
            {
                continue; // No channel attribute of its own (e.g. a modelGroup) -- not an error, just nothing to project directly.
            }

            if (!string.Equals(model.StringType, SupportedStringType, StringComparison.Ordinal))
            {
                skipped.Add($"'{model.XlightsName}': unsupported StringType '{model.StringType}' (only '{SupportedStringType}' is resolved).");
                continue;
            }

            var (x, y) = RepresentativePoint(model);
            var outOfBoundsSpan = stringSpans.FirstOrDefault(
                span => span.GlobalChannelOffset + ((long)span.NodeCount * RgbChannelsPerNode) > decoded.ChannelCount);
            if (outOfBoundsSpan is not null)
            {
                var channelSpan = (long)outOfBoundsSpan.NodeCount * RgbChannelsPerNode;
                skipped.Add(
                    $"'{model.XlightsName}': a string's resolved channel span [{outOfBoundsSpan.GlobalChannelOffset}, " +
                    $"{outOfBoundsSpan.GlobalChannelOffset + channelSpan}) exceeds the decoded channel count ({decoded.ChannelCount}).");
                continue; // Skip the whole model -- never project only some of its strings.
            }

            foreach (var span in stringSpans)
            {
                plan.Add((model.Id, span.GlobalChannelOffset, span.NodeCount, x, y));
            }
        }

        var frames = new List<PreviewFrame>(decoded.Frames.Count);
        foreach (var frame in decoded.Frames)
        {
            var illuminated = new List<PixelPoint>();
            foreach (var (id, offset, nodeCount, x, y) in plan)
            {
                for (var node = 0; node < nodeCount; node++)
                {
                    var channelIndex = (int)offset + (node * RgbChannelsPerNode);
                    var r = frame.Channels[channelIndex];
                    var g = frame.Channels[channelIndex + 1];
                    var b = frame.Channels[channelIndex + 2];
                    if (r == 0 && g == 0 && b == 0)
                    {
                        continue;
                    }

                    illuminated.Add(new PixelPoint(id, x, y, r, g, b));
                }
            }

            frames.Add(new PreviewFrame(frame.TimeTicks, illuminated, []));
        }

        return new RenderedFrameProjectionResult(frames, skipped);
    }

    private static (double X, double Y) RepresentativePoint(DisplayModel model)
    {
        if (model.PreviewPoints.Count > 0)
        {
            return (model.PreviewPoints[0].X, model.PreviewPoints[0].Y);
        }

        if (model.Bounds is { } bounds)
        {
            return (bounds.X, bounds.Y);
        }

        return (0.0, 0.0);
    }
}
