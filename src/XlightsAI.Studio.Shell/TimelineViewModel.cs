using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// PHASE-03 Step 016 ("Build time ruler, horizontal scroll and zoom") — SCREEN 13 TIMELINE.
/// Owns the bindable <see cref="TimelineViewport"/> state (scroll/zoom) and the ruler tick marks
/// derived from it; contains no Avalonia types, same convention as every other screen view model
/// in this project. Mirrors <see cref="ShowImportViewModel"/>'s explicit loading/empty/error/loaded
/// state machine: no media loaded yet is <see cref="ScreenLoadState.Empty"/>, never a silently
/// blank ruler — the required failure/edge case for this step: "Silence, invalid timing boundaries
/// and missing media must be handled explicitly."
/// </summary>
public sealed class TimelineViewModel : ObservableObject
{
    /// <summary>A readable default zoom level: 1 pixel == 1,000,000 ticks == 100 ms.</summary>
    public const double DefaultTicksPerPixel = 1_000_000d;

    /// <summary>A reasonable default ruler/viewport width for a desktop window, before the real view reports its own via <see cref="ViewportWidthPixels"/>.</summary>
    public const double DefaultViewportWidthPixels = 760d;

    /// <summary>30 seconds at the canonical 100 ns/tick unit — same default duration <see cref="ShowImportViewModel.DefaultSongDurationTicks"/> uses, so a freshly navigated-to screen has a sensible starting value.</summary>
    public const long DefaultTotalDurationTicks = ShowImportViewModel.DefaultSongDurationTicks;

    private ScreenLoadState _loadState = ScreenLoadState.Empty;
    private string? _errorMessage;
    private TimelineViewport? _viewport;
    private long _totalDurationTicks = DefaultTotalDurationTicks;
    private double _viewportWidthPixels = DefaultViewportWidthPixels;
    private TimelineRowTree? _rowTree;
    private SequenceDocument? _document;
    private Guid? _selectedEffectId;
    private EditScope? _selectedRangeScope;
    private int _rangeSelectionStartRowIndex;
    private double _rangeSelectionStartPixel;
    private int _rangeSelectionEndRowIndex;
    private double _rangeSelectionEndPixel;
    private WaveformPyramidCache? _waveformCache;
    private long _waveformOriginTicks;
    private long _playheadTicks;
    private bool _hasPlayhead;
    private string _scrubTargetTicksText = "0";
    private string? _scrubErrorMessage;
    private TimeWindow? _loopWindow;
    private string? _loopErrorMessage;
    private SnapPolicy _snapPolicy = SnapPolicy.None;
    private long? _lastScrubRequestedTicks;
    private long? _lastScrubActualTicks;

    // PHASE-03 Step 017: keyed by the same stable TimelineRowId collapse-state is keyed by
    // everywhere else, so a group's collapsed/expanded state survives re-loading the same layout
    // (this step's own "retain user group collapse state" implementation guidance) even though
    // _rowTree itself is rebuilt from scratch on every LoadTimeline call.
    private readonly HashSet<TimelineRowId> _collapsedGroupIds = [];

    /// <summary>Bindable "known total timeline duration" input for <see cref="LoadTimeline"/>, in 100 ns ticks — this phase has no real <c>SongAsset</c> duration yet (Phase 04's own scope), so the user supplies one explicitly, same as <see cref="ShowImportViewModel.SongDurationTicks"/>.</summary>
    public long TotalDurationTicks
    {
        get => _totalDurationTicks;
        set => SetField(ref _totalDurationTicks, value);
    }

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

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    public bool IsEmpty => LoadState == ScreenLoadState.Empty;

    public bool IsError => LoadState == ScreenLoadState.Error;

    public bool IsLoaded => LoadState == ScreenLoadState.Loaded;

    /// <summary><see langword="null"/> exactly when no media/content has been loaded yet (<see cref="IsEmpty"/>) or the last load attempt failed (<see cref="IsError"/>) — "missing media must be handled explicitly," not a viewport silently pointing at nothing.</summary>
    public TimelineViewport? Viewport
    {
        get => _viewport;
        private set => SetField(ref _viewport, value);
    }

    /// <summary>Bindable current pixel width of the real ruler/timeline control, set by the view on layout; drives <see cref="RulerTicks"/> and zoom-anchor centering.</summary>
    public double ViewportWidthPixels
    {
        get => _viewportWidthPixels;
        set
        {
            if (SetField(ref _viewportWidthPixels, value))
            {
                RaiseViewportDerivedChanged();
            }
        }
    }

    /// <summary>
    /// The labeled ruler ticks currently visible, re-derived from <see cref="Viewport"/>/
    /// <see cref="ViewportWidthPixels"/> on every scroll/zoom/resize — the actual "time ruler"
    /// this step builds. Each tick's absolute pixel offset
    /// (<see cref="TimelineRulerTick.X"/>) is converted to a left margin relative to the previous
    /// tick (see <see cref="TimelineRulerTickView"/>), so a plain horizontal panel lays them out
    /// left-to-right without this Avalonia-free project needing absolute/Canvas positioning.
    /// </summary>
    public IReadOnlyList<TimelineRulerTickView> RulerTicks
    {
        get
        {
            if (Viewport is null)
            {
                return [];
            }

            var ticks = TimelineRulerTickGenerator.GenerateTicks(Viewport, ViewportWidthPixels);
            var views = new List<TimelineRulerTickView>(ticks.Count);
            var previousX = 0d;
            foreach (var tick in ticks)
            {
                views.Add(new TimelineRulerTickView(tick.Label, Math.Max(0d, tick.X - previousX)));
                previousX = tick.X;
            }

            return views;
        }
    }

    /// <summary>
    /// PHASE-03 Step 017: the virtualized, pre-order-flattened prop rows currently visible — empty
    /// exactly when <see cref="IsLoaded"/> is <see langword="false"/> (same "missing media" rule
    /// <see cref="RulerTicks"/> already follows), even though the underlying <see cref="TimelineRowTree"/>
    /// and collapse state are retained in memory for the next successful <see cref="LoadTimeline"/>.
    /// Each group row's <see cref="TimelineRowItemView.ToggleExpandCommand"/> calls back into
    /// <see cref="ToggleGroupExpansion"/> on this same instance.
    /// </summary>
    public IReadOnlyList<TimelineRowItemView> Rows
    {
        get
        {
            if (Viewport is null || _rowTree is null)
            {
                return [];
            }

            var slots = TimelineRowFlattener.Flatten(_rowTree.Roots, _collapsedGroupIds);
            var views = new List<TimelineRowItemView>(slots.Count);
            foreach (var slot in slots)
            {
                var toggleCommand = slot.IsGroup ? new DelegateCommand(() => ToggleGroupExpansion(slot.Id)) : null;
                var effectLayers = BuildEffectLayers(slot.ModelId);
                views.Add(new TimelineRowItemView(slot, toggleCommand, effectLayers));
            }

            return views;
        }
    }

    /// <summary>Non-fatal findings from building <see cref="Rows"/> (e.g. a group-reference cycle) — empty whenever nothing unusual was found. Never a substitute for the real import warnings already surfaced on <see cref="ShowImportViewModel.Layout"/>.</summary>
    public IReadOnlyList<string> RowWarnings => _rowTree?.Warnings ?? [];

    /// <summary>
    /// PHASE-03 Step 018 ("Render effect blocks and inspector selection"): the currently selected
    /// effect's stable identity, or <see langword="null"/> when nothing is selected. This, not any
    /// row index or on-screen position, is "which effect is selected" — the step's own required
    /// acceptance: "Selection maps exactly to effect ID and time span."
    /// </summary>
    public Guid? SelectedEffectId
    {
        get => _selectedEffectId;
        private set
        {
            if (SetField(ref _selectedEffectId, value))
            {
                OnPropertyChanged(nameof(SelectedEffectInspector));
                OnPropertyChanged(nameof(Rows));
            }
        }
    }

    /// <summary>
    /// The real "inspector" projection of <see cref="SelectedEffectId"/> against the currently loaded
    /// document — <see langword="null"/> exactly when nothing is selected, no document is loaded, or
    /// the selected id no longer exists in the current document (never a dangling/stale inspector).
    /// </summary>
    public EffectInspectorView? SelectedEffectInspector
    {
        get
        {
            if (_document is null || _selectedEffectId is null)
            {
                return null;
            }

            var effect = _document.Effects.FirstOrDefault(e => e.Id == _selectedEffectId.Value);
            if (effect is null)
            {
                return null;
            }

            return new EffectInspectorView(
                effect.Id,
                effect.Target.Value,
                effect.Layer,
                effect.StartTicks,
                effect.EndTicks,
                effect.RecipeId,
                effect.RecipeVersion,
                effect.Locked,
                effect.Parameters.ToList());
        }
    }

    /// <summary>
    /// Selects <paramref name="effectId"/> for the inspector — a no-op (not a crash, not a dangling
    /// selection) when no document is loaded or <paramref name="effectId"/> does not exist in it,
    /// e.g. a stale id from before an undo/reload.
    /// </summary>
    public void SelectEffect(Guid effectId)
    {
        if (_document is null || !_document.Effects.Any(e => e.Id == effectId))
        {
            return;
        }

        SelectedEffectId = effectId;
    }

    /// <summary>Clears the current selection; a no-op when nothing is selected.</summary>
    public void ClearSelection() => SelectedEffectId = null;

    /// <summary>
    /// Keyboard navigation (Step 018 implementation guidance: "hit test click and keyboard
    /// navigation"): selects the next effect in visual row order (model rows top-to-bottom, each
    /// row's layers ascending, each layer's blocks by start time ascending), wrapping from the last
    /// effect back to the first. A no-op while no effect exists anywhere in the currently visible rows.
    /// </summary>
    public void SelectNextEffect() => MoveSelection(+1);

    /// <summary>Same contract as <see cref="SelectNextEffect"/>, in the opposite direction.</summary>
    public void SelectPreviousEffect() => MoveSelection(-1);

    private void MoveSelection(int direction)
    {
        var ordered = VisibleEffectIdsInOrder();
        if (ordered.Count == 0)
        {
            return;
        }

        var currentIndex = _selectedEffectId is null ? -1 : ordered.IndexOf(_selectedEffectId.Value);
        var nextIndex = currentIndex < 0
            ? (direction > 0 ? 0 : ordered.Count - 1)
            : ((currentIndex + direction) % ordered.Count + ordered.Count) % ordered.Count;

        SelectedEffectId = ordered[nextIndex];
    }

    /// <summary>Every effect id currently rendered in <see cref="Rows"/>, in visual row/layer/start-time order.</summary>
    private List<Guid> VisibleEffectIdsInOrder()
    {
        var ids = new List<Guid>();
        foreach (var row in Rows)
        {
            foreach (var layer in row.EffectLayers)
            {
                foreach (var block in layer.Blocks)
                {
                    ids.Add(block.EffectId);
                }
            }
        }

        return ids;
    }

    /// <summary>
    /// Builds the <see cref="EffectLayerRowView"/>s for one row's <paramref name="modelId"/> — empty
    /// for a group row (<paramref name="modelId"/> is <see langword="null"/>), or when no document is
    /// loaded yet, or when <paramref name="modelId"/> has no effects (the "silence" edge case).
    /// </summary>
    private IReadOnlyList<EffectLayerRowView> BuildEffectLayers(ModelId? modelId)
    {
        if (modelId is null || _document is null || Viewport is null)
        {
            return [];
        }

        var tracks = EffectBlockLayoutBuilder.BuildLayerTracks(_document.Effects, modelId.Value, Viewport);
        var layerViews = new List<EffectLayerRowView>(tracks.Count);
        foreach (var track in tracks)
        {
            var blockViews = new List<EffectBlockItemView>(track.Blocks.Count);
            var previousRightEdge = 0d;
            foreach (var block in track.Blocks)
            {
                var marginLeft = Math.Max(0d, block.X - previousRightEdge);
                previousRightEdge = block.X + block.Width;
                var effectId = block.EffectId;
                blockViews.Add(new EffectBlockItemView(
                    effectId,
                    marginLeft,
                    block.Width,
                    block.RecipeId,
                    block.Locked,
                    effectId == _selectedEffectId,
                    new DelegateCommand(() => SelectEffect(effectId))));
            }

            layerViews.Add(new EffectLayerRowView(track.Layer, blockViews));
        }

        return layerViews;
    }

    /// <summary>
    /// PHASE-04 Step 027 ("Render synchronized waveform beneath ruler"): <see langword="true"/> only
    /// when a waveform pyramid was supplied to the most recent <see cref="LoadTimeline"/> call — the
    /// "missing media must be handled explicitly" edge case this screen already follows for
    /// <see cref="RulerTicks"/>/<see cref="Rows"/>, applied to the waveform strip.
    /// </summary>
    public bool HasWaveform => _waveformCache is not null;

    /// <summary>
    /// The real waveform bars currently visible beneath the ruler, re-derived from
    /// <see cref="Viewport"/>/<see cref="ViewportWidthPixels"/> on every scroll/zoom/resize exactly
    /// like <see cref="RulerTicks"/> — "Waveform position matches timeline coordinates" (this step's
    /// required acceptance), through the exact same shared <see cref="TimelineViewport"/> transform.
    /// Empty whenever <see cref="HasWaveform"/> is <see langword="false"/>, never a stale/mismatched
    /// render from a previously loaded song.
    /// </summary>
    public IReadOnlyList<WaveformBarView> WaveformBars
    {
        get
        {
            if (Viewport is null || _waveformCache is null)
            {
                return [];
            }

            var bars = WaveformBarLayoutBuilder.BuildBars(_waveformCache, Viewport, ViewportWidthPixels, _waveformOriginTicks);
            var views = new List<WaveformBarView>(bars.Count);
            var previousRightEdge = 0d;
            foreach (var bar in bars)
            {
                var marginLeft = Math.Max(0d, bar.X - previousRightEdge);
                previousRightEdge = bar.X + bar.Width;
                views.Add(new WaveformBarView(marginLeft, bar.Width, bar.Min, bar.Max, bar.Rms));
            }

            return views;
        }
    }

    /// <summary>
    /// PHASE-04 Step 028 ("Use one master media time clock for playback and playhead"): the single
    /// authoritative playhead position in canonical 100 ns ticks, pushed here by
    /// <see cref="ShellViewModel"/> from the exact same <see cref="MasterPlaybackClock"/> the Audio
    /// screen's own <see cref="AudioImportViewModel.PlaybackPositionTicks"/> is derived from —
    /// never a second, independently-tracked UI timer. Set via <see cref="SetPlayhead"/>.
    /// </summary>
    public long PlayheadTicks
    {
        get => _playheadTicks;
        private set => SetField(ref _playheadTicks, value);
    }

    /// <summary><see langword="true"/> exactly when the master playback clock currently has a song open — "missing media must be handled explicitly," applied to the playhead marker.</summary>
    public bool HasPlayhead
    {
        get => _hasPlayhead;
        private set => SetField(ref _hasPlayhead, value);
    }

    /// <summary>
    /// The real pixel offset of <see cref="PlayheadTicks"/> through the exact same
    /// <see cref="Editing.TimelineViewport.XFromTime"/> transform the ruler and waveform already
    /// render through — <see langword="null"/> whenever no <see cref="Viewport"/> is loaded.
    /// </summary>
    public double? PlayheadX => Viewport?.XFromTime(PlayheadTicks);

    /// <summary>
    /// A clamped, always-non-negative margin-left for binding the playhead marker without a
    /// separate converter for a nullable <see cref="double"/>; meaningless (and not rendered, see
    /// <see cref="IsPlayheadVisible"/>) whenever <see cref="Viewport"/> is <see langword="null"/>.
    /// </summary>
    public double PlayheadMarginLeft => Math.Max(0d, PlayheadX ?? 0d);

    /// <summary><see langword="true"/> only when the playhead marker should actually be drawn: a timeline is loaded and the master clock currently has a song open.</summary>
    public bool IsPlayheadVisible => Viewport is not null && HasPlayhead;

    /// <summary>PHASE-04 Step 028: bindable text for the "Scrub playhead to (100 ns ticks)" field consumed by <see cref="ShellViewModel"/>'s scrub command.</summary>
    public string ScrubTargetTicksText
    {
        get => _scrubTargetTicksText;
        set => SetField(ref _scrubTargetTicksText, value);
    }

    /// <summary>Set whenever the most recent scrub attempt failed (out-of-scope target, or no timeline/playback session open); cleared on the next successful scrub. The playhead itself is left completely unchanged on failure — never a partial mutation.</summary>
    public string? ScrubErrorMessage
    {
        get => _scrubErrorMessage;
        private set => SetField(ref _scrubErrorMessage, value);
    }

    /// <summary>
    /// The single authoritative mutator for <see cref="PlayheadTicks"/>/<see cref="HasPlayhead"/>,
    /// called by <see cref="ShellViewModel"/> every time the master clock's own
    /// <see cref="MasterPlaybackClock.CanonicalPositionTicks"/> changes (Play/Pause/Seek/Refresh/
    /// Open/Scrub) — the one place the playhead is ever written, so the Timeline screen and the
    /// Audio screen can never disagree about "where is the playhead right now."
    /// </summary>
    public void SetPlayhead(long ticks, bool isActive)
    {
        PlayheadTicks = ticks;
        HasPlayhead = isActive;
        OnPropertyChanged(nameof(PlayheadFrameIndex));
        OnPropertyChanged(nameof(TimingDiagnosticsSummary));

        // PHASE-04 Step 029 ("auto-scroll independent of zoom"): whenever the real playhead moves
        // outside the currently visible window — at any zoom level, since this reads the viewport's
        // own live TicksPerPixel rather than any fixed pixel/zoom assumption — scroll (never zoom)
        // so it becomes visible again, centered. A no-op while no timeline is loaded, or while the
        // playhead is inactive (no playback session open — nothing meaningful to auto-scroll to).
        var scrolled = false;
        if (isActive && Viewport is { } viewport)
        {
            var visible = viewport.VisibleWindow(ViewportWidthPixels);
            if (ticks < visible.StartTicks || ticks >= visible.EndTicks)
            {
                var halfWidthTicks = (long)Math.Round(ViewportWidthPixels / 2 * viewport.TicksPerPixel, MidpointRounding.AwayFromZero);
                Viewport = TimelineViewport.Create(viewport.ContentBounds, viewport.TicksPerPixel, ticks - halfWidthTicks);
                scrolled = true;
            }
        }

        if (scrolled)
        {
            RaiseViewportDerivedChanged();
        }
        else
        {
            OnPropertyChanged(nameof(PlayheadX));
            OnPropertyChanged(nameof(PlayheadMarginLeft));
            OnPropertyChanged(nameof(IsPlayheadVisible));
        }
    }

    /// <summary>
    /// PHASE-04 Step 029 ("Add zoom-to-selection..."): rezooms/scrolls the loaded <see cref="Viewport"/>
    /// so <see cref="SelectedRangeScope"/>'s own half-open <see cref="Editing.EditScope.Time"/> window
    /// exactly fills the current <see cref="ViewportWidthPixels"/> — the real "zoom to selection"
    /// action, through the exact same <see cref="TimelineViewport.Create"/> factory every other zoom/
    /// scroll mutator in this class already funnels through (so an out-of-bounds result is still
    /// impossible by construction). A no-op (not a crash, not a silently stale zoom) while no timeline
    /// is loaded, nothing is selected, or the selection is zero-length — the same "missing media"/
    /// "silence" edge-case convention every other mutator here already follows.
    /// </summary>
    public void ZoomToSelection()
    {
        if (Viewport is not { } viewport || SelectedRangeScope is not { } scope || ViewportWidthPixels <= 0)
        {
            return;
        }

        var durationTicks = scope.Time.EndTicks - scope.Time.StartTicks;
        if (durationTicks <= 0)
        {
            return;
        }

        var newTicksPerPixel = durationTicks / ViewportWidthPixels;
        Viewport = TimelineViewport.Create(viewport.ContentBounds, newTicksPerPixel, scope.Time.StartTicks);
        RaiseViewportDerivedChanged();
    }

    /// <summary>
    /// PHASE-04 Step 029 ("A/B loop playback"): the real loop window currently armed on the shared
    /// playback transport (set only by <see cref="SetLoop"/>, called by <see cref="ShellViewModel"/>
    /// only after the real transport's own <c>IAudioTransport.SetLoop</c> call has already succeeded
    /// — never a cosmetic-only preview value that could disagree with what is actually looping).
    /// <see langword="null"/> whenever no loop is armed.
    /// </summary>
    public TimeWindow? LoopWindow
    {
        get => _loopWindow;
        private set
        {
            if (SetField(ref _loopWindow, value))
            {
                OnPropertyChanged(nameof(HasLoop));
                OnPropertyChanged(nameof(LoopSummary));
            }
        }
    }

    /// <summary><see langword="true"/> exactly when a loop window is currently armed.</summary>
    public bool HasLoop => LoopWindow is not null;

    /// <summary>The real "display exact scope" projection of <see cref="LoopWindow"/> — <see langword="null"/> exactly when <see cref="HasLoop"/> is <see langword="false"/>.</summary>
    public string? LoopSummary => LoopWindow is { } window
        ? $"Loop: [{window.StartTicks}, {window.EndTicks})"
        : null;

    /// <summary>Set whenever the most recent set-loop attempt failed (out-of-scope window, or no playback session open); cleared on the next successful set/clear. <see cref="LoopWindow"/> is left completely unchanged on failure — never a partial mutation.</summary>
    public string? LoopErrorMessage
    {
        get => _loopErrorMessage;
        private set => SetField(ref _loopErrorMessage, value);
    }

    /// <summary>
    /// The single authoritative mutator for <see cref="LoopWindow"/>, called by
    /// <see cref="ShellViewModel"/> only after the shared real transport's own loop state has already
    /// been updated (set or cleared) — the same "one place state is ever written" convention
    /// <see cref="SetPlayhead"/> already follows.
    /// </summary>
    public void SetLoop(TimeWindow? window) => LoopWindow = window;

    /// <summary>Sets/clears <see cref="LoopErrorMessage"/>; used by <see cref="ShellViewModel"/>'s set/clear-loop actions, same convention as <see cref="SetScrubError"/>.</summary>
    public void SetLoopError(string? message) => LoopErrorMessage = message;

    /// <summary>
    /// PHASE-04 Step 030 ("Add frame/beat/off-grid snapping and timing-diagnostics panel"): the snap
    /// policy currently applied to this Timeline's own drag/scrub call sites (<see cref="ApplyRangeSelection"/>
    /// and <see cref="ShellViewModel"/>'s scrub action) — a bindable, user-selectable dependency, never a
    /// hardcoded rounding call baked into either call site (this step's own implementation guidance:
    /// "Implement snap policy ... as dependency not baked into drag math"). Defaults to
    /// <see cref="Editing.SnapPolicy.None"/>, so every pre-Step-030 drag/scrub behavior is unchanged
    /// until a user explicitly opts in. A UI preference, not playback-session state — unlike
    /// <see cref="LoopWindow"/>/<see cref="ScrubErrorMessage"/>, it deliberately survives
    /// <see cref="Reset"/>/<see cref="LoadTimeline"/> (the user's chosen snap grid is not invalidated by
    /// loading a different song).
    /// </summary>
    public SnapPolicy SnapPolicy
    {
        get => _snapPolicy;
        set
        {
            if (SetField(ref _snapPolicy, value))
            {
                OnPropertyChanged(nameof(TimingDiagnosticsSummary));
            }
        }
    }

    /// <summary>The zero-based xLights frame index <see cref="PlayheadTicks"/> currently falls within — the real "time-to-frame conversion" this step's own required acceptance names, applied to the live playhead for the timing-diagnostics panel.</summary>
    public long PlayheadFrameIndex => XLightsFrameGrid.TicksToFrameIndex(PlayheadTicks);

    /// <summary>The most recent scrub's own requested target, in 100 ns ticks, <em>after</em> <see cref="SnapPolicy"/> was applied — <see langword="null"/> until the first successful scrub. Set only by <see cref="RecordScrubTelemetry"/>.</summary>
    public long? LastScrubRequestedTicks
    {
        get => _lastScrubRequestedTicks;
        private set => SetField(ref _lastScrubRequestedTicks, value);
    }

    /// <summary>The same scrub's actually-resulting <see cref="MasterPlaybackClock.CanonicalPositionTicks"/>, read back from the real transport after the seek completed — may differ from <see cref="LastScrubRequestedTicks"/> by the real device's own sample-rate quantization (see <c>WaveOutAudioTransport.TicksToSamples</c>/<c>SamplesToTicks</c>), never an estimate.</summary>
    public long? LastScrubActualTicks
    {
        get => _lastScrubActualTicks;
        private set => SetField(ref _lastScrubActualTicks, value);
    }

    /// <summary>
    /// This step's own "gather sync/drift telemetry" (implementation guidance): the real, observed
    /// difference between what was requested and what the device actually ended up at —
    /// <see langword="null"/> exactly when no scrub has completed yet (the "silence" edge case this
    /// screen already follows for every other derived field).
    /// </summary>
    public long? LastScrubDriftTicks => LastScrubRequestedTicks is { } requested && LastScrubActualTicks is { } actual
        ? actual - requested
        : null;

    /// <summary>The real "display exact scope" projection (same convention as <see cref="LoopSummary"/>/<see cref="SelectedRangeScopeSummary"/>) of this step's own timing-diagnostics panel: snap policy, playhead frame index, and the most recent scrub's own requested/actual/drift ticks — <see langword="null"/> fields render as "n/a", never a silently blank panel.</summary>
    public string TimingDiagnosticsSummary
    {
        get
        {
            var snapText = SnapPolicy.Mode switch
            {
                SnapMode.None => "None",
                SnapMode.Frame => $"Frame ({XLightsFrameGrid.FrameIntervalMs} ms / {XLightsFrameGrid.FrameIntervalTicks} ticks)",
                _ => SnapPolicy.Mode.ToString(),
            };

            var driftText = LastScrubDriftTicks is { } drift
                ? $"requested {LastScrubRequestedTicks}, actual {LastScrubActualTicks}, drift {drift} ticks"
                : "n/a (no scrub yet)";

            return $"Snap: {snapText} | Playhead frame: {PlayheadFrameIndex} | Last scrub: {driftText}";
        }
    }

    /// <summary>
    /// The single authoritative recorder of scrub sync/drift telemetry, called by
    /// <see cref="ShellViewModel"/> immediately after a successful scrub seek — never computed from a
    /// second, independently-tracked estimate. <paramref name="requestedTicks"/> is the already-snapped
    /// target that was actually requested of the real transport; <paramref name="actualTicks"/> is the
    /// real <see cref="MasterPlaybackClock.CanonicalPositionTicks"/> read back afterward.
    /// </summary>
    public void RecordScrubTelemetry(long requestedTicks, long actualTicks)
    {
        LastScrubRequestedTicks = requestedTicks;
        LastScrubActualTicks = actualTicks;
        OnPropertyChanged(nameof(LastScrubDriftTicks));
        OnPropertyChanged(nameof(TimingDiagnosticsSummary));
    }

    /// <summary>Clears the most recent scrub's own telemetry (not <see cref="SnapPolicy"/>, a user preference — see its own remarks); used by <see cref="Reset"/>/<see cref="LoadTimeline"/>, same "stale state never survives a reload" convention already applied to <see cref="LoopWindow"/>.</summary>
    private void ClearScrubTelemetry()
    {
        _lastScrubRequestedTicks = null;
        _lastScrubActualTicks = null;
    }

    /// <summary>
    /// Validates <paramref name="window"/> as a loop target against the currently loaded
    /// <see cref="Viewport"/>'s own content bounds — this step's required failure/edge case
    /// ("Out-of-scope... must be rejected without partial mutation"), applied to the loop window
    /// exactly the same way <see cref="ValidateScrubTarget"/> already applies it to a scrub target.
    /// Throws <em>before</em> any real transport call is attempted (see <see cref="ShellViewModel"/>'s
    /// own set-loop command), so an invalid window never partially arms the loop.
    /// </summary>
    /// <exception cref="InvalidOperationException">No timeline is currently loaded (<see cref="Viewport"/> is <see langword="null"/>) — the "stale" case: nothing to loop against.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="window"/> is empty/inverted, or falls outside the loaded timeline's own content bounds — the "out-of-scope" case.</exception>
    public void ValidateLoopWindow(TimeWindow window)
    {
        if (Viewport is not { } viewport)
        {
            throw new InvalidOperationException(
                "Cannot set a loop: no timeline is currently loaded.");
        }

        if (window.EndTicks <= window.StartTicks)
        {
            throw new ArgumentOutOfRangeException(nameof(window), window,
                "Loop window must be a non-empty half-open range.");
        }

        if (window.StartTicks < viewport.ContentBounds.StartTicks || window.EndTicks > viewport.ContentBounds.EndTicks)
        {
            throw new ArgumentOutOfRangeException(nameof(window), window,
                $"Loop window must be within the loaded timeline's bounds [{viewport.ContentBounds.StartTicks}, {viewport.ContentBounds.EndTicks}].");
        }
    }

    /// <summary>
    /// Validates <paramref name="ticks"/> as a scrub target against the currently loaded
    /// <see cref="Viewport"/>'s own content bounds — the Timeline-level half of this step's required
    /// failure/edge case ("Out-of-scope... must be rejected without partial mutation"). Throws
    /// <em>before</em> any real transport seek is attempted (see <see cref="ShellViewModel"/>'s own
    /// scrub command), so an invalid target never partially updates <see cref="PlayheadTicks"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No timeline is currently loaded (<see cref="Viewport"/> is <see langword="null"/>) — the "stale" case: nothing to scrub against.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ticks"/> falls outside the loaded timeline's own content bounds.</exception>
    public void ValidateScrubTarget(long ticks)
    {
        if (Viewport is not { } viewport)
        {
            throw new InvalidOperationException(
                "Cannot scrub the playhead: no timeline is currently loaded.");
        }

        if (ticks < viewport.ContentBounds.StartTicks || ticks > viewport.ContentBounds.EndTicks)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks), ticks,
                $"Scrub target must be within the loaded timeline's bounds [{viewport.ContentBounds.StartTicks}, {viewport.ContentBounds.EndTicks}].");
        }
    }

    /// <summary>Sets/clears <see cref="ScrubErrorMessage"/>; used by <see cref="ShellViewModel"/>'s scrub command, same convention as every other screen's own action-error field.</summary>
    public void SetScrubError(string? message) => ScrubErrorMessage = message;

    public long VisibleStartTicks => Viewport?.VisibleWindow(ViewportWidthPixels).StartTicks ?? 0;

    public long VisibleEndTicks => Viewport?.VisibleWindow(ViewportWidthPixels).EndTicks ?? 0;

    public double TicksPerPixel => Viewport?.TicksPerPixel ?? 0d;

    /// <summary>
    /// Builds a fresh <see cref="Viewport"/> spanning <c>[0, <see cref="TotalDurationTicks"/>)</c> at
    /// <see cref="DefaultTicksPerPixel"/>. An invalid duration (zero/negative is still valid — it is
    /// this step's own "silence" edge case, an empty document with nothing on the timeline yet — but
    /// a value that <see cref="TimelineViewport.Create"/> itself rejects, e.g. via an inverted window,
    /// cannot occur from a single duration value) is handled by the same explicit Error path every
    /// other screen in this project uses; this method never throws out of a UI command.
    /// </summary>
    /// <summary>
    /// PHASE-03 Step 017: <paramref name="layout"/> is the already-imported <see cref="DisplayGraph"/>
    /// (<see cref="ShowImportViewModel.Layout"/>) to build <see cref="Rows"/> from; defaults to
    /// <see langword="null"/> so Step 016's existing call sites keep compiling and behaving
    /// identically (no layout imported yet simply means no rows — never a crash, same "missing
    /// media must be handled explicitly" edge case this screen already follows for the ruler).
    /// Row-tree building never throws (see <see cref="TimelineRowTreeBuilder"/>), so a malformed or
    /// cyclic layout still successfully loads the timeline; any finding is surfaced via
    /// <see cref="RowWarnings"/>, not an Error state.
    /// </summary>
    /// <summary>PHASE-03 Step 021: the row index the current drag rectangle started at — bound from the UI's own row-index input.</summary>
    public int RangeSelectionStartRowIndex
    {
        get => _rangeSelectionStartRowIndex;
        set => SetField(ref _rangeSelectionStartRowIndex, value);
    }

    /// <summary>The horizontal pixel the current drag rectangle started at.</summary>
    public double RangeSelectionStartPixel
    {
        get => _rangeSelectionStartPixel;
        set => SetField(ref _rangeSelectionStartPixel, value);
    }

    /// <summary>The row index the current drag rectangle currently ends at (order-independent relative to <see cref="RangeSelectionStartRowIndex"/>).</summary>
    public int RangeSelectionEndRowIndex
    {
        get => _rangeSelectionEndRowIndex;
        set => SetField(ref _rangeSelectionEndRowIndex, value);
    }

    /// <summary>The horizontal pixel the current drag rectangle currently ends at.</summary>
    public double RangeSelectionEndPixel
    {
        get => _rangeSelectionEndPixel;
        set => SetField(ref _rangeSelectionEndPixel, value);
    }

    /// <summary>
    /// PHASE-03 Step 021 ("Multi-row time-range selection with inclusive/exclusive boundary
    /// semantics"): the real <see cref="EditScope"/> most recently built by
    /// <see cref="ApplyRangeSelection"/>, or <see langword="null"/> when no range is currently
    /// selected — this, not any transient drag-rectangle coordinate, is "what is selected" (Section 3:
    /// never mixing selected row indices with identity).
    /// </summary>
    public EditScope? SelectedRangeScope
    {
        get => _selectedRangeScope;
        private set
        {
            if (SetField(ref _selectedRangeScope, value))
            {
                OnPropertyChanged(nameof(SelectedRangeScopeSummary));
            }
        }
    }

    /// <summary>
    /// The real "display exact scope" projection (Step 021 implementation guidance) of
    /// <see cref="SelectedRangeScope"/>: every selected model's own display name, and the exact
    /// half-open time window, in one user-facing string — <see langword="null"/> exactly when nothing
    /// is selected.
    /// </summary>
    public string? SelectedRangeScopeSummary
    {
        get
        {
            if (_selectedRangeScope is not { } scope)
            {
                return null;
            }

            var modelNames = scope.Models.Count == 0
                ? "(none)"
                : string.Join(", ", scope.Models.Select(m => m.Value).OrderBy(n => n, StringComparer.Ordinal));
            return $"Models: {modelNames} | Time: [{scope.Time.StartTicks}, {scope.Time.EndTicks})";
        }
    }

    /// <summary>
    /// Builds <see cref="SelectedRangeScope"/> from the current
    /// <see cref="RangeSelectionStartRowIndex"/>/<see cref="RangeSelectionStartPixel"/>/
    /// <see cref="RangeSelectionEndRowIndex"/>/<see cref="RangeSelectionEndPixel"/> — the real "drag
    /// rectangle" this step's own title names, against the same real, currently-visible, collapse-aware
    /// row list <see cref="Rows"/> itself renders (never a stale or re-derived copy). A no-op (not a
    /// crash, not a stale leftover scope silently kept) while no media/layout is loaded yet.
    /// </summary>
    public void ApplyRangeSelection()
    {
        if (Viewport is null || _rowTree is null)
        {
            return;
        }

        var visibleRows = TimelineRowFlattener.Flatten(_rowTree.Roots, _collapsedGroupIds);
        SelectedRangeScope = TimelineRangeSelectionBuilder.Build(
            visibleRows,
            RangeSelectionStartRowIndex,
            RangeSelectionEndRowIndex,
            RangeSelectionStartPixel,
            RangeSelectionEndPixel,
            Viewport,
            // PHASE-04 Step 030: the drag rectangle itself is never changed — only the resulting
            // tick window is snapped, through the exact same injected-policy parameter every other
            // Step 030 call site uses, never a second, hardcoded rounding call here.
            snapPolicy: SnapPolicy);
    }

    /// <summary>Clears the current range selection; a no-op when nothing is selected.</summary>
    public void ClearRangeSelection() => SelectedRangeScope = null;

    /// <summary>
    /// PHASE-03 Step 018: <paramref name="document"/> is the already-generated
    /// <see cref="SequenceDocument"/> (<see cref="ShowImportViewModel.Plan"/>) to render effect
    /// blocks from; defaults to <see langword="null"/> so Step 016/017's existing call sites keep
    /// compiling and behaving identically (no plan generated yet simply means no effect blocks,
    /// never a crash — the same "missing media" edge case this screen already follows). Any prior
    /// selection is cleared on every (re)load — a selected effect id from a now-replaced document
    /// would otherwise dangle.
    /// </summary>
    /// <summary>
    /// PHASE-04 Step 027: <paramref name="waveformCache"/> is the already-built/reopened waveform
    /// pyramid (<see cref="AudioImportViewModel.LoadedWaveformCache"/>) to render beneath the ruler;
    /// defaults to <see langword="null"/> so Step 016-021's existing call sites keep compiling and
    /// behaving identically (no waveform cache built yet simply means no waveform strip, never a
    /// crash — the same "missing media" edge case this screen already follows for rows/effect
    /// blocks). <paramref name="waveformOriginTicks"/> is the same cache's source
    /// <see cref="XlightsAI.Studio.Audio.SongAsset.OriginTicks"/>, so bars align with the canonical
    /// timeline even once a later step re-origins a song (always 0 today).
    /// </summary>
    public void LoadTimeline(
        DisplayGraph? layout = null, SequenceDocument? document = null,
        WaveformPyramidCache? waveformCache = null, long waveformOriginTicks = 0)
    {
        ErrorMessage = null;
        _selectedEffectId = null;
        _selectedRangeScope = null;
        ScrubErrorMessage = null;
        LoopWindow = null;
        LoopErrorMessage = null;
        ClearScrubTelemetry();
        try
        {
            if (TotalDurationTicks < 0)
            {
                throw new TimelineViewportInvalidException(
                    $"Invalid timeline duration: TotalDurationTicks ({TotalDurationTicks}) must not be negative.");
            }

            var bounds = new TimeWindow(0, TotalDurationTicks);
            Viewport = TimelineViewport.Create(bounds, DefaultTicksPerPixel);
            _rowTree = TimelineRowTreeBuilder.Build(layout);
            _document = document;
            _waveformCache = waveformCache;
            _waveformOriginTicks = waveformOriginTicks;
            LoadState = ScreenLoadState.Loaded;
        }
        catch (Exception ex)
        {
            Viewport = null;
            _rowTree = null;
            _document = null;
            _waveformCache = null;
            _waveformOriginTicks = 0;
            ErrorMessage = ex.Message;
            LoadState = ScreenLoadState.Error;
        }
        finally
        {
            RaiseViewportDerivedChanged();
        }
    }

    /// <summary>Resets to <see cref="ScreenLoadState.Empty"/> with no viewport — the explicit "missing media" state before any <see cref="LoadTimeline"/> call has ever succeeded. Collapse state (<see cref="_collapsedGroupIds"/>) is deliberately preserved across a reset, same as it is across a reload: it is keyed by stable row identity, not by any single load's row tree.</summary>
    public void Reset()
    {
        Viewport = null;
        _rowTree = null;
        _document = null;
        _waveformCache = null;
        _waveformOriginTicks = 0;
        _selectedEffectId = null;
        _selectedRangeScope = null;
        ErrorMessage = null;
        ScrubErrorMessage = null;
        LoopWindow = null;
        LoopErrorMessage = null;
        ClearScrubTelemetry();
        LoadState = ScreenLoadState.Empty;
        RaiseViewportDerivedChanged();
    }

    /// <summary>Toggles a group row's expand/collapse state; a no-op for a <see cref="TimelineRowId"/> that is not currently a group row's id (it would simply sit unused in <see cref="_collapsedGroupIds"/>).</summary>
    public void ToggleGroupExpansion(TimelineRowId groupId)
    {
        if (!_collapsedGroupIds.Remove(groupId))
        {
            _collapsedGroupIds.Add(groupId);
        }

        OnPropertyChanged(nameof(Rows));
    }

    /// <summary>Zooms in (halves <see cref="TicksPerPixel"/>), anchored at the current viewport's horizontal center. A no-op while <see cref="Viewport"/> is <see langword="null"/> (no media loaded).</summary>
    public void ZoomIn() => ApplyZoom(0.5d);

    /// <summary>Zooms out (doubles <see cref="TicksPerPixel"/>), anchored at the current viewport's horizontal center. A no-op while <see cref="Viewport"/> is <see langword="null"/> (no media loaded).</summary>
    public void ZoomOut() => ApplyZoom(2.0d);

    /// <summary>Scrolls the viewport by <paramref name="pixelDelta"/> horizontal pixels, clamped to the virtualized content bounds. A no-op while <see cref="Viewport"/> is <see langword="null"/> (no media loaded).</summary>
    public void ScrollByPixels(double pixelDelta)
    {
        if (Viewport is null)
        {
            return;
        }

        Viewport = Viewport.ScrollByPixels(pixelDelta);
        RaiseViewportDerivedChanged();
    }

    private void ApplyZoom(double factor)
    {
        if (Viewport is null)
        {
            return;
        }

        Viewport = Viewport.ZoomAt(ViewportWidthPixels / 2, factor);
        RaiseViewportDerivedChanged();
    }

    private void RaiseViewportDerivedChanged()
    {
        OnPropertyChanged(nameof(Viewport));
        OnPropertyChanged(nameof(RulerTicks));
        OnPropertyChanged(nameof(VisibleStartTicks));
        OnPropertyChanged(nameof(VisibleEndTicks));
        OnPropertyChanged(nameof(TicksPerPixel));
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(RowWarnings));
        OnPropertyChanged(nameof(HasWaveform));
        OnPropertyChanged(nameof(WaveformBars));
        OnPropertyChanged(nameof(SelectedEffectId));
        OnPropertyChanged(nameof(SelectedEffectInspector));
        OnPropertyChanged(nameof(SelectedRangeScope));
        OnPropertyChanged(nameof(SelectedRangeScopeSummary));
        OnPropertyChanged(nameof(PlayheadX));
        OnPropertyChanged(nameof(PlayheadMarginLeft));
        OnPropertyChanged(nameof(IsPlayheadVisible));
        OnPropertyChanged(nameof(PlayheadFrameIndex));
        OnPropertyChanged(nameof(LastScrubRequestedTicks));
        OnPropertyChanged(nameof(LastScrubActualTicks));
        OnPropertyChanged(nameof(LastScrubDriftTicks));
        OnPropertyChanged(nameof(TimingDiagnosticsSummary));
    }
}
