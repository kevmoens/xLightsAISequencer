using System.Security.Cryptography;
using XlightsAI.Studio.Reference.Fixtures;

namespace XlightsAI.Studio.Reference.Tests;

/// <summary>
/// PHASE-01 / Step 003 — "Create baseline fixture corpus: synthetic notes, drum pulse,
/// instrumental, complex mixed song and 2 layouts." Required acceptance: record input hashes,
/// permitted usage, outputs and repeatable invocation. Failure/edge case: invalid model/format/
/// version must fail safely without rewriting source files.
/// </summary>
[Trait("Category", "Golden")]
public sealed class Step003AcceptanceTests
{
    private static string FixturePath(string fileName) => Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);

    private static string CorpusRoot(string corpusFolderName) => Path.Combine(AppContext.BaseDirectory, "Fixtures", corpusFolderName);

    private static string CorpusManifestPath(string corpusFolderName) =>
        Path.Combine(CorpusRoot(corpusFolderName), "FIXTURE-CORPUS-MANIFEST.json");

    [Fact]
    public async Task Step003_Acceptance_Passes()
    {
        var service = new FixtureCorpusService(new JsonFixtureCorpusManifestReader());
        var corpusRoot = CorpusRoot("fixture-corpus");

        var report = await service.AuditAsync(CorpusManifestPath("fixture-corpus"), corpusRoot, CancellationToken.None);

        // Persisted domain state: the real baseline corpus the step requires, with hashes that
        // match what's actually on disk (hash drift = none) — this is "repeatable invocation".
        Assert.True(report.IsAccepted, string.Join("; ", report.Validation.Errors.Concat(
            report.HashMismatches.Select(m => $"{m.FixtureId}: recorded {m.RecordedSha256} actual {m.ActualSha256 ?? "MISSING"}"))));
        Assert.Empty(report.HashMismatches);
        var manifest = report.Manifest!;

        // Required fixture kinds per the step's implementation guidance.
        Assert.Contains(manifest.Fixtures, f => f.Kind == FixtureKind.SyntheticNotes);
        Assert.Contains(manifest.Fixtures, f => f.Kind == FixtureKind.DrumPulseClickTrack);
        Assert.Contains(manifest.Fixtures, f => f.Kind == FixtureKind.InstrumentalPhrase);
        Assert.Contains(manifest.Fixtures, f => f.Kind == FixtureKind.ComplexMixedSong);
        Assert.True(manifest.Fixtures.Count(f => f.Kind == FixtureKind.LayoutShape) >= 2,
            "Required acceptance: at least two layout XML shapes.");

        // Required acceptance: hashes, permitted usage, outputs and reference version recorded for every fixture.
        Assert.All(manifest.Fixtures, f =>
        {
            Assert.Matches("^[0-9a-f]{64}$", f.Sha256);
            Assert.False(string.IsNullOrWhiteSpace(f.PermittedUsage));
            Assert.False(string.IsNullOrWhiteSpace(f.ReferenceVersion));
            Assert.NotEmpty(f.ExpectedOutputs);
        });

        // Public output: what a future Settings/Diagnostics screen would render verbatim.
        var displayText = report.ToDisplayText();
        Assert.Contains("FixtureCorpusGenerator", displayText);
        Assert.Contains("Hash drift: none", displayText);
    }

    [Fact]
    public async Task Step003_RepeatableInvocation_GeneratorReproducesCommittedAudioBytesExactly()
    {
        // Direct proof of "repeatable invocation": the exact same recipe parameters used to author
        // the committed fixture produce byte-identical PCM output, called twice, matching the file.
        double[] notesScaleHz = [261.63, 293.66, 329.63, 349.23, 392.00];
        var firstRun = DeterministicSyntheticAudioGenerator.EncodeWav(
            DeterministicSyntheticAudioGenerator.BuildNoteSequence(notesScaleHz, noteDurationSeconds: 0.3, sampleRate: 8000), 8000);
        var secondRun = DeterministicSyntheticAudioGenerator.EncodeWav(
            DeterministicSyntheticAudioGenerator.BuildNoteSequence(notesScaleHz, noteDurationSeconds: 0.3, sampleRate: 8000), 8000);

        Assert.Equal(Convert.ToHexString(SHA256.HashData(firstRun)), Convert.ToHexString(SHA256.HashData(secondRun)));

        var committedFile = Path.Combine(CorpusRoot("fixture-corpus"), "audio", "synthetic-notes.wav");
        Assert.Equal(SHA256.HashData(firstRun), SHA256.HashData(File.ReadAllBytes(committedFile)));
    }

    [Fact]
    public async Task AuditAsync_MissingFixtureFilesOnDisk_ReportsHashMismatchesAndIsNotAccepted()
    {
        // The manifest itself validates (it's the real, well-formed manifest), but the audio
        // directory is absent from this corpus root — proving drift/missing-file detection works
        // independently of schema validation.
        var service = new FixtureCorpusService(new JsonFixtureCorpusManifestReader());

        var report = await service.AuditAsync(
            CorpusManifestPath("fixture-corpus-incomplete"), CorpusRoot("fixture-corpus-incomplete"), CancellationToken.None);

        Assert.True(report.Validation.IsValid, "Manifest content itself is well-formed; only files are missing.");
        Assert.False(report.IsAccepted);
        Assert.NotEmpty(report.HashMismatches);
        Assert.Contains(report.HashMismatches, m => m.FixtureId == "synthetic-notes" && m.ActualSha256 == null);
        Assert.Contains("MISSING", report.ToDisplayText());
    }

    [Fact]
    public async Task AuditAsync_TamperedFixtureFile_DetectsHashDriftAndIsNotAccepted()
    {
        var service = new FixtureCorpusService(new JsonFixtureCorpusManifestReader());

        var report = await service.AuditAsync(
            CorpusManifestPath("fixture-corpus"), CorpusRoot("fixture-corpus-tampered"), CancellationToken.None);

        Assert.True(report.Validation.IsValid);
        Assert.False(report.IsAccepted);
        var mismatch = Assert.Single(report.HashMismatches, m => m.FixtureId == "synthetic-notes");
        Assert.NotNull(mismatch.ActualSha256);
        Assert.NotEqual(mismatch.RecordedSha256, mismatch.ActualSha256, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RejectsManifestMissingRequiredLayoutShapeCount_NegativePath()
    {
        var service = new FixtureCorpusService(new JsonFixtureCorpusManifestReader());

        var report = await service.AuditAsync(FixturePath("missing-layout-kind-manifest.json"), fixturesRootDir: null, CancellationToken.None);

        Assert.False(report.IsAccepted);
        Assert.Contains(report.Validation.Errors, e => e.Contains("at least 2 fixture(s) of kind LayoutShape", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RejectsInvalidFixtureEntries_BadHashDuplicateIdAndMissingUsage_NegativePath()
    {
        var service = new FixtureCorpusService(new JsonFixtureCorpusManifestReader());

        var report = await service.AuditAsync(FixturePath("invalid-fixture-entries-manifest.json"), fixturesRootDir: null, CancellationToken.None);

        Assert.False(report.IsAccepted);
        Assert.Contains(report.Validation.Errors, e => e.Contains("Sha256 must be a 64-character hex digest", StringComparison.Ordinal));
        Assert.Contains(report.Validation.Errors, e => e.Contains("Duplicate fixture Id", StringComparison.Ordinal));
        Assert.Contains(report.Validation.Errors, e => e.Contains("PermittedUsage must be recorded", StringComparison.Ordinal));
        Assert.Contains("INVALID", report.ToDisplayText());
    }

    [Fact]
    public async Task AuditAsync_MissingManifestFile_FailsSafelyWithoutThrowing()
    {
        var service = new FixtureCorpusService(new JsonFixtureCorpusManifestReader());

        var report = await service.AuditAsync(FixturePath("does-not-exist.json"), fixturesRootDir: null, CancellationToken.None);

        Assert.False(report.IsAccepted);
        Assert.Null(report.Manifest);
        Assert.Contains(report.Validation.Errors, e => e.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AuditAsync_MalformedJson_FailsSafelyWithoutThrowing()
    {
        var service = new FixtureCorpusService(new JsonFixtureCorpusManifestReader());

        var report = await service.AuditAsync(FixturePath("malformed.json"), fixturesRootDir: null, CancellationToken.None);

        Assert.False(report.IsAccepted);
        Assert.Null(report.Manifest);
        Assert.NotEmpty(report.Validation.Errors);
    }

    [Fact]
    public async Task InvalidModelFormatOrVersion_NeverRewritesAnyFixtureFileOnDisk()
    {
        // Direct proof of the step's stated edge case: "invalid model/format/version must fail
        // safely without rewriting source files." Hash every real on-disk fixture (manifest +
        // audio + layouts) before and after running every known failure mode, and assert nothing
        // changed. FixtureCorpusService has no write path at all; this proves it by observation.
        var service = new FixtureCorpusService(new JsonFixtureCorpusManifestReader());
        var realFiles = Directory.GetFiles(CorpusRoot("fixture-corpus"), "*", SearchOption.AllDirectories);
        var hashesBefore = realFiles.ToDictionary(f => f, Hash);

        await service.AuditAsync(FixturePath("missing-layout-kind-manifest.json"), fixturesRootDir: null, CancellationToken.None);
        await service.AuditAsync(FixturePath("invalid-fixture-entries-manifest.json"), fixturesRootDir: null, CancellationToken.None);
        await service.AuditAsync(FixturePath("malformed.json"), fixturesRootDir: null, CancellationToken.None);
        await service.AuditAsync(FixturePath("does-not-exist.json"), fixturesRootDir: null, CancellationToken.None);
        await service.AuditAsync(CorpusManifestPath("fixture-corpus-incomplete"), CorpusRoot("fixture-corpus-incomplete"), CancellationToken.None);
        await service.AuditAsync(CorpusManifestPath("fixture-corpus"), CorpusRoot("fixture-corpus-tampered"), CancellationToken.None);

        foreach (var f in realFiles)
        {
            Assert.Equal(hashesBefore[f], Hash(f));
        }
    }

    [Fact]
    public void Validator_AcceptsAFullyPopulatedManifest()
    {
        var result = FixtureCorpusValidator.Validate(ValidManifest());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validator_RejectsNullManifest()
    {
        var result = FixtureCorpusValidator.Validate(null);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("null", StringComparison.OrdinalIgnoreCase));
    }

    private static FixtureCorpusManifest ValidManifest()
    {
        IReadOnlyDictionary<string, string> outputs = new Dictionary<string, string> { ["durationSeconds"] = "1.5" };

        return new FixtureCorpusManifest(
            SchemaVersion: 1,
            GeneratedByTool: "FixtureCorpusGenerator",
            GeneratedWithToolVersion: "1.0.0",
            GeneratedUtc: DateTimeOffset.Parse("2026-10-10T00:00:00Z"),
            Fixtures:
            [
                new FixtureEntry("notes", FixtureKind.SyntheticNotes, "audio/notes.wav",
                    new string('a', 64), "synthetic", "no rights", "v1", outputs),
                new FixtureEntry("clicks", FixtureKind.DrumPulseClickTrack, "audio/clicks.wav",
                    new string('b', 64), "synthetic", "no rights", "v1", outputs),
                new FixtureEntry("phrase", FixtureKind.InstrumentalPhrase, "audio/phrase.wav",
                    new string('c', 64), "synthetic", "no rights", "v1", outputs),
                new FixtureEntry("mixed", FixtureKind.ComplexMixedSong, "audio/mixed.wav",
                    new string('d', 64), "synthetic", "no rights", "v1", outputs),
                new FixtureEntry("layout-a", FixtureKind.LayoutShape, "layouts/a.xml",
                    new string('e', 64), "synthetic", "no rights", "v1", outputs),
                new FixtureEntry("layout-b", FixtureKind.LayoutShape, "layouts/b.xml",
                    new string('f', 64), "synthetic", "no rights", "v1", outputs),
            ]);
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
