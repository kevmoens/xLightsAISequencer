using System.Net;
using System.Security.Cryptography;
using System.Text;
using XlightsAI.Studio.Distribution;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-15 Step 096 ("Add opt-in model asset installs, offline validation, logs and update
/// notices"). Proves the real vertical slice end to end through <see cref="ShellViewModel"/>: a real
/// HTTP download through the real <see cref="HttpReleaseAssetDownloader"/>/<see cref="AssetInstaller"/>
/// pipeline, gated on explicit <see cref="FirstRunViewModel.OptionalAssetConsent"/>, with the real
/// outcome recorded to a real on-disk <see cref="IAppDiagnostics"/> log (this step's own "...logs..."
/// requirement) — this step's own required acceptance, verbatim: "Core app works offline after assets
/// installed; missing models explained."
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step096ShellWiringTests : IDisposable
{
    private readonly string _rootDirectory =
        Path.Combine(Path.GetTempPath(), "xlai-step096-shell-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static int GetFreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task Step096_Acceptance_Passes()
    {
        var payload = Encoding.UTF8.GetBytes("real optional model asset bytes served over a real socket");
        var installDirectory = Path.Combine(_rootDirectory, "assets");
        var diagnosticsDirectory = Path.Combine(_rootDirectory, "diagnostics");
        var diagnostics = new AppDiagnostics(diagnosticsDirectory);

        using var listener = new HttpListener();
        var prefix = $"http://127.0.0.1:{GetFreeTcpPort()}/";
        listener.Prefixes.Add(prefix);
        listener.Start();
        var serveTask = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            context.Response.ContentLength64 = payload.Length;
            await context.Response.OutputStream.WriteAsync(payload);
            context.Response.OutputStream.Close();
        });

        var asset = new DownloadableAsset(
            Id: "optional-stem-model.onnx",
            Version: "1.0.0",
            Url: new Uri(prefix),
            Sha256: Sha256Hex(payload),
            SizeBytes: payload.Length,
            LicenseId: "Apache-2.0",
            Required: false);

        using var realDownloader = new HttpReleaseAssetDownloader();
        var realInstaller = new AssetInstaller(installDirectory, realDownloader);

        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            diagnostics: diagnostics,
            assetInstaller: realInstaller,
            optionalModelAsset: asset);
        vm.RunStartup();

        // Required acceptance, verbatim, first half: "optionally download model with consent."
        vm.FirstRun.OptionalAssetConsent = true;
        Assert.True(vm.InstallOptionalAssetCommand.CanExecute(null));
        vm.InstallOptionalAssetCommand.Execute(null);
        await vm.LastInstallOptionalAssetCommandTask!;
        await serveTask;
        listener.Stop();

        Assert.Contains(asset.Id, vm.FirstRun.OptionalAssetMessage);
        Assert.True(File.Exists(realInstaller.GetInstalledPath(asset)));

        // This step's own "...logs..." requirement: the real outcome was recorded to the real,
        // on-disk diagnostics log, not silently dropped.
        Assert.Contains(diagnostics.RecentEvents, e => e.Code == "optional-asset-install" && e.Message.Contains(asset.Id));

        // Required acceptance, verbatim, second half: "Core app works offline after assets installed."
        // The real server is now stopped; re-running the install command must still succeed with zero
        // network access, because the asset is already verified on disk.
        vm.InstallOptionalAssetCommand.Execute(null);
        await vm.LastInstallOptionalAssetCommandTask!;
        Assert.Contains(asset.Id, vm.FirstRun.OptionalAssetMessage);
    }

    [Fact]
    public async Task InstallOptionalAssetCommand_NoAssetDeclared_ExplainsMissingModel_NegativePath()
    {
        // Required acceptance, verbatim, second half: "...missing models explained." No
        // optionalModelAsset/assetInstaller configured at all (the real current composition-root
        // state, same disclosed deferral as Step 095 Section 6 item 3) must still explain itself
        // rather than crash or silently do nothing observable.
        var diagnostics = new AppDiagnostics(Path.Combine(_rootDirectory, "diagnostics"));
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>(), diagnostics: diagnostics);
        vm.RunStartup();

        vm.FirstRun.OptionalAssetConsent = true;
        vm.InstallOptionalAssetCommand.Execute(null);
        await vm.LastInstallOptionalAssetCommandTask!;

        Assert.Contains("No optional model asset is declared", vm.FirstRun.OptionalAssetMessage);
        Assert.Contains(diagnostics.RecentEvents, e => e.Code == "optional-asset-install");
    }

    [Fact]
    public async Task InstallOptionalAssetCommand_ConsentNotGiven_NeverCallsInstaller_ExplainsSkip_NegativePath()
    {
        var diagnostics = new AppDiagnostics(Path.Combine(_rootDirectory, "diagnostics"));
        var asset = new DownloadableAsset(
            "optional.onnx", "1.0.0", new Uri("https://example.invalid/optional.onnx"),
            new string('0', 64), 10, "MIT", Required: false);
        var neverCalledInstaller = new NeverCalledAssetInstaller();

        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            diagnostics: diagnostics,
            assetInstaller: neverCalledInstaller,
            optionalModelAsset: asset);
        vm.RunStartup();

        // OptionalAssetConsent defaults to false -- never explicitly set here.
        vm.InstallOptionalAssetCommand.Execute(null);
        await vm.LastInstallOptionalAssetCommandTask!;

        Assert.Equal(0, neverCalledInstaller.CallCount);
        Assert.Contains("not installed without consent", vm.FirstRun.OptionalAssetMessage);
    }

    [Fact]
    public async Task InstallOptionalAssetCommand_TamperedDownload_IsRejectedAndLogged_NegativePath()
    {
        // Phase test matrix row 3, "Model download hash tampering rejected" -- driven through the
        // real ShellViewModel/FirstRunViewModel wiring, not just the isolated Distribution-layer test.
        var payload = Encoding.UTF8.GetBytes("declared bytes");
        var tamperedServedPayload = Encoding.UTF8.GetBytes("different, tampered bytes actually served");
        var diagnostics = new AppDiagnostics(Path.Combine(_rootDirectory, "diagnostics"));

        using var listener = new HttpListener();
        var prefix = $"http://127.0.0.1:{GetFreeTcpPort()}/";
        listener.Prefixes.Add(prefix);
        listener.Start();
        var serveTask = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            context.Response.ContentLength64 = tamperedServedPayload.Length;
            await context.Response.OutputStream.WriteAsync(tamperedServedPayload);
            context.Response.OutputStream.Close();
        });

        var asset = new DownloadableAsset(
            "tampered.onnx", "1.0.0", new Uri(prefix), Sha256Hex(payload), payload.Length, "MIT", Required: false);

        using var realDownloader = new HttpReleaseAssetDownloader();
        var realInstaller = new AssetInstaller(Path.Combine(_rootDirectory, "assets"), realDownloader);

        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            diagnostics: diagnostics,
            assetInstaller: realInstaller,
            optionalModelAsset: asset);
        vm.RunStartup();

        vm.FirstRun.OptionalAssetConsent = true;
        vm.InstallOptionalAssetCommand.Execute(null);
        await vm.LastInstallOptionalAssetCommandTask!;
        await serveTask;
        listener.Stop();

        Assert.Contains("integrity verification", vm.FirstRun.OptionalAssetMessage);
        Assert.False(File.Exists(realInstaller.GetInstalledPath(asset)));
        Assert.Contains(diagnostics.RecentEvents, e => e.Code == "optional-asset-install" && e.Level == DiagnosticEventLevel.Warning);
    }

    private sealed class NeverCalledAssetInstaller : IAssetInstaller
    {
        public int CallCount { get; private set; }

        public Task<string> EnsureInstalledAsync(DownloadableAsset asset, IProgress<double>? progress, CancellationToken ct)
        {
            CallCount++;
            throw new InvalidOperationException("Installer must not be invoked without user consent.");
        }
    }
}
