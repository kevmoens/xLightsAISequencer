using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using XlightsAI.Studio.Distribution;

namespace XlightsAI.Studio.Distribution.Tests;

/// <summary>
/// <see cref="FfmpegInstaller"/> composes a real <see cref="AssetInstaller"/> (download/verify, already
/// covered by <c>Step096AcceptanceTests</c>) with zip extraction. These tests exercise the real
/// <see cref="FfmpegInstaller"/>/<see cref="AssetInstaller"/> against real, synthetic in-memory zip
/// archives — no mocking framework, no real network access (a fake <see cref="IReleaseAssetDownloader"/>
/// stands in for the remote server, same convention as <c>Step096AcceptanceTests</c>).
/// </summary>
public sealed class FfmpegInstallerTests : IDisposable
{
    private readonly string _rootDirectory;

    public FfmpegInstallerTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "xlai-ffmpeg-installer-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static DownloadableAsset FixtureAsset(byte[] archiveBytes) => new(
        Id: "ffmpeg-win64-lgpl-shared",
        Version: "9.9.9-fixture",
        Url: new Uri("https://example.invalid/ffmpeg.zip"),
        Sha256: Sha256Hex(archiveBytes),
        SizeBytes: archiveBytes.Length,
        LicenseId: "LGPL-3.0-or-later",
        Required: false);

    private static byte[] BuildZip(params (string EntryName, byte[] Content)[] entries)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (entryName, content) in entries)
            {
                var entry = archive.CreateEntry(entryName);
                using var entryStream = entry.Open();
                entryStream.Write(content);
            }
        }

        return memory.ToArray();
    }

    /// <summary>Deterministic in-memory stand-in for a real remote server; never touches the network — same convention as <c>Step096AcceptanceTests.FakeReleaseAssetDownloader</c>.</summary>
    private sealed class FakeReleaseAssetDownloader : IReleaseAssetDownloader
    {
        private readonly byte[]? _payload;
        public int CallCount { get; private set; }

        private FakeReleaseAssetDownloader(byte[]? payload) => _payload = payload;

        public static FakeReleaseAssetDownloader ReturningBytes(byte[] payload) => new(payload);

        public static FakeReleaseAssetDownloader NeverCalled() => new(null);

        public async Task DownloadAsync(Uri source, string destinationPath, IProgress<double>? progress, CancellationToken ct)
        {
            CallCount++;
            if (_payload is null)
            {
                throw new InvalidOperationException("Downloader must not be invoked when an already-extracted copy exists.");
            }

            await File.WriteAllBytesAsync(destinationPath, _payload, ct).ConfigureAwait(false);
            progress?.Report(1.0);
        }
    }

    [Fact]
    public async Task EnsureInstalledAsync_DownloadsVerifiesAndExtracts_ThenReusesOfflineWithoutRedownloading()
    {
        var exeBytes = Encoding.UTF8.GetBytes("fake ffmpeg.exe bytes");
        var archiveBytes = BuildZip(
            ("ffmpeg-fixture-build/LICENSE.txt", Encoding.UTF8.GetBytes("LGPL-3.0-or-later (fixture)")),
            ("ffmpeg-fixture-build/bin/ffmpeg.exe", exeBytes),
            ("ffmpeg-fixture-build/bin/avcodec-99.dll", Encoding.UTF8.GetBytes("fake dll bytes")));
        var asset = FixtureAsset(archiveBytes);

        var downloadsDirectory = Path.Combine(_rootDirectory, "downloads");
        var extractionRoot = Path.Combine(_rootDirectory, "ffmpeg");
        var assetInstaller = new AssetInstaller(downloadsDirectory, FakeReleaseAssetDownloader.ReturningBytes(archiveBytes));
        var installer = new FfmpegInstaller(extractionRoot, assetInstaller, asset);

        Assert.Null(installer.TryGetInstalledExecutablePath());

        var exePath = await installer.EnsureInstalledAsync(progress: null, CancellationToken.None);

        Assert.True(File.Exists(exePath));
        Assert.Equal(exeBytes, await File.ReadAllBytesAsync(exePath));
        Assert.Equal(exePath, installer.TryGetInstalledExecutablePath());
        // A sibling file from the same archive entry tree was extracted alongside it (not just the exe).
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(exePath)!, "avcodec-99.dll")));

        // Offline/idempotent re-run against a fresh installer instance pointed at the same directories:
        // no network access, no re-extraction.
        var neverCalledDownloader = FakeReleaseAssetDownloader.NeverCalled();
        var secondAssetInstaller = new AssetInstaller(downloadsDirectory, neverCalledDownloader);
        var secondInstaller = new FfmpegInstaller(extractionRoot, secondAssetInstaller, asset);
        var secondPath = await secondInstaller.EnsureInstalledAsync(progress: null, CancellationToken.None);

        Assert.Equal(exePath, secondPath);
        Assert.Equal(0, neverCalledDownloader.CallCount);
    }

    [Fact]
    public async Task EnsureInstalledAsync_ArchiveMissingExecutable_ThrowsClearly()
    {
        var archiveBytes = BuildZip(("ffmpeg-fixture-build/README.txt", Encoding.UTF8.GetBytes("no ffmpeg.exe in here")));
        var asset = FixtureAsset(archiveBytes);
        var assetInstaller = new AssetInstaller(Path.Combine(_rootDirectory, "downloads"), FakeReleaseAssetDownloader.ReturningBytes(archiveBytes));
        var installer = new FfmpegInstaller(Path.Combine(_rootDirectory, "ffmpeg"), assetInstaller, asset);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => installer.EnsureInstalledAsync(progress: null, CancellationToken.None));

        Assert.Contains("ffmpeg.exe", ex.Message);
    }

    [Fact]
    public void TryGetInstalledExecutablePath_NothingInstalledYet_ReturnsNull()
    {
        var asset = FixtureAsset(Encoding.UTF8.GetBytes("unused"));
        var assetInstaller = new AssetInstaller(Path.Combine(_rootDirectory, "downloads"), FakeReleaseAssetDownloader.NeverCalled());
        var installer = new FfmpegInstaller(Path.Combine(_rootDirectory, "ffmpeg"), assetInstaller, asset);

        Assert.Null(installer.TryGetInstalledExecutablePath());
    }

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        var assetInstaller = new AssetInstaller(_rootDirectory, FakeReleaseAssetDownloader.NeverCalled());
        Assert.Throws<ArgumentException>(() => new FfmpegInstaller("", assetInstaller));
        Assert.Throws<ArgumentNullException>(() => new FfmpegInstaller(_rootDirectory, null!));
    }
}
