using System.Text.Json;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Projects.Tests;

/// <summary>
/// PHASE-01 Step 006: "Implement create/open/save project with versioned manifest and no direct
/// writes to show folder." Required acceptance: "Round-trip sample project and reject newer
/// unsupported schema safely." Failure/edge case: "Invalid input, cancellation or stale persisted
/// state must not corrupt the project."
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step006AcceptanceTests : IDisposable
{
    private readonly string _rootDirectory;

    public Step006AcceptanceTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "xlai-step006-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private (ProjectService Service, string AppDir) NewService()
    {
        var appDir = Path.Combine(_rootDirectory, "app-" + Guid.NewGuid().ToString("N"));
        var store = new JsonProjectStore();
        var recents = new RecentProjectsStore(appDir);
        return (new ProjectService(store, recents), appDir);
    }

    [Fact]
    public async Task Step006_Acceptance_Passes()
    {
        var projectDir = Path.Combine(_rootDirectory, "Demo Song");
        var (service, appDir) = NewService();

        // --- Create ---
        var created = await service.CreateAsync(projectDir, "Demo Song", CancellationToken.None);
        Assert.NotEqual(default, created.Id.Value);
        Assert.Equal("Demo Song", created.Name);
        Assert.Equal(JsonProjectStore.CurrentSchemaVersion, created.SchemaVersion);
        Assert.Equal(Path.GetFullPath(projectDir), created.RootDirectory);

        var manifestPath = Path.Combine(projectDir, JsonProjectStore.ManifestFileName);
        Assert.True(File.Exists(manifestPath), "project.json must be written by CreateAsync.");

        // No direct write to any "show folder": this step never receives, stores or writes to an
        // xLights show-file path anywhere; the only directory touched is the one the caller
        // explicitly asked to become the project's own workspace.
        var writtenOnDisk = JsonSerializer.Deserialize<ProjectManifest>(await File.ReadAllTextAsync(manifestPath));
        Assert.Equal(created, writtenOnDisk);

        // --- Close/reopen: simulate a real process restart with a brand-new ProjectService
        // instance pointed at the same app directory, per PHASE-01's own "close, reopen exactly
        // the same project" user story. ---
        var store2 = new JsonProjectStore();
        var recents2 = new RecentProjectsStore(appDir);
        var restarted = new ProjectService(store2, recents2);

        var reopened = await restarted.OpenAsync(projectDir, CancellationToken.None);
        Assert.Equal(created, reopened);

        // --- Save round-trips without corrupting the manifest ---
        await restarted.SaveAsync(reopened.Id, CancellationToken.None);
        var afterSave = JsonSerializer.Deserialize<ProjectManifest>(await File.ReadAllTextAsync(manifestPath));
        Assert.Equal(created, afterSave);

        // --- "Open a second project" (phase gate demo step) ---
        var secondDir = Path.Combine(_rootDirectory, "Second Song");
        var second = await restarted.CreateAsync(secondDir, "Second Song", CancellationToken.None);
        Assert.NotEqual(created.Id, second.Id);

        var recentList = await restarted.RecentAsync(CancellationToken.None);
        Assert.Contains(recentList, s => s.Id == created.Id);
        Assert.Contains(recentList, s => s.Id == second.Id);

        // --- Reject newer unsupported schema safely, without corrupting the file ---
        var futureManifest = created with { SchemaVersion = created.SchemaVersion + 999 };
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(futureManifest));

        var ex = await Assert.ThrowsAsync<ProjectSchemaNotSupportedException>(
            () => restarted.OpenAsync(projectDir, CancellationToken.None));
        Assert.Equal(created.SchemaVersion + 999, ex.FoundSchemaVersion);
        Assert.Equal(JsonProjectStore.CurrentSchemaVersion, ex.SupportedSchemaVersion);

        // The rejected-newer-schema manifest on disk must be completely untouched (no downgrade,
        // no "repair", no deletion).
        var afterRejectedOpen = await File.ReadAllTextAsync(manifestPath);
        Assert.Equal(JsonSerializer.Serialize(futureManifest), afterRejectedOpen);
    }

    [Theory]
    [InlineData(null, "Demo Song")]
    [InlineData("", "Demo Song")]
    [InlineData("   ", "Demo Song")]
    public async Task CreateAsync_WithInvalidDirectory_ThrowsAndWritesNothing_NegativePath(string? directory, string name)
    {
        var (service, _) = NewService();

        // ArgumentException.ThrowIfNullOrWhiteSpace throws ArgumentNullException for a null value
        // and ArgumentException for empty/whitespace; ArgumentNullException IS an ArgumentException,
        // so assert on the base type to cover both without over-specifying which subtype a given
        // invalid input produces.
        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.CreateAsync(directory!, name, CancellationToken.None));

        Assert.False(Directory.Exists(_rootDirectory) && Directory.EnumerateFileSystemEntries(_rootDirectory).Any());
    }

    [Fact]
    public async Task CreateAsync_WithInvalidName_ThrowsAndWritesNothing_NegativePath()
    {
        var projectDir = Path.Combine(_rootDirectory, "Invalid Name Target");
        var (service, _) = NewService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(projectDir, "  ", CancellationToken.None));

        Assert.False(Directory.Exists(projectDir), "An invalid-name create must not leave a directory or manifest behind.");
    }

    [Fact]
    public async Task CreateAsync_WithAlreadyCancelledToken_ThrowsAndWritesNothing_NegativePath()
    {
        var projectDir = Path.Combine(_rootDirectory, "Cancelled Create");
        var (service, _) = NewService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.CreateAsync(projectDir, "Cancelled", cts.Token));

        Assert.False(Directory.Exists(projectDir), "A cancelled create must leave no partial project directory.");
    }

    [Fact]
    public async Task CreateAsync_WhenDirectoryAlreadyHasADifferentProject_ThrowsAndLeavesOriginalUntouched_NegativePath()
    {
        var projectDir = Path.Combine(_rootDirectory, "Already A Project");
        var (service, _) = NewService();
        var original = await service.CreateAsync(projectDir, "Original Name", CancellationToken.None);

        await Assert.ThrowsAsync<ProjectDirectoryInUseException>(
            () => service.CreateAsync(projectDir, "Overwriting Name", CancellationToken.None));

        var manifestPath = Path.Combine(projectDir, JsonProjectStore.ManifestFileName);
        var onDisk = JsonSerializer.Deserialize<ProjectManifest>(await File.ReadAllTextAsync(manifestPath));
        Assert.Equal(original, onDisk);
    }

    [Fact]
    public async Task OpenAsync_WhenNoManifestExists_ThrowsDirectoryNotFoundException_NegativePath()
    {
        var projectDir = Path.Combine(_rootDirectory, "Never Created");
        var (service, _) = NewService();

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => service.OpenAsync(projectDir, CancellationToken.None));
    }

    [Fact]
    public async Task SaveAsync_WhenProjectWasNeverOpened_ThrowsInvalidOperationException_NegativePath()
    {
        var (service, _) = NewService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveAsync(new ProjectId(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task SaveAsync_WhenManifestDeletedExternally_ThrowsProjectStateStale_NegativePath()
    {
        var projectDir = Path.Combine(_rootDirectory, "Deleted Underneath");
        var (service, _) = NewService();
        var created = await service.CreateAsync(projectDir, "Deleted Underneath", CancellationToken.None);

        File.Delete(Path.Combine(projectDir, JsonProjectStore.ManifestFileName));

        await Assert.ThrowsAsync<ProjectStateStaleException>(() => service.SaveAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task SaveAsync_WhenManifestNowBelongsToADifferentProject_ThrowsProjectStateStaleAndDoesNotOverwrite_NegativePath()
    {
        var projectDir = Path.Combine(_rootDirectory, "Repointed Directory");
        var (service, _) = NewService();
        var created = await service.CreateAsync(projectDir, "Original Project", CancellationToken.None);

        // Simulate the directory being repointed at a different project's manifest out from under
        // the running process (e.g. another process, a sync tool, a restored backup).
        var foreignManifest = new ProjectManifest(
            new ProjectId(Guid.NewGuid()), JsonProjectStore.CurrentSchemaVersion, "Foreign Project", created.RootDirectory, DateTimeOffset.UtcNow);
        var manifestPath = Path.Combine(projectDir, JsonProjectStore.ManifestFileName);
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(foreignManifest));

        await Assert.ThrowsAsync<ProjectStateStaleException>(() => service.SaveAsync(created.Id, CancellationToken.None));

        // The foreign manifest must survive untouched — this is the literal "must not corrupt the
        // project" requirement: saving the stale in-memory project must not clobber what is
        // actually on disk now.
        var onDisk = JsonSerializer.Deserialize<ProjectManifest>(await File.ReadAllTextAsync(manifestPath));
        Assert.Equal(foreignManifest, onDisk);
    }

    [Fact]
    public async Task RecentAsync_PrunesEntriesWhoseProjectNoLongerExistsOnDisk_NegativePath()
    {
        var keepDir = Path.Combine(_rootDirectory, "Keep Me");
        var removeDir = Path.Combine(_rootDirectory, "Remove Me");
        var (service, _) = NewService();

        var kept = await service.CreateAsync(keepDir, "Keep Me", CancellationToken.None);
        var removed = await service.CreateAsync(removeDir, "Remove Me", CancellationToken.None);

        Directory.Delete(removeDir, recursive: true);

        var recents = await service.RecentAsync(CancellationToken.None);

        Assert.Contains(recents, s => s.Id == kept.Id);
        Assert.DoesNotContain(recents, s => s.Id == removed.Id);
    }

    [Fact]
    public async Task JsonProjectStore_WriteThenReadManifest_RoundTripsExactly()
    {
        var store = new JsonProjectStore();
        var directory = Path.Combine(_rootDirectory, "Store Round Trip");
        var manifest = new ProjectManifest(new ProjectId(Guid.NewGuid()), 1, "Round Trip", directory, DateTimeOffset.UtcNow);

        await store.WriteManifestAtomicallyAsync(manifest, CancellationToken.None);
        var read = await store.ReadManifestAsync(directory, CancellationToken.None);

        Assert.Equal(manifest, read);
    }

    [Fact]
    public async Task JsonProjectStore_ReadManifestAsync_WhenDirectoryHasNoManifest_ReturnsNull_NegativePath()
    {
        var store = new JsonProjectStore();
        var directory = Path.Combine(_rootDirectory, "No Manifest Here");
        Directory.CreateDirectory(directory);

        var read = await store.ReadManifestAsync(directory, CancellationToken.None);

        Assert.Null(read);
    }

    /// <summary>
    /// Code-review finding: a corrupt <c>project.json</c> previously threw a raw
    /// <see cref="JsonException"/> straight out of <see cref="JsonProjectStore.ReadManifestAsync"/>,
    /// inconsistent with every other project-layer failure (all typed exceptions). It must now
    /// surface as <see cref="ProjectManifestCorruptException"/>, through both the store directly and
    /// through <see cref="ProjectService.OpenAsync"/>, which calls it.
    /// </summary>
    [Fact]
    public async Task JsonProjectStore_ReadManifestAsync_WithCorruptJson_ThrowsProjectManifestCorruptException_NegativePath()
    {
        var store = new JsonProjectStore();
        var directory = Path.Combine(_rootDirectory, "Corrupt Manifest");
        Directory.CreateDirectory(directory);
        var manifestPath = Path.Combine(directory, JsonProjectStore.ManifestFileName);
        await File.WriteAllTextAsync(manifestPath, "{ not valid json ");

        var ex = await Assert.ThrowsAsync<ProjectManifestCorruptException>(
            () => store.ReadManifestAsync(directory, CancellationToken.None));
        Assert.Equal(manifestPath, ex.ManifestPath);

        var (service, _) = NewService();
        await Assert.ThrowsAsync<ProjectManifestCorruptException>(
            () => service.OpenAsync(directory, CancellationToken.None));
    }

    [Fact]
    public async Task JsonProjectStore_MigrateDatabaseAsync_CreatesStudioDbAtRequestedSchema()
    {
        // Step 006 shipped this as a disclosed NotSupportedException stub (see Step 006 evidence
        // §9/§10: "real SQLite migration is Step 007's scope"). Step 007 supersedes it with a real
        // implementation; this test is the accepted replacement for
        // JsonProjectStore_MigrateDatabaseAsync_IsDisclosedNotYetImplemented_NegativePath, which
        // asserted the now-removed stub behavior. Full coverage of the migration contract itself
        // (fresh/prior-version/locked/interrupted) lives in Step007AcceptanceTests.
        var store = new JsonProjectStore();
        var directory = Path.Combine(_rootDirectory, "Db Schema Target");
        Directory.CreateDirectory(directory);

        await store.MigrateDatabaseAsync(directory, JsonProjectStore.CurrentDatabaseSchemaVersion, CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(directory, SqliteMigrationRunner.DatabaseFileName)));
    }
}
