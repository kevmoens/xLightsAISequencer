using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Melody;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// PHASE-09 Step 061 ("Create graphical layout/path editor with position correction") -- SCREEN 05
/// DISPLAY PATHS. Owns the bindable rendering of a <see cref="SpatialDisplayGraph"/> with its
/// <see cref="DisplayLayoutCorrections"/> (manual position corrections and list-editor reorder) applied
/// via <see cref="DisplayLayoutProjection.Apply"/>, and routes user edits through
/// <see cref="IDisplayLayoutEditor"/>. Contains no Avalonia types, same convention as every other screen
/// view model in this project (<see cref="PianoRollViewModel"/>/<see cref="TimelineViewModel"/>).
/// Mirrors <see cref="PianoRollViewModel"/>'s explicit loading/empty/error/loaded state machine: no
/// layout opened yet is <see cref="ScreenLoadState.Empty"/>, never a silently blank canvas.
///
/// Every mutating action here (<see cref="CorrectSelectedNodePositionAsync"/>/<see cref="ReorderAsync"/>)
/// catches <see cref="SpatialDisplayGraphInvalidException"/>, sets <see cref="ActionErrorMessage"/>, and
/// leaves <see cref="Rows"/> completely unchanged on failure -- the same "catch, set an error property,
/// never let an exception reach the UI, never a partial mutation" convention every other screen action
/// in this project already follows.
/// </summary>
public sealed class DisplayPathEditorViewModel : ObservableObject
{
    private ScreenLoadState _loadState = ScreenLoadState.Empty;
    private string? _errorMessage;
    private string? _actionErrorMessage;
    private SpatialDisplayGraph? _graph;
    private DisplayLayoutCorrections? _corrections;
    private ModelId? _selectedNodeId;
    private string _pendingPositionXText = string.Empty;
    private string _pendingPositionYText = string.Empty;
    private SpatialPathSet? _pathSet;
    private RoleAssignmentSet? _roleAssignmentSet;
    private ResolvedGesturePreview? _gesturePreview;
    private LockedNodeSet? _lockedNodes;
    private RouteValidationReport? _routeValidation;

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

    /// <summary>Set whenever a correction/reorder action fails (unknown node, out-of-range position, or an invalid reorder set); cleared on the next successful action. <see cref="Rows"/> is left completely unchanged on failure.</summary>
    public string? ActionErrorMessage
    {
        get => _actionErrorMessage;
        private set => SetField(ref _actionErrorMessage, value);
    }

    public bool IsEmpty => LoadState == ScreenLoadState.Empty;

    public bool IsError => LoadState == ScreenLoadState.Error;

    public bool IsLoaded => LoadState == ScreenLoadState.Loaded;

    /// <summary>The source layout hash currently open, or <see langword="null"/> while <see cref="IsEmpty"/>.</summary>
    public string? SourceLayoutHash => _graph?.SourceLayoutHash;

    /// <summary>The current persisted corrections version, or <see langword="null"/> while <see cref="IsEmpty"/>.</summary>
    public int? Version => _corrections?.Version;

    public ModelId? SelectedNodeId
    {
        get => _selectedNodeId;
        private set
        {
            if (SetField(ref _selectedNodeId, value))
            {
                OnPropertyChanged(nameof(Rows));
            }
        }
    }

    /// <summary>
    /// The real, currently-rendered node rows -- <see cref="DisplayLayoutProjection.Apply"/> applied to
    /// the currently open graph/corrections, in the list editor's own current display order. Empty
    /// exactly when <see cref="IsLoaded"/> is <see langword="false"/>, never a stale leftover from a
    /// previously opened layout.
    /// </summary>
    public IReadOnlyList<DisplayNodeRowView> Rows
    {
        get
        {
            if (_graph is not { } graph || _corrections is not { } corrections)
            {
                return [];
            }

            var projected = DisplayLayoutProjection.Apply(graph, corrections);
            var rows = new List<DisplayNodeRowView>(projected.Nodes.Count);
            foreach (var node in projected.Nodes)
            {
                var nodeId = node.Id;
                rows.Add(new DisplayNodeRowView(
                    nodeId,
                    node.XlightsName,
                    node.X,
                    node.Y,
                    node.ModelKind,
                    IsSelected: nodeId == SelectedNodeId,
                    SelectCommand: new DelegateCommand(() => SelectNode(nodeId))));
            }

            return rows;
        }
    }

    /// <summary>
    /// PHASE-09 Step 062 ("Define named custom paths and semantic spatial motions"): the real,
    /// currently-persisted named paths for the currently open layout -- empty exactly when
    /// <see cref="IsLoaded"/> is <see langword="false"/> or <see cref="LoadPathsAsync"/> has not yet been
    /// called for this layout, never a stale leftover from a previously opened one.
    /// </summary>
    public IReadOnlyList<SpatialPath> NamedPaths => _pathSet?.Paths ?? [];

    /// <summary>The current persisted path-set version, or <see langword="null"/> while no path set has been loaded.</summary>
    public int? PathSetVersion => _pathSet?.Version;

    /// <summary>
    /// PHASE-09 Step 063 ("Persist role tags: lead, rhythm, ambient, hero and accent"): the real,
    /// currently-persisted role assignments for the currently open layout -- SCREEN 12 CHOREOGRAPHY's own
    /// required acceptance, verbatim: "Role assignments are available in choreography UI." Empty exactly
    /// when <see cref="IsLoaded"/> is <see langword="false"/> or <see cref="LoadRoleAssignmentsAsync"/> has
    /// not yet been called for this layout, never a stale leftover from a previously opened one.
    /// </summary>
    public IReadOnlyList<RoleAssignment> RoleAssignments => _roleAssignmentSet?.Assignments ?? [];

    /// <summary>The current persisted role-assignment-set version, or <see langword="null"/> while no role assignment set has been loaded.</summary>
    public int? RoleAssignmentSetVersion => _roleAssignmentSet?.Version;

    /// <summary>
    /// PHASE-09 Step 064 ("Map pitch intervals/movement to jumps, holds and path traversal"): the real,
    /// currently-resolved geometry-aware gesture walk for the currently open layout -- the "synchronized
    /// waveform/timeline demonstration" this step's own required text names, at the bindable layer a real
    /// SCREEN 12 timeline would read through. Empty exactly when <see cref="ResolveGesturePreviewAsync"/>
    /// has not yet been called for this layout, never a stale leftover from a previously opened one.
    /// </summary>
    public IReadOnlyList<ResolvedGesture> GesturePreview => _gesturePreview?.Gestures ?? [];

    /// <summary>Identity of the most recently resolved <see cref="GesturePreview"/> run, or <see langword="null"/> while none has been resolved yet.</summary>
    public Guid? GesturePreviewVersionId => _gesturePreview?.VersionId;

    /// <summary>
    /// PHASE-09 Step 065 ("Add route validation, conflicting-write checks and incompatible prop fallback"):
    /// the real, currently-persisted locked/excluded node ids for the currently open layout. Empty exactly
    /// when <see cref="IsLoaded"/> is <see langword="false"/> or <see cref="LoadLockedNodesAsync"/> has not
    /// yet been called for this layout, never a stale leftover from a previously opened one.
    /// </summary>
    public IReadOnlyList<ModelId> LockedNodeIds => _lockedNodes?.LockedNodeIds ?? [];

    /// <summary>The current persisted locked-node-set version, or <see langword="null"/> while no locked-node set has been loaded.</summary>
    public int? LockedNodeSetVersion => _lockedNodes?.Version;

    /// <summary>
    /// PHASE-09 Step 065: the real, most recently produced route validation report for the currently open
    /// layout -- SCREEN 05/12's own "report incompatibilities before generating XSQ" (implementation
    /// guidance, verbatim), at the bindable layer a real export/XSQ-generation action would gate on. Empty
    /// (no issues) exactly when <see cref="ValidateRouteAsync"/> has not yet been called for this layout --
    /// never conflated with "validated and found clean."
    /// </summary>
    public IReadOnlyList<RouteIssue> RouteIssues => _routeValidation?.Issues ?? [];

    /// <summary>
    /// <see langword="true"/> only after <see cref="ValidateRouteAsync"/> has actually run and the resulting
    /// report carries at least one <see cref="RouteIssueSeverity.Error"/> -- this step's own required
    /// acceptance, verbatim: "Invalid path cannot silently produce mis-targeted XSQ effects." A real SCREEN
    /// 05/12 export action should refuse to proceed while this is <see langword="true"/>.
    /// </summary>
    public bool HasBlockingRouteIssues => _routeValidation?.HasBlockingIssues ?? false;

    /// <summary>Identity of the most recently produced <see cref="RouteIssues"/> run, or <see langword="null"/> while none has been validated yet.</summary>
    public Guid? RouteValidationVersionId => _routeValidation?.VersionId;

    /// <summary>
    /// Bindable pending-correction text for <see cref="CorrectSelectedNodePositionAsync"/> — same
    /// "text field + button, parsed on explicit action" convention as
    /// <see cref="TimelineViewModel.ScrubTargetTicksText"/>. Pre-filled with the selected node's
    /// current position by <see cref="SelectNode"/> so entering a position means nudging the current
    /// value, not re-typing it from scratch.
    /// </summary>
    public string PendingPositionXText
    {
        get => _pendingPositionXText;
        set => SetField(ref _pendingPositionXText, value);
    }

    public string PendingPositionYText
    {
        get => _pendingPositionYText;
        set => SetField(ref _pendingPositionYText, value);
    }

    /// <summary>Selects <paramref name="nodeId"/> -- a no-op (not a crash, not a dangling selection) when no layout is open or <paramref name="nodeId"/> does not exist in it.</summary>
    public void SelectNode(ModelId nodeId)
    {
        if (_graph is not { } graph)
        {
            return;
        }

        var node = graph.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null)
        {
            return;
        }

        SelectedNodeId = nodeId;
        var row = Rows.FirstOrDefault(r => r.NodeId == nodeId);
        PendingPositionXText = (row?.X ?? 0d).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        PendingPositionYText = (row?.Y ?? 0d).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Clears the current selection; a no-op when nothing is selected.</summary>
    public void ClearSelection()
    {
        SelectedNodeId = null;
        PendingPositionXText = string.Empty;
        PendingPositionYText = string.Empty;
    }

    /// <summary>
    /// Opens <paramref name="graph"/> (Step 059/060's own already-built <see cref="SpatialDisplayGraph"/>),
    /// loading whatever corrections <paramref name="editor"/> has previously persisted for its source
    /// layout hash (empty corrections if none exist yet -- never an error).
    /// </summary>
    public async Task<bool> OpenAsync(IDisplayLayoutEditor editor, SpatialDisplayGraph graph, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(graph);

        ErrorMessage = null;
        ActionErrorMessage = null;
        _pathSet = null;
        _roleAssignmentSet = null;
        _gesturePreview = null;
        _lockedNodes = null;
        _routeValidation = null;
        try
        {
            var corrections = await editor.GetCorrectionsAsync(graph.SourceLayoutHash, ct).ConfigureAwait(true);
            ApplyLoaded(graph, corrections);
            return true;
        }
        catch (Exception ex)
        {
            _graph = null;
            _corrections = null;
            _selectedNodeId = null;
            ErrorMessage = ex.Message;
            LoadState = ScreenLoadState.Error;
            RaiseStateDerivedChanged();
            return false;
        }
    }

    /// <summary>
    /// Corrects <see cref="SelectedNodeId"/>'s own normalized preview position ("drag," implementation
    /// guidance). Returns <see langword="false"/> (via <see cref="ActionErrorMessage"/>) when nothing is
    /// selected, no layout is open, or <paramref name="editor"/> rejects the correction (out-of-range
    /// coordinate -- never possible for an unknown node id, since <see cref="SelectedNodeId"/> can only
    /// ever hold an id that exists in the currently open graph) -- never a partial mutation of
    /// <see cref="Rows"/>.
    /// </summary>
    public async Task<bool> CorrectSelectedNodePositionAsync(
        IDisplayLayoutEditor editor, double newX, double newY, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(editor);

        if (_graph is not { } graph || SelectedNodeId is not { } selectedId)
        {
            ActionErrorMessage = "Select a prop before correcting its position.";
            return false;
        }

        try
        {
            var updated = await editor.CorrectPositionAsync(graph, selectedId, newX, newY, ct).ConfigureAwait(true);
            ApplyLoaded(graph, updated);
            ActionErrorMessage = null;
            return true;
        }
        catch (SpatialDisplayGraphInvalidException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Saves a new list-editor row order ("reorder," implementation guidance). Returns
    /// <see langword="false"/> (via <see cref="ActionErrorMessage"/>) when no layout is open or
    /// <paramref name="editor"/> rejects the order (missing/duplicate/unknown node id) -- never a
    /// partial mutation of <see cref="Rows"/>.
    /// </summary>
    public async Task<bool> ReorderAsync(IDisplayLayoutEditor editor, IReadOnlyList<ModelId> newOrder, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(newOrder);

        if (_graph is not { } graph)
        {
            ActionErrorMessage = "Open a layout before reordering.";
            return false;
        }

        try
        {
            var updated = await editor.ReorderAsync(graph, newOrder, ct).ConfigureAwait(true);
            ApplyLoaded(graph, updated);
            ActionErrorMessage = null;
            return true;
        }
        catch (SpatialDisplayGraphInvalidException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// PHASE-09 Step 062: loads the real, currently-persisted named paths for the currently open layout
    /// into <see cref="NamedPaths"/>. A no-op (via <see cref="ActionErrorMessage"/>) when no layout is
    /// open.
    /// </summary>
    public async Task<bool> LoadPathsAsync(ISpatialPathCatalog catalog, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (_graph is not { } graph)
        {
            ActionErrorMessage = "Open a layout before loading its named paths.";
            return false;
        }

        _pathSet = await catalog.GetPathsAsync(graph.SourceLayoutHash, ct).ConfigureAwait(true);
        ActionErrorMessage = null;
        RaiseStateDerivedChanged();
        return true;
    }

    /// <summary>
    /// Creates (or, with <paramref name="allowReplaceExisting"/>, replaces) a named spatial path via
    /// <see cref="ISpatialPathCatalog.CreateAsync"/>. Returns <see langword="false"/> (via
    /// <see cref="ActionErrorMessage"/>) when no layout is open or the catalog rejects the request
    /// (unknown node id, unsupported direction policy, or a duplicate name without
    /// <paramref name="allowReplaceExisting"/>) -- never a partial mutation of <see cref="NamedPaths"/>.
    /// </summary>
    public async Task<bool> CreatePathAsync(ISpatialPathCatalog catalog, string name, IReadOnlyList<ModelId> candidateNodeIds,
        string directionPolicy, bool isClosed, bool allowReplaceExisting, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (_graph is not { } graph)
        {
            ActionErrorMessage = "Open a layout before creating a named path.";
            return false;
        }

        try
        {
            _pathSet = await catalog.CreateAsync(graph, name, candidateNodeIds, directionPolicy, isClosed, allowReplaceExisting, ct).ConfigureAwait(true);
            ActionErrorMessage = null;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (SpatialDisplayGraphInvalidException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>Reorders a named path identified by <paramref name="pathId"/> via <see cref="ISpatialPathCatalog.ReorderAsync"/>.</summary>
    public async Task<bool> ReorderPathAsync(ISpatialPathCatalog catalog, Guid pathId, IReadOnlyList<ModelId> newOrder, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (_graph is not { } graph)
        {
            ActionErrorMessage = "Open a layout before reordering a named path.";
            return false;
        }

        try
        {
            _pathSet = await catalog.ReorderAsync(graph, pathId, newOrder, ct).ConfigureAwait(true);
            ActionErrorMessage = null;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (SpatialDisplayGraphInvalidException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>Reverses a named path identified by <paramref name="pathId"/> via <see cref="ISpatialPathCatalog.ReverseAsync"/>.</summary>
    public async Task<bool> ReversePathAsync(ISpatialPathCatalog catalog, Guid pathId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (_graph is not { } graph)
        {
            ActionErrorMessage = "Open a layout before reversing a named path.";
            return false;
        }

        try
        {
            _pathSet = await catalog.ReverseAsync(graph, pathId, ct).ConfigureAwait(true);
            ActionErrorMessage = null;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (SpatialDisplayGraphInvalidException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// PHASE-09 Step 063: loads the real, currently-persisted role assignments for the currently open
    /// layout into <see cref="RoleAssignments"/>. A no-op (via <see cref="ActionErrorMessage"/>) when no
    /// layout is open.
    /// </summary>
    public async Task<bool> LoadRoleAssignmentsAsync(IRoleAssignmentCatalog catalog, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (_graph is not { } graph)
        {
            ActionErrorMessage = "Open a layout before loading its role assignments.";
            return false;
        }

        _roleAssignmentSet = await catalog.GetAssignmentsAsync(graph.SourceLayoutHash, ct).ConfigureAwait(true);
        ActionErrorMessage = null;
        RaiseStateDerivedChanged();
        return true;
    }

    /// <summary>
    /// Assigns <paramref name="role"/> to <paramref name="nodeId"/> for <paramref name="scopeId"/> via
    /// <see cref="IRoleAssignmentCatalog.AssignAsync"/>. Returns <see langword="false"/> (via
    /// <see cref="ActionErrorMessage"/>) when no layout is open or the catalog rejects the request (unknown
    /// node id or unrecognized role tag) -- never a partial mutation of <see cref="RoleAssignments"/>.
    /// </summary>
    public async Task<bool> AssignRoleAsync(IRoleAssignmentCatalog catalog, ModelId nodeId, string role, Guid scopeId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (_graph is not { } graph)
        {
            ActionErrorMessage = "Open a layout before assigning a role.";
            return false;
        }

        try
        {
            _roleAssignmentSet = await catalog.AssignAsync(graph, nodeId, role, scopeId, ct).ConfigureAwait(true);
            ActionErrorMessage = null;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (SpatialDisplayGraphInvalidException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Clears the live role assignment for (<paramref name="nodeId"/>, <paramref name="scopeId"/>) via
    /// <see cref="IRoleAssignmentCatalog.ClearAsync"/>. Returns <see langword="false"/> (via
    /// <see cref="ActionErrorMessage"/>) when no layout is open or no live assignment exists for this exact
    /// pair -- never a partial mutation of <see cref="RoleAssignments"/>.
    /// </summary>
    public async Task<bool> ClearRoleAsync(IRoleAssignmentCatalog catalog, ModelId nodeId, Guid scopeId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (_graph is not { } graph)
        {
            ActionErrorMessage = "Open a layout before clearing a role.";
            return false;
        }

        try
        {
            _roleAssignmentSet = await catalog.ClearAsync(graph, nodeId, scopeId, ct).ConfigureAwait(true);
            ActionErrorMessage = null;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (SpatialDisplayGraphInvalidException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// PHASE-09 Step 064: resolves <paramref name="track"/>'s gestures against the named path identified by
    /// <paramref name="pathId"/> via <see cref="IGesturePreviewCatalog.ResolveAsync"/>, exposing the result
    /// on <see cref="GesturePreview"/>. Returns <see langword="false"/> (via
    /// <see cref="ActionErrorMessage"/>) when no layout is open, <paramref name="pathId"/> does not match any
    /// entry in <see cref="NamedPaths"/>, or the catalog rejects the request (empty path, invalid window,
    /// unknown boundary policy, or an unavailable node with <see cref="ResolutionSettings.SkipUnavailable"/>
    /// <see langword="false"/>) -- never a partial mutation of <see cref="GesturePreview"/>.
    /// </summary>
    public async Task<bool> ResolveGesturePreviewAsync(
        IGesturePreviewCatalog catalog, Guid pathId, MelodyTrack track, TimeWindow window, ResolutionSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(settings);

        if (_graph is not { } graph)
        {
            ActionErrorMessage = "Open a layout before resolving a gesture preview.";
            return false;
        }

        var path = (_pathSet?.Paths ?? graph.Paths).FirstOrDefault(p => p.Id == pathId);
        if (path is null)
        {
            ActionErrorMessage = $"No named path with id '{pathId}' exists for the currently open layout.";
            return false;
        }

        try
        {
            _gesturePreview = await catalog.ResolveAsync(graph, path, track, window, settings, ct).ConfigureAwait(true);
            ActionErrorMessage = null;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (SpatialDisplayGraphInvalidException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// PHASE-09 Step 065: loads the real, currently-persisted locked nodes for the currently open layout
    /// into <see cref="LockedNodeIds"/>. A no-op (via <see cref="ActionErrorMessage"/>) when no layout is
    /// open.
    /// </summary>
    public async Task<bool> LoadLockedNodesAsync(ILockedNodeCatalog catalog, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (_graph is not { } graph)
        {
            ActionErrorMessage = "Open a layout before loading its locked nodes.";
            return false;
        }

        _lockedNodes = await catalog.GetLockedNodesAsync(graph.SourceLayoutHash, ct).ConfigureAwait(true);
        ActionErrorMessage = null;
        RaiseStateDerivedChanged();
        return true;
    }

    /// <summary>
    /// Locks <paramref name="nodeId"/> via <see cref="ILockedNodeCatalog.LockAsync"/>. Returns
    /// <see langword="false"/> (via <see cref="ActionErrorMessage"/>) when no layout is open or the catalog
    /// rejects the request (unknown node id or already locked) -- never a partial mutation of
    /// <see cref="LockedNodeIds"/>.
    /// </summary>
    public async Task<bool> LockNodeAsync(ILockedNodeCatalog catalog, ModelId nodeId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (_graph is not { } graph)
        {
            ActionErrorMessage = "Open a layout before locking a node.";
            return false;
        }

        try
        {
            _lockedNodes = await catalog.LockAsync(graph, nodeId, ct).ConfigureAwait(true);
            ActionErrorMessage = null;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (SpatialDisplayGraphInvalidException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Unlocks <paramref name="nodeId"/> via <see cref="ILockedNodeCatalog.UnlockAsync"/>. Returns
    /// <see langword="false"/> (via <see cref="ActionErrorMessage"/>) when no layout is open or the node is
    /// not currently locked -- never a partial mutation of <see cref="LockedNodeIds"/>.
    /// </summary>
    public async Task<bool> UnlockNodeAsync(ILockedNodeCatalog catalog, ModelId nodeId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (_graph is not { } graph)
        {
            ActionErrorMessage = "Open a layout before unlocking a node.";
            return false;
        }

        try
        {
            _lockedNodes = await catalog.UnlockAsync(graph, nodeId, ct).ConfigureAwait(true);
            ActionErrorMessage = null;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (SpatialDisplayGraphInvalidException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// PHASE-09 Step 065: validates the named path identified by <paramref name="pathId"/> via
    /// <see cref="IRouteValidationCatalog.ValidateAsync"/>, exposing the result on <see cref="RouteIssues"/>/
    /// <see cref="HasBlockingRouteIssues"/>. Returns <see langword="false"/> (via
    /// <see cref="ActionErrorMessage"/>) when no layout is open, <paramref name="pathId"/> does not match any
    /// entry in <see cref="NamedPaths"/>, or the catalog rejects the request (empty path, mismatched locked-
    /// node layout, or a conflicting/stale path version) -- never a partial mutation of
    /// <see cref="RouteIssues"/>. Locked nodes are loaded automatically if <see cref="LoadLockedNodesAsync"/>
    /// has not yet been called for this layout (an empty locked-node set, never an error, the same "no
    /// locks yet" default <see cref="LockedNodeSet.Empty"/> establishes).
    /// </summary>
    public async Task<bool> ValidateRouteAsync(
        IRouteValidationCatalog catalog, ILockedNodeCatalog lockedNodeCatalog, Guid pathId,
        string? recipeId = null, EffectRecipeSupport? recipeSupport = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(lockedNodeCatalog);

        if (_graph is not { } graph)
        {
            ActionErrorMessage = "Open a layout before validating a route.";
            return false;
        }

        var path = (_pathSet?.Paths ?? graph.Paths).FirstOrDefault(p => p.Id == pathId);
        if (path is null)
        {
            ActionErrorMessage = $"No named path with id '{pathId}' exists for the currently open layout.";
            return false;
        }

        try
        {
            _lockedNodes ??= await lockedNodeCatalog.GetLockedNodesAsync(graph.SourceLayoutHash, ct).ConfigureAwait(true);
            var graphWithLivePaths = _pathSet is null ? graph : graph with { Paths = _pathSet.Paths };
            _routeValidation = await catalog.ValidateAsync(
                graphWithLivePaths, path, _lockedNodes, recipeId, recipeSupport ?? EffectRecipeSupport.Unrestricted, ct).ConfigureAwait(true);
            ActionErrorMessage = null;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (SpatialDisplayGraphInvalidException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    private void ApplyLoaded(SpatialDisplayGraph graph, DisplayLayoutCorrections corrections)
    {
        _graph = graph;
        _corrections = corrections;
        if (SelectedNodeId is { } selected && !graph.Nodes.Any(n => n.Id == selected))
        {
            // A stale selection from before this reload (e.g. a reorder/reimport that no longer
            // contains it) never dangles.
            _selectedNodeId = null;
        }

        LoadState = ScreenLoadState.Loaded;
        RaiseStateDerivedChanged();
    }

    private void RaiseStateDerivedChanged()
    {
        OnPropertyChanged(nameof(SourceLayoutHash));
        OnPropertyChanged(nameof(Version));
        OnPropertyChanged(nameof(SelectedNodeId));
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(NamedPaths));
        OnPropertyChanged(nameof(PathSetVersion));
        OnPropertyChanged(nameof(RoleAssignments));
        OnPropertyChanged(nameof(RoleAssignmentSetVersion));
        OnPropertyChanged(nameof(GesturePreview));
        OnPropertyChanged(nameof(GesturePreviewVersionId));
        OnPropertyChanged(nameof(LockedNodeIds));
        OnPropertyChanged(nameof(LockedNodeSetVersion));
        OnPropertyChanged(nameof(RouteIssues));
        OnPropertyChanged(nameof(HasBlockingRouteIssues));
        OnPropertyChanged(nameof(RouteValidationVersionId));
    }
}
