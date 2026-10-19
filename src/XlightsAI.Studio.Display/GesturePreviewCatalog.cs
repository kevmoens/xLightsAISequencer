using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;

namespace XlightsAI.Studio.Display;

/// <summary>Default <see cref="IGesturePreviewCatalog"/>. See interface remarks.</summary>
public sealed class GesturePreviewCatalog : IGesturePreviewCatalog
{
    private readonly IResolvedGesturePreviewStore _store;
    private readonly ISpatialGestureResolver _resolver;

    public GesturePreviewCatalog(IResolvedGesturePreviewStore store, ISpatialGestureResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(resolver);
        _store = store;
        _resolver = resolver;
    }

    public async Task<ResolvedGesturePreview> ResolveAsync(
        SpatialDisplayGraph display,
        SpatialPath path,
        MelodyTrack track,
        TimeWindow window,
        ResolutionSettings settings,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(settings);

        ct.ThrowIfCancellationRequested();

        // Throws SpatialDisplayGraphInvalidException synchronously, before any cache file is touched.
        var gestures = _resolver.Resolve(track, window, path, display, settings);

        ct.ThrowIfCancellationRequested();

        var preview = new ResolvedGesturePreview(
            display.SourceLayoutHash, path.Id, path.Version, track.Id, settings.BoundaryPolicy, gestures, Guid.NewGuid());

        await _store.SaveAsync(preview, ct).ConfigureAwait(false);
        return preview;
    }
}
