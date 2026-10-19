using XlightsAI.Studio.Reference;

namespace XlightsAI.Studio.Reference.Tests;

/// <summary>
/// PHASE-01 / Step 001 — "Fork or vendor upstream source at an immutable commit; document license
/// and downstream notices." Required acceptance: record URL, SHA, license and attribution; clean
/// checkout resolves reference. Failure/edge case: invalid input, cancellation or stale persisted
/// state must not corrupt the project.
/// </summary>
[Trait("Category", "Golden")]
public sealed class Step001AcceptanceTests
{
    private static string FixturePath(string fileName) => Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);

    [Fact]
    public async Task Step001_Acceptance_Passes()
    {
        var service = new UpstreamReferenceService(new JsonUpstreamReferenceManifestReader());

        var report = await service.AuditAsync(FixturePath("valid-manifest.json"), CancellationToken.None);

        // Persisted domain state: the real pinned manifest fields the gate requires.
        Assert.True(report.IsPinned, string.Join("; ", report.Validation.Errors));
        Assert.NotNull(report.Manifest);
        var manifest = report.Manifest!;
        Assert.Equal("https://example.invalid/example-org/example-upstream-reference.git", manifest.RepositoryUrl);
        Assert.Equal("a1b2c3d4e5f60718293a4b5c6d7e8f9011121314", manifest.CommitSha);
        Assert.Matches("^[0-9a-f]{40}$", manifest.CommitSha);
        Assert.NotEqual("main", manifest.CommitSha);
        Assert.Equal("MIT", manifest.License.SpdxId);
        Assert.False(string.IsNullOrWhiteSpace(manifest.AttributionNotice));
        Assert.NotEmpty(manifest.Components);
        Assert.Contains(manifest.Components, c => c.Path == "src/analyzer");
        Assert.NotEmpty(manifest.KnownLicenseRisks);

        // Public output: what a future Settings/Diagnostics screen would render verbatim.
        var displayText = report.ToDisplayText();
        Assert.Contains(manifest.CommitSha, displayText);
        Assert.Contains("MIT", displayText);
        Assert.Contains(manifest.RepositoryUrl, displayText);
    }

    [Fact]
    public async Task Step001_Acceptance_Passes_RejectsInvalidManifest_NegativePath()
    {
        var service = new UpstreamReferenceService(new JsonUpstreamReferenceManifestReader());

        var report = await service.AuditAsync(FixturePath("invalid-manifest.json"), CancellationToken.None);

        Assert.False(report.IsPinned);
        Assert.Contains(report.Validation.Errors, e => e.Contains("CommitSha", StringComparison.Ordinal));
        Assert.Contains(report.Validation.Errors, e => e.Contains("License.SpdxId", StringComparison.Ordinal));
        Assert.Contains(report.Validation.Errors, e => e.Contains("AttributionNotice", StringComparison.Ordinal));
        Assert.Contains(report.Validation.Errors, e => e.Contains("Components", StringComparison.Ordinal));
        Assert.Contains("INVALID", report.ToDisplayText());
    }

    [Fact]
    public async Task AuditAsync_MissingManifestFile_FailsSafelyWithoutThrowing()
    {
        var service = new UpstreamReferenceService(new JsonUpstreamReferenceManifestReader());

        var report = await service.AuditAsync(FixturePath("does-not-exist.json"), CancellationToken.None);

        Assert.False(report.IsPinned);
        Assert.Null(report.Manifest);
        Assert.Contains(report.Validation.Errors, e => e.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AuditAsync_MalformedJson_FailsSafelyWithoutThrowing()
    {
        var service = new UpstreamReferenceService(new JsonUpstreamReferenceManifestReader());

        var report = await service.AuditAsync(FixturePath("malformed.json"), CancellationToken.None);

        Assert.False(report.IsPinned);
        Assert.Null(report.Manifest);
        Assert.NotEmpty(report.Validation.Errors);
    }

    [Theory]
    [InlineData("main")]
    [InlineData("e3d5b06")]
    [InlineData("")]
    [InlineData(null)]
    public void Validator_RejectsAnythingOtherThanAFullCommitSha(string? commitSha)
    {
        var manifest = ValidManifest() with { CommitSha = commitSha! };

        var result = UpstreamReferenceValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("CommitSha", StringComparison.Ordinal));
    }

    [Fact]
    public void Validator_AcceptsAFullyPopulatedManifest()
    {
        var result = UpstreamReferenceValidator.Validate(ValidManifest());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    private static UpstreamReferenceManifest ValidManifest() => new(
        SchemaVersion: 1,
        Name: "example-upstream-reference",
        RepositoryUrl: "https://example.invalid/example-org/example-upstream-reference.git",
        CommitSha: "a1b2c3d4e5f60718293a4b5c6d7e8f9011121314",
        CommitMessage: "chore: example pinned commit for fixture purposes",
        CommitDateUtc: DateTimeOffset.Parse("2026-10-04T17:46:40Z"),
        PinnedUtc: DateTimeOffset.Parse("2026-10-10T00:00:00Z"),
        VendoringStrategy: "git-submodule-pinned-commit",
        LocalReferencePath: "reference/example-upstream-reference",
        License: new UpstreamLicense("MIT", "Example Upstream Author", "2026",
            "reference/example-upstream-reference/LICENSE", "reference/UPSTREAM-LICENSE-MIT.txt", "scope note"),
        AttributionNotice: "attribution",
        Components: [new UpstreamComponent("src/analyzer", "audio analysis")],
        ThirdPartyDependencyNotes: [],
        KnownLicenseRisks: []);
}
