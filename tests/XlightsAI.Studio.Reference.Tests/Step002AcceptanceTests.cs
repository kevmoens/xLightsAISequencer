using System.Security.Cryptography;
using XlightsAI.Studio.Reference;

namespace XlightsAI.Studio.Reference.Tests;

/// <summary>
/// PHASE-01 / Step 002 — "Audit upstream analysis, layout, theme, effects, XSQ writer, preview and
/// dependency boundaries." Required acceptance: create a module-by-module reuse/port/replace/omit
/// matrix with implementation risks. Failure/edge case: invalid model/format/version must fail
/// safely without rewriting source files.
/// </summary>
[Trait("Category", "Golden")]
public sealed class Step002AcceptanceTests
{
    private const string PinnedCommitSha = "a1b2c3d4e5f60718293a4b5c6d7e8f9011121314";

    private static string FixturePath(string fileName) => Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);

    private static async Task<string> ExpectedPinnedShaFromStep001Async()
    {
        var step001Service = new UpstreamReferenceService(new JsonUpstreamReferenceManifestReader());
        var step001Report = await step001Service.AuditAsync(FixturePath("valid-manifest.json"), CancellationToken.None);
        Assert.True(step001Report.IsPinned, "Step 001's own fixture must still be valid; its contract is immutable.");
        return step001Report.Manifest!.CommitSha;
    }

    [Fact]
    public async Task Step002_Acceptance_Passes()
    {
        var expectedSha = await ExpectedPinnedShaFromStep001Async();
        Assert.Equal(PinnedCommitSha, expectedSha); // cross-step contract check, not a tautology: proves both fixtures agree.

        var service = new ModuleAuditService(new JsonModuleAuditMatrixReader());

        var report = await service.AuditAsync(FixturePath("valid-module-audit-matrix.json"), expectedSha, CancellationToken.None);

        Assert.True(report.IsAccepted, string.Join("; ", report.Validation.Errors));
        var matrix = report.Matrix!;
        Assert.Equal(expectedSha, matrix.SourceCommitSha);

        // Required acceptance: a module-by-module reuse/port/replace/omit matrix with implementation risks.
        Assert.True(matrix.Modules.Count >= 10, "Expected a real multi-module audit, not a token entry.");
        Assert.Contains(matrix.Modules, m => m.Decision == ModuleDecision.Reuse);
        Assert.Contains(matrix.Modules, m => m.Decision == ModuleDecision.Port);
        Assert.Contains(matrix.Modules, m => m.Decision == ModuleDecision.Replace);
        Assert.Contains(matrix.Modules, m => m.Decision == ModuleDecision.Omit);
        Assert.All(matrix.Modules, m => Assert.False(string.IsNullOrWhiteSpace(m.Rationale)));
        Assert.All(matrix.Modules.Where(m => m.RiskLevel == ModuleRiskLevel.High),
            m => Assert.NotEmpty(m.Risks));

        // The concrete, real finding this step exists to surface: src/grouper overwrites the user's
        // real xlights_rgbeffects.xml in place, and that risk must be recorded with a mitigation.
        var grouper = Assert.Single(matrix.Modules, m => m.ModulePath == "src/grouper");
        Assert.True(grouper.SourceOverwriteRisk);
        Assert.False(string.IsNullOrWhiteSpace(grouper.OverwriteMitigation));

        // Public output: what a future architecture/diagnostics screen would render verbatim.
        var displayText = report.ToDisplayText();
        Assert.Contains("Reuse", displayText);
        Assert.Contains("Port", displayText);
        Assert.Contains("src/grouper", displayText);
    }

    [Fact]
    public async Task Step002_Acceptance_Passes_RejectsUnmitigatedOverwriteRisk_NegativePath()
    {
        var expectedSha = await ExpectedPinnedShaFromStep001Async();
        var service = new ModuleAuditService(new JsonModuleAuditMatrixReader());

        var report = await service.AuditAsync(FixturePath("unmitigated-overwrite-risk-matrix.json"), expectedSha, CancellationToken.None);

        Assert.False(report.IsAccepted);
        Assert.Contains(report.Validation.Errors, e => e.Contains("OverwriteMitigation", StringComparison.Ordinal));
        Assert.Contains("INVALID", report.ToDisplayText());
    }

    [Fact]
    public async Task RejectsMatrixAuditedAgainstADifferentCommit_NegativePath()
    {
        var expectedSha = await ExpectedPinnedShaFromStep001Async();
        var service = new ModuleAuditService(new JsonModuleAuditMatrixReader());

        var report = await service.AuditAsync(FixturePath("stale-commit-matrix.json"), expectedSha, CancellationToken.None);

        Assert.False(report.IsAccepted);
        Assert.Contains(report.Validation.Errors, e => e.Contains("Step 001 pinned commit", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RejectsUnsupportedSchemaVersion_NegativePath()
    {
        var expectedSha = await ExpectedPinnedShaFromStep001Async();
        var service = new ModuleAuditService(new JsonModuleAuditMatrixReader());

        var report = await service.AuditAsync(FixturePath("unsupported-schema-version-matrix.json"), expectedSha, CancellationToken.None);

        Assert.False(report.IsAccepted);
        Assert.Contains(report.Validation.Errors, e => e.Contains("Unsupported SchemaVersion", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AuditAsync_MissingMatrixFile_FailsSafelyWithoutThrowing()
    {
        var service = new ModuleAuditService(new JsonModuleAuditMatrixReader());

        var report = await service.AuditAsync(FixturePath("does-not-exist.json"), PinnedCommitSha, CancellationToken.None);

        Assert.False(report.IsAccepted);
        Assert.Null(report.Matrix);
        Assert.Contains(report.Validation.Errors, e => e.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AuditAsync_MalformedJson_FailsSafelyWithoutThrowing()
    {
        var service = new ModuleAuditService(new JsonModuleAuditMatrixReader());

        var report = await service.AuditAsync(FixturePath("malformed.json"), PinnedCommitSha, CancellationToken.None);

        Assert.False(report.IsAccepted);
        Assert.Null(report.Matrix);
        Assert.NotEmpty(report.Validation.Errors);
    }

    [Fact]
    public async Task InvalidModelFormatOrVersion_NeverRewritesTheSourceFixtureOnDisk()
    {
        // Direct proof of the step's stated edge case: "invalid model/format/version must fail
        // safely without rewriting source files." Hash every on-disk input fixture before and
        // after a run that includes every known failure mode, and assert nothing changed.
        var service = new ModuleAuditService(new JsonModuleAuditMatrixReader());
        string[] fixtures =
        [
            FixturePath("unmitigated-overwrite-risk-matrix.json"),
            FixturePath("stale-commit-matrix.json"),
            FixturePath("unsupported-schema-version-matrix.json"),
            FixturePath("malformed.json"),
        ];
        var hashesBefore = fixtures.ToDictionary(f => f, Hash);

        await service.AuditAsync(fixtures[0], PinnedCommitSha, CancellationToken.None);
        await service.AuditAsync(fixtures[1], PinnedCommitSha, CancellationToken.None);
        await service.AuditAsync(fixtures[2], PinnedCommitSha, CancellationToken.None);
        await service.AuditAsync(fixtures[3], PinnedCommitSha, CancellationToken.None);
        await service.AuditAsync(FixturePath("does-not-exist.json"), PinnedCommitSha, CancellationToken.None);

        foreach (var f in fixtures)
        {
            Assert.Equal(hashesBefore[f], Hash(f));
        }
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
