using XlightsAI.Studio.Projects;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// UI cleanup item 1 (see docs/UI-CLEANUP-PLAN.md): <see cref="ShellViewModel.CurrentProject"/>.
///
/// Before this, <see cref="ProjectsViewModel.CreateProjectAsync"/>/<see cref="ProjectsViewModel.OpenProjectAsync"/>
/// discarded the <see cref="ProjectManifest"/> <see cref="IProjectService.CreateAsync"/>/
/// <see cref="IProjectService.OpenAsync"/> already return, so nothing in the shell could ever answer
/// "which project is open." These tests prove the manifest now survives end to end: into
/// <see cref="ShellViewModel.CurrentProject"/>, into <see cref="ShellViewModel.CloseProjectCommand"/>'s
/// gating, into <see cref="AppSettings.LastProjectRootDirectory"/> across a restart (restored as a
/// suggestion only, never auto-reopened), and into a default <see cref="ShowImportViewModel.XsqOutputPath"/>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CurrentProjectShellWiringTests : IDisposable
{
    private readonly string _rootDirectory =
        Path.Combine(Path.GetTempPath(), "xlai-current-project-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private ProjectService CreateProjectService(string appDirectory) =>
        new(new JsonProjectStore(), new RecentProjectsStore(appDirectory));

    [Fact]
    public async Task CreateProjectCommand_OnSuccess_SetsCurrentProject_AndEnablesCloseProjectCommand()
    {
        var projectService = CreateProjectService(Path.Combine(_rootDirectory, "app"));
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>(), projectService: projectService);
        vm.RunStartup();
        vm.NavigateToProjectsCommand.Execute(null);

        Assert.Null(vm.CurrentProject);
        Assert.False(vm.HasCurrentProject);
        Assert.False(vm.CloseProjectCommand.CanExecute(null));

        var projectDirectory = Path.Combine(_rootDirectory, "Demo Song");
        vm.Projects.WorkspaceDirectory = projectDirectory;
        vm.Projects.NewProjectName = "Demo Song";
        vm.CreateProjectCommand.Execute(null);
        await vm.LastProjectCommandTask!;

        Assert.NotNull(vm.CurrentProject);
        Assert.Equal("Demo Song", vm.CurrentProject!.Name);
        Assert.Equal(Path.GetFullPath(projectDirectory), vm.CurrentProject.RootDirectory);
        Assert.True(vm.HasCurrentProject);
        Assert.True(vm.CloseProjectCommand.CanExecute(null));
    }

    [Fact]
    public async Task OpenProjectCommand_OnSuccess_SetsCurrentProject()
    {
        var appDirectory = Path.Combine(_rootDirectory, "app");
        var projectService = CreateProjectService(appDirectory);
        var secondDirectory = Path.Combine(_rootDirectory, "Second Song");
        await projectService.CreateAsync(secondDirectory, "Second Song", CancellationToken.None);

        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>(), projectService: projectService);
        vm.RunStartup();
        vm.NavigateToProjectsCommand.Execute(null);

        vm.Projects.WorkspaceDirectory = secondDirectory;
        vm.OpenProjectCommand.Execute(null);
        await vm.LastProjectCommandTask!;

        Assert.NotNull(vm.CurrentProject);
        Assert.Equal("Second Song", vm.CurrentProject!.Name);
    }

    [Fact]
    public async Task CreateProjectCommand_WhenDirectoryInvalid_DoesNotSetCurrentProject_NegativePath()
    {
        var projectService = CreateProjectService(Path.Combine(_rootDirectory, "app"));
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>(), projectService: projectService);
        vm.RunStartup();
        vm.NavigateToProjectsCommand.Execute(null);

        vm.Projects.WorkspaceDirectory = string.Empty;
        vm.Projects.NewProjectName = "Whatever";
        vm.CreateProjectCommand.Execute(null);
        await vm.LastProjectCommandTask!;

        Assert.False(string.IsNullOrEmpty(vm.Projects.ActionErrorMessage));
        Assert.Null(vm.CurrentProject);
        Assert.False(vm.HasCurrentProject);
    }

    [Fact]
    public async Task CloseProjectCommand_ClearsCurrentProject_ButNeverTouchesDiskOrPersistedDirectory()
    {
        var appDirectory = Path.Combine(_rootDirectory, "app");
        var projectService = CreateProjectService(appDirectory);
        var settingsStore = new SettingsStore(_rootDirectory, new AppDiagnostics(Path.Combine(_rootDirectory, "logs")));
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>(), settingsStore, projectService: projectService);
        vm.RunStartup();
        vm.FirstRun.WorkspaceDirectory = Path.Combine(_rootDirectory, "Workspace");
        vm.CompleteFirstRunCommand.Execute(null);
        await vm.LastCompleteFirstRunCommandTask!;
        vm.NavigateToProjectsCommand.Execute(null);

        var projectDirectory = Path.Combine(_rootDirectory, "Demo Song");
        vm.Projects.WorkspaceDirectory = projectDirectory;
        vm.Projects.NewProjectName = "Demo Song";
        vm.CreateProjectCommand.Execute(null);
        await vm.LastProjectCommandTask!;
        Assert.True(vm.HasCurrentProject);

        var manifestPath = Path.Combine(Path.GetFullPath(projectDirectory), JsonProjectStore.ManifestFileName);
        Assert.True(File.Exists(manifestPath));
        var manifestBytesBeforeClose = File.ReadAllBytes(manifestPath);

        vm.CloseProjectCommand.Execute(null);

        Assert.Null(vm.CurrentProject);
        Assert.False(vm.HasCurrentProject);
        Assert.False(vm.CloseProjectCommand.CanExecute(null));
        // Closing is session-local: the manifest on disk, and the persisted "last project" directory
        // the Projects screen will offer back next time, are both untouched by Close.
        Assert.Equal(manifestBytesBeforeClose, File.ReadAllBytes(manifestPath));
        Assert.Equal(Path.GetFullPath(projectDirectory), settingsStore.Load().LastProjectRootDirectory);
    }

    [Fact]
    public async Task LastProjectRootDirectory_SurvivesRestart_AsASuggestion_NotAnAutomaticReopen()
    {
        var appDirectory = Path.Combine(_rootDirectory, "app");
        var projectService = CreateProjectService(appDirectory);
        var projectDirectory = Path.Combine(_rootDirectory, "Demo Song");

        // --- First run: create a project, then "restart" ---
        var diagnostics1 = new AppDiagnostics(Path.Combine(_rootDirectory, "logs"));
        var settingsStore1 = new SettingsStore(_rootDirectory, diagnostics1);
        var navigator1 = new ShellNavigator();
        var shell1 = new ShellViewModel(navigator1, () => Array.Empty<string>(), settingsStore1, diagnostics1, projectService);
        shell1.RunStartup();
        shell1.FirstRun.WorkspaceDirectory = Path.Combine(_rootDirectory, "Workspace");
        shell1.CompleteFirstRunCommand.Execute(null);
        await shell1.LastCompleteFirstRunCommandTask!;
        shell1.NavigateToProjectsCommand.Execute(null);
        shell1.Projects.WorkspaceDirectory = projectDirectory;
        shell1.Projects.NewProjectName = "Demo Song";
        shell1.CreateProjectCommand.Execute(null);
        await shell1.LastProjectCommandTask!;
        Assert.True(shell1.HasCurrentProject);

        // --- Simulate process restart: brand-new instances pointed at the same persisted root ---
        var diagnostics2 = new AppDiagnostics(Path.Combine(_rootDirectory, "logs"));
        var settingsStore2 = new SettingsStore(_rootDirectory, diagnostics2);
        var navigator2 = new ShellNavigator();
        var shell2 = new ShellViewModel(navigator2, () => Array.Empty<string>(), settingsStore2, diagnostics2, projectService);
        shell2.RunStartup();

        // No project is automatically reopened after a restart — CurrentProject starts fresh.
        Assert.Null(shell2.CurrentProject);
        Assert.False(shell2.HasCurrentProject);

        // But the directory is offered back the moment the Projects screen is shown, same
        // restore-the-text-never-the-action convention as ShowImport/FirstRun's restored paths.
        shell2.NavigateToProjectsCommand.Execute(null);
        Assert.Equal(Path.GetFullPath(projectDirectory), shell2.Projects.WorkspaceDirectory);
        Assert.Null(shell2.CurrentProject);
    }

    [Fact]
    public async Task OpeningAProject_DefaultsXsqOutputPath_IntoTheProjectsExportsFolder_OnlyWhenBlank()
    {
        var projectService = CreateProjectService(Path.Combine(_rootDirectory, "app"));
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>(), projectService: projectService);
        vm.RunStartup();
        vm.NavigateToProjectsCommand.Execute(null);

        var projectDirectory = Path.Combine(_rootDirectory, "Demo Song");
        vm.Projects.WorkspaceDirectory = projectDirectory;
        vm.Projects.NewProjectName = "Demo Song";
        vm.CreateProjectCommand.Execute(null);
        await vm.LastProjectCommandTask!;

        Assert.Equal(
            Path.Combine(Path.GetFullPath(projectDirectory), "exports", "sequence.xsq"),
            vm.ShowImport.XsqOutputPath);
    }

    [Fact]
    public async Task OpeningAProject_NeverOverwritesAnAlreadyTypedXsqOutputPath()
    {
        var appDirectory = Path.Combine(_rootDirectory, "app");
        var projectService = CreateProjectService(appDirectory);
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>(), projectService: projectService);
        vm.RunStartup();
        vm.NavigateToProjectsCommand.Execute(null);

        vm.ShowImport.XsqOutputPath = @"C:\already\typed\output.xsq";

        var projectDirectory = Path.Combine(_rootDirectory, "Demo Song");
        vm.Projects.WorkspaceDirectory = projectDirectory;
        vm.Projects.NewProjectName = "Demo Song";
        vm.CreateProjectCommand.Execute(null);
        await vm.LastProjectCommandTask!;

        Assert.Equal(@"C:\already\typed\output.xsq", vm.ShowImport.XsqOutputPath);
    }
}
