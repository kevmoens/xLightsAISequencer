using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// <see cref="SpectrogramStemSeparatorAdapter"/> is pure translation logic — these tests exercise it
/// against a fake <see cref="ISpectrogramStemSeparator"/> rather than the real, large, network-downloaded
/// <c>sherpa-onnx-spleeter-2stems</c> model (that real end-to-end path is exercised manually/opt-in, same
/// as <c>XlightsAI.Studio.Stems.Tests.Step044FAcceptanceTests</c>'s own disclosed "real network access
/// required" scope).
/// </summary>
public sealed class SpectrogramStemSeparatorAdapterTests
{
    private sealed class FakeSpectrogramStemSeparator : ISpectrogramStemSeparator
    {
        public SongAsset? LastSource { get; private set; }
        public SpectrogramModelBundle? LastBundle { get; private set; }
        public SpectrogramSeparationRecipe? LastRecipe { get; private set; }

        public Task<StemManifest> SeparateAsync(
            SongAsset source, SpectrogramModelBundle modelBundle, SpectrogramSeparationRecipe recipe,
            IProgress<SeparationProgress>? progress, CancellationToken ct)
        {
            LastSource = source;
            LastBundle = modelBundle;
            LastRecipe = recipe;
            return Task.FromResult(new StemManifest(Guid.NewGuid(), source.Sha256, modelBundle.Archive.Id, [], []));
        }
    }

    private static SongAsset FixtureSong() => new(
        new SongId(Guid.NewGuid()), "song.wav", "song.wav", new string('a', 64), 44100, 44100, 2, 0, 16, 0, "test/1.0.0");

    [Fact]
    public async Task SeparateAsync_MatchingModelId_DelegatesWithConfiguredBundleAndRecipe()
    {
        var fake = new FakeSpectrogramStemSeparator();
        var adapter = new SpectrogramStemSeparatorAdapter(
            fake, SherpaOnnxSpleeterModelAsset.Bundle, SherpaOnnxSpleeterModelAsset.Recipe);
        var song = FixtureSong();

        var manifest = await adapter.SeparateAsync(song, SherpaOnnxSpleeterModelAsset.Archive, progress: null, CancellationToken.None);

        Assert.Same(song, fake.LastSource);
        Assert.Same(SherpaOnnxSpleeterModelAsset.Bundle, fake.LastBundle);
        Assert.Same(SherpaOnnxSpleeterModelAsset.Recipe, fake.LastRecipe);
        Assert.Equal(SherpaOnnxSpleeterModelAsset.Archive.Id, manifest.ModelId);
    }

    [Fact]
    public async Task SeparateAsync_MismatchedModelId_ThrowsWithoutCallingInner()
    {
        var fake = new FakeSpectrogramStemSeparator();
        var adapter = new SpectrogramStemSeparatorAdapter(
            fake, SherpaOnnxSpleeterModelAsset.Bundle, SherpaOnnxSpleeterModelAsset.Recipe);
        var wrongModel = SherpaOnnxSpleeterModelAsset.Archive with { Id = "some-other-model" };

        await Assert.ThrowsAsync<ArgumentException>(
            () => adapter.SeparateAsync(FixtureSong(), wrongModel, progress: null, CancellationToken.None));
        Assert.Null(fake.LastSource);
    }

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        var fake = new FakeSpectrogramStemSeparator();
        Assert.Throws<ArgumentNullException>(() => new SpectrogramStemSeparatorAdapter(null!, SherpaOnnxSpleeterModelAsset.Bundle, SherpaOnnxSpleeterModelAsset.Recipe));
        Assert.Throws<ArgumentNullException>(() => new SpectrogramStemSeparatorAdapter(fake, null!, SherpaOnnxSpleeterModelAsset.Recipe));
        Assert.Throws<ArgumentNullException>(() => new SpectrogramStemSeparatorAdapter(fake, SherpaOnnxSpleeterModelAsset.Bundle, null!));
    }
}
