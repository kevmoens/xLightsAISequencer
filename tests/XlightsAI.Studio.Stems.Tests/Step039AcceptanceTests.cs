using System.Net;
using System.Security.Cryptography;
using System.Text;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Stems.Tests;

/// <summary>
/// PHASE-06 Step 039 — "Implement model asset manifest, verified download, consent and checksum
/// validation." Required acceptance (verbatim): "Tampered models rejected and clean offline restart
/// supported." Failure/edge case (verbatim, shared across this phase's steps): "Missing or incompatible
/// model, cancellation and offline mode must yield a recoverable state."
/// </summary>
public sealed class Step039AcceptanceTests : IDisposable
{
    private readonly string _cacheDirectory;

    public Step039AcceptanceTests()
    {
        _cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step039-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
    }

    private static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static ModelAsset FixtureAsset(byte[] payload, string id = "fixture-model") => new(
        Id: id,
        Version: "1.0.0-fixture",
        Source: new Uri("https://example.invalid/models/fixture.bin"),
        Sha256: Sha256Hex(payload),
        LicenseId: "MIT",
        InferenceBackend: "Fixture ONNX Runtime (CPU)");

    /// <summary>Deterministic in-memory stand-in for a real remote server; never touches the network.</summary>
    private sealed class FakeModelAssetDownloader : IModelAssetDownloader
    {
        private readonly byte[]? _payload;
        private readonly Exception? _failure;
        public int CallCount { get; private set; }

        private FakeModelAssetDownloader(byte[]? payload, Exception? failure)
        {
            _payload = payload;
            _failure = failure;
        }

        public static FakeModelAssetDownloader ReturningBytes(byte[] payload) => new(payload, null);

        public static FakeModelAssetDownloader ThrowingOnCall(Exception failure) => new(null, failure);

        public static FakeModelAssetDownloader NeverCalled() => new(null, new InvalidOperationException(
            "Downloader must not be invoked when an already-verified local copy exists (offline mode)."));

        public async Task DownloadAsync(Uri source, string destinationPath, CancellationToken ct)
        {
            CallCount++;
            if (_failure is not null)
            {
                throw _failure;
            }

            await File.WriteAllBytesAsync(destinationPath, _payload!, ct).ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task Step039_Acceptance_Passes_ValidDownloadVerifiedAndCached()
    {
        var payload = Encoding.UTF8.GetBytes("real separated-stem model weights (fixture)");
        var asset = FixtureAsset(payload);
        var downloader = FakeModelAssetDownloader.ReturningBytes(payload);
        var manager = new ModelAssetManager(_cacheDirectory, downloader);

        var path = await manager.EnsureVerifiedAsync(asset, userConsent: true, CancellationToken.None);

        Assert.True(File.Exists(path));
        Assert.Equal(Sha256Hex(await File.ReadAllBytesAsync(path)), asset.Sha256);
        Assert.Equal(1, downloader.CallCount);

        // Idempotent re-run: already verified locally, so the downloader must not be invoked again.
        var secondPath = await manager.EnsureVerifiedAsync(asset, userConsent: false, CancellationToken.None);
        Assert.Equal(path, secondPath);
        Assert.Equal(1, downloader.CallCount);
    }

    [Fact]
    public async Task Step039_Acceptance_Passes_TamperedHashRejected_NegativePath()
    {
        var declaredPayload = Encoding.UTF8.GetBytes("the real, expected model weights");
        var tamperedPayload = Encoding.UTF8.GetBytes("a tampered/substituted payload");
        var asset = FixtureAsset(declaredPayload);
        var downloader = FakeModelAssetDownloader.ReturningBytes(tamperedPayload);
        var manager = new ModelAssetManager(_cacheDirectory, downloader);

        var ex = await Assert.ThrowsAsync<ModelAssetHashMismatchException>(
            () => manager.EnsureVerifiedAsync(asset, userConsent: true, CancellationToken.None));

        Assert.Contains("checksum", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(manager.GetVerifiedPath(asset)));
        Assert.Empty(Directory.Exists(_cacheDirectory)
            ? Directory.GetFiles(_cacheDirectory)
            : Array.Empty<string>());
    }

    [Fact]
    public async Task Step039_Acceptance_Passes_OfflinePreProvisionedAssetSkipsDownload()
    {
        var payload = Encoding.UTF8.GetBytes("an offline pre-provisioned model file, placed by an installer");
        var asset = FixtureAsset(payload);
        var downloader = FakeModelAssetDownloader.NeverCalled();
        var manager = new ModelAssetManager(_cacheDirectory, downloader);

        Directory.CreateDirectory(_cacheDirectory);
        var preProvisionedPath = manager.GetVerifiedPath(asset);
        await File.WriteAllBytesAsync(preProvisionedPath, payload);

        // No network available and no consent given -- still succeeds from the pre-provisioned file,
        // per the phase gate demo: "install model through app (or use offline pre-provisioned asset)".
        var path = await manager.EnsureVerifiedAsync(asset, userConsent: false, CancellationToken.None);

        Assert.Equal(preProvisionedPath, path);
        Assert.Equal(0, downloader.CallCount);
    }

    [Fact]
    public async Task Step039_Acceptance_Passes_InterruptedDownloadRecoversCleanly()
    {
        var payload = Encoding.UTF8.GetBytes("recoverable download content");
        var asset = FixtureAsset(payload);
        var failingDownloader = FakeModelAssetDownloader.ThrowingOnCall(new IOException("connection reset (simulated)"));
        var failingManager = new ModelAssetManager(_cacheDirectory, failingDownloader);

        await Assert.ThrowsAsync<IOException>(
            () => failingManager.EnsureVerifiedAsync(asset, userConsent: true, CancellationToken.None));

        Assert.False(File.Exists(failingManager.GetVerifiedPath(asset)));
        Assert.Empty(Directory.GetFiles(_cacheDirectory)); // no partial/temp file left behind

        // A clean, offline-capable restart with a working downloader must succeed afterwards.
        var workingDownloader = FakeModelAssetDownloader.ReturningBytes(payload);
        var workingManager = new ModelAssetManager(_cacheDirectory, workingDownloader);
        var path = await workingManager.EnsureVerifiedAsync(asset, userConsent: true, CancellationToken.None);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task EnsureVerifiedAsync_ConsentFalseNoLocalCopy_ThrowsModelAssetConsentRequiredException_NegativePath()
    {
        var payload = Encoding.UTF8.GetBytes("payload never actually fetched");
        var asset = FixtureAsset(payload);
        var downloader = FakeModelAssetDownloader.NeverCalled();
        var manager = new ModelAssetManager(_cacheDirectory, downloader);

        await Assert.ThrowsAsync<ModelAssetConsentRequiredException>(
            () => manager.EnsureVerifiedAsync(asset, userConsent: false, CancellationToken.None));

        Assert.Equal(0, downloader.CallCount);
    }

    [Fact]
    public async Task EnsureVerifiedAsync_AlreadyCancelled_ThrowsOperationCanceledException_NegativePath()
    {
        var payload = Encoding.UTF8.GetBytes("irrelevant");
        var asset = FixtureAsset(payload);
        var downloader = FakeModelAssetDownloader.NeverCalled();
        var manager = new ModelAssetManager(_cacheDirectory, downloader);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => manager.EnsureVerifiedAsync(asset, userConsent: true, cts.Token));

        Assert.Equal(0, downloader.CallCount);
    }

    [Fact]
    public async Task EnsureVerifiedAsync_NonHttpsSource_ThrowsModelAssetInvalidException_NegativePath()
    {
        var payload = Encoding.UTF8.GetBytes("irrelevant");
        var insecureAsset = new ModelAsset(
            Id: "insecure-model",
            Version: "1.0.0",
            Source: new Uri("http://example.invalid/models/insecure.bin"),
            Sha256: Sha256Hex(payload),
            LicenseId: "MIT",
            InferenceBackend: "Fixture ONNX Runtime (CPU)");
        var downloader = FakeModelAssetDownloader.NeverCalled();
        var manager = new ModelAssetManager(_cacheDirectory, downloader);

        var ex = await Assert.ThrowsAsync<ModelAssetInvalidException>(
            () => manager.EnsureVerifiedAsync(insecureAsset, userConsent: true, CancellationToken.None));

        Assert.Contains("HTTPS", ex.Message);
    }

    [Fact]
    public async Task EnsureVerifiedAsync_NullAsset_ThrowsArgumentNullException()
    {
        var manager = new ModelAssetManager(_cacheDirectory, FakeModelAssetDownloader.NeverCalled());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => manager.EnsureVerifiedAsync(null!, userConsent: true, CancellationToken.None));
    }

    // --- ModelAssetManifest / JsonModelAssetManifestStore -------------------------------------------

    [Fact]
    public async Task ManifestStore_SaveAsync_ThenLoadAsync_RoundTrips()
    {
        var payload = Encoding.UTF8.GetBytes("manifest round-trip fixture");
        var manifest = new ModelAssetManifest(
            Guid.NewGuid(),
            CreatedAtTicks: 638_700_000_000_000_000L,
            Assets: new[] { FixtureAsset(payload, "model-a"), FixtureAsset(payload, "model-b") with { Version = "2.0.0" } });

        var store = new JsonModelAssetManifestStore(_cacheDirectory);
        var path = await store.SaveAsync(manifest, CancellationToken.None);

        Assert.True(File.Exists(path));

        var reloaded = await store.LoadAsync(path, CancellationToken.None);

        Assert.Equal(manifest.Id, reloaded.Id);
        Assert.Equal(manifest.CreatedAtTicks, reloaded.CreatedAtTicks);
        Assert.Equal(2, reloaded.Assets.Count);
        Assert.Contains(reloaded.Assets, a => a.Id == "model-a" && a.Source == manifest.Assets[0].Source);
    }

    [Fact]
    public async Task ManifestStore_SaveAsync_DuplicateIdVersionPair_ThrowsModelAssetManifestInvalidException_NegativePath()
    {
        var payload = Encoding.UTF8.GetBytes("duplicate fixture");
        var asset = FixtureAsset(payload, "dup-model");
        var manifest = new ModelAssetManifest(Guid.NewGuid(), 0L, new[] { asset, asset });

        var store = new JsonModelAssetManifestStore(_cacheDirectory);

        var ex = await Assert.ThrowsAsync<ModelAssetManifestInvalidException>(
            () => store.SaveAsync(manifest, CancellationToken.None));
        Assert.Contains("more than once", ex.Message);
    }

    [Fact]
    public async Task ManifestStore_SaveAsync_InvalidAssetEntry_ThrowsModelAssetManifestInvalidException_NegativePath()
    {
        var invalidAsset = new ModelAsset(
            Id: "",
            Version: "1.0.0",
            Source: new Uri("https://example.invalid/x.bin"),
            Sha256: Sha256Hex(Encoding.UTF8.GetBytes("x")),
            LicenseId: "MIT",
            InferenceBackend: "Fixture ONNX Runtime (CPU)");
        var manifest = new ModelAssetManifest(Guid.NewGuid(), 0L, new[] { invalidAsset });

        var store = new JsonModelAssetManifestStore(_cacheDirectory);

        await Assert.ThrowsAsync<ModelAssetManifestInvalidException>(
            () => store.SaveAsync(manifest, CancellationToken.None));
    }

    [Fact]
    public async Task ManifestStore_LoadAsync_MissingFile_ThrowsFileNotFoundException_NegativePath()
    {
        var store = new JsonModelAssetManifestStore(_cacheDirectory);
        var missingPath = Path.Combine(_cacheDirectory, "model-asset-manifest-does-not-exist.json");

        await Assert.ThrowsAsync<FileNotFoundException>(() => store.LoadAsync(missingPath, CancellationToken.None));
    }

    [Fact]
    public async Task ManifestStore_LoadAsync_CorruptFile_ThrowsModelAssetManifestCorruptException_NegativePath()
    {
        Directory.CreateDirectory(_cacheDirectory);
        var corruptPath = Path.Combine(_cacheDirectory, "model-asset-manifest-corrupt.json");
        await File.WriteAllTextAsync(corruptPath, "{ not valid json");

        var store = new JsonModelAssetManifestStore(_cacheDirectory);

        await Assert.ThrowsAsync<ModelAssetManifestCorruptException>(
            () => store.LoadAsync(corruptPath, CancellationToken.None));
    }

    [Fact]
    public async Task ManifestStore_SaveAsync_AlreadyCancelled_ThrowsOperationCanceledException_NegativePath()
    {
        var manifest = new ModelAssetManifest(Guid.NewGuid(), 0L, Array.Empty<ModelAsset>());
        var store = new JsonModelAssetManifestStore(_cacheDirectory);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => store.SaveAsync(manifest, cts.Token));
        Assert.False(File.Exists(store.GetPath(manifest.Id)));
    }

    // --- HttpModelAssetDownloader: real sockets, real loopback server, no mocking -------------------

    [Fact]
    public async Task HttpModelAssetDownloader_RealLoopbackServer_DownloadsBytesCorrectly()
    {
        var payload = Encoding.UTF8.GetBytes("bytes served over a real local socket, not mocked");

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

        using var downloader = new HttpModelAssetDownloader();
        var destinationPath = Path.Combine(_cacheDirectory, "downloaded.bin");
        Directory.CreateDirectory(_cacheDirectory);

        await downloader.DownloadAsync(new Uri(prefix), destinationPath, CancellationToken.None);
        await serveTask;
        listener.Stop();

        Assert.Equal(payload, await File.ReadAllBytesAsync(destinationPath));
    }

    [Fact]
    public async Task HttpModelAssetDownloader_ServerReturns404_ThrowsHttpRequestException_NegativePath()
    {
        using var listener = new HttpListener();
        var prefix = $"http://127.0.0.1:{GetFreeTcpPort()}/";
        listener.Prefixes.Add(prefix);
        listener.Start();
        var serveTask = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            context.Response.StatusCode = 404;
            context.Response.OutputStream.Close();
        });

        using var downloader = new HttpModelAssetDownloader();
        var destinationPath = Path.Combine(_cacheDirectory, "never-written.bin");
        Directory.CreateDirectory(_cacheDirectory);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => downloader.DownloadAsync(new Uri(prefix), destinationPath, CancellationToken.None));
        await serveTask;
        listener.Stop();

        Assert.False(File.Exists(destinationPath));
    }

    private static int GetFreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
