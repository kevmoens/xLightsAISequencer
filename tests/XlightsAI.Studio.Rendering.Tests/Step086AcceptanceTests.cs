using System.Diagnostics;

namespace XlightsAI.Studio.Rendering.Tests;

/// <summary>
/// PHASE-13 Step 086 ("Probe installed xLights capabilities and implement version-aware render adapter"):
/// proves <see cref="XlightsRenderCapabilityProbe"/> and <see cref="XlightsRenderAdapter"/> really start a
/// real OS process, really parse its real captured output/exit code/on-disk artifact, and that every named
/// failure/edge case fails safely.
///
/// These tests must run identically whether or not a real installed xLights executable happens to be
/// present (originally written when none was -- PHASE-02/STEP-14.md, PHASE-13/STEP-085.md -- but Phase 0's
/// de-risk spike later found a real install at <c>C:\Program Files\xLights</c> on this machine).
/// <c>tools/FakeXlightsHost</c> -- a real, separately-compiled .NET console apphost built as part of this
/// repository -- stands in for "an installed target xLights executable" purely to prove the real
/// process-launch plumbing (argument list, stdout/stderr capture, timeout/kill, exit code, on-disk .fseq
/// artifact) end to end, deterministically, everywhere (CI included); it is explicitly NOT xLights and
/// these tests never claim it is. This mirrors Step 014's own disclosed precedent of standing in a real
/// executable (there, the OS's own <c>where.exe</c>) rather than mocking the process boundary.
/// <see cref="RealXlightsRenderCapabilityProbeTests"/> separately corroborates this probe's behavior
/// against the real install when present.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step086AcceptanceTests : IDisposable
{
    private static readonly string FakeXlightsHostPath = ResolveFakeXlightsHostPath();

    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step086-" + Guid.NewGuid().ToString("N"));
    private readonly string _showDirectory;
    private readonly List<int> _spawnedProcessIds = new();

    public Step086AcceptanceTests()
    {
        Directory.CreateDirectory(_root);
        _showDirectory = Path.Combine(_root, "show");
        Directory.CreateDirectory(_showDirectory);
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
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (ArgumentException)
            {
                // Already exited before we could look it up -- expected for the success/failure fixtures.
            }
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string ResolveFakeXlightsHostPath()
    {
        var configuration = AppContext.BaseDirectory.Contains(
            $"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";

        var path = Path.Combine(RepoRoot(), "tools", "FakeXlightsHost", "bin", configuration, "net10.0", "FakeXlightsHost.exe");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Expected the FakeXlightsHost stand-in executable at '{path}' -- build it first " +
                "('dotnet build tools/FakeXlightsHost/FakeXlightsHost.csproj') before running these PHASE-13 " +
                "Step 086 tests.",
                path);
        }

        return path;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "XlightsAI.Studio.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root (XlightsAI.Studio.slnx) from the test base directory.");
    }

    /// <summary>
    /// A real, non-empty "exported .xsq" fixture. Its exact XML shape is irrelevant to these tests (the
    /// fake stand-in never parses it) -- what matters is that it is a real file at a real path with real,
    /// hashable bytes, and that its file name stem drives the stand-in's deterministic behavior (see class
    /// remarks).
    /// </summary>
    private string CreateExportedXsq(string stem)
    {
        var path = Path.Combine(_root, stem + ".xsq");
        File.WriteAllText(path, "<xsequence><!-- PHASE-13 Step 086 test fixture, not a real xLights export --></xsequence>");
        return path;
    }

    // ---- Probe: required acceptance ("Supported build renders fixture") ----

    [Fact]
    public async Task Step086_Acceptance_Passes()
    {
        var probe = new XlightsRenderCapabilityProbe();
        var capability = await probe.ProbeAsync(FakeXlightsHostPath, CancellationToken.None);

        Assert.True(capability.SupportsHeadless);
        Assert.True(capability.SupportsOutputDirectory);
        Assert.Equal(FakeXlightsHostPath, capability.ExecutablePath);
        Assert.False(string.IsNullOrWhiteSpace(capability.XlightsVersion));

        var adapter = new XlightsRenderAdapter(probe, FakeXlightsHostPath);
        var xsqPath = CreateExportedXsq("fixture-success");
        var outputDirectory = Path.Combine(_root, "output");

        var artifact = await adapter.RenderAsync(
            new RenderRequest(Guid.NewGuid(), _showDirectory, xsqPath, outputDirectory, Selection: null, RenderAuthority.ActualXlights),
            CancellationToken.None);

        Assert.True(artifact.Success, string.Join("; ", artifact.Warnings));
        Assert.Equal(RenderAuthority.ActualXlights, artifact.Authority);
        Assert.NotNull(artifact.FseqPath);
        Assert.True(File.Exists(artifact.FseqPath));
        Assert.StartsWith(outputDirectory, artifact.FseqPath!, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(artifact.RendererVersion));
        Assert.Equal(64, artifact.EvidenceHash.Length);
        Assert.NotEqual(Guid.Empty, artifact.Id);
    }

    /// <summary>
    /// Phase 0 finding (docs/PHASE0-FINDINGS.md section 4): the original probe sniffed <c>--help</c> text,
    /// which false-negatived against the real installed xLights 2024.13 (launches the GUI, never prints
    /// usage). The probe now attempts a real trivial headless render instead; this test drives
    /// FakeXlightsHost's own <c>-fail-</c> stem convention to simulate a build whose real render attempt
    /// fails, the same way a build that does not actually support <c>--headless</c> would.
    /// </summary>
    [Fact]
    public async Task Probe_RealRenderAttemptFails_ReportsUnsupported()
    {
        var probe = new XlightsRenderCapabilityProbe(probeSequenceStem: "fixture-fail-case");
        var capability = await probe.ProbeAsync(FakeXlightsHostPath, CancellationToken.None);

        Assert.False(capability.SupportsHeadless);
        Assert.False(capability.SupportsOutputDirectory);
    }

    [Fact]
    public async Task UnsupportedBuild_FallsBackWithoutLaunchingRenderProcess_NegativePath()
    {
        var limitedProbe = new XlightsRenderCapabilityProbe(probeSequenceStem: "fixture-fail-case");
        var adapter = new XlightsRenderAdapter(limitedProbe, FakeXlightsHostPath);
        var xsqPath = CreateExportedXsq("fixture-success");
        var outputDirectory = Path.Combine(_root, "output");

        var artifact = await adapter.RenderAsync(
            new RenderRequest(Guid.NewGuid(), _showDirectory, xsqPath, outputDirectory, Selection: null, RenderAuthority.ActualXlights),
            CancellationToken.None);

        Assert.False(artifact.Success);
        Assert.Null(artifact.FseqPath);
        Assert.Contains(artifact.Warnings, w => w.Contains("did not report supporting '--headless'", StringComparison.Ordinal));
        Assert.Contains(artifact.Warnings, w => w.Contains("open", StringComparison.OrdinalIgnoreCase) && w.Contains("Render All", StringComparison.Ordinal));

        // No render process was ever started for the unsupported build: no .fseq exists anywhere.
        Assert.False(File.Exists(Path.Combine(outputDirectory, "fixture-success.fseq")));
        Assert.Equal(64, artifact.EvidenceHash.Length);
    }

    [Fact]
    public async Task BadRenderExitsNonZero_FailsHardGate_NegativePath()
    {
        var probe = new XlightsRenderCapabilityProbe();
        var adapter = new XlightsRenderAdapter(probe, FakeXlightsHostPath);
        var xsqPath = CreateExportedXsq("fixture-fail-case");
        var outputDirectory = Path.Combine(_root, "output");

        var artifact = await adapter.RenderAsync(
            new RenderRequest(Guid.NewGuid(), _showDirectory, xsqPath, outputDirectory, Selection: null, RenderAuthority.ActualXlights),
            CancellationToken.None);

        Assert.False(artifact.Success);
        Assert.Null(artifact.FseqPath);
        Assert.Contains(artifact.Warnings, w => w.Contains("exited with code 3", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(outputDirectory, "fixture-fail-case.fseq")));
    }

    [Fact]
    public async Task RenderProcessTimeout_KillsChildCleanly_NegativePath()
    {
        var probe = new XlightsRenderCapabilityProbe();
        var adapter = new XlightsRenderAdapter(probe, FakeXlightsHostPath, timeout: TimeSpan.FromMilliseconds(500));
        var xsqPath = CreateExportedXsq("fixture-hang-case");
        var outputDirectory = Path.Combine(_root, "output");

        var stopwatch = Stopwatch.StartNew();
        var artifact = await adapter.RenderAsync(
            new RenderRequest(Guid.NewGuid(), _showDirectory, xsqPath, outputDirectory, Selection: null, RenderAuthority.ActualXlights),
            CancellationToken.None);
        stopwatch.Stop();

        Assert.False(artifact.Success);
        Assert.Null(artifact.FseqPath);
        Assert.Contains(artifact.Warnings, w => w.Contains("timeout", StringComparison.OrdinalIgnoreCase));

        // The fixture sleeps for 5 minutes; completing in well under that proves the child was actually
        // killed rather than the test simply waiting the hang out.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"Expected the timed-out render to return quickly; took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task Probe_Timeout_FallsBackGracefully_NegativePath()
    {
        var probe = new XlightsRenderCapabilityProbe(timeout: TimeSpan.FromMilliseconds(500), probeSequenceStem: "fixture-hang-case");

        var stopwatch = Stopwatch.StartNew();
        var capability = await probe.ProbeAsync(FakeXlightsHostPath, CancellationToken.None);
        stopwatch.Stop();

        Assert.False(capability.SupportsHeadless);
        Assert.False(capability.SupportsOutputDirectory);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"Expected the timed-out probe to return quickly; took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task Probe_MissingExecutable_FailsSafely_NegativePath()
    {
        var probe = new XlightsRenderCapabilityProbe();
        var missingPath = Path.Combine(_root, "no-such-xlights.exe");

        var ex = await Assert.ThrowsAsync<RenderCapabilityProbeInvalidException>(
            () => probe.ProbeAsync(missingPath, CancellationToken.None));

        Assert.Contains(missingPath, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingExportedXsq_FailsSafely_NegativePath()
    {
        var probe = new XlightsRenderCapabilityProbe();
        var adapter = new XlightsRenderAdapter(probe, FakeXlightsHostPath);
        var missingXsqPath = Path.Combine(_root, "never-exported.xsq");
        var outputDirectory = Path.Combine(_root, "output");

        var ex = await Assert.ThrowsAsync<RenderRequestInvalidException>(() => adapter.RenderAsync(
            new RenderRequest(Guid.NewGuid(), _showDirectory, missingXsqPath, outputDirectory, Selection: null, RenderAuthority.ActualXlights),
            CancellationToken.None));

        Assert.Contains(missingXsqPath, ex.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(outputDirectory), "No output directory should be created for a request rejected before any render was attempted.");
    }

    [Fact]
    public async Task MissingShowDirectory_FailsSafely_NegativePath()
    {
        var probe = new XlightsRenderCapabilityProbe();
        var adapter = new XlightsRenderAdapter(probe, FakeXlightsHostPath);
        var xsqPath = CreateExportedXsq("fixture-success");
        var missingShowDirectory = Path.Combine(_root, "no-such-show-dir");
        var outputDirectory = Path.Combine(_root, "output");

        await Assert.ThrowsAsync<RenderRequestInvalidException>(() => adapter.RenderAsync(
            new RenderRequest(Guid.NewGuid(), missingShowDirectory, xsqPath, outputDirectory, Selection: null, RenderAuthority.ActualXlights),
            CancellationToken.None));
    }

    [Fact]
    public async Task Cancellation_BeforeProbe_FailsSafely_NegativePath()
    {
        var probe = new XlightsRenderCapabilityProbe();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => probe.ProbeAsync(FakeXlightsHostPath, cts.Token));
    }

    [Fact]
    public async Task Selection_RequestedWithoutInventingACliFlag_RecordedAsWarningOnly()
    {
        var probe = new XlightsRenderCapabilityProbe();
        var adapter = new XlightsRenderAdapter(probe, FakeXlightsHostPath);
        var xsqPath = CreateExportedXsq("fixture-success-scoped");
        var outputDirectory = Path.Combine(_root, "output");

        var artifact = await adapter.RenderAsync(
            new RenderRequest(
                Guid.NewGuid(),
                _showDirectory,
                xsqPath,
                outputDirectory,
                Selection: new Contracts.TimeWindow(0, 10_000_000),
                RenderAuthority.ActualXlights),
            CancellationToken.None);

        Assert.True(artifact.Success, string.Join("; ", artifact.Warnings));
        Assert.Contains(artifact.Warnings, w => w.Contains("no verified upstream xLights CLI flag", StringComparison.Ordinal));
    }
}
