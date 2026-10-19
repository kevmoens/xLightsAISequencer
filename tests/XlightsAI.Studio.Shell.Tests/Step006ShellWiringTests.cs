using XlightsAI.Studio.Projects;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-01 Step 006: proves <see cref="ShellViewModel"/>'s new <see cref="ShellViewModel.CreateProjectCommand"/>/
/// <see cref="ShellViewModel.OpenProjectCommand"/> are wired end to end through a real
/// <see cref="ProjectService"/> (not a stand-in), and that Step 004/005's existing two/four-argument
/// <see cref="ShellViewModel"/> construction (no <see cref="IProjectService"/> supplied) keeps
/// behaving exactly as before — the "accepted migration plan" PHASE-01 requires before extending an
/// already-completed step's contract.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step006ShellWiringTests : IDisposable
{
    private readonly string _rootDirectory =
        Path.Combine(Path.GetTempPath(), "xlai-step006-shell-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Step006_Acceptance_Passes_CreateThenOpenThroughShellViewModel()
    {
        var appDirectory = Path.Combine(_rootDirectory, "app");
        var projectDirectory = Path.Combine(_rootDirectory, "Demo Song");
        var projectService = new ProjectService(new JsonProjectStore(), new RecentProjectsStore(appDirectory));

        // Same adapter App.axaml.cs uses in real composition: Projects.Load's Step 004 contract is
        // still a synchronous Func, so the real async RecentAsync is bridged via a blocking call.
        IReadOnlyList<string> ProjectNamesSource() =>
            projectService.RecentAsync(CancellationToken.None).GetAwaiter().GetResult()
                .Select(s => s.Name).ToArray();

        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, ProjectNamesSource, projectService: projectService);
        vm.RunStartup();
        vm.NavigateToProjectsCommand.Execute(null);

        vm.Projects.WorkspaceDirectory = projectDirectory;
        vm.Projects.NewProjectName = "Demo Song";

        Assert.True(vm.CreateProjectCommand.CanExecute(null));
        vm.CreateProjectCommand.Execute(null);
        await vm.LastProjectCommandTask!;

        Assert.Null(vm.Projects.ActionErrorMessage);
        Assert.True(File.Exists(Path.Combine(projectDirectory, JsonProjectStore.ManifestFileName)));
        // The screen's existing Step 004 "loaded list" contract reflects the real create: the
        // project the command just created through the real service is now visible.
        Assert.Contains("Demo Song", vm.Projects.ProjectNames);

        // --- Open a second project through the real service, same command wiring ---
        var secondDirectory = Path.Combine(_rootDirectory, "Second Song");
        await projectService.CreateAsync(secondDirectory, "Second Song", CancellationToken.None);

        vm.Projects.WorkspaceDirectory = secondDirectory;
        vm.OpenProjectCommand.Execute(null);
        await vm.LastProjectCommandTask!;

        Assert.Null(vm.Projects.ActionErrorMessage);
        Assert.Contains("Second Song", vm.Projects.ProjectNames);
    }

    [Fact]
    public async Task CreateProjectCommand_WhenDirectoryInvalid_SetsActionErrorMessage_NotSilent_NegativePath()
    {
        var appDirectory = Path.Combine(_rootDirectory, "app");
        var projectService = new ProjectService(new JsonProjectStore(), new RecentProjectsStore(appDirectory));
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>(), projectService: projectService);
        vm.RunStartup();
        vm.NavigateToProjectsCommand.Execute(null);

        vm.Projects.WorkspaceDirectory = string.Empty;
        vm.Projects.NewProjectName = "Whatever";

        vm.CreateProjectCommand.Execute(null);
        await vm.LastProjectCommandTask!;

        Assert.False(string.IsNullOrEmpty(vm.Projects.ActionErrorMessage));
    }

    [Fact]
    public void ShellViewModel_WithoutProjectService_StillConstructsAndStarts_BackwardCompatible()
    {
        // Step 004's original two-argument call site must keep compiling and behaving identically:
        // no IProjectService supplied defaults to NullProjectService, never a null-reference crash.
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => new[] { "Demo Song" });

        vm.RunStartup();

        Assert.Equal(StartupState.Ready, vm.State);
        Assert.True(vm.CreateProjectCommand.CanExecute(null));
    }

    [Fact]
    public async Task CreateProjectCommand_WithNoProjectServiceConfigured_ReportsErrorRatherThanThrowing_NegativePath()
    {
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>());
        vm.RunStartup();
        vm.NavigateToProjectsCommand.Execute(null);

        vm.Projects.WorkspaceDirectory = Path.Combine(_rootDirectory, "Unused");
        vm.Projects.NewProjectName = "Unused";

        vm.CreateProjectCommand.Execute(null);
        await vm.LastProjectCommandTask!;

        Assert.False(string.IsNullOrEmpty(vm.Projects.ActionErrorMessage));
    }

    /// <summary>
    /// Code-review finding (double-click reentrancy): invoking <see cref="ShellViewModel.CreateProjectCommand"/>
    /// a second time while the first invocation's <see cref="IProjectService.CreateAsync"/> call is
    /// still in flight must not start a second, concurrent call to the real service — the second
    /// invocation must return without calling the service at all. A deliberately slow, call-counting
    /// fake stands in for the real <see cref="ProjectService"/> so this is deterministic (a real
    /// filesystem call usually completes before a second synchronous `Execute(null)` could land, so a
    /// test against the real service would be flaky rather than reliably reproducing the race).
    /// </summary>
    [Fact]
    public async Task CreateProjectCommand_InvokedTwiceBeforeFirstCompletes_OnlyCallsServiceOnce_NegativePath()
    {
        var gate = new TaskCompletionSource();
        var slowService = new SlowCountingProjectService(gate.Task);
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>(), projectService: slowService);
        vm.RunStartup();
        vm.NavigateToProjectsCommand.Execute(null);

        vm.Projects.WorkspaceDirectory = Path.Combine(_rootDirectory, "Double Clicked");
        vm.Projects.NewProjectName = "Double Clicked";

        vm.CreateProjectCommand.Execute(null);
        var firstTask = vm.LastProjectCommandTask!;

        // Simulates a second click landing while the first is still in flight (the service call
        // has started but gate.Task has not been released yet).
        vm.CreateProjectCommand.Execute(null);
        var secondTask = vm.LastProjectCommandTask!;

        gate.SetResult();
        await firstTask;
        await secondTask;

        Assert.Equal(1, slowService.CreateCallCount);
    }

    private sealed class SlowCountingProjectService(Task gate) : IProjectService
    {
        public int CreateCallCount { get; private set; }

        public async Task<ProjectManifest> CreateAsync(string directory, string name, CancellationToken ct)
        {
            CreateCallCount++;
            await gate;
            return new ProjectManifest(new XlightsAI.Studio.Contracts.ProjectId(Guid.NewGuid()), 1, name, directory, DateTimeOffset.UtcNow);
        }

        public Task<ProjectManifest> OpenAsync(string directory, CancellationToken ct) =>
            throw new NotSupportedException("Not exercised by this test.");

        public Task SaveAsync(XlightsAI.Studio.Contracts.ProjectId id, CancellationToken ct) =>
            throw new NotSupportedException("Not exercised by this test.");

        public Task<IReadOnlyList<ProjectSummary>> RecentAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ProjectSummary>>(Array.Empty<ProjectSummary>());
    }
}
