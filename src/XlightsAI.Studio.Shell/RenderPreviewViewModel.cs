using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Rendering;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// PHASE-13 Step 085 ("Build fast lightweight internal preview for supported primitive gestures") --
/// SCREEN 16 EXPORT/SCREEN 15 COMPARE's own bindable fast-preview panel. Projects
/// <see cref="ILocalPreviewRenderer"/> into the loading/empty/error/loaded screen-state shape every other
/// screen view model in this project already follows (<see cref="CandidateCompareViewModel"/>, this step's
/// own closest sibling). Contains no Avalonia types.
///
/// <para>
/// Required acceptance, verbatim: "Manual known fixtures match approximate rendered path." --
/// <see cref="LoadAsync"/> eagerly materializes every <see cref="PreviewFrame"/> the renderer yields (a
/// "fast lightweight" preview over a short selected window is always small enough to hold in memory at
/// once) so a bound view can scrub/inspect any already-produced frame without re-invoking the renderer.
/// <see cref="HasApproximateEffects"/> is this step's own required "unmistakable 'approximate' indication
/// for unsupported effects" surfaced at the screen level, aggregated from every frame's own
/// <see cref="PreviewFrame.UnsupportedEffectIds"/>.
/// </para>
/// </summary>
public sealed class RenderPreviewViewModel : ObservableObject
{
    private ScreenLoadState _loadState = ScreenLoadState.Empty;
    private IReadOnlyList<PreviewFrame> _frames = [];
    private string? _errorMessage;

    public ScreenLoadState LoadState
    {
        get => _loadState;
        private set
        {
            if (SetField(ref _loadState, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(IsError));
                OnPropertyChanged(nameof(IsLoaded));
            }
        }
    }

    public bool IsEmpty => LoadState == ScreenLoadState.Empty;

    public bool IsError => LoadState == ScreenLoadState.Error;

    public bool IsLoaded => LoadState == ScreenLoadState.Loaded;

    /// <summary>Every sampled frame of the most recently loaded preview, in time order; empty while <see cref="IsEmpty"/>.</summary>
    public IReadOnlyList<PreviewFrame> Frames => _frames;

    /// <summary>True when any loaded frame could not fully resolve one or more effects -- this screen's own "approximate" indicator.</summary>
    public bool HasApproximateEffects => _frames.Any(f => f.UnsupportedEffectIds.Count > 0);

    /// <summary>Set whenever <see cref="LoadAsync"/> rejects its input; cleared on the next successful call.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    /// <exception cref="ArgumentNullException"><paramref name="renderer"/>, <paramref name="sequence"/> or <paramref name="layout"/> is <see langword="null"/>.</exception>
    public async Task<bool> LoadAsync(
        ILocalPreviewRenderer renderer,
        SequenceDocument sequence,
        SpatialDisplayGraph layout,
        TimeWindow window,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(layout);

        try
        {
            var frames = new List<PreviewFrame>();
            await foreach (var frame in renderer.PreviewAsync(sequence, layout, window, ct).ConfigureAwait(false))
            {
                frames.Add(frame);
            }

            _frames = frames;
            ErrorMessage = null;
            LoadState = ScreenLoadState.Loaded;
            OnPropertyChanged(nameof(Frames));
            OnPropertyChanged(nameof(HasApproximateEffects));
            return true;
        }
        catch (RenderPreviewInvalidException ex)
        {
            _frames = [];
            ErrorMessage = ex.Message;
            LoadState = ScreenLoadState.Error;
            OnPropertyChanged(nameof(Frames));
            OnPropertyChanged(nameof(HasApproximateEffects));
            return false;
        }
    }
}
