using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-02 Step 013: proves <see cref="ShellViewModel.ExportXsqCommand"/> runs the real
/// <see cref="XsqExporter"/> end to end from the same <see cref="ShowImportViewModel.Layout"/>/
/// <see cref="ShowImportViewModel.Plan"/> Steps 010-012 already populate, and that a malformed
/// request surfaces as an explicit screen error rather than an unhandled exception or a silently
/// partial file.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step013ShellWiringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step013-shell-" + Guid.NewGuid().ToString("N"));

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

    /// <summary>The phase gate's exact demo workflow continued: after Discover + Generate 3 Effects, Export XSQ writes a real, structurally valid file.</summary>
    [Fact]
    public async Task Step013_Acceptance_Passes_ExportXsqEndToEnd()
    {
        var shell = await BuildShellWithGeneratedPlanAsync();
        var outputPath = Path.Combine(_root, "exported.xsq");
        shell.ShowImport.XsqOutputPath = outputPath;
        shell.ShowImport.TargetXlightsVersion = "2026.03";

        shell.ExportXsqCommand.Execute(null);
        await shell.LastExportXsqCommandTask!;

        Assert.Null(shell.ShowImport.ExportErrorMessage);
        Assert.NotNull(shell.ShowImport.LastExport);
        Assert.Equal(outputPath, shell.ShowImport.LastExport!.Path);
        Assert.True(File.Exists(outputPath));

        var structural = new XsqStructuralReader().Read(outputPath);
        Assert.Equal(3, structural.ElementEffects.Count);
    }

    /// <summary>Exporting before Discover has ever populated a Layout fails safely with an explicit screen error, never an unhandled exception.</summary>
    [Fact]
    public async Task ExportWithoutALayoutYet_SetsScreenError_NegativePath()
    {
        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(navigator, () => Array.Empty<string>());
        shell.RunStartup();
        shell.NavigateToShowImportCommand.Execute(null);

        shell.ShowImport.XsqOutputPath = Path.Combine(_root, "no-layout.xsq");
        shell.ExportXsqCommand.Execute(null);
        await shell.LastExportXsqCommandTask!;

        Assert.False(string.IsNullOrEmpty(shell.ShowImport.ExportErrorMessage));
        Assert.Null(shell.ShowImport.LastExport);
    }

    /// <summary>Exporting after Discover but before Generate 3 Effects has ever populated a Plan fails safely with an explicit screen error.</summary>
    [Fact]
    public async Task ExportWithoutAPlanYet_SetsScreenError_NegativePath()
    {
        var showDirectory = Path.Combine(_root, "show");
        Directory.CreateDirectory(showDirectory);
        File.WriteAllText(Path.Combine(showDirectory, ShowDirectoryFileNames.RgbEffects), RgbEffectsXml);

        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            showDirectoryDiscoveryService: new ShowDirectoryDiscoveryService(),
            showLayoutImporter: new ShowLayoutImporter());
        shell.RunStartup();
        shell.NavigateToShowImportCommand.Execute(null);

        shell.ShowImport.ShowDirectory = showDirectory;
        shell.DiscoverShowDirectoryCommand.Execute(null);
        await shell.LastShowImportCommandTask!;

        shell.ShowImport.XsqOutputPath = Path.Combine(_root, "no-plan.xsq");
        shell.ExportXsqCommand.Execute(null);
        await shell.LastExportXsqCommandTask!;

        Assert.False(string.IsNullOrEmpty(shell.ShowImport.ExportErrorMessage));
        Assert.Null(shell.ShowImport.LastExport);
        Assert.False(File.Exists(Path.Combine(_root, "no-plan.xsq")));
    }

    /// <summary>An invalid output path (wrong extension) fails safely and never writes a file, surfaced as a screen error, not an unhandled exception.</summary>
    [Fact]
    public async Task ExportWithInvalidOutputPath_SetsScreenError_NegativePath()
    {
        var shell = await BuildShellWithGeneratedPlanAsync();
        var outputPath = Path.Combine(_root, "wrong-extension.xml");
        shell.ShowImport.XsqOutputPath = outputPath;

        shell.ExportXsqCommand.Execute(null);
        await shell.LastExportXsqCommandTask!;

        Assert.False(string.IsNullOrEmpty(shell.ShowImport.ExportErrorMessage));
        Assert.Null(shell.ShowImport.LastExport);
        Assert.False(File.Exists(outputPath));
    }

    /// <summary>Constructing without an explicit <see cref="IXsqExporter"/> (every earlier step's own existing call-site shape) keeps compiling and still exports for real via the real default instance.</summary>
    [Fact]
    public async Task ShellViewModel_WithoutExplicitXsqExporter_StillExportsForReal_BackwardCompatible()
    {
        var shell = await BuildShellWithGeneratedPlanAsync();
        var outputPath = Path.Combine(_root, "default-exporter.xsq");
        shell.ShowImport.XsqOutputPath = outputPath;

        shell.ExportXsqCommand.Execute(null);
        await shell.LastExportXsqCommandTask!;

        Assert.Null(shell.ShowImport.ExportErrorMessage);
        Assert.True(File.Exists(outputPath));
    }
}
