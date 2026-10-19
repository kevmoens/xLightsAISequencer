using XlightsAI.Studio.Distribution;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-15 Step 096 ("Add opt-in model asset installs, offline validation, logs and update
/// notices"). Isolated tests of <see cref="FirstRunViewModel.InstallOptionalAssetAsync"/> — the
/// "opt-in model asset installs" half of this step's own title, verbatim, with
/// "missing models explained" (required acceptance) and a recoverable state for every required
/// failure/edge case.
/// </summary>
public sealed class Step096FirstRunViewModelTests
{
    private static DownloadableAsset FixtureAsset(string sha256 = "aa", bool required = false) => new(
        Id: "optional-stem-model.onnx",
        Version: "1.0.0",
        Url: new Uri("https://example.invalid/models/optional-stem-model.onnx"),
        Sha256: sha256,
        SizeBytes: 1024,
        LicenseId: "Apache-2.0",
        Required: required);

    private sealed class FixtureAssetInstaller : IAssetInstaller
    {
        private readonly Func<DownloadableAsset, Task<string>> _behavior;
        public int CallCount { get; private set; }

        private FixtureAssetInstaller(Func<DownloadableAsset, Task<string>> behavior)
        {
            _behavior = behavior;
        }

        public static FixtureAssetInstaller Succeeding(string installedPath) =>
            new(_ => Task.FromResult(installedPath));

        public static FixtureAssetInstaller Throwing(Exception exception) =>
            new(_ => Task.FromException<string>(exception));

        public static FixtureAssetInstaller NeverCalled() => new(_ => Task.FromException<string>(
            new InvalidOperationException("Installer must not be invoked without user consent.")));

        public Task<string> EnsureInstalledAsync(DownloadableAsset asset, IProgress<double>? progress, CancellationToken ct)
        {
            CallCount++;
            ct.ThrowIfCancellationRequested();
            return _behavior(asset);
        }
    }

    [Fact]
    public async Task InstallOptionalAssetAsync_NoAssetDeclared_ExplainsAndReturnsFalse_NeverCallsInstaller()
    {
        var viewModel = new FirstRunViewModel();
        var installer = FixtureAssetInstaller.NeverCalled();

        var result = await viewModel.InstallOptionalAssetAsync(asset: null, userConsent: true, installer);

        Assert.False(result);
        Assert.Equal(0, installer.CallCount);
        Assert.Contains("No optional model asset is declared", viewModel.OptionalAssetMessage);
    }

    [Fact]
    public async Task InstallOptionalAssetAsync_ConsentDeclined_ExplainsAndReturnsFalse_NeverCallsInstaller_NegativePath()
    {
        var viewModel = new FirstRunViewModel();
        var installer = FixtureAssetInstaller.NeverCalled();
        var asset = FixtureAsset();

        var result = await viewModel.InstallOptionalAssetAsync(asset, userConsent: false, installer);

        Assert.False(result);
        Assert.Equal(0, installer.CallCount);
        Assert.Contains(asset.Id, viewModel.OptionalAssetMessage);
        Assert.Contains("not installed without consent", viewModel.OptionalAssetMessage);
    }

    [Fact]
    public async Task InstallOptionalAssetAsync_ConsentGiven_InstallsAndExplainsSuccess()
    {
        var viewModel = new FirstRunViewModel();
        var asset = FixtureAsset();
        var installer = FixtureAssetInstaller.Succeeding(@"C:\fake\installed\path.onnx");

        var result = await viewModel.InstallOptionalAssetAsync(asset, userConsent: true, installer);

        Assert.True(result);
        Assert.Equal(1, installer.CallCount);
        Assert.Contains(asset.Id, viewModel.OptionalAssetMessage);
        Assert.Contains(@"C:\fake\installed\path.onnx", viewModel.OptionalAssetMessage);
        Assert.False(viewModel.IsInstallingOptionalAsset);
    }

    [Fact]
    public async Task InstallOptionalAssetAsync_HashMismatch_ExplainsRecoverableFailure_NegativePath()
    {
        // Phase test matrix row 3, "Model download hash tampering rejected" + this step's own required
        // failure/edge case verbatim: "...must yield a recoverable state."
        var viewModel = new FirstRunViewModel();
        var asset = FixtureAsset();
        var installer = FixtureAssetInstaller.Throwing(
            new AssetInstallationHashMismatchException(asset.Id, "expected", "actual"));

        var result = await viewModel.InstallOptionalAssetAsync(asset, userConsent: true, installer);

        Assert.False(result);
        Assert.False(viewModel.IsInstallingOptionalAsset);
        Assert.Contains("integrity verification", viewModel.OptionalAssetMessage);
    }

    [Fact]
    public async Task InstallOptionalAssetAsync_OfflineOrNetworkFailure_ExplainsRecoverableFailure_NegativePath()
    {
        // This step's own required failure/edge case, verbatim: "...offline mode must yield a
        // recoverable state."
        var viewModel = new FirstRunViewModel();
        var asset = FixtureAsset();
        var installer = FixtureAssetInstaller.Throwing(
            new AssetInstallationFailedException(asset.Id, "could not be downloaded (offline)", null));

        var result = await viewModel.InstallOptionalAssetAsync(asset, userConsent: true, installer);

        Assert.False(result);
        Assert.False(viewModel.IsInstallingOptionalAsset);
        Assert.Contains("could not be downloaded", viewModel.OptionalAssetMessage);

        // Recoverable: a subsequent attempt (e.g. connectivity restored) can still succeed.
        var workingInstaller = FixtureAssetInstaller.Succeeding(@"C:\fake\retry\path.onnx");
        var retryResult = await viewModel.InstallOptionalAssetAsync(asset, userConsent: true, workingInstaller);
        Assert.True(retryResult);
    }

    [Fact]
    public async Task InstallOptionalAssetAsync_Cancellation_ExplainsRecoverableFailure_NegativePath()
    {
        var viewModel = new FirstRunViewModel();
        var asset = FixtureAsset();
        var installer = FixtureAssetInstaller.Throwing(new OperationCanceledException());

        var result = await viewModel.InstallOptionalAssetAsync(asset, userConsent: true, installer);

        Assert.False(result);
        Assert.False(viewModel.IsInstallingOptionalAsset);
        Assert.Contains("cancelled", viewModel.OptionalAssetMessage);
    }

    [Fact]
    public async Task InstallOptionalAssetAsync_NullInstaller_Throws()
    {
        var viewModel = new FirstRunViewModel();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => viewModel.InstallOptionalAssetAsync(FixtureAsset(), true, null!));
    }
}
