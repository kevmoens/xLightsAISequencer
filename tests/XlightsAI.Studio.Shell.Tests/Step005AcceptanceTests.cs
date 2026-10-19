using XlightsAI.Studio.Shell;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-01 Step 005: structured logging, crash handling, settings and diagnostics page.
/// Required acceptance: "Restart restores settings and an injected error is reported without
/// silent data loss." Failure/edge case: "Clean machine and unsupported runtime must produce
/// user-readable diagnostics."
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step005AcceptanceTests : IDisposable
{
    private readonly string _root;

    public Step005AcceptanceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xlai-step005-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Step005_Acceptance_Passes()
    {
        // --- First run: navigate, then "restart" ---
        var diagnostics1 = new AppDiagnostics(Path.Combine(_root, "logs"));
        var settingsStore1 = new SettingsStore(_root, diagnostics1);
        var navigator1 = new ShellNavigator();
        var shell1 = new ShellViewModel(navigator1, () => Array.Empty<string>(), settingsStore1, diagnostics1);

        shell1.RunStartup();
        // PHASE-15 Step 095 (disclosed, deliberate behavior change to this already-accepted test):
        // a fresh install with no persisted AppSettings.HasCompletedFirstRun now lands on the
        // first-run onboarding wizard before Welcome. Complete it explicitly (same as a real user
        // would) before exercising this test's own original "restart restores last screen" scope —
        // restoring a *pre*-first-run last screen across a restart would be meaningless anyway,
        // since first run itself must still be completed regardless of which screen was last shown.
        Assert.Equal(ShellScreen.FirstRun, shell1.CurrentScreen);
        shell1.FirstRun.WorkspaceDirectory = Path.Combine(_root, "Projects");
        shell1.CompleteFirstRunCommand.Execute(null);
        await shell1.LastCompleteFirstRunCommandTask!;
        Assert.Null(shell1.FirstRun.ErrorMessage);
        Assert.Equal(ShellScreen.Welcome, shell1.CurrentScreen);

        shell1.NavigateToSettingsCommand.Execute(null);
        Assert.Equal(ShellScreen.Settings, shell1.CurrentScreen);
        Assert.True(shell1.Settings.IsLoaded);

        // Inject a real failure (not a mock invocation assertion): a genuine exception recorded
        // through the public IAppDiagnostics surface.
        diagnostics1.RecordFailure("demo-injected-error", new InvalidOperationException("Disk nearly full"));

        // --- Simulate process restart: brand-new instances pointed at the same persisted root ---
        var diagnostics2 = new AppDiagnostics(Path.Combine(_root, "logs"));
        var settingsStore2 = new SettingsStore(_root, diagnostics2);
        var navigator2 = new ShellNavigator();
        var shell2 = new ShellViewModel(navigator2, () => Array.Empty<string>(), settingsStore2, diagnostics2);

        shell2.RunStartup();

        // Required acceptance: restart restores settings (last screen survives, not reset to Welcome).
        Assert.Equal(ShellScreen.Settings, shell2.CurrentScreen);

        // Required acceptance: the injected error is reported, not silently dropped, across restart.
        shell2.NavigateToDiagnosticsCommand.Execute(null);
        Assert.True(shell2.Diagnostics.IsLoaded);
        var recorded = Assert.Single(shell2.Diagnostics.Events, e => e.Code == "demo-injected-error");
        Assert.Equal(DiagnosticEventLevel.Error, recorded.Level);
        Assert.Equal("Disk nearly full", recorded.Message);
        Assert.False(string.IsNullOrWhiteSpace(recorded.CorrelationId));
    }

    [Fact]
    public void SettingsStore_Load_WhenFileMissing_CleanMachine_ReturnsDefaultsWithoutThrowing_NegativePath()
    {
        var diagnostics = new AppDiagnostics(Path.Combine(_root, "logs"));
        var store = new SettingsStore(Path.Combine(_root, "does-not-exist-yet"), diagnostics);

        var settings = store.Load();

        Assert.Equal(AppSettings.Default, settings);
        Assert.Empty(diagnostics.RecentEvents);
    }

    [Fact]
    public void SettingsStore_Load_WhenFileCorrupt_UnsupportedContent_ReportsDiagnosticAndFallsBack_NegativePath()
    {
        Directory.CreateDirectory(_root);
        var settingsPath = Path.Combine(_root, "settings.json");
        File.WriteAllText(settingsPath, "{ not valid json");

        var diagnostics = new AppDiagnostics(Path.Combine(_root, "logs"));
        var store = new SettingsStore(_root, diagnostics);

        var settings = store.Load();

        Assert.Equal(AppSettings.Default, settings);
        var failure = Assert.Single(diagnostics.RecentEvents);
        Assert.Equal("settings-load-failed", failure.Code);
        Assert.Equal(DiagnosticEventLevel.Error, failure.Level);
    }

    [Fact]
    public void SettingsStore_Load_WhenSchemaNewerThanSupported_UnsupportedRuntime_ReportsDiagnosticAndFallsBack_NegativePath()
    {
        Directory.CreateDirectory(_root);
        var settingsPath = Path.Combine(_root, "settings.json");
        File.WriteAllText(settingsPath, """{"SchemaVersion":999,"LastScreen":1}""");

        var diagnostics = new AppDiagnostics(Path.Combine(_root, "logs"));
        var store = new SettingsStore(_root, diagnostics);

        var settings = store.Load();

        Assert.Equal(AppSettings.Default, settings);
        var failure = Assert.Single(diagnostics.RecentEvents);
        Assert.Equal("settings-unsupported-schema", failure.Code);
    }

    [Fact]
    public void AppDiagnostics_RecordFailure_WhenLogDirectoryMissing_CleanMachine_CreatesItWithoutThrowing()
    {
        var logDirectory = Path.Combine(_root, "brand-new", "logs");
        Assert.False(Directory.Exists(logDirectory));

        var diagnostics = new AppDiagnostics(logDirectory);
        diagnostics.RecordFailure("clean-machine-test", new Exception("boom"));

        Assert.True(Directory.Exists(logDirectory));
        Assert.True(File.Exists(Path.Combine(logDirectory, "diagnostics.log")));
        Assert.Single(diagnostics.RecentEvents);
    }

    [Fact]
    public void AppDiagnostics_WhenPersistedLogWriteFails_StillKeepsEventInMemory_NegativePath()
    {
        // Simulate an I/O failure: the "log directory" path is actually a pre-existing file, so
        // Directory.CreateDirectory(...) inside AppDiagnostics must throw IOException, which
        // AppDiagnostics is required to swallow without losing the event itself.
        Directory.CreateDirectory(_root);
        var blockedPath = Path.Combine(_root, "logs-blocked");
        File.WriteAllText(blockedPath, "this is a file, not a directory");

        var diagnostics = new AppDiagnostics(blockedPath);

        var exception = Record.Exception(() =>
            diagnostics.RecordFailure("simulated-io-failure", new InvalidOperationException("can't persist")));

        Assert.Null(exception);
        var recorded = Assert.Single(diagnostics.RecentEvents);
        Assert.Equal("simulated-io-failure", recorded.Code);
        Assert.False(File.Exists(Path.Combine(blockedPath, "diagnostics.log")));
    }

    /// <summary>
    /// Code-review finding: <c>diagnostics.log</c> previously grew without bound. It must now be
    /// rotated (truncated to the most recent events) once it grows past the rotation threshold,
    /// rather than being re-read and re-grown forever.
    /// </summary>
    [Fact]
    public void AppDiagnostics_WhenPersistedLogExceedsRotationThreshold_TruncatesToRecentEvents()
    {
        var logDirectory = Path.Combine(_root, "logs");
        Directory.CreateDirectory(logDirectory);
        var logPath = Path.Combine(logDirectory, "diagnostics.log");

        // Pre-populate the persisted log past the rotation threshold with real, individually valid
        // DiagnosticEvent JSON lines (not synthetic junk), so rotation is exercised against the same
        // shape of data AppDiagnostics itself writes.
        var preExisting = new AppDiagnostics(logDirectory);
        for (var i = 0; i < 2001; i++)
        {
            preExisting.RecordEvent(DiagnosticEventLevel.Info, "pre-existing", $"event {i}");
        }

        var lineCountBeforeReload = File.ReadAllLines(logPath).Length;
        Assert.True(lineCountBeforeReload <= 200, "The persisted log must already have rotated during the pre-population loop itself.");

        // A fresh instance re-reading the (already-rotated) file must still only see the capped,
        // most-recent set of events -- rotation must not have lost or corrupted any surviving event.
        var reloaded = new AppDiagnostics(logDirectory);
        Assert.True(reloaded.RecentEvents.Count <= 200);
        Assert.Contains(reloaded.RecentEvents, e => e.Message == "event 2000");
        Assert.DoesNotContain(reloaded.RecentEvents, e => e.Message == "event 0");
    }

    [Fact]
    public void AppDiagnostics_RedactsUserProfilePathAndUserName_FromMessage()
    {
        var diagnostics = new AppDiagnostics(Path.Combine(_root, "logs"));
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userName = Environment.UserName;

        diagnostics.RecordFailure(
            "path-in-message",
            new Exception($"Could not read file at {home}\\secret-project for user {userName}"));

        var recorded = Assert.Single(diagnostics.RecentEvents);
        Assert.DoesNotContain(home, recorded.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(userName, recorded.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<home>", recorded.Message);
        Assert.Contains("<user>", recorded.Message);
    }

    [Fact]
    public void ShellViewModel_RunStartup_WhenStartupWorkThrows_RecordsDiagnosticFailure_NegativePath()
    {
        var diagnostics = new AppDiagnostics(Path.Combine(_root, "logs"));
        var settingsStore = new SettingsStore(_root, diagnostics);
        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(navigator, () => Array.Empty<string>(), settingsStore, diagnostics);

        shell.RunStartup(() => throw new InvalidOperationException("boot sensor failed"));

        Assert.True(shell.IsFailed);
        var recorded = Assert.Single(diagnostics.RecentEvents);
        Assert.Equal("startup-failed", recorded.Code);
        Assert.Equal("boot sensor failed", recorded.Message);
    }

    [Fact]
    public void DiagnosticsScreen_WithNoEvents_ShowsExplicitEmptyState_NotBlank()
    {
        var diagnostics = new AppDiagnostics(Path.Combine(_root, "logs"));
        var settingsStore = new SettingsStore(_root, diagnostics);
        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(navigator, () => Array.Empty<string>(), settingsStore, diagnostics);

        shell.RunStartup();
        shell.NavigateToDiagnosticsCommand.Execute(null);

        Assert.True(shell.Diagnostics.IsEmpty);
        Assert.False(shell.Diagnostics.IsError);
        Assert.False(shell.Diagnostics.IsLoading);
    }

    [Fact]
    public void SettingsScreen_WhenSourceThrows_ShowsExplicitErrorState_NegativePath()
    {
        var viewModel = new SettingsViewModel();

        viewModel.Load(() => throw new InvalidOperationException("settings source exploded"));

        Assert.True(viewModel.IsError);
        Assert.Equal("settings source exploded", viewModel.ErrorMessage);
        Assert.Null(viewModel.Settings);
    }

    [Fact]
    public void DiagnosticsScreen_WhenSourceThrows_ShowsExplicitErrorState_NegativePath()
    {
        var viewModel = new DiagnosticsViewModel();

        viewModel.Load(() => throw new InvalidOperationException("diagnostics source exploded"));

        Assert.True(viewModel.IsError);
        Assert.Equal("diagnostics source exploded", viewModel.ErrorMessage);
        Assert.Empty(viewModel.Events);
    }

    [Fact]
    public void SettingsStore_SaveThenLoad_RoundTripsExactly()
    {
        var diagnostics = new AppDiagnostics(Path.Combine(_root, "logs"));
        var store = new SettingsStore(_root, diagnostics);

        store.Save(new AppSettings(AppSettings.CurrentSchemaVersion, ShellScreen.Projects));
        var reloaded = store.Load();

        Assert.Equal(ShellScreen.Projects, reloaded.LastScreen);
        Assert.Empty(diagnostics.RecentEvents);
    }
}
