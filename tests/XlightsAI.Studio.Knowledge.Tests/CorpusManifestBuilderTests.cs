namespace XlightsAI.Studio.Knowledge.Tests;

/// <summary>
/// Hermetic tests for <see cref="CorpusManifestBuilder"/>'s exclusion classification (RenderCache,
/// Backup, this app's own exports, backup files) against a synthetic directory tree — these rules are
/// general, unlike <see cref="CorpusManifestBuilder.KnownResolutions"/> itself, which names this user's
/// actual <c>C:\xLights</c> files and is instead covered by <c>RealXLightsCorpusTests</c>.
/// </summary>
public sealed class CorpusManifestBuilderTests
{
    [Fact]
    public void Build_ClassifiesRenderCacheBackupAndExportPathsAsExcluded_NeverAsEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), $"corpus-manifest-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            WriteFile(Path.Combine(root, "RenderCache", "foo.xsq"));
            WriteFile(Path.Combine(root, "Backup", "2024-01-01", "foo.xsq"));
            WriteFile(Path.Combine(root, "Bad Feelings", "exports", "sequence.xsq"));
            WriteFile(Path.Combine(root, "unknown-new-sequence.xsq"));

            var manifest = CorpusManifestBuilder.Build(root);

            Assert.Empty(manifest.Entries);
            Assert.Contains(manifest.ExcludedPaths, kv => kv.Key.Contains("RenderCache") && kv.Value.Contains("RenderCache"));
            Assert.Contains(manifest.ExcludedPaths, kv => kv.Key.Contains("Backup") && kv.Value.Contains("Backup"));
            Assert.Contains(manifest.ExcludedPaths, kv => kv.Key.Contains("Bad Feelings") && kv.Value.Contains("this app's own"));
            Assert.Contains(manifest.ExcludedPaths, kv => kv.Key.Contains("unknown-new-sequence") && kv.Value.Contains("not part of the known-resolution set"));

            // A real .xsq.bak_pre_pop_edit backup file (seen in the real corpus) never even reaches
            // ClassifyUnresolvedPath: it does not end in ".xsq", so Directory.EnumerateFiles(root, "*.xsq", ...)
            // never enumerates it in the first place -- confirmed here rather than assumed.
            WriteFile(Path.Combine(root, "something.xsq.bak_pre_pop_edit"));
            var manifestAfterBakFile = CorpusManifestBuilder.Build(root);
            Assert.DoesNotContain(manifestAfterBakFile.ExcludedPaths.Keys, p => p.Contains("bak_pre_pop_edit"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Build_ThrowsForMissingRoot()
    {
        Assert.Throws<DirectoryNotFoundException>(() => CorpusManifestBuilder.Build(Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}")));
    }

    [Fact]
    public void Build_ExcludesAnyExportsFolderGenerically_NotJustTheOneHardCodedProjectName()
    {
        // Build-plan Phase 17 §2a: the exports/ rule must not be special-cased to one project name --
        // any project's own exports/ folder is excluded.
        var root = Path.Combine(Path.GetTempPath(), $"corpus-manifest-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            WriteFile(Path.Combine(root, "Some Other Project", "exports", "sequence.xsq"));

            var manifest = CorpusManifestBuilder.Build(root);

            Assert.Empty(manifest.Entries);
            Assert.Contains(manifest.ExcludedPaths, kv => kv.Key.Contains("Some Other Project") && kv.Value.Contains("exports"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Build_ExcludesFilesUnderARecordedProjectRootDirectory_EvenOutsideItsExportsFolder()
    {
        // Build-plan Phase 17 §2a: a generated .xsq later copied/moved outside its project's own
        // exports/ folder must still be excluded via the project-root cross-reference.
        var root = Path.Combine(Path.GetTempPath(), $"corpus-manifest-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var projectRoot = Path.Combine(root, "MyProject");
            WriteFile(Path.Combine(projectRoot, "moved-sequence.xsq"));
            WriteFile(Path.Combine(root, "unrelated-sequence.xsq"));

            var manifest = CorpusManifestBuilder.Build(root, excludedProjectRootDirectories: [projectRoot]);

            Assert.Empty(manifest.Entries);
            Assert.Contains(manifest.ExcludedPaths, kv => kv.Key.Contains("moved-sequence") && kv.Value.Contains("this app's own recorded project directory"));
            Assert.Contains(manifest.ExcludedPaths, kv => kv.Key.Contains("unrelated-sequence") && kv.Value.Contains("not part of the known-resolution set"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<xsequence/>");
    }
}
