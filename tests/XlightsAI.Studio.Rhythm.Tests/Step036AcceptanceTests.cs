using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Reference.Fixtures;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Rhythm.Tests;

/// <summary>
/// PHASE-05 / Step 036 — "Create beat-pop and alternating-chase recipe to real selected props."
/// Required acceptance: "Rendered/exported effects follow detected beats." Failure/edge case:
/// "Silence, invalid timing boundaries and missing media must be handled explicitly." Implementation
/// guidance: "Translate selected beat strength into finite pulse duration and verified effect recipe,
/// respecting visual density and output frame snapping."
/// </summary>
public sealed class Step036AcceptanceTests : IDisposable
{
    private const int SampleRate = 8000;
    private static readonly RhythmSettings WindowSettings = new(AnalysisSampleRate: SampleRate, FftSize: 256, HopSamples: 128);
    private static readonly OnsetDetectionSettings OnsetSettings = new(SensitivityMultiplier: 1.5f, MinInterOnsetSamples: 256);
    private static readonly TempoDetectionSettings TempoSettings = new(MinBpm: 40, MaxBpm: 300, TimeResolutionSeconds: 0.01, MaxCandidates: 5);
    private static readonly long AlignmentToleranceTicks = 2L * WindowSettings.HopSamples * (10_000_000L / SampleRate);
    private static readonly BeatGridSettings GridSettings = new(BeatsPerBar: 4, AlignmentToleranceTicks: AlignmentToleranceTicks);

    private static readonly ModelId Arch1 = new("Arch 1");
    private static readonly ModelId Arch2 = new("Arch 2");

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-step036-{Guid.NewGuid():N}");
    private readonly RhythmGesturePlanner _planner = new(new EffectRecipeRegistry());

    public Step036AcceptanceTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private async Task<SongAsset> ProbeAsync(string fileName, float[] samples)
    {
        var wavBytes = DeterministicSyntheticAudioGenerator.EncodeWav(samples, SampleRate);
        var path = Path.Combine(_tempDir, fileName);
        await File.WriteAllBytesAsync(path, wavBytes);
        return await new WavAudioDecoder().ProbeAsync(path, CancellationToken.None);
    }

    private async Task<BeatGrid> BuildClickGridAsync(string fileName, int bpm, int clickCount, int leadInSamples = 4000)
    {
        var clicks = DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm, clickCount, clickDurationSeconds: 0.02, toneHz: 1000.0, SampleRate);
        var samples = new float[leadInSamples + clicks.Length];
        Array.Copy(clicks, 0, samples, leadInSamples, clicks.Length);

        var asset = await ProbeAsync(fileName, samples);
        var onsetStore = new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "onset-cache"));
        var onsets = await new OnsetDetector(new WavAudioDecoder(), onsetStore)
            .DetectAsync(asset, WindowSettings, OnsetSettings, CancellationToken.None);
        var tempoStore = new JsonTempoAnalysisStore(Path.Combine(_tempDir, "tempo-cache"));
        var tempo = await new TempoDetector(tempoStore).DetectAsync(onsets, TempoSettings, CancellationToken.None);

        var gridStore = new JsonBeatGridStore(Path.Combine(_tempDir, "beatgrid-cache"));
        var builder = new BeatGridBuilder(gridStore);
        return await builder.BuildAsync(onsets, tempo, GridSettings, CancellationToken.None);
    }

    private static DisplayGraph TwoArchLayout() => new(
        SourceLayoutHash: "TEST-HASH-NOT-A-REAL-FILE",
        Models:
        [
            new DisplayModel(Arch1, "Arch 1", "Arches", [], []),
            new DisplayModel(Arch2, "Arch 2", "Arches", [], []),
        ],
        Groups: [],
        Warnings: []);

    [Fact]
    public async Task Step036_Acceptance_Passes_RenderedExportedEffectsFollowDetectedBeats()
    {
        var grid = await BuildClickGridAsync("gate-demo.wav", bpm: 120, clickCount: 16);
        Assert.True(grid.Beats.Length >= 8);

        // Comfortably past the last beat's own position, so even its full max-duration pulse fits
        // inside [range.Start, range.End) without being clipped — "confined to scope" clipping itself
        // is exercised separately, in BuildPulses_RangeNarrowerThanGrid_OnlyConfinesToScope_NegativePath.
        var range = new TimeWindow(0, grid.Beats[^1].TimeTicks + TimeSpan.FromMilliseconds(500).Ticks);
        var targets = new HashSet<ModelId> { Arch1, Arch2 };

        var pulses = _planner.BuildPulses(grid, targets, EffectRecipeRegistry.SimpleOnRecipeId, range);

        // One real pulse per detected beat — "Rendered/exported effects follow detected beats",
        // verbatim: every beat the analyzer/grid actually produced gets a real, concrete effect.
        Assert.Equal(grid.Beats.Length, pulses.Count);

        var layout = TwoArchLayout();
        var document = new SequenceDocument(Guid.NewGuid(), Revision: 1, pulses);
        var outputPath = Path.Combine(_tempDir, "step036.xsq");
        await new XsqExporter().ExportAsync(layout, document, outputPath, "2026.03", XsqExportPolicy.RejectIfExists, CancellationToken.None);

        // Independently re-read the real, on-disk exported file — not the in-memory plan — proving the
        // beats actually survived a real xLights XML round trip.
        var reread = new XsqStructuralReader().Read(outputPath);
        var orderedBeats = grid.Beats.OrderBy(b => b.TimeTicks).ToArray();
        var allPlacements = reread.ElementEffects
            .SelectMany(e => e.Effects.Select(p => (Element: e.Name, Placement: p)))
            .OrderBy(x => x.Placement.StartTimeMs)
            .ToArray();

        Assert.Equal(orderedBeats.Length, allPlacements.Length);

        const int frameMs = XsqExporter.FrameIntervalMs;
        for (var i = 0; i < orderedBeats.Length; i++)
        {
            var expectedStartMs = (long)Math.Round(TimeSpan.FromTicks(orderedBeats[i].TimeTicks).TotalMilliseconds / frameMs) * frameMs;
            Assert.Equal(expectedStartMs, allPlacements[i].Placement.StartTimeMs);
            Assert.True(allPlacements[i].Placement.EndTimeMs > allPlacements[i].Placement.StartTimeMs);

            // Alternating-chase: consecutive beats land on alternating real props, never the same prop
            // twice in a row (this step's own "alternating-chase" requirement, verbatim).
            var expectedElement = i % 2 == 0 ? Arch1.Value : Arch2.Value;
            Assert.Equal(expectedElement, allPlacements[i].Element);
        }

        // Every exported effect is confined to the requested [range.Start, range.End) scope — quality
        // gate matrix row 7, verbatim: "remain confined to time scope".
        var rangeEndMs = range.EndTicks / TimeSpan.TicksPerMillisecond;
        Assert.All(allPlacements, p => Assert.True(p.Placement.StartTimeMs >= 0));
        Assert.All(allPlacements, p => Assert.True(p.Placement.EndTimeMs <= rangeEndMs));
    }

    [Fact]
    public async Task BuildPulses_HigherConfidenceBeatsGetLongerPulses_BeatStrengthTranslatesToDuration()
    {
        var grid = await BuildClickGridAsync("confidence.wav", bpm: 120, clickCount: 16);
        var range = new TimeWindow(0, grid.Beats[^1].TimeTicks + TimeSpan.FromMilliseconds(500).Ticks);
        var targets = new HashSet<ModelId> { Arch1, Arch2 };

        var pulses = _planner.BuildPulses(grid, targets, EffectRecipeRegistry.SimpleOnRecipeId, range);
        var ordered = grid.Beats.OrderBy(b => b.TimeTicks).ToArray();

        Assert.True(ordered.Select(b => b.Confidence).Distinct().Count() > 1, "fixture must produce varying confidence to exercise this assertion");

        var byConfidence = ordered
            .Zip(pulses, (beat, pulse) => (beat.Confidence, Duration: pulse.EndTicks - pulse.StartTicks))
            .OrderBy(x => x.Confidence)
            .ToArray();

        // Monotonic non-decreasing: never a higher-confidence beat producing a strictly shorter pulse
        // than a lower-confidence one.
        for (var i = 1; i < byConfidence.Length; i++)
        {
            Assert.True(byConfidence[i].Duration >= byConfidence[i - 1].Duration);
        }
    }

    [Fact]
    public async Task BuildPulses_DurationsAreFrameSnapped()
    {
        var grid = await BuildClickGridAsync("frame-snap.wav", bpm: 120, clickCount: 8);
        var range = new TimeWindow(0, grid.Beats[^1].TimeTicks + TimeSpan.FromMilliseconds(500).Ticks);
        var targets = new HashSet<ModelId> { Arch1, Arch2 };

        var pulses = _planner.BuildPulses(grid, targets, EffectRecipeRegistry.SimpleOnRecipeId, range);

        var frameTicks = XsqExporter.FrameIntervalMs * TimeSpan.TicksPerMillisecond;
        Assert.All(pulses, p => Assert.Equal(0, (p.EndTicks - p.StartTicks) % frameTicks));
        Assert.All(pulses, p => Assert.True(p.EndTicks - p.StartTicks >= frameTicks));
    }

    [Fact]
    public async Task BuildPulses_RangeNarrowerThanGrid_OnlyConfinesToScope_NegativePath()
    {
        var grid = await BuildClickGridAsync("scoped.wav", bpm: 120, clickCount: 16);
        var ordered = grid.Beats.OrderBy(b => b.TimeTicks).ToArray();
        var targets = new HashSet<ModelId> { Arch1, Arch2 };

        // A narrow window covering only the first half of the beats.
        var midpointTicks = ordered[ordered.Length / 2].TimeTicks;
        var range = new TimeWindow(ordered[0].TimeTicks, midpointTicks);

        var pulses = _planner.BuildPulses(grid, targets, EffectRecipeRegistry.SimpleOnRecipeId, range);

        var expectedCount = ordered.Count(b => b.TimeTicks >= range.StartTicks && b.TimeTicks < range.EndTicks);
        Assert.Equal(expectedCount, pulses.Count);
        Assert.All(pulses, p => Assert.True(p.StartTicks >= range.StartTicks && p.EndTicks <= range.EndTicks));
    }

    [Fact]
    public void BuildPulses_GridWithNoBeatsInRange_ReturnsEmpty_SilenceEdgeCase()
    {
        var silentGrid = new BeatGrid(
            VersionId: Guid.NewGuid(),
            SongId: new SongId(Guid.NewGuid()),
            SourceHash: "silent-hash",
            SourceOnsetAnalysisVersionId: Guid.NewGuid(),
            SourceTempoAnalysisVersionId: Guid.NewGuid(),
            AnalyzerVersion: "test",
            Settings: GridSettings,
            SelectedTempo: null,
            Beats: [],
            Candidates: [],
            CorrectionVersion: 0);

        var targets = new HashSet<ModelId> { Arch1, Arch2 };
        var pulses = _planner.BuildPulses(silentGrid, targets, EffectRecipeRegistry.SimpleOnRecipeId, new TimeWindow(0, 1_000_000));

        Assert.Empty(pulses);
    }

    [Fact]
    public async Task BuildPulses_RangeOutsideAllBeats_ReturnsEmpty_SilenceEdgeCase()
    {
        var grid = await BuildClickGridAsync("outside-range.wav", bpm: 120, clickCount: 6);
        var targets = new HashSet<ModelId> { Arch1, Arch2 };
        var beyond = grid.Beats.Max(b => b.TimeTicks) + 10_000_000;

        var pulses = _planner.BuildPulses(grid, targets, EffectRecipeRegistry.SimpleOnRecipeId, new TimeWindow(beyond, beyond + 1_000_000));

        Assert.Empty(pulses);
    }

    [Fact]
    public async Task BuildPulses_NullGrid_ThrowsArgumentNullException()
    {
        var targets = new HashSet<ModelId> { Arch1, Arch2 };
        Assert.Throws<ArgumentNullException>(
            () => _planner.BuildPulses(null!, targets, EffectRecipeRegistry.SimpleOnRecipeId, new TimeWindow(0, 1)));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task BuildPulses_NullTargets_ThrowsArgumentNullException()
    {
        var grid = await BuildClickGridAsync("null-targets.wav", bpm: 120, clickCount: 6);
        Assert.Throws<ArgumentNullException>(
            () => _planner.BuildPulses(grid, null!, EffectRecipeRegistry.SimpleOnRecipeId, new TimeWindow(0, 1)));
    }

    [Fact]
    public async Task BuildPulses_EmptyTargets_ThrowsInvalid_NegativePath()
    {
        var grid = await BuildClickGridAsync("empty-targets.wav", bpm: 120, clickCount: 6);
        var range = new TimeWindow(0, grid.Beats[^1].TimeTicks + 1);

        var ex = Assert.Throws<RhythmGesturePlanInvalidException>(
            () => _planner.BuildPulses(grid, new HashSet<ModelId>(), EffectRecipeRegistry.SimpleOnRecipeId, range));

        Assert.Contains("At least one target", ex.Reason);
    }

    [Fact]
    public async Task BuildPulses_BlankRecipeId_ThrowsInvalid_NegativePath()
    {
        var grid = await BuildClickGridAsync("blank-recipe.wav", bpm: 120, clickCount: 6);
        var targets = new HashSet<ModelId> { Arch1 };
        var range = new TimeWindow(0, grid.Beats[^1].TimeTicks + 1);

        Assert.Throws<RhythmGesturePlanInvalidException>(() => _planner.BuildPulses(grid, targets, "  ", range));
    }

    [Fact]
    public async Task BuildPulses_UnknownRecipeId_ThrowsEffectPlanInvalid_NegativePath()
    {
        var grid = await BuildClickGridAsync("unknown-recipe.wav", bpm: 120, clickCount: 6);
        var targets = new HashSet<ModelId> { Arch1 };
        var range = new TimeWindow(0, grid.Beats[^1].TimeTicks + 1);

        // Surfaced by the real, injected IEffectRecipeRegistry itself — this planner never invents its
        // own verification of a recipe id (Section 2: "do not invent xLights effect parameters").
        Assert.Throws<EffectPlanInvalidException>(() => _planner.BuildPulses(grid, targets, "not.a.real.recipe", range));
    }

    [Fact]
    public async Task BuildPulses_InvalidTimingBoundary_EndNotAfterStart_ThrowsInvalid_NegativePath()
    {
        var grid = await BuildClickGridAsync("invalid-range.wav", bpm: 120, clickCount: 6);
        var targets = new HashSet<ModelId> { Arch1 };

        var ex = Assert.Throws<RhythmGesturePlanInvalidException>(
            () => _planner.BuildPulses(grid, targets, EffectRecipeRegistry.SimpleOnRecipeId, new TimeWindow(1000, 1000)));
        Assert.Contains("non-empty", ex.Reason);

        var ex2 = Assert.Throws<RhythmGesturePlanInvalidException>(
            () => _planner.BuildPulses(grid, targets, EffectRecipeRegistry.SimpleOnRecipeId, new TimeWindow(2000, 1000)));
        Assert.Contains("non-empty", ex2.Reason);
    }

    [Fact]
    public async Task BuildPulses_NegativeRangeStart_ThrowsInvalid_InvalidTimingBoundaryNegativePath()
    {
        var grid = await BuildClickGridAsync("negative-range.wav", bpm: 120, clickCount: 6);
        var targets = new HashSet<ModelId> { Arch1 };

        var ex = Assert.Throws<RhythmGesturePlanInvalidException>(
            () => _planner.BuildPulses(grid, targets, EffectRecipeRegistry.SimpleOnRecipeId, new TimeWindow(-1, 1000)));
        Assert.Contains("StartTicks", ex.Reason);
    }

    [Fact]
    public void Constructor_InvalidSettings_ThrowsArgumentOutOfRange_NegativePath()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RhythmGesturePlanner(new EffectRecipeRegistry(), new RhythmGestureSettings(MinPulseDurationTicks: 0, MaxPulseDurationTicks: 100)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RhythmGesturePlanner(new EffectRecipeRegistry(), new RhythmGestureSettings(MinPulseDurationTicks: 100, MaxPulseDurationTicks: 50)));
    }

    [Fact]
    public void Constructor_NullRegistry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RhythmGesturePlanner(null!));
    }
}
