using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Display;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// PHASE-10 Step 067 ("Add configurable instrument-to-role and role-to-prop routing UI") -- SCREEN 12
/// CHOREOGRAPHY's own bindable routing-table view model. Owns the UI-facing projection of one section's
/// (phrase's) <see cref="MusicalRoleAssignmentSet"/> -- loaded/saved through <see cref="IMusicalRoleAssignmentCatalog"/>
/// -- against the currently open layout's named <see cref="SpatialPathSet"/> and a caller-supplied set of
/// currently available instrument/stem track ids. Contains no Avalonia types, the same convention every
/// other screen view model in this project already follows (<see cref="DisplayPathEditorViewModel"/>/
/// <see cref="PianoRollViewModel"/>).
///
/// <para>
/// Mirrors <see cref="DisplayPathEditorViewModel"/>'s own explicit loading/empty/error/loaded state machine
/// (no section opened yet is <see cref="ScreenLoadState.Empty"/>, never a silently blank table) and its own
/// "catch the domain's typed invalid-input exception, set <see cref="ActionErrorMessage"/>, leave bindable
/// state completely unchanged on failure" convention for every mutating action.
/// </para>
///
/// <para>
/// Required acceptance, verbatim: "Mappings persist and can change per section" -- <see cref="OpenSectionAsync"/>
/// loads a given phrase/section's own independently persisted <see cref="MusicalRoleAssignmentSet"/>
/// (Step 066's own already-accepted per-<c>PhraseId</c> persistence), and can be called again with a
/// different <see cref="Guid"/> section id at any time to switch the bound <see cref="Rows"/>/<see cref="Warnings"/>
/// to a different section's own independently saved routing -- proven directly by <see cref="SectionId"/>
/// changing and <see cref="Rows"/> reflecting the newly opened section's own assignments, not the previous
/// section's.
/// </para>
/// </summary>
public sealed class RoleRoutingEditorViewModel : ObservableObject
{
    private ScreenLoadState _loadState = ScreenLoadState.Empty;
    private string? _errorMessage;
    private string? _actionErrorMessage;
    private Guid? _sectionId;
    private SpatialPathSet? _pathSet;
    private IReadOnlyList<string> _availableTrackIds = [];
    private MusicalRoleAssignmentSet? _assignments;

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

    /// <summary>Set whenever an assign/clear action fails (invalid input); cleared on the next successful action. <see cref="Rows"/> is left completely unchanged on failure.</summary>
    public string? ActionErrorMessage
    {
        get => _actionErrorMessage;
        private set => SetField(ref _actionErrorMessage, value);
    }

    public bool IsEmpty => LoadState == ScreenLoadState.Empty;

    public bool IsError => LoadState == ScreenLoadState.Error;

    public bool IsLoaded => LoadState == ScreenLoadState.Loaded;

    /// <summary>The section (phrase) id currently open, or <see langword="null"/> while <see cref="IsEmpty"/>.</summary>
    public Guid? SectionId => _sectionId;

    /// <summary>The current persisted routing-set version for <see cref="SectionId"/>, or <see langword="null"/> while <see cref="IsEmpty"/>.</summary>
    public int? Version => _assignments?.Version;

    /// <summary>
    /// The real, currently-routed rows for the open section -- empty exactly when <see cref="IsLoaded"/> is
    /// <see langword="false"/>, never a stale leftover from a previously opened section.
    /// </summary>
    public IReadOnlyList<RoleRoutingRowView> Rows
    {
        get
        {
            if (_assignments is not { } assignments)
            {
                return [];
            }

            var rows = new List<RoleRoutingRowView>(assignments.Assignments.Count);
            foreach (var assignment in assignments.Assignments)
            {
                var path = _pathSet?.Paths.FirstOrDefault(p => p.Id == assignment.SpatialPathId);
                var trackId = assignment.SourceTrackId;
                rows.Add(new RoleRoutingRowView(
                    trackId,
                    assignment.Role,
                    assignment.SpatialPathId,
                    path?.Name,
                    assignment.PrecedenceTier,
                    assignment.Priority,
                    assignment.Weight,
                    IsSourceUnavailable: !_availableTrackIds.Contains(trackId, StringComparer.Ordinal),
                    IsPathUnavailable: path is null));
            }

            return rows;
        }
    }

    /// <summary>
    /// "... with role priority and empty/unavailable warnings" (implementation guidance, verbatim) -- every
    /// <see cref="MusicalRole"/> with no live routing in the open section (<see cref="RoleRoutingWarningKind.UnmappedRole"/>),
    /// every live routing whose source track is no longer in the currently supplied available set
    /// (<see cref="RoleRoutingWarningKind.UnavailableSource"/>), and every live routing whose target path no
    /// longer exists in the currently open layout (<see cref="RoleRoutingWarningKind.UnavailablePath"/>).
    /// Empty exactly when <see cref="IsLoaded"/> is <see langword="false"/>.
    /// </summary>
    public IReadOnlyList<RoleRoutingWarningView> Warnings
    {
        get
        {
            if (_assignments is not { } assignments)
            {
                return [];
            }

            var warnings = new List<RoleRoutingWarningView>();
            foreach (var role in Enum.GetValues<MusicalRole>())
            {
                if (!assignments.Assignments.Any(a => a.Role == role))
                {
                    warnings.Add(new RoleRoutingWarningView(
                        RoleRoutingWarningKind.UnmappedRole,
                        $"No source track is routed to role '{role}' for this section."));
                }
            }

            foreach (var assignment in assignments.Assignments)
            {
                if (!_availableTrackIds.Contains(assignment.SourceTrackId, StringComparer.Ordinal))
                {
                    warnings.Add(new RoleRoutingWarningView(
                        RoleRoutingWarningKind.UnavailableSource,
                        $"Source track '{assignment.SourceTrackId}' (role '{assignment.Role}') is no longer available."));
                }

                if (_pathSet is null || !_pathSet.Paths.Any(p => p.Id == assignment.SpatialPathId))
                {
                    warnings.Add(new RoleRoutingWarningView(
                        RoleRoutingWarningKind.UnavailablePath,
                        $"Target path for source track '{assignment.SourceTrackId}' (role '{assignment.Role}') is no longer available."));
                }
            }

            return warnings;
        }
    }

    /// <summary>
    /// Opens (or switches to) <paramref name="sectionId"/>'s own independently persisted routing, loaded
    /// through <paramref name="catalog"/>, against <paramref name="pathSet"/> and
    /// <paramref name="availableTrackIds"/>. Calling this again with a different <paramref name="sectionId"/>
    /// at any time loads that section's own independently saved routing -- the required acceptance text,
    /// verbatim: "Mappings ... can change per section."
    /// </summary>
    public async Task<bool> OpenSectionAsync(
        IMusicalRoleAssignmentCatalog catalog,
        SpatialPathSet pathSet,
        Guid sectionId,
        IReadOnlyList<string> availableTrackIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(pathSet);
        ArgumentNullException.ThrowIfNull(availableTrackIds);

        ErrorMessage = null;
        ActionErrorMessage = null;
        try
        {
            var assignments = await catalog.GetAssignmentsAsync(sectionId, ct).ConfigureAwait(true);
            _sectionId = sectionId;
            _pathSet = pathSet;
            _availableTrackIds = availableTrackIds;
            _assignments = assignments;
            LoadState = ScreenLoadState.Loaded;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (Exception ex)
        {
            _sectionId = null;
            _pathSet = null;
            _availableTrackIds = [];
            _assignments = null;
            ErrorMessage = ex.Message;
            LoadState = ScreenLoadState.Error;
            RaiseStateDerivedChanged();
            return false;
        }
    }

    /// <summary>
    /// Routes <paramref name="sourceTrackId"/> to <paramref name="role"/>/<paramref name="spatialPathId"/>
    /// for the currently open section via <see cref="IMusicalRoleAssignmentCatalog.AssignAsync"/>. Returns
    /// <see langword="false"/> (via <see cref="ActionErrorMessage"/>) when no section is open or the catalog
    /// rejects the request (blank track id, undefined role, unrecognized precedence tier, or an unknown
    /// target path) -- never a partial mutation of <see cref="Rows"/>.
    /// </summary>
    public async Task<bool> AssignAsync(
        IMusicalRoleAssignmentCatalog catalog,
        string sourceTrackId,
        MusicalRole role,
        Guid spatialPathId,
        string precedenceTier,
        int priority,
        double weight,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (_sectionId is not { } sectionId || _pathSet is not { } pathSet)
        {
            ActionErrorMessage = "Open a section before routing a source track.";
            return false;
        }

        try
        {
            _assignments = await catalog.AssignAsync(
                pathSet, sectionId, sourceTrackId, role, spatialPathId, precedenceTier, priority, weight, ct).ConfigureAwait(true);
            ActionErrorMessage = null;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (CompositionInvalidException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Clears the live routing for <paramref name="sourceTrackId"/> in the currently open section via
    /// <paramref name="catalog"/>. Returns <see langword="false"/> (via <see cref="ActionErrorMessage"/>)
    /// when no section is open or no live routing exists for <paramref name="sourceTrackId"/> -- never a
    /// partial mutation of <see cref="Rows"/>.
    /// </summary>
    public async Task<bool> ClearAsync(IMusicalRoleAssignmentCatalog catalog, string sourceTrackId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (_sectionId is not { } sectionId)
        {
            ActionErrorMessage = "Open a section before clearing a routed track.";
            return false;
        }

        try
        {
            _assignments = await catalog.ClearAsync(sectionId, sourceTrackId, ct).ConfigureAwait(true);
            ActionErrorMessage = null;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (CompositionInvalidException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    private void RaiseStateDerivedChanged()
    {
        OnPropertyChanged(nameof(SectionId));
        OnPropertyChanged(nameof(Version));
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(Warnings));
    }
}
