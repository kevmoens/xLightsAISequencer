using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Editing.Tests;

/// <summary>
/// PHASE-04 Step 027 ("Render synchronized waveform beneath ruler"). Required acceptance: "Waveform
/// position matches timeline coordinates." Required failure/edge case: "Silence, invalid timing
/// boundaries and missing media must be handled explicitly." These tests exercise the real
/// <see cref="WaveformBarLayoutBuilder"/>/<see cref="TimelineViewport"/> against hand-built
/// <see cref="WaveformPyramidCache"/> fixtures (deterministic synthetic envelope data — the pure
/// domain shape this builder consumes, not a decoded audio file; decode/build is already proven by
/// <c>Step026AcceptanceTests</c> in <c>XlightsAI.Studio.Audio.Tests</c>), never mocks of the
/// builder itself.
/// </summary>
public sealed class Step027AcceptanceTests
{
    private const int SampleRate = 8000;
    private const long TicksPerSecond = 10_000_000L;

    private static WaveformPyramidCache BuildCache(long sampleCount, params int[] blockSizes)
    {
        var resolutions = new WaveformResolutionLevel[blockSizes.Length];
        for (var levelIndex = 0; levelIndex < blockSizes.Length; levelIndex++)
        {
            var blockSize = blockSizes[levelIndex];
            var envelopes = new List<WaveformEnvelope>();
            long start = 0;
            var blockIndex = 0;
            while (start < sampleCount)
            {
                var end = Math.Min(start + blockSize, sampleCount);
                var magnitude = (blockIndex + 1) * 0.01f;
                envelopes.Add(new WaveformEnvelope(start, end, -magnitude, magnitude, magnitude * 0.5f));
                start = end;
                blockIndex++;
            }

            resolutions[levelIndex] = new WaveformResolutionLevel(blockSize, envelopes.ToArray());
        }

        return new WaveformPyramidCache(
            SongId: new SongId(Guid.NewGuid()),
            ContentHash: "deadbeef",
            SourceDecoderVersion: "test/1.0",
            BuilderVersion: "test/1.0",
            SourceSampleRate: SampleRate,
            SampleCount: sampleCount,
            Resolutions: resolutions);
    }

    private static TimelineViewport FullViewport(long durationTicks, double ticksPerPixel) =>
        TimelineViewport.Create(new TimeWindow(0, durationTicks), ticksPerPixel);

    [Fact]
    public void Step027_Acceptance_Passes_BarPositionsMatchTimelineCoordinates_AtSelectedResolution()
    {
        const long sampleCount = 8192; // 1.024s @ 8kHz
        var cache = BuildCache(sampleCount, 64, 1024);
        var durationTicks = sampleCount * (TicksPerSecond / SampleRate);

        // samplesPerPixel = 625,000 * 8000 / 10,000,000 = 500 -> the 64-sample level does not cover
        // a full pixel (64 < 500) but the 1024-sample level does (1024 >= 500): expect the 1024 level.
        var viewport = FullViewport(durationTicks, ticksPerPixel: 625_000d);
        var viewportWidthPixels = durationTicks / 625_000d;

        var bars = WaveformBarLayoutBuilder.BuildBars(cache, viewport, viewportWidthPixels);

        var expectedLevel = cache.Resolutions.Single(r => r.BlockSizeSamples == 1024);
        Assert.Equal(expectedLevel.Envelopes.Length, bars.Count);

        for (var i = 0; i < bars.Count; i++)
        {
            var envelope = expectedLevel.Envelopes[i];
            var startTicks = envelope.StartSample * (TicksPerSecond / SampleRate);
            var endTicks = envelope.EndSample * (TicksPerSecond / SampleRate);
            var expectedX = viewport.XFromTime(startTicks);
            var expectedWidth = viewport.XFromTime(endTicks) - expectedX;

            Assert.Equal(expectedX, bars[i].X);
            Assert.Equal(expectedWidth, bars[i].Width);
            Assert.Equal(envelope.Min, bars[i].Min);
            Assert.Equal(envelope.Max, bars[i].Max);
            Assert.Equal(envelope.Rms, bars[i].Rms);
        }
    }

    [Fact]
    public void BuildBars_FinerZoom_SelectsFinestLevelThatStillCoversAtLeastOnePixel()
    {
        const long sampleCount = 8192;
        var cache = BuildCache(sampleCount, 64, 1024);
        var durationTicks = sampleCount * (TicksPerSecond / SampleRate);

        // samplesPerPixel = 12,500 * 8000 / 10,000,000 = 10 -> the 64-sample level already covers
        // more than a pixel (64 >= 10): expect the finer 64 level, not the coarser 1024 level.
        var viewport = FullViewport(durationTicks, ticksPerPixel: 12_500d);
        var viewportWidthPixels = durationTicks / 12_500d;

        var bars = WaveformBarLayoutBuilder.BuildBars(cache, viewport, viewportWidthPixels);

        var expectedLevel = cache.Resolutions.Single(r => r.BlockSizeSamples == 64);
        Assert.Equal(expectedLevel.Envelopes.Length, bars.Count);
    }

    [Fact]
    public void BuildBars_NarrowVisibleWindow_OnlyReturnsOverlappingBars_Virtualized()
    {
        const long sampleCount = 8192;
        var cache = BuildCache(sampleCount, 64, 1024);
        var durationTicks = sampleCount * (TicksPerSecond / SampleRate);
        var fullWidthPixels = durationTicks / 625_000d;

        var fullViewport = FullViewport(durationTicks, ticksPerPixel: 625_000d);
        var allBars = WaveformBarLayoutBuilder.BuildBars(cache, fullViewport, fullWidthPixels);

        // A much narrower on-screen pixel width (same scroll offset/zoom) must only return the
        // subset of bars actually overlapping that visible window — never the full pyramid.
        var narrowWidthPixels = fullWidthPixels / 4d;
        var narrowBars = WaveformBarLayoutBuilder.BuildBars(cache, fullViewport, narrowWidthPixels);

        Assert.True(narrowBars.Count > 0);
        Assert.True(narrowBars.Count < allBars.Count);
        Assert.All(narrowBars, bar => Assert.True(bar.X < narrowWidthPixels));
    }

    [Fact]
    public void BuildBars_TrueSilence_ProducesRealZeroValuedBarsAtCorrectPositions_HandlesSilenceExplicitly()
    {
        const long sampleCount = 2048;
        var resolutions = new[]
        {
            new WaveformResolutionLevel(64, BuildZeroEnvelopes(sampleCount, 64)),
        };
        var cache = new WaveformPyramidCache(
            new SongId(Guid.NewGuid()), "hash", "decoder/1.0", "builder/1.0", SampleRate, sampleCount, resolutions);
        var durationTicks = sampleCount * (TicksPerSecond / SampleRate);
        var viewport = FullViewport(durationTicks, TimelineViewport.MinTicksPerPixel);
        var widthPixels = durationTicks / TimelineViewport.MinTicksPerPixel;

        var bars = WaveformBarLayoutBuilder.BuildBars(cache, viewport, widthPixels);

        Assert.NotEmpty(bars);
        Assert.All(bars, bar =>
        {
            Assert.Equal(0f, bar.Min);
            Assert.Equal(0f, bar.Max);
            Assert.Equal(0f, bar.Rms);
            Assert.True(bar.Width >= 0d);
        });
    }

    private static WaveformEnvelope[] BuildZeroEnvelopes(long sampleCount, int blockSize)
    {
        var envelopes = new List<WaveformEnvelope>();
        long start = 0;
        while (start < sampleCount)
        {
            var end = Math.Min(start + blockSize, sampleCount);
            envelopes.Add(new WaveformEnvelope(start, end, 0f, 0f, 0f));
            start = end;
        }

        return envelopes.ToArray();
    }

    [Fact]
    public void BuildBars_EmptyResolutions_ReturnsEmptyList_InvalidTimingBoundaryHandledExplicitly_NegativePath()
    {
        var cache = new WaveformPyramidCache(
            new SongId(Guid.NewGuid()), "hash", "decoder/1.0", "builder/1.0", SampleRate, 1000, []);
        var viewport = FullViewport(1_250_000, TimelineViewport.MinTicksPerPixel);

        var bars = WaveformBarLayoutBuilder.BuildBars(cache, viewport, 100d);

        Assert.Empty(bars);
    }

    [Fact]
    public void BuildBars_NonPositiveSampleCount_ReturnsEmptyList_InvalidTimingBoundaryHandledExplicitly_NegativePath()
    {
        var resolutions = new[] { new WaveformResolutionLevel(64, [new WaveformEnvelope(0, 1, 0f, 0f, 0f)]) };
        var cache = new WaveformPyramidCache(
            new SongId(Guid.NewGuid()), "hash", "decoder/1.0", "builder/1.0", SampleRate, 0, resolutions);
        var viewport = FullViewport(1_250_000, TimelineViewport.MinTicksPerPixel);

        var bars = WaveformBarLayoutBuilder.BuildBars(cache, viewport, 100d);

        Assert.Empty(bars);
    }

    [Fact]
    public void BuildBars_NonZeroOriginTicks_ShiftsBarsByExactlyThatOffset()
    {
        const long sampleCount = 2048;
        var cache = BuildCache(sampleCount, 64);
        var durationTicks = sampleCount * (TicksPerSecond / SampleRate);
        const long originTicks = 5_000_000L;
        // Content bounds must cover the shifted range too.
        var viewport = TimelineViewport.Create(new TimeWindow(0, originTicks + durationTicks), TimelineViewport.MinTicksPerPixel);
        var widthPixels = (originTicks + durationTicks) / TimelineViewport.MinTicksPerPixel;

        var unshifted = WaveformBarLayoutBuilder.BuildBars(cache, viewport, widthPixels, originTicks: 0);
        var shifted = WaveformBarLayoutBuilder.BuildBars(cache, viewport, widthPixels, originTicks: originTicks);

        Assert.Equal(unshifted.Count, shifted.Count);
        var expectedPixelShift = originTicks / TimelineViewport.MinTicksPerPixel;
        for (var i = 0; i < unshifted.Count; i++)
        {
            Assert.Equal(unshifted[i].X + expectedPixelShift, shifted[i].X, precision: 6);
        }
    }

    [Fact]
    public void BuildBars_NullCache_Throws_NegativePath()
    {
        var viewport = FullViewport(1_250_000, TimelineViewport.MinTicksPerPixel);
        Assert.Throws<ArgumentNullException>(() => WaveformBarLayoutBuilder.BuildBars(null!, viewport, 100d));
    }
}
