using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Distribution;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Release;

/// <summary>
/// Default <see cref="ICompatibilityMatrixRunner"/>. PHASE-16 Step 098 ("Run tested
/// OS/xLights/audio/layout compatibility matrix and record known limitations"; required acceptance:
/// "Supported combinations each have installation and XSQ smoke evidence"). For each
/// <see cref="CompatibilityCase"/>:
///
/// 1. "Installation" evidence: the case's own Os/Architecture is checked against the real,
///    <see cref="IRuntimeCapabilityDetector"/>-probed host (PHASE-15's own real runtime-detection
///    contract, reused rather than re-invented) — a combination this real host cannot actually run is
///    reported as a non-passing, specifically-reasoned <see cref="TestEvidence"/>, never silently
///    skipped and never claimed to pass.
/// 2. "XSQ smoke" evidence: for a combination this host *can* run, the real, already-verified
///    <see cref="IShowLayoutImporter"/>/<see cref="IAudioDecoder"/>/<see cref="IThreeEffectPlanFactory"/>/
///    <see cref="IXsqExporter"/> pipeline (every one of them PHASE-02/PHASE-04's own accepted,
///    unmodified production code — nothing here reimplements or mocks any of it) actually imports the
///    layout fixture, probes the audio fixture for a real duration, builds a real effect plan and
///    exports a real <c>.xsq</c> file to <see cref="ArtifactDirectory"/>; the resulting
///    <see cref="ExportResult"/> is this case's "XSQ smoke evidence."
///
/// Every real, expected failure (unknown fixture id, a layout fixture without at least three
/// distinct targetable models/groups, an unparseable layout, an unprobable audio file, or a target
/// xLights version/effect the exporter itself rejects) is caught and reported as a non-passing
/// <see cref="TestEvidence"/> with a specific <see cref="TestEvidence.Detail"/> — Step 098's own
/// required failure path, "Invalid model/format/version must fail safely without rewriting source
/// files," is satisfied directly by every wrapped production call's own pre-existing, unmodified
/// "throw before any file is written/rewritten" guarantee.
/// </summary>
public sealed class CompatibilityMatrixRunner : ICompatibilityMatrixRunner
{
    private readonly IRuntimeCapabilityDetector _capabilityDetector;
    private readonly ICompatibilityFixtureCatalog _fixtureCatalog;
    private readonly IShowLayoutImporter _layoutImporter;
    private readonly IAudioDecoder _audioDecoder;
    private readonly IThreeEffectPlanFactory _planFactory;
    private readonly IEffectRecipeRegistry _recipeRegistry;
    private readonly IXsqExporter _xsqExporter;
    private readonly string _artifactDirectory;

    public CompatibilityMatrixRunner(
        IRuntimeCapabilityDetector capabilityDetector,
        ICompatibilityFixtureCatalog fixtureCatalog,
        IShowLayoutImporter layoutImporter,
        IAudioDecoder audioDecoder,
        IThreeEffectPlanFactory planFactory,
        IEffectRecipeRegistry recipeRegistry,
        IXsqExporter xsqExporter,
        string artifactDirectory)
    {
        ArgumentNullException.ThrowIfNull(capabilityDetector);
        ArgumentNullException.ThrowIfNull(fixtureCatalog);
        ArgumentNullException.ThrowIfNull(layoutImporter);
        ArgumentNullException.ThrowIfNull(audioDecoder);
        ArgumentNullException.ThrowIfNull(planFactory);
        ArgumentNullException.ThrowIfNull(recipeRegistry);
        ArgumentNullException.ThrowIfNull(xsqExporter);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactDirectory);

        _capabilityDetector = capabilityDetector;
        _fixtureCatalog = fixtureCatalog;
        _layoutImporter = layoutImporter;
        _audioDecoder = audioDecoder;
        _planFactory = planFactory;
        _recipeRegistry = recipeRegistry;
        _xsqExporter = xsqExporter;
        _artifactDirectory = artifactDirectory;
    }

    /// <summary>The minimum number of distinct targetable models/groups a layout fixture must expose for the required three-effect plan (<see cref="ThreeEffectPlanFactory.RequiredTargetCount"/>).</summary>
    private const int RequiredTargetCount = ThreeEffectPlanFactory.RequiredTargetCount;

    public async Task<IReadOnlyList<TestEvidence>> RunAsync(IReadOnlyList<CompatibilityCase> cases, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_artifactDirectory);

        var hostCapability = await _capabilityDetector.ProbeAsync(ct).ConfigureAwait(false);

        var evidence = new List<TestEvidence>(cases.Count);
        foreach (var testCase in cases)
        {
            ct.ThrowIfCancellationRequested();
            evidence.Add(await RunOneAsync(testCase, hostCapability, ct).ConfigureAwait(false));
        }

        return evidence;
    }

    private async Task<TestEvidence> RunOneAsync(CompatibilityCase testCase, RuntimeCapability hostCapability, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(testCase);

        var testId = BuildTestId(testCase);

        if (string.IsNullOrWhiteSpace(testCase.Os) ||
            string.IsNullOrWhiteSpace(testCase.Architecture) ||
            string.IsNullOrWhiteSpace(testCase.XlightsVersion) ||
            string.IsNullOrWhiteSpace(testCase.LayoutFixtureId) ||
            string.IsNullOrWhiteSpace(testCase.AudioFixtureId))
        {
            return Fail(testId, testCase, "Compatibility case is missing a required field (Os/Architecture/XlightsVersion/LayoutFixtureId/AudioFixtureId); no fixture was touched.");
        }

        if (!HostCanRun(testCase, hostCapability))
        {
            return Fail(
                testId,
                testCase,
                $"Cannot install/run '{testCase.Os}/{testCase.Architecture}' on this real host ('{hostCapability.OperatingSystem}/{hostCapability.Architecture}'); documented as unsupported rather than claimed.");
        }

        string showDirectory;
        string audioPath;
        try
        {
            showDirectory = _fixtureCatalog.ResolveLayoutShowDirectory(testCase.LayoutFixtureId);
            audioPath = _fixtureCatalog.ResolveAudioFilePath(testCase.AudioFixtureId);
        }
        catch (CompatibilityFixtureNotFoundException ex)
        {
            return Fail(testId, testCase, ex.Message);
        }

        DisplayGraph layout;
        try
        {
            layout = await _layoutImporter.ImportReadOnlyAsync(showDirectory, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ShowLayoutMalformedException or DirectoryNotFoundException or FileNotFoundException)
        {
            return Fail(testId, testCase, $"Layout fixture '{testCase.LayoutFixtureId}' failed safe import: {ex.Message}");
        }

        var targetNames = layout.Models.Select(m => m.XlightsName)
            .Concat(layout.Groups.Select(g => g.Name))
            .Distinct(StringComparer.Ordinal)
            .Take(RequiredTargetCount)
            .ToList();

        if (targetNames.Count < RequiredTargetCount)
        {
            return Fail(
                testId,
                testCase,
                $"Layout fixture '{testCase.LayoutFixtureId}' exposes only {targetNames.Count} distinct targetable model(s)/group(s); at least {RequiredTargetCount} are required. No effect plan or export was attempted.");
        }

        SongAsset song;
        try
        {
            song = await _audioDecoder.ProbeAsync(audioPath, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or AudioProbeInvalidException)
        {
            return Fail(testId, testCase, $"Audio fixture '{testCase.AudioFixtureId}' failed safe probe: {ex.Message}");
        }

        SequenceDocument plan;
        try
        {
            plan = _planFactory.CreatePlan(layout, targetNames, song.DurationTicks, _recipeRegistry);
        }
        catch (EffectPlanInvalidException ex)
        {
            return Fail(testId, testCase, $"Effect plan rejected: {ex.Message}");
        }

        var outputPath = Path.Combine(_artifactDirectory, SanitizeFileName(testId) + ".xsq");
        ExportResult export;
        try
        {
            export = await _xsqExporter.ExportAsync(
                layout, plan, outputPath, testCase.XlightsVersion, XsqExportPolicy.AppendCollisionSuffix, ct).ConfigureAwait(false);
        }
        catch (XsqExportInvalidException ex)
        {
            return Fail(testId, testCase, $"XSQ export rejected: {ex.Message}");
        }

        return new TestEvidence(
            testId,
            Passed: true,
            export.Path,
            testCase.XlightsVersion,
            DateTimeOffset.UtcNow,
            Detail: $"sha256={export.Sha256}; targets=[{string.Join(", ", targetNames)}]; warnings={export.Warnings.Count}");
    }

    private static bool HostCanRun(CompatibilityCase testCase, RuntimeCapability hostCapability) =>
        hostCapability.OperatingSystem.Contains(testCase.Os, StringComparison.OrdinalIgnoreCase) &&
        hostCapability.Architecture.Equals(testCase.Architecture, StringComparison.OrdinalIgnoreCase);

    private static TestEvidence Fail(string testId, CompatibilityCase testCase, string detail) =>
        new(testId, Passed: false, ArtifactPath: string.Empty, testCase.XlightsVersion, DateTimeOffset.UtcNow, detail);

    private static string BuildTestId(CompatibilityCase testCase) =>
        $"{testCase.Os}_{testCase.Architecture}_xlights-{testCase.XlightsVersion}_{testCase.LayoutFixtureId}_{testCase.AudioFixtureId}";

    private static string SanitizeFileName(string value)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidChar, '-');
        }

        return value;
    }
}
