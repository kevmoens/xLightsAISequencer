using XlightsAI.Studio.Projects;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// The Projects screen: a loading/empty/error/loaded list (Step 004) plus, as of Step 006, real
/// create/open actions backed by <see cref="IProjectService"/>. <see cref="Load"/>'s signature is
/// unchanged from Step 004/005 (still a plain <c>Func&lt;IReadOnlyList&lt;string&gt;&gt;</c>) —
/// Step 006's new behavior is added as new members (<see cref="CreateProjectAsync"/>,
/// <see cref="OpenProjectAsync"/> each take the service as a parameter, the same pattern
/// <see cref="Load"/> already uses for its data source) rather than by changing the constructor or
/// any existing method signature, so Step 004/005's tests and call sites keep compiling unmodified.
/// </summary>
public sealed class ProjectsViewModel : ObservableObject
{
    private ProjectsLoadState _loadState = ProjectsLoadState.Loading;
    private string? _errorMessage;
    private IReadOnlyList<string> _projectNames = Array.Empty<string>();
    private string _workspaceDirectory = string.Empty;
    private string _newProjectName = string.Empty;
    private string? _actionErrorMessage;
    private bool _isBusy;

    public ProjectsLoadState LoadState
    {
        get => _loadState;
        private set
        {
            if (SetField(ref _loadState, value))
            {
                OnPropertyChanged(nameof(IsLoading));
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

    public IReadOnlyList<string> ProjectNames
    {
        get => _projectNames;
        private set => SetField(ref _projectNames, value);
    }

    public bool IsLoading => LoadState == ProjectsLoadState.Loading;

    public bool IsEmpty => LoadState == ProjectsLoadState.Empty;

    public bool IsError => LoadState == ProjectsLoadState.Error;

    public bool IsLoaded => LoadState == ProjectsLoadState.Loaded;

    /// <summary>Bindable directory text for the Create/Open forms (Step 006).</summary>
    public string WorkspaceDirectory
    {
        get => _workspaceDirectory;
        set => SetField(ref _workspaceDirectory, value);
    }

    /// <summary>Bindable project name text for the Create form (Step 006).</summary>
    public string NewProjectName
    {
        get => _newProjectName;
        set => SetField(ref _newProjectName, value);
    }

    /// <summary>
    /// Set when a Create/Open action fails (invalid input, unsupported schema, stale state, etc.);
    /// never a silent failure — the real exception message is surfaced here.
    /// </summary>
    public string? ActionErrorMessage
    {
        get => _actionErrorMessage;
        private set => SetField(ref _actionErrorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    /// <summary>
    /// Creates a project at <see cref="WorkspaceDirectory"/> named <see cref="NewProjectName"/> via
    /// <paramref name="projectService"/>. Returns <see langword="true"/> on success; on failure,
    /// <see cref="ActionErrorMessage"/> is set with the real exception message and
    /// <see langword="false"/> is returned — never an unhandled exception out of a UI command.
    /// </summary>
    public async Task<bool> CreateProjectAsync(IProjectService projectService, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(projectService);

        // Reentrancy guard: this check-and-set happens synchronously (no await before it), so even
        // if the UI command is invoked twice before CanExecuteChanged disables it (e.g. a fast
        // double-click), only the first call proceeds. The second returns false immediately without
        // touching the store, instead of racing the first call's atomic-write temp file.
        if (_isBusy)
        {
            return false;
        }

        IsBusy = true;
        ActionErrorMessage = null;
        try
        {
            await projectService.CreateAsync(WorkspaceDirectory, NewProjectName, ct).ConfigureAwait(true);
            return true;
        }
        catch (Exception ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Opens the project at <see cref="WorkspaceDirectory"/> via <paramref name="projectService"/>.
    /// Same success/failure contract as <see cref="CreateProjectAsync"/>.
    /// </summary>
    public async Task<bool> OpenProjectAsync(IProjectService projectService, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(projectService);

        // Reentrancy guard: see CreateProjectAsync's identical guard above.
        if (_isBusy)
        {
            return false;
        }

        IsBusy = true;
        ActionErrorMessage = null;
        try
        {
            await projectService.OpenAsync(WorkspaceDirectory, ct).ConfigureAwait(true);
            return true;
        }
        catch (Exception ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Loads project names from <paramref name="projectNamesSource"/>. A thrown exception
    /// (e.g. missing/unreadable storage) is surfaced as an explicit Error state with a
    /// user-readable message rather than left silent or crashing the shell.
    /// </summary>
    public void Load(Func<IReadOnlyList<string>> projectNamesSource)
    {
        ArgumentNullException.ThrowIfNull(projectNamesSource);

        LoadState = ProjectsLoadState.Loading;
        ErrorMessage = null;

        try
        {
            var names = projectNamesSource();
            ProjectNames = names;
            LoadState = names.Count == 0 ? ProjectsLoadState.Empty : ProjectsLoadState.Loaded;
        }
        catch (Exception ex)
        {
            ProjectNames = Array.Empty<string>();
            ErrorMessage = ex.Message;
            LoadState = ProjectsLoadState.Error;
        }
    }
}
