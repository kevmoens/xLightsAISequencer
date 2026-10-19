using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Distribution;
using XlightsAI.Studio.Reference.Fixtures;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Release.Tests;

/// <summary>
/// PHASE-16 Step 098 — "Run tested OS/xLights/audio/layout compatibility matrix and record known
/// limitations." Required acceptance: "Supported combinations each have installation and XSQ smoke
/// evidence." Failure/edge case: "Invalid model/format/version must fail safely without rewriting
/// source files." Every test below exercises the real <see cref="CompatibilityMatrixRunner"/> against
/// real fixture directories/files on a fresh per-test temp directory, the real
/// <see cref="RuntimeCapabilityDetector"/> (PHASE-15's own production type, unmodified) for host
/// detection, and the real, already-accepted <see cref="ShowLayoutImporter"/>/<see cref="WavAudioDecoder"/>/
/// <see cref="ThreeEffectPlanFactory"/>/<see cref="XsqExporter"/> pipeline — no mocking framework
/// anywhere in this file.
/// </summary>
public sealed class Step098AcceptanceTests : IDisposable
{
    private readonly string _artifactDirectory =
        Path.Combine(Path.GetTempPath(), "xlai-step098-" + Guid.NewGuid().ToString("N"));

    private static string FixtureDirectory(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    public void Dispose()
    {
        if (Directory.Exists(_artifactDirectory))
        {
            Directory.Delete(_artifactDirectory, recursive: true);
        }
    }

    private static CompatibilityMatrixRunner BuildRunner(ICompatibilityFixtureCatalog catalog, string artifactDirectory) =>
        new(
            new RuntimeCapabilityDetector(),
            catalog,
            new ShowLayoutImporter(),
            new WavAudioDecoder(),
            new ThreeEffectPlanFactory(),
            new EffectRecipeRegistry(),
            new XsqExporter(),
            artifactDirectory);

    private static string WriteRealWavFixture(string directory, string fileName)
    {
        const int sampleRate = 8000;
        var samples = DeterministicSyntheticAudioGenerator.BuildNoteSequence([440.0, 523.25, 659.25], 0.25, sampleRate);
        var bytes = DeterministicSyntheticAudioGenerator.EncodeWav(samples, sampleRate);
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public async Task Step098_Acceptance_Passes_SupportedCombinationsHaveInstallationAndXsqSmokeEvidence()
    {
        Directory.CreateDirectory(_artifactDirectory);
        var audioPath = WriteRealWavFixture(_artifactDirectory, "notes.wav");

        var catalog = new DictionaryCompatibilityFixtureCatalog(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["layout-a"] = FixtureDirectory("compat-show-a"),
                ["layout-b"] = FixtureDirectory("compat-show-b"),
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["audio-notes"] = audioPath,
            });

        // The real host's own actual capability (RuntimeCapabilityDetector, unmodified from PHASE-15)
        // decides which Os/Architecture combination this process can actually run -- never hard-coded,
        // so this test proves the real thing against whichever real machine executes it.
        var hostCapability = await new RuntimeCapabilityDetector().ProbeAsync(CancellationToken.None);

        var supportedCase = new CompatibilityCase(
            hostCapability.OperatingSystem, hostCapability.Architecture, "2024.14", "layout-a", "audio-notes");
        var unsupportedOsCase = new CompatibilityCase(
            "Some Other OS This Host Is Not", hostCapability.Architecture, "2024.14", "layout-b", "audio-notes");

        var runner = BuildRunner(catalog, _artifactDirectory);
        var evidence = await runner.RunAsync([supportedCase, unsupportedOsCase], CancellationToken.None);

        Assert.Equal(2, evidence.Count);

        var supportedEvidence = evidence[0];
        Assert.True(supportedEvidence.Passed);
        Assert.False(string.IsNullOrWhiteSpace(supportedEvidence.ArtifactPath));
        Assert.True(File.Exists(supportedEvidence.ArtifactPath)); // real, persisted XSQ artifact, not a mocked invocation.
        Assert.Equal("2024.14", supportedEvidence.ToolVersion);
        Assert.Contains("sha256=", supportedEvidence.Detail);

        // Independently confirm the persisted XSQ artifact is real, well-formed xLights XML naming the
        // real layout targets -- not merely that ExportAsync returned without throwing.
        var exportedXml = await File.ReadAllTextAsync(supportedEvidence.ArtifactPath);
        Assert.Contains("Arch-01", exportedXml);
        Assert.Contains("MegaTree-Center", exportedXml);
        Assert.Contains("Front-Yard", exportedXml);

        var unsupportedEvidence = evidence[1];
        Assert.False(unsupportedEvidence.Passed);
        Assert.Equal(string.Empty, unsupportedEvidence.ArtifactPath);
        Assert.Contains("Cannot install/run", unsupportedEvidence.Detail);
    }

    [Fact]
    public async Task Step098_NegativePath_UnknownLayoutFixtureId_IsFlaggedNotThrown_NoFileWritten()
    {
        Directory.CreateDirectory(_artifactDirectory);
        var audioPath = WriteRealWavFixture(_artifactDirectory, "notes.wav");
        var catalog = new DictionaryCompatibilityFixtureCatalog(
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["audio-notes"] = audioPath });

        var hostCapability = await new RuntimeCapabilityDetector().ProbeAsync(CancellationToken.None);
        var invalidCase = new CompatibilityCase(
            hostCapability.OperatingSystem, hostCapability.Architecture, "2024.14", "no-such-layout", "audio-notes");

        var runner = BuildRunner(catalog, _artifactDirectory);
        var evidence = await runner.RunAsync([invalidCase], CancellationToken.None);

        var result = Assert.Single(evidence);
        Assert.False(result.Passed);
        Assert.Equal(string.Empty, result.ArtifactPath);
        Assert.Contains("no-such-layout", result.Detail);
        Assert.Empty(Directory.GetFiles(_artifactDirectory, "*.xsq"));
    }

    [Fact]
    public async Task Step098_NegativePath_MalformedLayout_IsFlaggedNotThrown()
    {
        Directory.CreateDirectory(_artifactDirectory);
        var audioPath = WriteRealWavFixture(_artifactDirectory, "notes.wav");
        var catalog = new DictionaryCompatibilityFixtureCatalog(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["layout-malformed"] = FixtureDirectory("compat-show-malformed") },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["audio-notes"] = audioPath });

        var hostCapability = await new RuntimeCapabilityDetector().ProbeAsync(CancellationToken.None);
        var invalidCase = new CompatibilityCase(
            hostCapability.OperatingSystem, hostCapability.Architecture, "2024.14", "layout-malformed", "audio-notes");

        var runner = BuildRunner(catalog, _artifactDirectory);
        var result = Assert.Single(await runner.RunAsync([invalidCase], CancellationToken.None));

        Assert.False(result.Passed);
        Assert.Contains("failed safe import", result.Detail);
        Assert.Empty(Directory.GetFiles(_artifactDirectory, "*.xsq"));
    }

    [Fact]
    public async Task Step098_NegativePath_InsufficientTargetableModels_IsFlaggedNotThrown()
    {
        Directory.CreateDirectory(_artifactDirectory);
        var audioPath = WriteRealWavFixture(_artifactDirectory, "notes.wav");
        var catalog = new DictionaryCompatibilityFixtureCatalog(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["layout-thin"] = FixtureDirectory("compat-show-insufficient-targets") },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["audio-notes"] = audioPath });

        var hostCapability = await new RuntimeCapabilityDetector().ProbeAsync(CancellationToken.None);
        var invalidCase = new CompatibilityCase(
            hostCapability.OperatingSystem, hostCapability.Architecture, "2024.14", "layout-thin", "audio-notes");

        var runner = BuildRunner(catalog, _artifactDirectory);
        var result = Assert.Single(await runner.RunAsync([invalidCase], CancellationToken.None));

        Assert.False(result.Passed);
        Assert.Contains("at least 3", result.Detail);
    }

    [Fact]
    public async Task Step098_NegativePath_BlankXlightsVersion_RejectedByExporter_NoFileWritten()
    {
        Directory.CreateDirectory(_artifactDirectory);
        var audioPath = WriteRealWavFixture(_artifactDirectory, "notes.wav");
        var catalog = new DictionaryCompatibilityFixtureCatalog(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["layout-a"] = FixtureDirectory("compat-show-a") },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["audio-notes"] = audioPath });

        var hostCapability = await new RuntimeCapabilityDetector().ProbeAsync(CancellationToken.None);
        var invalidCase = new CompatibilityCase(
            hostCapability.OperatingSystem, hostCapability.Architecture, "   ", "layout-a", "audio-notes");

        var runner = BuildRunner(catalog, _artifactDirectory);
        var result = Assert.Single(await runner.RunAsync([invalidCase], CancellationToken.None));

        Assert.False(result.Passed);
        Assert.Equal(string.Empty, result.ArtifactPath);
        Assert.Contains("missing a required field", result.Detail);
        Assert.Empty(Directory.GetFiles(_artifactDirectory, "*.xsq"));
    }

    [Fact]
    public async Task Step098_NegativePath_Cancellation_ThrowsBeforeAnyWork()
    {
        var catalog = new DictionaryCompatibilityFixtureCatalog(
            new Dictionary<string, string>(StringComparer.Ordinal), new Dictionary<string, string>(StringComparer.Ordinal));
        var runner = BuildRunner(catalog, _artifactDirectory);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => runner.RunAsync([], cts.Token));

        Assert.False(Directory.Exists(_artifactDirectory)); // cancelled before even the artifact directory was created.
    }

    [Fact]
    public async Task Step098_NullCases_Throws()
    {
        var catalog = new DictionaryCompatibilityFixtureCatalog(
            new Dictionary<string, string>(StringComparer.Ordinal), new Dictionary<string, string>(StringComparer.Ordinal));
        var runner = BuildRunner(catalog, _artifactDirectory);

        await Assert.ThrowsAsync<ArgumentNullException>(() => runner.RunAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void Step098_UnknownFixtureIds_ThrowFromCatalog()
    {
        var catalog = new DictionaryCompatibilityFixtureCatalog(
            new Dictionary<string, string>(StringComparer.Ordinal), new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.Throws<CompatibilityFixtureNotFoundException>(() => catalog.ResolveLayoutShowDirectory("missing"));
        Assert.Throws<CompatibilityFixtureNotFoundException>(() => catalog.ResolveAudioFilePath("missing"));
    }
}
