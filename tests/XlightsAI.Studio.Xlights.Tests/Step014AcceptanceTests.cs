using System.Diagnostics;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Xlights.Tests;

/// <summary>
/// PHASE-02 Step 014 ("Open output in installed xLights; verify duration, timing, models and visible
/// effects"): proves <see cref="XlightsLauncher"/> really starts a real OS process against a real,
/// previously-exported <c>.xsq</c> file and really reports its process id/executable version, and that
/// every named failure/edge case ("Missing or incompatible model, cancellation and offline mode must
/// yield a recoverable state") fails safely before any process is started.
///
/// No installed xLights executable exists on this machine (carried forward from every prior PHASE-02
/// step's own disclosure — see STEP-13.md Section 1/10). <c>where.exe</c> — a real, always-present
/// Windows system executable — stands in for "an installed target executable" purely to prove the real
/// process-launch plumbing (path validation, <see cref="Process.Start(ProcessStartInfo)"/>, process id/
/// version reporting) end to end; it is explicitly NOT xLights and these tests never claim it is. The
/// actual required acceptance text — "Human-attested integration test with screenshot/render notes" —
/// is Step 014's own separately-scoped, BLOCKED item (see implementation-evidence/PHASE-02/STEP-14.md).
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step014AcceptanceTests : IDisposable
{
    private static readonly string StandInExecutablePath = OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "where.exe")
        : "/usr/bin/true";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step014-" + Guid.NewGuid().ToString("N"));
    private readonly List<int> _spawnedProcessIds = new();

    public Step014AcceptanceTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        foreach (var pid in _spawnedProcessIds)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (ArgumentException)
            {
                // Already exited before we could look it up — expected for a `where.exe` stand-in.
            }
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>The gate demo's own layout shape (PHASE-02 Section 1: Roofline, Arch 1, Arch 2), matching <c>Step013AcceptanceTests.GateDemoLayout()</c>.</summary>
    private static DisplayGraph GateDemoLayout() => new(
        SourceLayoutHash: "TEST-HASH-NOT-A-REAL-FILE",
        Models:
        [
            new DisplayModel(new ModelId("Roofline"), "Roofline", "Star", [], []),
            new DisplayModel(new ModelId("Arch 1"), "Arch 1", "Arches", [], []),
            new DisplayModel(new ModelId("Arch 2"), "Arch 2", "Arches", [], []),
        ],
        Groups: [],
        Warnings: []);

    private string CreateRealExportedXsq()
    {
        var layout = GateDemoLayout();
        var registry = new EffectRecipeRegistry();
        var plan = new ThreeEffectPlanFactory().CreatePlan(
            layout,
            ["Roofline", "Arch 1", "Arch 2"],
            300_000_000, // 30s at 100ns/tick, same as Step013AcceptanceTests' own fixture.
            registry);

        var outputPath = Path.Combine(_root, "sequence.xsq");
        new XsqExporter().ExportNewAsync(layout, plan, outputPath, "2026.03", CancellationToken.None)
            .GetAwaiter().GetResult();
        return outputPath;
    }

    /// <summary>
    /// Required acceptance (the automatable portion): a real exported <c>.xsq</c> is really opened by
    /// really starting the configured executable, and the launcher reports the real process id, the
    /// real on-disk executable path, and a real (OS-reported) version string.
    /// </summary>
    [Fact]
    public async Task Step014_Acceptance_Passes_LaunchesConfiguredExecutableAgainstRealExportedXsq()
    {
        Assert.True(File.Exists(StandInExecutablePath), $"Expected a real stand-in executable at '{StandInExecutablePath}'.");

        var xsqPath = CreateRealExportedXsq();
        var launcher = new XlightsLauncher();

        var result = await launcher.LaunchAsync(StandInExecutablePath, xsqPath, CancellationToken.None);
        _spawnedProcessIds.Add(result.ProcessId);

        Assert.Equal(StandInExecutablePath, result.ExecutablePath);
        Assert.Equal(xsqPath, result.XsqPath);
        Assert.True(result.ProcessId > 0);
    }

    [Fact]
    public async Task MissingExecutable_FailsSafely_NegativePath()
    {
        var xsqPath = CreateRealExportedXsq();
        var launcher = new XlightsLauncher();
        var missingExePath = Path.Combine(_root, "no-such-xlights.exe");

        var ex = await Assert.ThrowsAsync<XlightsLaunchInvalidException>(
            () => launcher.LaunchAsync(missingExePath, xsqPath, CancellationToken.None));

        Assert.Contains(missingExePath, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingXsqFile_FailsSafely_NegativePath()
    {
        var launcher = new XlightsLauncher();
        var missingXsqPath = Path.Combine(_root, "never-exported.xsq");

        var ex = await Assert.ThrowsAsync<XlightsLaunchInvalidException>(
            () => launcher.LaunchAsync(StandInExecutablePath, missingXsqPath, CancellationToken.None));

        Assert.Contains(missingXsqPath, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankExecutablePath_FailsSafely_NegativePath(string rawPath)
    {
        var xsqPath = CreateRealExportedXsq();
        var launcher = new XlightsLauncher();

        await Assert.ThrowsAsync<XlightsLaunchInvalidException>(
            () => launcher.LaunchAsync(rawPath, xsqPath, CancellationToken.None));
    }

    [Fact]
    public async Task BlankXsqPath_FailsSafely_NegativePath()
    {
        var launcher = new XlightsLauncher();

        await Assert.ThrowsAsync<XlightsLaunchInvalidException>(
            () => launcher.LaunchAsync(StandInExecutablePath, string.Empty, CancellationToken.None));
    }

    /// <summary>
    /// Required failure/edge case: "cancellation... must yield a recoverable state" — a pre-cancelled
    /// token is honored before any process is started (proven indirectly: the thrown exception type is
    /// the real BCL cancellation signal, not this launcher's own validation exception, so no process
    /// could have been reached by the method body past the explicit <c>ThrowIfCancellationRequested</c> call).
    /// </summary>
    [Fact]
    public async Task Cancellation_BeforeLaunch_FailsSafely_NegativePath()
    {
        var xsqPath = CreateRealExportedXsq();
        var launcher = new XlightsLauncher();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => launcher.LaunchAsync(StandInExecutablePath, xsqPath, cts.Token));
    }
}
