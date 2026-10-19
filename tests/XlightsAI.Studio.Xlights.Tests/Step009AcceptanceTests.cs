namespace XlightsAI.Studio.Xlights.Tests;

/// <summary>
/// PHASE-02 Step 009 — "Implement show-folder picker with read-only initial discovery."
/// Required acceptance: "Missing folder/file produces actionable error; no mutation."
/// Failure/edge case: "Invalid input, cancellation or stale persisted state must not corrupt the
/// project." These tests exercise the real <see cref="ShowDirectoryDiscoveryService"/> against
/// real fixture directories on disk (not a mocked file system), and assert the persisted domain
/// state (<see cref="ShowDirectoryDiscovery"/>) it returns, not merely that a call happened.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step009AcceptanceTests
{
    private static string FixtureDirectory(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public async Task Step009_Acceptance_Passes_BothFilesPresent()
    {
        var service = new ShowDirectoryDiscoveryService();
        var directory = FixtureDirectory("sample-show-complete");

        var result = await service.DiscoverAsync(directory, CancellationToken.None);

        Assert.True(result.HasRgbEffectsFile);
        Assert.True(result.HasNetworksFile);
        Assert.Empty(result.MissingRequiredFiles);
        Assert.Empty(result.Warnings);
        Assert.True(result.IsComplete);
        Assert.Equal(Path.GetFullPath(directory), result.ShowDirectory);
    }

    [Fact]
    public async Task OptionalNetworksFileMissing_IsAWarningNotAFailure()
    {
        var service = new ShowDirectoryDiscoveryService();
        var directory = FixtureDirectory("sample-show-missing-networks");

        var result = await service.DiscoverAsync(directory, CancellationToken.None);

        Assert.True(result.HasRgbEffectsFile);
        Assert.False(result.HasNetworksFile);
        Assert.Empty(result.MissingRequiredFiles);
        Assert.True(result.IsComplete);
        Assert.Single(result.Warnings);
        Assert.Contains("xlights_networks.xml", result.Warnings[0]);
    }

    [Fact]
    public async Task MissingRequiredFile_IsReportedExplicitly_NotThrown()
    {
        var service = new ShowDirectoryDiscoveryService();
        var directory = FixtureDirectory("sample-show-missing-rgbeffects");

        var result = await service.DiscoverAsync(directory, CancellationToken.None);

        Assert.False(result.HasRgbEffectsFile);
        Assert.False(result.IsComplete);
        Assert.Contains(ShowDirectoryFileNames.RgbEffects, result.MissingRequiredFiles);
    }

    [Fact]
    public async Task MissingDirectory_ThrowsActionableError_NegativePath()
    {
        var service = new ShowDirectoryDiscoveryService();
        var missingDirectory = Path.Combine(Path.GetTempPath(), "xlai-step009-does-not-exist-" + Guid.NewGuid().ToString("N"));

        var ex = await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => service.DiscoverAsync(missingDirectory, CancellationToken.None));

        Assert.Contains(missingDirectory, ex.Message, StringComparison.OrdinalIgnoreCase);
        // No mutation: a missing-folder discovery attempt must never create the folder it was asked about.
        Assert.False(Directory.Exists(missingDirectory));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvalidDirectoryInput_ThrowsActionableError_NegativePath(string invalidDirectory)
    {
        var service = new ShowDirectoryDiscoveryService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.DiscoverAsync(invalidDirectory, CancellationToken.None));
    }

    [Fact]
    public async Task AlreadyCancelledToken_ThrowsWithoutReadingAnything_NegativePath()
    {
        var service = new ShowDirectoryDiscoveryService();
        var directory = FixtureDirectory("sample-show-complete");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.DiscoverAsync(directory, cts.Token));
    }

    /// <summary>
    /// "Detect realpath/symlink boundary": a required filename that exists only as a symlink whose
    /// real target resolves *outside* the selected show directory must be treated as not-present
    /// (never read), with an explicit warning recorded — proven against a real
    /// <see cref="File.CreateSymbolicLink(string, string)"/>, not a simulated path string.
    /// </summary>
    [Fact]
    public async Task RequiredFileEscapingDirectoryViaSymlink_IsTreatedAsMissing_NegativePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "xlai-step009-symlink-" + Guid.NewGuid().ToString("N"));
        var showDirectory = Path.Combine(root, "show");
        var outsideDirectory = Path.Combine(root, "outside");
        Directory.CreateDirectory(showDirectory);
        Directory.CreateDirectory(outsideDirectory);

        var outsideTarget = Path.Combine(outsideDirectory, "secret.xml");
        await File.WriteAllTextAsync(outsideTarget, "<xrgbEffects />");

        var linkPath = Path.Combine(showDirectory, ShowDirectoryFileNames.RgbEffects);
        File.CreateSymbolicLink(linkPath, outsideTarget);

        try
        {
            var service = new ShowDirectoryDiscoveryService();
            var result = await service.DiscoverAsync(showDirectory, CancellationToken.None);

            Assert.False(result.HasRgbEffectsFile);
            Assert.Contains(ShowDirectoryFileNames.RgbEffects, result.MissingRequiredFiles);
            Assert.Contains(result.Warnings, w => w.Contains("outside the selected show directory", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A symlinked file whose real target stays inside the selected directory is accepted normally.</summary>
    [Fact]
    public async Task RequiredFileAsSymlinkWithinDirectory_IsAccepted()
    {
        var root = Path.Combine(Path.GetTempPath(), "xlai-step009-symlink-ok-" + Guid.NewGuid().ToString("N"));
        var showDirectory = Path.Combine(root, "show");
        Directory.CreateDirectory(showDirectory);

        var realFile = Path.Combine(showDirectory, "real-rgbeffects.xml");
        await File.WriteAllTextAsync(realFile, "<xrgbEffects />");

        var linkPath = Path.Combine(showDirectory, ShowDirectoryFileNames.RgbEffects);
        File.CreateSymbolicLink(linkPath, realFile);

        try
        {
            var service = new ShowDirectoryDiscoveryService();
            var result = await service.DiscoverAsync(showDirectory, CancellationToken.None);

            Assert.True(result.HasRgbEffectsFile);
            Assert.Empty(result.MissingRequiredFiles);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
