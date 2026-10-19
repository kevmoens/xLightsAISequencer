using XlightsAI.Studio.Distribution;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-15 Step 095: domain-level tests of <see cref="FirstRunViewModel"/> in isolation from
/// <see cref="ShellViewModel"/> wiring (covered separately by <c>Step095ShellWiringTests</c>),
/// against the real Step 094 <see cref="RuntimeCapabilityDetector"/>.
/// </summary>
public sealed class Step095FirstRunViewModelTests : IDisposable
{
    private readonly string _rootDirectory =
        Path.Combine(Path.GetTempPath(), "xlai-step095-firstrun-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CompleteFirstRunAsync_CreatesRealWorkspaceDirectory_AndProbesRealCapability()
    {
        var vm = new FirstRunViewModel { WorkspaceDirectory = Path.Combine(_rootDirectory, "Nested", "Projects") };

        var succeeded = await vm.CompleteFirstRunAsync(new RuntimeCapabilityDetector(), NullStartupHealthChecks.Instance);

        Assert.True(succeeded);
        Assert.True(vm.IsCompleted);
        Assert.Null(vm.ErrorMessage);
        Assert.True(Directory.Exists(vm.WorkspaceDirectory));
        Assert.NotNull(vm.ProbedCapability);
        Assert.Empty(vm.HealthIssues);
        Assert.False(vm.HasHealthIssues);
    }

    [Fact]
    public async Task CompleteFirstRunAsync_WithCancelledToken_SetsErrorMessage_NeverThrows_NegativePath()
    {
        var vm = new FirstRunViewModel { WorkspaceDirectory = Path.Combine(_rootDirectory, "Projects") };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var succeeded = await vm.CompleteFirstRunAsync(
            new RuntimeCapabilityDetector(), NullStartupHealthChecks.Instance, cts.Token);

        Assert.False(succeeded);
        Assert.False(vm.IsCompleted);
        Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
    }

    [Fact]
    public void RestoreLastWorkspaceDirectory_DoesNotOverwriteAnAlreadyTypedValue()
    {
        var vm = new FirstRunViewModel { WorkspaceDirectory = "C:\\already-typed" };

        vm.RestoreLastWorkspaceDirectory("C:\\persisted");

        Assert.Equal("C:\\already-typed", vm.WorkspaceDirectory);
    }

    [Fact]
    public void RestoreLastWorkspaceDirectory_RestoresIntoEmptyField()
    {
        var vm = new FirstRunViewModel();

        vm.RestoreLastWorkspaceDirectory("C:\\persisted");

        Assert.Equal("C:\\persisted", vm.WorkspaceDirectory);
    }

    [Fact]
    public async Task CompleteFirstRunAsync_NullCollaborators_Throw()
    {
        var vm = new FirstRunViewModel { WorkspaceDirectory = Path.Combine(_rootDirectory, "Projects") };

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => vm.CompleteFirstRunAsync(null!, NullStartupHealthChecks.Instance));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => vm.CompleteFirstRunAsync(new RuntimeCapabilityDetector(), null!));
    }
}
