using System.Text.Json;
using XlightsAI.Studio.Rendering;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Build-plan Phase 18 §3 (Phase B). Same "real data by necessity, not choice" convention as
/// <see cref="EffectFixturePlannerTests"/> -- a real headless render proves nothing against invented
/// effect names/parameters or a fake renderer, so these tests skip cleanly (never fail) when
/// <see cref="RealXLightsFixture.IsRenderAvailable"/> is false (no real, configured xLights executable in
/// addition to the static effect metadata <see cref="RealXLightsFixture.IsAvailable"/> already requires).
/// Deliberately exercises only a small, fixed subset of fixtures (never the full ~112-fixture sweep) --
/// Phase 18 §3's own disclosed cost ("100+ real renders... minutes-to-hours") makes a full-catalog run
/// unsuitable for a normal test pass; the full sweep is `tools/EffectCharacterizer`'s own job.
/// </summary>
public sealed class EffectFixtureRendererTests : IDisposable
{
    private readonly string _outputRoot = Path.Combine(Path.GetTempPath(), "xlai-effect-fixture-render-" + Guid.NewGuid().ToString("N"));

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

    [Fact]
    public async Task RenderAsync_WellKnownOnEffect_ProducesRealDecodedFramesOnBothTiers()
    {
        if (!RealXLightsFixture.IsRenderAvailable)
        {
            return;
        }

        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var fullManifest = EffectFixturePlanner.Build(vocabulary, recipes, RealXLightsFixture.EffectMetadataRoot);
        var manifest = BuildSmallManifest(fullManifest, "On");

        var renderManifest = await EffectFixtureRenderer.RenderAsync(
            manifest, vocabulary, recipes, RealXLightsFixture.XlightsExecutablePath, _outputRoot, log: null, CancellationToken.None);

        Assert.Equal(2, renderManifest.Outcomes.Count);
        foreach (var outcome in renderManifest.Outcomes)
        {
            Assert.True(
                outcome.Status == EffectFixtureRenderStatus.Rendered,
                $"{outcome.EffectName} ({outcome.ResolutionTier}) was not rendered: {outcome.FailureReason}");
            Assert.NotNull(outcome.FramesPath);
            Assert.True(File.Exists(outcome.FramesPath));
            Assert.True(outcome.FrameCount > 0);
        }
    }

    /// <summary>
    /// Regression test for a real defect found while building Phase 18 §4 (Phase C): both fixture models'
    /// own <see cref="Xlights.DisplayModel.StringType"/> previously defaulted to <c>""</c> instead of
    /// <c>"RGB Nodes"</c>, so <see cref="Rendering.RenderedFrameProjector"/> silently skipped every single
    /// fixture (reported only in <see cref="Rendering.RenderPipelineResult.ProjectionWarnings"/>, which
    /// nothing read) -- every one of the 112 real fixtures in a full sweep decoded with zero illuminated
    /// points despite <see cref="EffectFixtureRenderOutcome.FrameCount"/> itself being correctly non-zero
    /// (frame count measures decoded frames, not lit pixels, so it never caught this). A trivially "always
    /// lit" effect on a trivially small fixture is the sharpest possible check that real colour actually
    /// reaches <see cref="PreviewFrame.Illuminated"/>, not just that decoding ran.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WellKnownOnEffect_ProducesRealIlluminatedPointsNotJustEmptyFrames()
    {
        if (!RealXLightsFixture.IsRenderAvailable)
        {
            return;
        }

        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var fullManifest = EffectFixturePlanner.Build(vocabulary, recipes, RealXLightsFixture.EffectMetadataRoot);
        var manifest = BuildSmallManifest(fullManifest, "On");

        var renderManifest = await EffectFixtureRenderer.RenderAsync(
            manifest, vocabulary, recipes, RealXLightsFixture.XlightsExecutablePath, _outputRoot, log: null, CancellationToken.None);

        foreach (var outcome in renderManifest.Outcomes)
        {
            Assert.Equal(EffectFixtureRenderStatus.Rendered, outcome.Status);
            await using var stream = File.OpenRead(outcome.FramesPath!);
            var frames = await JsonSerializer.DeserializeAsync<List<PreviewFrame>>(stream, cancellationToken: CancellationToken.None);
            Assert.NotNull(frames);
            Assert.True(
                frames!.Any(f => f.Illuminated.Count > 0),
                $"{outcome.EffectName} ({outcome.ResolutionTier}): every decoded frame had zero illuminated points " +
                "-- the real rendered colour never reached PreviewFrame (e.g. a DisplayModel.StringType mismatch " +
                "causing RenderedFrameProjector to silently skip the model).");
        }
    }

    /// <summary>
    /// Regression test for a real finding from manual.xlights.org, cross-checked against this machine's own
    /// real sequence corpus (<c>C:\xLights\*.xsq</c>): Kaleidoscope/Warp/Adjust are canvas-mode effects that
    /// "by itself do nothing" -- they sample/modify a real effect layer beneath them. The fix is two real,
    /// vocabulary-registered settings (<c>T_CHECKBOX_Canvas</c>/<c>T_LayersSelected</c>, added to
    /// <c>EffectVocabularyBuilder.BuildLegacyBlendingProperties</c>) plus a real base "On" layer
    /// <see cref="EffectFixturePlanner"/> now adds beneath each. All three were 0/240 lit frames before this
    /// fix; this guards against that regressing silently.
    /// </summary>
    [Theory]
    [InlineData("Kaleidoscope")]
    [InlineData("Warp")]
    [InlineData("Adjust")]
    public async Task RenderAsync_CanvasModeEffect_RendersRealContentViaBaseLayer(string effectName)
    {
        if (!RealXLightsFixture.IsRenderAvailable)
        {
            return;
        }

        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var fullManifest = EffectFixturePlanner.Build(vocabulary, recipes, RealXLightsFixture.EffectMetadataRoot);
        var manifest = BuildSmallManifest(fullManifest, effectName);

        var renderManifest = await EffectFixtureRenderer.RenderAsync(
            manifest, vocabulary, recipes, RealXLightsFixture.XlightsExecutablePath, _outputRoot, log: null, CancellationToken.None);

        foreach (var outcome in renderManifest.Outcomes)
        {
            Assert.Equal(EffectFixtureRenderStatus.Rendered, outcome.Status);
            await using var stream = File.OpenRead(outcome.FramesPath!);
            var frames = await JsonSerializer.DeserializeAsync<List<PreviewFrame>>(stream, cancellationToken: CancellationToken.None);
            Assert.True(frames!.Any(f => f.Illuminated.Count > 0), $"{effectName} ({outcome.ResolutionTier}) rendered no real content.");
        }
    }

    /// <summary>
    /// Regression test for a real defect found investigating why 38/112 real fixtures rendered dark: Text's
    /// own real metadata default (<c>E_TEXTCTRL_Text</c>) is a blank string, which renders nothing by
    /// design. <see cref="EffectFixturePlanner"/> now overrides it with a real, non-blank literal -- this
    /// guards against that override silently regressing (e.g. a wrong SettingKey being accepted by
    /// <c>LookCompiler</c> but never actually reaching the exported effect).
    /// </summary>
    [Fact]
    public async Task RenderAsync_TextEffect_RendersRealContentNotABlankDefault()
    {
        if (!RealXLightsFixture.IsRenderAvailable)
        {
            return;
        }

        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var fullManifest = EffectFixturePlanner.Build(vocabulary, recipes, RealXLightsFixture.EffectMetadataRoot);
        var manifest = BuildSmallManifest(fullManifest, "Text");

        var renderManifest = await EffectFixtureRenderer.RenderAsync(
            manifest, vocabulary, recipes, RealXLightsFixture.XlightsExecutablePath, _outputRoot, log: null, CancellationToken.None);

        foreach (var outcome in renderManifest.Outcomes)
        {
            Assert.Equal(EffectFixtureRenderStatus.Rendered, outcome.Status);
            await using var stream = File.OpenRead(outcome.FramesPath!);
            var frames = await JsonSerializer.DeserializeAsync<List<PreviewFrame>>(stream, cancellationToken: CancellationToken.None);
            Assert.True(frames!.Any(f => f.Illuminated.Count > 0), $"Text ({outcome.ResolutionTier}) rendered no real content.");
        }
    }

    /// <summary>
    /// Regression test for a real defect found investigating the same 38 dark fixtures: several effects'
    /// own real metadata (Shape/Kaleidoscope/Circles/Warp/Lines/Morph/Life/Fireworks) describe genuinely
    /// 2D-plane behaviour, but the High-tier fixture was a 1-row "Single Line" (verified by direct
    /// inspection). Morph's own real vertical range (<c>Morph_Start_Y1</c>/<c>Morph_End_Y1</c>, 0-100%)
    /// makes it the sharpest real check that the High tier's own Matrix shape (<see cref="EffectFixturePlanner.HighResolutionMatrixColumns"/>x<see cref="EffectFixturePlanner.HighResolutionMatrixRows"/>)
    /// and <see cref="Rendering.RenderedFrameProjector"/>'s own Matrix fan-out genuinely deliver real,
    /// varying vertical position over time, not just a non-zero illuminated count.
    /// </summary>
    [Fact]
    public async Task RenderAsync_MorphEffectOnHighTier_ShowsRealVerticalMotionAcrossFrames()
    {
        if (!RealXLightsFixture.IsRenderAvailable)
        {
            return;
        }

        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var fullManifest = EffectFixturePlanner.Build(vocabulary, recipes, RealXLightsFixture.EffectMetadataRoot);
        var manifest = fullManifest with
        {
            Fixtures = fullManifest.Fixtures
                .Where(f => f.EffectName == "Morph" && f.ResolutionTier == EffectFixtureResolutionTier.High)
                .ToList(),
        };

        var renderManifest = await EffectFixtureRenderer.RenderAsync(
            manifest, vocabulary, recipes, RealXLightsFixture.XlightsExecutablePath, _outputRoot, log: null, CancellationToken.None);

        var outcome = Assert.Single(renderManifest.Outcomes);
        Assert.Equal(EffectFixtureRenderStatus.Rendered, outcome.Status);

        await using var stream = File.OpenRead(outcome.FramesPath!);
        var frames = (await JsonSerializer.DeserializeAsync<List<PreviewFrame>>(stream, cancellationToken: CancellationToken.None))!;

        var distinctYValuesSeenAcrossFrames = frames
            .Where(f => f.Illuminated.Count > 0)
            .SelectMany(f => f.Illuminated.Select(p => Math.Round(p.Y, 2)))
            .Distinct()
            .Count();

        Assert.True(
            distinctYValuesSeenAcrossFrames > 1,
            "Morph (High) never showed more than one distinct Y position across the whole sequence -- " +
            "the High tier's own 2D Matrix shape is not actually reaching RenderedFrameProjector's Matrix fan-out.");
    }

    [Fact]
    public async Task RenderAsync_NeverSilentlySkipsAFixture_EveryOutcomeIsRenderedOrHasAFailureReason()
    {
        if (!RealXLightsFixture.IsRenderAvailable)
        {
            return;
        }

        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var fullManifest = EffectFixturePlanner.Build(vocabulary, recipes, RealXLightsFixture.EffectMetadataRoot);
        var manifest = BuildSmallManifest(fullManifest, "On", "Bars", "Twinkle");

        var renderManifest = await EffectFixtureRenderer.RenderAsync(
            manifest, vocabulary, recipes, RealXLightsFixture.XlightsExecutablePath, _outputRoot, log: null, CancellationToken.None);

        Assert.Equal(manifest.Fixtures.Count, renderManifest.Outcomes.Count);
        foreach (var outcome in renderManifest.Outcomes)
        {
            if (outcome.Status == EffectFixtureRenderStatus.Rendered)
            {
                Assert.Null(outcome.FailureReason);
            }
            else
            {
                Assert.False(string.IsNullOrWhiteSpace(outcome.FailureReason));
            }
        }

        Assert.Equal(renderManifest.Outcomes.Count, renderManifest.RenderedCount + renderManifest.FailedCount);
    }

    [Fact]
    public async Task RenderAsync_CompileSkippedFixture_IsRecordedWithoutAttemptingARender()
    {
        if (!RealXLightsFixture.IsRenderAvailable)
        {
            return;
        }

        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var fullManifest = EffectFixturePlanner.Build(vocabulary, recipes, RealXLightsFixture.EffectMetadataRoot);
        var onPlan = fullManifest.Fixtures.First(f => f.EffectName == "On" && f.ResolutionTier == EffectFixtureResolutionTier.Low);
        var fakeFailedPlan = onPlan with { CompiledSuccessfully = false, CompileFailureReason = "synthetic failure for this test" };
        var manifest = fullManifest with { Fixtures = [fakeFailedPlan] };

        var renderManifest = await EffectFixtureRenderer.RenderAsync(
            manifest, vocabulary, recipes, RealXLightsFixture.XlightsExecutablePath, _outputRoot, log: null, CancellationToken.None);

        var outcome = Assert.Single(renderManifest.Outcomes);
        Assert.Equal(EffectFixtureRenderStatus.CompileSkipped, outcome.Status);
        Assert.Contains("synthetic failure for this test", outcome.FailureReason);
        Assert.Null(outcome.FramesPath);
    }

    [Fact]
    public async Task RenderAsync_NoRealXlightsExecutableConfigured_ThrowsEffectFixtureRenderUnavailableException()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var fullManifest = EffectFixturePlanner.Build(vocabulary, recipes, RealXLightsFixture.EffectMetadataRoot);
        var manifest = BuildSmallManifest(fullManifest, "On");

        await Assert.ThrowsAsync<EffectFixtureRenderUnavailableException>(() =>
            EffectFixtureRenderer.RenderAsync(
                manifest, vocabulary, recipes, @"C:\definitely\not\a\real\xlights.exe", _outputRoot, log: null, CancellationToken.None));
    }

    [Fact]
    public async Task JsonEffectFixtureRenderManifestStore_SaveThenLoad_RoundTripsTheManifest()
    {
        if (!RealXLightsFixture.IsRenderAvailable)
        {
            return;
        }

        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var fullManifest = EffectFixturePlanner.Build(vocabulary, recipes, RealXLightsFixture.EffectMetadataRoot);
        var manifest = BuildSmallManifest(fullManifest, "On");

        var renderManifest = await EffectFixtureRenderer.RenderAsync(
            manifest, vocabulary, recipes, RealXLightsFixture.XlightsExecutablePath, _outputRoot, log: null, CancellationToken.None);

        var store = new JsonEffectFixtureRenderManifestStore(_outputRoot);
        await store.SaveAsync(renderManifest, CancellationToken.None);

        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(renderManifest.Outcomes.Count, loaded.Outcomes.Count);
        Assert.Equal(renderManifest.RenderedCount, loaded.RenderedCount);
    }
}
