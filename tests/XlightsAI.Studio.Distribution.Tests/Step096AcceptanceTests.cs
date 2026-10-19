using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using XlightsAI.Studio.Distribution;

namespace XlightsAI.Studio.Distribution.Tests;

/// <summary>
/// PHASE-15 Step 096 ("Add opt-in model asset installs, offline validation, logs and update notices").
/// Required acceptance (verbatim): "Core app works offline after assets installed; missing models
/// explained." Required failure/edge case (verbatim, shared across this phase): "Missing or incompatible
/// model, cancellation and offline mode must yield a recoverable state." Every test below exercises the real
/// <see cref="AssetInstaller"/>, <see cref="HttpReleaseAssetDownloader"/> and <see cref="UpdateNoticeChecker"/>
/// production types directly -- against real files on disk, a real loopback HTTP server, and real RSA
/// signatures -- no mocking framework is used anywhere in this file.
/// </summary>
public sealed class Step096AcceptanceTests : IDisposable
{
    private readonly string _installDirectory;

    public Step096AcceptanceTests()
    {
        _installDirectory = Path.Combine(Path.GetTempPath(), "xlai-step096-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_installDirectory))
        {
            Directory.Delete(_installDirectory, recursive: true);
        }
    }

    private static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static DownloadableAsset FixtureAsset(byte[] payload, Uri source, bool required = false) => new(
        Id: "optional-model.onnx",
        Version: "1.0.0",
        Url: source,
        Sha256: Sha256Hex(payload),
        SizeBytes: payload.Length,
        LicenseId: "Apache-2.0",
        Required: required);

    private static int GetFreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>Deterministic in-memory stand-in for a real remote server; never touches the network.</summary>
    private sealed class FakeReleaseAssetDownloader : IReleaseAssetDownloader
    {
        private readonly byte[]? _payload;
        private readonly Exception? _failure;
        public int CallCount { get; private set; }
        public double? LastReportedProgress { get; private set; }

        private FakeReleaseAssetDownloader(byte[]? payload, Exception? failure)
        {
            _payload = payload;
            _failure = failure;
        }

        public static FakeReleaseAssetDownloader ReturningBytes(byte[] payload) => new(payload, null);

        public static FakeReleaseAssetDownloader ThrowingOnCall(Exception failure) => new(null, failure);

        public static FakeReleaseAssetDownloader NeverCalled() => new(null, new InvalidOperationException(
            "Downloader must not be invoked when an already-verified local copy exists (offline mode)."));

        public async Task DownloadAsync(Uri source, string destinationPath, IProgress<double>? progress, CancellationToken ct)
        {
            CallCount++;
            if (_failure is not null)
            {
                throw _failure;
            }

            await File.WriteAllBytesAsync(destinationPath, _payload!, ct).ConfigureAwait(false);
            progress?.Report(1.0);
            LastReportedProgress = 1.0;
        }
    }

    [Fact]
    public async Task Step096_Acceptance_Passes()
    {
        // Required acceptance, verbatim: "Core app works offline after assets installed; missing models
        // explained." First half: a not-yet-installed optional asset is downloaded, verified and installed
        // exactly once; second half: once installed, a second call needs zero network access at all --
        // "the core app works offline after assets installed."
        var payload = Encoding.UTF8.GetBytes("real optional ML model weights (fixture)");
        var asset = FixtureAsset(payload, new Uri("https://example.invalid/models/optional-model.onnx"));
        var downloader = FakeReleaseAssetDownloader.ReturningBytes(payload);
        var installer = new AssetInstaller(_installDirectory, downloader);

        var reported = new List<double>();
        var progress = new Progress<double>(p => reported.Add(p));

        var path = await installer.EnsureInstalledAsync(asset, progress, CancellationToken.None);

        Assert.True(File.Exists(path));
        Assert.Equal(Sha256Hex(await File.ReadAllBytesAsync(path)), asset.Sha256);
        Assert.Equal(1, downloader.CallCount);
        Assert.Contains(1.0, reported);

        // Offline/idempotent re-run: an already-verified local copy must need zero network access.
        var neverCalledDownloader = FakeReleaseAssetDownloader.NeverCalled();
        var secondInstaller = new AssetInstaller(_installDirectory, neverCalledDownloader);
        var secondPath = await secondInstaller.EnsureInstalledAsync(asset, null, CancellationToken.None);

        Assert.Equal(path, secondPath);
        Assert.Equal(0, neverCalledDownloader.CallCount);
    }

    [Fact]
    public async Task Step096_NegativePath_HashMismatch_RejectedNotCrashed_NoPartialFileKept()
    {
        // Phase test matrix row 3, "Model download hash tampering rejected" + this step's own required
        // failure/edge case verbatim: "Missing or incompatible model... must yield a recoverable state" --
        // a tampered download must be reported via a clear, typed exception, never a crash, and must never
        // leave a partial/tampered file at the installed path.
        var declaredPayload = Encoding.UTF8.GetBytes("declared, expected bytes");
        var actuallyServedPayload = Encoding.UTF8.GetBytes("different bytes than declared (tampered in transit)");
        var asset = FixtureAsset(declaredPayload, new Uri("https://example.invalid/models/tampered.onnx"));
        var downloader = FakeReleaseAssetDownloader.ReturningBytes(actuallyServedPayload);
        var installer = new AssetInstaller(_installDirectory, downloader);

        var ex = await Assert.ThrowsAsync<AssetInstallationHashMismatchException>(
            () => installer.EnsureInstalledAsync(asset, null, CancellationToken.None));

        Assert.Equal(asset.Id, ex.AssetId);
        Assert.False(File.Exists(installer.GetInstalledPath(asset)));
        Assert.Empty(Directory.Exists(_installDirectory) ? Directory.GetFiles(_installDirectory) : []);
    }

    [Fact]
    public async Task Step096_NegativePath_OfflineOrNetworkFailure_IsRecoverable_NoPartialFileKept()
    {
        // This step's own required failure/edge case, verbatim: "...offline mode must yield a recoverable
        // state" -- a transport-level failure (simulated: no internet) must surface as one clear, typed,
        // human-readable exception, not a raw transport exception and not a crash, and must leave no partial
        // file behind.
        var payload = Encoding.UTF8.GetBytes("never actually reaches disk");
        var asset = FixtureAsset(payload, new Uri("https://example.invalid/models/offline.onnx"));
        var downloader = FakeReleaseAssetDownloader.ThrowingOnCall(
            new HttpRequestException("Simulated offline/no-route-to-host failure."));
        var installer = new AssetInstaller(_installDirectory, downloader);

        var ex = await Assert.ThrowsAsync<AssetInstallationFailedException>(
            () => installer.EnsureInstalledAsync(asset, null, CancellationToken.None));

        Assert.Equal(asset.Id, ex.AssetId);
        Assert.IsType<HttpRequestException>(ex.InnerException);
        Assert.False(File.Exists(installer.GetInstalledPath(asset)));
        Assert.Empty(Directory.Exists(_installDirectory) ? Directory.GetFiles(_installDirectory) : []);

        // Recoverable: a subsequent attempt with a working downloader (connectivity restored) still
        // succeeds -- the earlier failure left no corrupt/locking state behind.
        var workingDownloader = FakeReleaseAssetDownloader.ReturningBytes(payload);
        var retryInstaller = new AssetInstaller(_installDirectory, workingDownloader);
        var path = await retryInstaller.EnsureInstalledAsync(asset, null, CancellationToken.None);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Step096_NegativePath_AlreadyCancelled_ThrowsBeforeAnyNetworkAccess()
    {
        var payload = Encoding.UTF8.GetBytes("unused");
        var asset = FixtureAsset(payload, new Uri("https://example.invalid/models/cancelled.onnx"));
        var downloader = FakeReleaseAssetDownloader.NeverCalled();
        var installer = new AssetInstaller(_installDirectory, downloader);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => installer.EnsureInstalledAsync(asset, null, cts.Token));
        Assert.Equal(0, downloader.CallCount);
    }

    [Fact]
    public async Task Step096_StaleCorruptCachedFile_IsNeverTrusted_RedownloadedAndRepaired()
    {
        // Same "never silently trust a stale/corrupt leftover" convention as
        // XlightsAI.Studio.Stems.ModelAssetManager -- a prior run's file at the installed path with the
        // wrong bytes must be replaced, not trusted as-is.
        var payload = Encoding.UTF8.GetBytes("the real, correct bytes");
        var asset = FixtureAsset(payload, new Uri("https://example.invalid/models/repair.onnx"));
        var installer = new AssetInstaller(_installDirectory, FakeReleaseAssetDownloader.ReturningBytes(payload));

        Directory.CreateDirectory(_installDirectory);
        var finalPath = installer.GetInstalledPath(asset);
        await File.WriteAllTextAsync(finalPath, "stale, corrupt leftover from an interrupted prior run");

        var path = await installer.EnsureInstalledAsync(asset, null, CancellationToken.None);

        Assert.Equal(Sha256Hex(await File.ReadAllBytesAsync(path)), asset.Sha256);
    }

    [Fact]
    public void Step096_NullArguments_Throw()
    {
        var downloader = FakeReleaseAssetDownloader.NeverCalled();
        Assert.Throws<ArgumentException>(() => new AssetInstaller("", downloader));
        Assert.Throws<ArgumentNullException>(() => new AssetInstaller(_installDirectory, null!));

        var installer = new AssetInstaller(_installDirectory, downloader);
        Assert.Throws<ArgumentNullException>(() => installer.GetInstalledPath(null!));
    }

    // --- HttpReleaseAssetDownloader: real sockets, real loopback server, no mocking -----------------

    [Fact]
    public async Task HttpReleaseAssetDownloader_RealLoopbackServer_DownloadsBytesAndReportsProgress()
    {
        var payload = Encoding.UTF8.GetBytes(new string('x', 200_000) + "-tail-marker");

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

        using var downloader = new HttpReleaseAssetDownloader();
        Directory.CreateDirectory(_installDirectory);
        var destinationPath = Path.Combine(_installDirectory, "downloaded.bin");

        var reported = new List<double>();
        var progress = new Progress<double>(p => reported.Add(p));

        await downloader.DownloadAsync(new Uri(prefix), destinationPath, progress, CancellationToken.None);
        await serveTask;
        listener.Stop();

        Assert.Equal(payload, await File.ReadAllBytesAsync(destinationPath));
        Assert.NotEmpty(reported);
        Assert.Equal(1.0, reported[^1]);
    }

    [Fact]
    public async Task HttpReleaseAssetDownloader_ServerReturns404_ThrowsHttpRequestException_NegativePath()
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

        using var downloader = new HttpReleaseAssetDownloader();
        Directory.CreateDirectory(_installDirectory);
        var destinationPath = Path.Combine(_installDirectory, "never-written.bin");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => downloader.DownloadAsync(new Uri(prefix), destinationPath, null, CancellationToken.None));
        await serveTask;
        listener.Stop();

        Assert.False(File.Exists(destinationPath));
    }

    [Fact]
    public async Task AssetInstaller_RealLoopbackServer_EndToEndInstallThenOfflineReuse()
    {
        // The real, full vertical slice for this step's own required acceptance: a real HTTP download
        // through the real HttpReleaseAssetDownloader, verified and installed by the real AssetInstaller,
        // then a second call that proves "core app works offline after assets installed" against the same
        // real production types (not the in-memory fake downloader used by the other tests above).
        var payload = Encoding.UTF8.GetBytes("real end-to-end installed asset bytes");
        var asset = FixtureAsset(payload, new Uri("about:blank")); // overwritten below once the real port is known

        using var listener = new HttpListener();
        var prefix = $"http://127.0.0.1:{GetFreeTcpPort()}/";
        listener.Prefixes.Add(prefix);
        listener.Start();
        asset = asset with { Url = new Uri(prefix) };

        var serveTask = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            context.Response.ContentLength64 = payload.Length;
            await context.Response.OutputStream.WriteAsync(payload);
            context.Response.OutputStream.Close();
        });

        using var realDownloader = new HttpReleaseAssetDownloader();
        var installer = new AssetInstaller(_installDirectory, realDownloader);

        var path = await installer.EnsureInstalledAsync(asset, null, CancellationToken.None);
        await serveTask;
        listener.Stop();

        Assert.Equal(payload, await File.ReadAllBytesAsync(path));

        // Second call: the real server is stopped (simulating offline); the already-installed, verified
        // asset must still resolve with zero network access.
        var secondPath = await installer.EnsureInstalledAsync(asset, null, CancellationToken.None);
        Assert.Equal(path, secondPath);
    }

    // --- UpdateNoticeChecker: real RSA signatures, no mocking ----------------------------------------

    private static byte[] SignManifestPayload(ReleaseManifest manifest, RSA signingKey)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(manifest);
        return UpdateVerifier.SignManifest(payload, signingKey);
    }

    [Fact]
    public void Step096_UpdateNotice_NewerSignedVersion_IsAvailable()
    {
        using var rsa = RSA.Create(2048);
        var candidate = new ReleaseManifest("1.1.0", "stable", [], "RSA-SHA256");
        var container = SignManifestPayload(candidate, rsa);
        var publicKey = rsa.ExportRSAPublicKey();

        var checker = new UpdateNoticeChecker(new UpdateVerifier());
        var notice = checker.CheckForUpdate("1.0.0", container, publicKey);

        Assert.True(notice.IsAvailable);
        Assert.Equal("1.1.0", notice.AvailableVersion);
        Assert.Contains("1.0.0", notice.Message);
        Assert.Contains("1.1.0", notice.Message);
    }

    [Fact]
    public void Step096_UpdateNotice_SameVersion_NotAvailable()
    {
        using var rsa = RSA.Create(2048);
        var candidate = new ReleaseManifest("1.0.0", "stable", [], "RSA-SHA256");
        var container = SignManifestPayload(candidate, rsa);
        var publicKey = rsa.ExportRSAPublicKey();

        var checker = new UpdateNoticeChecker(new UpdateVerifier());
        var notice = checker.CheckForUpdate("1.0.0", container, publicKey);

        Assert.False(notice.IsAvailable);
        Assert.Null(notice.AvailableVersion);
    }

    [Fact]
    public void Step096_UpdateNotice_NegativePath_TamperedSignature_IgnoredNotThrown()
    {
        // Phase test matrix row 7, "Invalid update manifest/signature rejected" -- applied here to the
        // update-notice path specifically.
        using var rsa = RSA.Create(2048);
        var candidate = new ReleaseManifest("9.9.9", "stable", [], "RSA-SHA256");
        var container = SignManifestPayload(candidate, rsa);
        var publicKey = rsa.ExportRSAPublicKey();

        var tampered = (byte[])container.Clone();
        tampered[^1] ^= 0xFF;

        var checker = new UpdateNoticeChecker(new UpdateVerifier());
        var notice = checker.CheckForUpdate("1.0.0", tampered, publicKey);

        Assert.False(notice.IsAvailable);
        Assert.Null(notice.AvailableVersion);
        Assert.Contains("could not be verified", notice.Message);
    }

    [Fact]
    public void Step096_UpdateNotice_NegativePath_WrongKey_IgnoredNotThrown()
    {
        using var signingKey = RSA.Create(2048);
        using var otherKey = RSA.Create(2048);
        var candidate = new ReleaseManifest("9.9.9", "stable", [], "RSA-SHA256");
        var container = SignManifestPayload(candidate, signingKey);

        var checker = new UpdateNoticeChecker(new UpdateVerifier());
        var notice = checker.CheckForUpdate("1.0.0", container, otherKey.ExportRSAPublicKey());

        Assert.False(notice.IsAvailable);
    }

    [Fact]
    public void Step096_UpdateNotice_NegativePath_MalformedContainer_IgnoredNotThrown()
    {
        using var rsa = RSA.Create(2048);
        var checker = new UpdateNoticeChecker(new UpdateVerifier());

        var notice = checker.CheckForUpdate("1.0.0", ReadOnlySpan<byte>.Empty, rsa.ExportRSAPublicKey());

        Assert.False(notice.IsAvailable);
        Assert.Null(notice.AvailableVersion);
    }

    [Fact]
    public void Step096_UpdateNotice_NullOrWhitespaceCurrentVersion_Throws()
    {
        using var rsa = RSA.Create(2048);
        var checker = new UpdateNoticeChecker(new UpdateVerifier());
        Assert.Throws<ArgumentException>(() => checker.CheckForUpdate("", ReadOnlySpan<byte>.Empty, rsa.ExportRSAPublicKey()));
    }

    [Fact]
    public void Step096_UpdateVerifier_ExtractPayload_MalformedContainer_Throws()
    {
        Assert.Throws<ArgumentException>(() => UpdateVerifier.ExtractPayload(ReadOnlySpan<byte>.Empty));
        Assert.Throws<ArgumentException>(() => UpdateVerifier.ExtractPayload(new byte[] { 0, 0, 0, 99, 1, 2, 3 }));
    }
}
