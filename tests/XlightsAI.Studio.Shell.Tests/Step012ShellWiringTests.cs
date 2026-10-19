using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-02 Step 012: proves <see cref="ShellViewModel.GenerateEffectPlanCommand"/> runs the real
/// <see cref="ThreeEffectPlanFactory"/>/<see cref="EffectRecipeRegistry"/>/<see cref="JsonSequenceDocumentStore"/>
/// end to end from the same <see cref="ShowImportViewModel.Layout"/> Step 010/011 already populate,
/// and that a malformed request surfaces as an explicit screen error rather than an unhandled
/// exception or a silently-saved invalid plan.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step012ShellWiringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step012-shell-" + Guid.NewGuid().ToString("N"));

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

    private ShellViewModel BuildShellWithImportedLayout(out string exportsDirectory)
    {
        var showDirectory = Path.Combine(_root, "show");
        Directory.CreateDirectory(showDirectory);
        File.WriteAllText(Path.Combine(showDirectory, ShowDirectoryFileNames.RgbEffects), RgbEffectsXml);

        var appDirectory = Path.Combine(_root, "app");
        exportsDirectory = Path.Combine(appDirectory, "exports");
        var diagnostics = new AppDiagnostics(Path.Combine(appDirectory, "logs"));
        var settingsStore = new SettingsStore(appDirectory, diagnostics);
        var discoveryService = new ShowDirectoryDiscoveryService();
        var layoutImporter = new ShowLayoutImporter();
        var sequenceDocumentStore = new JsonSequenceDocumentStore(exportsDirectory);

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

        return shell;
    }

    /// <summary>
    /// The phase gate's exact demo workflow: discover a layout with Roofline/Arch 1/Arch 2, then
    /// click Generate 3 Effects — the plan is created, saved, and bound back onto the same screen.
    /// </summary>
    [Fact]
    public async Task Step012_Acceptance_Passes_GenerateThreeEffectsEndToEnd()
    {
        var shell = BuildShellWithImportedLayout(out var exportsDirectory);

        shell.ShowImport.ShowDirectory = Path.Combine(_root, "show");
        shell.DiscoverShowDirectoryCommand.Execute(null);
        await shell.LastShowImportCommandTask!;
        Assert.True(shell.ShowImport.IsLoaded);

        shell.ShowImport.EffectTargetNamesCsv = "Roofline, Arch 1, Arch 2";
        shell.GenerateEffectPlanCommand.Execute(null);
        await shell.LastGenerateEffectPlanCommandTask!;

        Assert.Null(shell.ShowImport.PlanErrorMessage);
        Assert.NotNull(shell.ShowImport.Plan);
        Assert.Equal(3, shell.ShowImport.Plan!.Effects.Count);
        Assert.Equal(["Roofline", "Arch 1", "Arch 2"], shell.ShowImport.Plan.Effects.Select(e => e.Target.Value));

        Assert.NotNull(shell.ShowImport.PlanSavedPath);
        Assert.True(File.Exists(shell.ShowImport.PlanSavedPath));
        Assert.StartsWith(exportsDirectory, shell.ShowImport.PlanSavedPath!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Generating a plan before Discover has ever populated a Layout fails safely with an explicit screen error, never an unhandled exception.</summary>
    [Fact]
    public async Task GenerateWithoutALayoutYet_SetsScreenError_NegativePath()
    {
        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(navigator, () => Array.Empty<string>());
        shell.RunStartup();
        shell.NavigateToShowImportCommand.Execute(null);

        shell.ShowImport.EffectTargetNamesCsv = "Roofline, Arch 1, Arch 2";
        shell.GenerateEffectPlanCommand.Execute(null);
        await shell.LastGenerateEffectPlanCommandTask!;

        Assert.False(string.IsNullOrEmpty(shell.ShowImport.PlanErrorMessage));
        Assert.Null(shell.ShowImport.Plan);
    }

    /// <summary>A target naming a prop not present in the real imported layout fails safely and saves nothing.</summary>
    [Fact]
    public async Task GenerateWithUnknownTarget_SetsScreenError_SavesNothing_NegativePath()
    {
        var shell = BuildShellWithImportedLayout(out var exportsDirectory);

        shell.ShowImport.ShowDirectory = Path.Combine(_root, "show");
        shell.DiscoverShowDirectoryCommand.Execute(null);
        await shell.LastShowImportCommandTask!;

        shell.ShowImport.EffectTargetNamesCsv = "Roofline, Arch 1, Ghost-Prop";
        shell.GenerateEffectPlanCommand.Execute(null);
        await shell.LastGenerateEffectPlanCommandTask!;

        Assert.Contains("Ghost-Prop", shell.ShowImport.PlanErrorMessage);
        Assert.Null(shell.ShowImport.Plan);
        Assert.Null(shell.ShowImport.PlanSavedPath);
        Assert.False(Directory.Exists(exportsDirectory));
    }

    /// <summary>Constructing without an explicit <see cref="ISequenceDocumentStore"/> (every earlier step's own existing call-site shape) fails safely rather than silently pretending to save.</summary>
    [Fact]
    public async Task ShellViewModel_WithoutExplicitSequenceDocumentStore_FailsSafely_BackwardCompatible()
    {
        var showDirectory = Path.Combine(_root, "default-store-show");
        Directory.CreateDirectory(showDirectory);
        File.WriteAllText(Path.Combine(showDirectory, ShowDirectoryFileNames.RgbEffects), RgbEffectsXml);

        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            showDirectoryDiscoveryService: new ShowDirectoryDiscoveryService());
        shell.RunStartup();
        shell.NavigateToShowImportCommand.Execute(null);

        shell.ShowImport.ShowDirectory = showDirectory;
        shell.DiscoverShowDirectoryCommand.Execute(null);
        await shell.LastShowImportCommandTask!;

        shell.ShowImport.EffectTargetNamesCsv = "Roofline, Arch 1, Arch 2";
        shell.GenerateEffectPlanCommand.Execute(null);
        await shell.LastGenerateEffectPlanCommandTask!;

        Assert.False(string.IsNullOrEmpty(shell.ShowImport.PlanErrorMessage));
        Assert.Null(shell.ShowImport.Plan);
    }
}
