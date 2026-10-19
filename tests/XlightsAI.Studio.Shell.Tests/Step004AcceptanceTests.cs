namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// Acceptance tests for PHASE-01 Step 004: "Create .NET 10 Avalonia solution with basic
/// navigation and no Python runtime." Required acceptance ("Launch Windows desktop executable
/// on a clean build") is proven by the manual/desktop demo recorded in the step evidence, not by
/// these tests; these tests prove the framework-agnostic navigation/startup state machine and
/// the one shipped project feature (Projects screen explicit load states) that the Avalonia
/// shell binds to, including the step's failure/edge-case requirement that invalid states and
/// missing data are handled explicitly rather than silently.
/// </summary>
[Trait("Category", "Unit")]
public class Step004AcceptanceTests
{
    [Fact]
    public void Step004_Acceptance_Passes()
    {
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => new[] { "Demo Song" });

        // Observable startup state before the app is usable.
        Assert.Equal(StartupState.Starting, vm.State);
        Assert.False(vm.NavigateToProjectsCommand.CanExecute(null));

        vm.RunStartup();

        Assert.Equal(StartupState.Ready, vm.State);
        Assert.Null(vm.Failure);
        // PHASE-15 Step 095 (disclosed, deliberate behavior change to this already-accepted test):
        // a fresh install with no persisted AppSettings.HasCompletedFirstRun now lands on the
        // first-run onboarding wizard before Welcome — Step 095's own required acceptance, "User
        // completes first run without command line." Navigate away explicitly, same as a real user
        // would, before exercising this test's own original Projects-navigation scope.
        Assert.Equal(ShellScreen.FirstRun, vm.CurrentScreen);
        vm.NavigateToWelcomeCommand.Execute(null);
        Assert.Equal(ShellScreen.Welcome, vm.CurrentScreen);
        Assert.True(vm.NavigateToProjectsCommand.CanExecute(null));

        vm.NavigateToProjectsCommand.Execute(null);

        Assert.Equal(ShellScreen.Projects, vm.CurrentScreen);
        Assert.Equal(ProjectsLoadState.Loaded, vm.Projects.LoadState);
        Assert.Equal(new[] { "Demo Song" }, vm.Projects.ProjectNames);

        vm.NavigateToWelcomeCommand.Execute(null);
        Assert.Equal(ShellScreen.Welcome, vm.CurrentScreen);
    }

    [Fact]
    public void ProjectsScreen_WithNoProjects_ShowsExplicitEmptyState_NotBlank()
    {
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>());

        vm.RunStartup();
        vm.NavigateToProjectsCommand.Execute(null);

        Assert.Equal(ProjectsLoadState.Empty, vm.Projects.LoadState);
        Assert.Empty(vm.Projects.ProjectNames);
    }

    [Fact]
    public void ProjectsScreen_WhenSourceThrows_ShowsExplicitErrorState_NegativePath()
    {
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => throw new InvalidOperationException("storage unavailable"));

        vm.RunStartup();
        vm.NavigateToProjectsCommand.Execute(null);

        Assert.Equal(ProjectsLoadState.Error, vm.Projects.LoadState);
        Assert.Equal("storage unavailable", vm.Projects.ErrorMessage);
        Assert.Empty(vm.Projects.ProjectNames);
    }

    [Fact]
    public void NavigateTo_BeforeStartupCompletes_ThrowsRatherThanSilentlyNoOp_NegativePath()
    {
        var navigator = new ShellNavigator();

        var ex = Assert.Throws<InvalidOperationException>(() => navigator.NavigateTo(ShellScreen.Projects));
        Assert.Contains("Starting", ex.Message);
        Assert.Equal(ShellScreen.Welcome, navigator.CurrentScreen);
    }

    [Fact]
    public void RunStartup_WhenStartupWorkThrows_TransitionsToFailed_NotCrash_NegativePath()
    {
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>());

        vm.RunStartup(() => throw new IOException("workspace directory missing"));

        Assert.Equal(StartupState.Failed, vm.State);
        Assert.NotNull(vm.Failure);
        Assert.Equal("startup-failed", vm.Failure!.Code);
        Assert.Equal("workspace directory missing", vm.Failure.Message);
        Assert.False(vm.NavigateToProjectsCommand.CanExecute(null));
    }

    [Fact]
    public void NavigateTo_AfterStartupFailed_StillThrowsRatherThanSilentlyNoOp_NegativePath()
    {
        var navigator = new ShellNavigator();
        navigator.FailStartup("boot-error", "disk read error");

        var ex = Assert.Throws<InvalidOperationException>(() => navigator.NavigateTo(ShellScreen.Projects));
        Assert.Contains("Failed", ex.Message);
    }

    [Fact]
    public void CompleteStartup_CalledTwice_Throws_NegativePath()
    {
        var navigator = new ShellNavigator();
        navigator.CompleteStartup();

        Assert.Throws<InvalidOperationException>(() => navigator.CompleteStartup());
    }

    [Fact]
    public void FailStartup_RequiresNonEmptyCodeAndMessage_NegativePath()
    {
        var navigator = new ShellNavigator();

        Assert.Throws<ArgumentException>(() => navigator.FailStartup("", "message"));
        Assert.Throws<ArgumentException>(() => navigator.FailStartup("code", ""));
    }

    [Fact]
    public void ShellNavigator_Changed_FiresOnEveryStateAndScreenTransition()
    {
        var navigator = new ShellNavigator();
        var raiseCount = 0;
        navigator.Changed += (_, _) => raiseCount++;

        navigator.CompleteStartup();
        navigator.NavigateTo(ShellScreen.Projects);
        navigator.NavigateTo(ShellScreen.Projects); // same screen: no-op, no extra event

        Assert.Equal(2, raiseCount);
    }
}
