using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-02 Step 015: proves <see cref="ShellViewModel.ExportXsqCommand"/> honors
/// <see cref="ShowImportViewModel.ExportPolicy"/> end to end (default unchanged from Step 013's own
/// original reject-on-collision behavior; an explicit collision-suffix/backup choice really changes
/// the real on-disk outcome), and that an invalid request still fails safely regardless of policy.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step015ShellWiringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step015-shell-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static readonly string RgbEffectsXml =
        """
        <xrgb FormatVersion="2" BaseChannel="0">
          <models>
            <model name="Roofline" DisplayAs="Star" />
            <model name="Arch 1" DisplayAs="Arches" />
            <model name="Arch 2" DisplayAs="Arches" />
          </models>
          <modelGroups>
            <modelGroup name="Front-Arches" models="Roofline,Arch 1,Arch 2" />
          </modelGroups>
        </xrgb>
        """;

    private async Task<ShellViewModel> BuildShellWithGeneratedPlanAsync()
    {
        var showDirectory = Path.Combine(_root, "show");
        Directory.CreateDirectory(showDirectory);
        File.WriteAllText(Path.Combine(showDirectory, ShowDirectoryFileNames.RgbEffects), RgbEffectsXml);

        var appDirectory = Path.Combine(_root, "app");
        var diagnostics = new AppDiagnostics(Path.Combine(appDirectory, "logs"));
        var settingsStore = new SettingsStore(appDirectory, diagnostics);
        var discoveryService = new ShowDirectoryDiscoveryService();
        var layoutImporter = new ShowLayoutImporter();
        var sequenceDocumentStore = new JsonSequenceDocumentStore(Path.Combine(appDirectory, "exports"));

        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            settingsStore,
            diagnostics,
            showDirectoryDiscoveryService: discoveryService,
            showLayoutImporter: layoutImporter,
            sequenceDocumentStore: sequenceDocumentStore);
        shell.RunStartup();
        shell.NavigateToShowImportCommand.Execute(null);

        shell.ShowImport.ShowDirectory = showDirectory;
        shell.DiscoverShowDirectoryCommand.Execute(null);
        await shell.LastShowImportCommandTask!;

        shell.ShowImport.EffectTargetNamesCsv = "Roofline, Arch 1, Arch 2";
        shell.GenerateEffectPlanCommand.Execute(null);
        await shell.LastGenerateEffectPlanCommandTask!;

        return shell;
    }

    /// <summary>Default screen state (no explicit policy choice) still behaves exactly like Step 013's own original "never overwrite" contract.</summary>
    [Fact]
    public async Task ExportPolicyDefault_StillRejectsExistingFile_BackwardCompatible_NegativePath()
    {
        var shell = await BuildShellWithGeneratedPlanAsync();
        var outputPath = Path.Combine(_root, "existing.xsq");
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(outputPath, "not touched");
        shell.ShowImport.XsqOutputPath = outputPath;

        Assert.Equal(XsqExportPolicy.RejectIfExists, shell.ShowImport.ExportPolicy);

        shell.ExportXsqCommand.Execute(null);
        await shell.LastExportXsqCommandTask!;

        Assert.False(string.IsNullOrEmpty(shell.ShowImport.ExportErrorMessage));
        Assert.Null(shell.ShowImport.LastExport);
        Assert.Equal("not touched", await File.ReadAllTextAsync(outputPath));
    }

    /// <summary>The phase gate's exact demo workflow, but a real screen choice of AppendCollisionSuffix really changes the real on-disk outcome: a colliding output path never blocks the export, and the screen reports the real alternate path used.</summary>
    [Fact]
    public async Task Step015_Acceptance_Passes_CollisionSuffixEndToEnd()
    {
        var shell = await BuildShellWithGeneratedPlanAsync();
        var outputPath = Path.Combine(_root, "sequence.xsq");
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(outputPath, "pre-existing file, must survive");
        shell.ShowImport.XsqOutputPath = outputPath;
        shell.ShowImport.ExportPolicy = XsqExportPolicy.AppendCollisionSuffix;

        shell.ExportXsqCommand.Execute(null);
        await shell.LastExportXsqCommandTask!;

        Assert.Null(shell.ShowImport.ExportErrorMessage);
        Assert.NotNull(shell.ShowImport.LastExport);
        Assert.Equal(Path.Combine(_root, "sequence (1).xsq"), shell.ShowImport.LastExport!.Path);
        Assert.Equal("pre-existing file, must survive", await File.ReadAllTextAsync(outputPath));
    }

    /// <summary>A real screen choice of OverwriteWithBackup really backs up the real pre-existing file before replacing it, and the screen surfaces the real backup path.</summary>
    [Fact]
    public async Task Step015_Acceptance_Passes_OverwriteWithBackupEndToEnd()
    {
        var shell = await BuildShellWithGeneratedPlanAsync();
        var outputPath = Path.Combine(_root, "sequence.xsq");
        Directory.CreateDirectory(_root);
        const string originalContent = "pre-existing file, must be backed up";
        await File.WriteAllTextAsync(outputPath, originalContent);
        shell.ShowImport.XsqOutputPath = outputPath;
        shell.ShowImport.ExportPolicy = XsqExportPolicy.OverwriteWithBackup;

        shell.ExportXsqCommand.Execute(null);
        await shell.LastExportXsqCommandTask!;

        Assert.Null(shell.ShowImport.ExportErrorMessage);
        Assert.NotNull(shell.ShowImport.LastExport);
        Assert.Equal(outputPath, shell.ShowImport.LastExport!.Path);
        Assert.NotNull(shell.ShowImport.LastExport!.BackupPath);
        Assert.Equal(originalContent, await File.ReadAllTextAsync(shell.ShowImport.LastExport!.BackupPath!));
        Assert.NotEqual(originalContent, await File.ReadAllTextAsync(outputPath));
    }
}
