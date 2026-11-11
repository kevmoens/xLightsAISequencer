namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Build-plan Phase 18 §4 (Phase C). Same "real data by necessity, not choice" convention as
/// <see cref="EffectFixtureRendererTests"/> -- a real video sanity-check proves nothing against invented
/// effect names or fabricated frames, so these tests skip cleanly (never fail) when
/// <see cref="RealXLightsFixture.IsRenderAvailable"/> is false. Deliberately exercises only a small, fixed
/// subset of fixtures (never the full ~112-fixture sweep) -- the full sweep is `tools/EffectCharacterizer`'s
/// own job.
/// </summary>
public sealed class EffectFixtureVideoEncoderTests : IDisposable
{
    private readonly string _outputRoot = Path.Combine(Path.GetTempPath(), "xlai-effect-fixture-video-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_outputRoot))
        {
            Directory.Delete(_outputRoot, recursive: true);
        }
    }

    private static EffectFixtureManifest BuildSmallManifest(EffectFixtureManifest fullManifest, params string[] effectNames) =>
        fullManifest with
        {
            Fixtures = fullManifest.Fixtures.Where(f => effectNames.Contains(f.EffectName, StringComparer.Ordinal)).ToList(),
        };

    private async Task<EffectFixtureRenderManifest> RenderSmallManifestAsync(params string[] effectNames)
    {
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var fullManifest = EffectFixturePlanner.Build(vocabulary, recipes, RealXLightsFixture.EffectMetadataRoot);
        var manifest = BuildSmallManifest(fullManifest, effectNames);
        return await EffectFixtureRenderer.RenderAsync(
            manifest, vocabulary, recipes, RealXLightsFixture.XlightsExecutablePath, _outputRoot, log: null, CancellationToken.None);
    }

    [Fact]
    public async Task EncodeAsync_WellKnownOnEffect_ProducesRealPlayableGifOnBothTiers()
    {
        if (!RealXLightsFixture.IsRenderAvailable)
        {
            return;
        }

        var renderManifest = await RenderSmallManifestAsync("On");
        Assert.Equal(2, renderManifest.RenderedCount);

        var videoManifest = await EffectFixtureVideoEncoder.EncodeAsync(renderManifest, log: null, CancellationToken.None);

        Assert.Equal(2, videoManifest.Outcomes.Count);
        foreach (var outcome in videoManifest.Outcomes)
        {
            Assert.True(
                outcome.Status == EffectFixtureVideoStatus.Encoded,
                $"{outcome.EffectName} ({outcome.ResolutionTier}) was not encoded: {outcome.FailureReason}");
            Assert.NotNull(outcome.VideoPath);
            Assert.True(File.Exists(outcome.VideoPath));

            var bytes = await File.ReadAllBytesAsync(outcome.VideoPath!);
            Assert.True(bytes.Length > 0);
            Assert.Equal("GIF89a", System.Text.Encoding.ASCII.GetString(bytes, 0, 6));
            Assert.Equal(0x3B, bytes[^1]); // real GIF trailer -- a genuinely complete, playable file, not a truncated write.
            Assert.True(outcome.FrameCount > 0);
        }
    }

    [Fact]
    public async Task EncodeAsync_SeveralCommonEffects_NeverSilentlySkipsAFixture()
    {
        if (!RealXLightsFixture.IsRenderAvailable)
        {
            return;
        }

        var renderManifest = await RenderSmallManifestAsync("On", "Bars", "Twinkle");
        var videoManifest = await EffectFixtureVideoEncoder.EncodeAsync(renderManifest, log: null, CancellationToken.None);

        Assert.Equal(renderManifest.Outcomes.Count, videoManifest.Outcomes.Count);
        foreach (var outcome in videoManifest.Outcomes)
        {
            if (outcome.Status == EffectFixtureVideoStatus.Encoded)
            {
                Assert.Null(outcome.FailureReason);
            }
            else
            {
                Assert.False(string.IsNullOrWhiteSpace(outcome.FailureReason));
            }
        }

        Assert.Equal(videoManifest.Outcomes.Count, videoManifest.EncodedCount + videoManifest.FailedCount);
    }

    [Fact]
    public async Task EncodeAsync_RenderSkippedFixture_IsRecordedWithoutAttemptingAnEncode()
    {
        if (!RealXLightsFixture.IsRenderAvailable)
        {
            return;
        }

        var renderManifest = await RenderSmallManifestAsync("On");
        var skippedOutcome = renderManifest.Outcomes.First() with
        {
            Status = EffectFixtureRenderStatus.RenderFailed,
            FailureReason = "synthetic failure for this test",
            FramesPath = null,
        };
        var manifest = renderManifest with { Outcomes = [skippedOutcome] };

        var videoManifest = await EffectFixtureVideoEncoder.EncodeAsync(manifest, log: null, CancellationToken.None);

        var outcome = Assert.Single(videoManifest.Outcomes);
        Assert.Equal(EffectFixtureVideoStatus.RenderSkipped, outcome.Status);
        Assert.Contains("synthetic failure for this test", outcome.FailureReason);
        Assert.Null(outcome.VideoPath);
    }

    [Fact]
    public async Task JsonEffectFixtureVideoManifestStore_SaveThenLoad_RoundTripsTheManifest()
    {
        if (!RealXLightsFixture.IsRenderAvailable)
        {
            return;
        }

        var renderManifest = await RenderSmallManifestAsync("On");
        var videoManifest = await EffectFixtureVideoEncoder.EncodeAsync(renderManifest, log: null, CancellationToken.None);

        var store = new JsonEffectFixtureVideoManifestStore(_outputRoot);
        await store.SaveAsync(videoManifest, CancellationToken.None);

        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(videoManifest.Outcomes.Count, loaded.Outcomes.Count);
        Assert.Equal(videoManifest.EncodedCount, loaded.EncodedCount);
    }
}
