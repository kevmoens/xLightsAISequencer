using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Structure.Tests;

/// <summary>
/// PHASE-08 / Step 052 -- "Compute normalized bar/phrase feature fingerprints from audio and events."
/// Required acceptance: "Similar motifs compare consistently across changed gain." Failure/edge case:
/// "Silence, invalid timing boundaries and missing media must be handled explicitly."
/// </summary>
public sealed class Step052AcceptanceTests : IDisposable
{
    private const int SampleRate = 8000;
    private const long TicksPerSecond = 10_000_000L;
    private static readonly RhythmSettings WindowSettings = new(AnalysisSampleRate: SampleRate, FftSize: 256, HopSamples: 128);
    private static readonly BarFingerprintSettings Settings = new(ChromaBins: 12, RecipeId: BarFingerprintBuilder.EnergyPlusChromaRecipeId);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-step052-{Guid.NewGuid():N}");

    public Step052AcceptanceTests()
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

    private static long Seconds(double s) => (long)Math.Round(s * TicksPerSecond);

    /// <summary>
    /// Four beats per bar at 2 beats/second (2 bars total), each beat's own downbeat landing exactly on
    /// a bar boundary -- deterministic, no real onset/tempo detector involved (out of this step's own
    /// scope; this step consumes an already-approved <see cref="BeatGrid"/>, it does not build one).
    /// </summary>
    private static BeatGrid BuildBeatGrid(SongId songId, string sourceHash, int barCount)
    {
        var beats = new List<BeatMarker>();
        for (var i = 0; i < barCount * 4; i++)
        {
            beats.Add(new BeatMarker(
                Id: Guid.NewGuid(),
                TimeTicks: Seconds(i * 0.5),
                BeatWithinBar: i % 4,
                BarIndex: i / 4,
                Confidence: 1.0,
                UserCorrected: false));
        }

        return new BeatGrid(
            VersionId: Guid.NewGuid(),
            SongId: songId,
            SourceHash: sourceHash,
            SourceOnsetAnalysisVersionId: Guid.NewGuid(),
            SourceTempoAnalysisVersionId: Guid.NewGuid(),
            AnalyzerVersion: "test-fixture/1.0.0",
            Settings: new BeatGridSettings(BeatsPerBar: 4, AlignmentToleranceTicks: 0),
            SelectedTempo: new TempoHypothesis(Bpm: 120.0, Confidence: 1.0, MeterGuess: "4/4"),
            Beats: [.. beats],
            Candidates: [new TempoHypothesis(Bpm: 120.0, Confidence: 1.0, MeterGuess: "4/4")],
            CorrectionVersion: 0);
    }

    /// <summary>
    /// A gapless, contiguous energy envelope covering the full fixture duration at a constant
    /// <paramref name="normalizedEnergy"/> level -- deliberately flat so the test isolates the chroma
    /// (melodic) comparison; <see cref="BuildVaryingEnergyEnvelope"/> below covers the energy-shape case.
    /// </summary>
    private static EnergyEnvelope BuildFlatEnergyEnvelope(SongId songId, string sourceHash, double durationSeconds, float rms, float normalizedEnergy)
    {
        var frames = new List<EnergyFrame>();
        long sample = 0;
        var totalSamples = (long)(durationSeconds * SampleRate);
        while (sample < totalSamples)
        {
            var end = Math.Min(sample + WindowSettings.HopSamples, totalSamples);
            frames.Add(new EnergyFrame(sample, end, rms, normalizedEnergy));
            sample = end;
        }

        return new EnergyEnvelope(
            VersionId: Guid.NewGuid(),
            SongId: songId,
            SourceHash: sourceHash,
            SourceDecoderVersion: "test-fixture/1.0.0",
            AnalyzerVersion: "test-fixture/1.0.0",
            Settings: WindowSettings,
            Frames: [.. frames]);
    }

    private static MelodyTrack BuildMelody(string sourceAssetHash, params (double startSeconds, double endSeconds, int midiNote)[] notes)
    {
        var events = notes.Select(n => new NoteEvent(
            Id: Guid.NewGuid(),
            StartTicks: Seconds(n.startSeconds),
            EndTicks: Seconds(n.endSeconds),
            MidiNote: n.midiNote,
            Velocity: 0.8,
            Confidence: 1.0,
            UserCorrected: false)).ToList();

        return new MelodyTrack(Guid.NewGuid(), sourceAssetHash, events, []);
    }

    [Fact]
    public async Task Step052_Acceptance_Passes_SimilarMotifsCompareConsistentlyAcrossChangedGain()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var beatGrid = BuildBeatGrid(songId, sourceHash, barCount: 3);

        // Bar 0 and bar 1 play the identical C-E-G motif (same relative pitch content, same bar
        // duration); bar 2 plays a deliberately different motif (a single low, sustained note) --
        // the "dissimilar bar" control.
        var melody = BuildMelody(
            sourceAssetHash: "melody-hash",
            (0.0, 0.4, 60), (0.4, 0.8, 64), (0.8, 1.2, 67), // bar 0: C4-E4-G4
            (2.0, 2.4, 60), (2.4, 2.8, 64), (2.8, 3.2, 67), // bar 1: identical C4-E4-G4
            (4.0, 4.6, 48)); // bar 2: a single low, sustained note -- a different musical idea.

        var quietEnergy = BuildFlatEnergyEnvelope(songId, sourceHash, durationSeconds: 6.0, rms: 0.1f, normalizedEnergy: 0.4f);
        var context = new BarFingerprintContext(songId, sourceHash, beatGrid, quietEnergy, melody);

        var builder = new BarFingerprintBuilder(new JsonBarFingerprintStore(Path.Combine(_tempDir, "fp-cache")));
        var quietSet = await builder.BuildAsync(context, Settings, CancellationToken.None);

        Assert.Equal(3, quietSet.Bars.Count);
        Assert.All(quietSet.Bars, b => Assert.Equal(2 + Settings.ChromaBins, b.Features.Count));

        // A loud recording of the *exact same musical content* -- every EnergyFrame.Rms is 10x louder,
        // but NormalizedEnergy (peak-normalized within its own envelope) is identical by construction,
        // mirroring what a real gain-invariant analyzer would report.
        var loudEnergy = BuildFlatEnergyEnvelope(songId, sourceHash, durationSeconds: 6.0, rms: 1.0f, normalizedEnergy: 0.4f);
        var loudContext = context with { Energy = loudEnergy };
        var loudBuilder = new BarFingerprintBuilder(new JsonBarFingerprintStore(Path.Combine(_tempDir, "fp-cache-loud")));
        var loudSet = await loudBuilder.BuildAsync(loudContext, Settings, CancellationToken.None);

        // Required acceptance, verbatim: "Similar motifs compare consistently across changed gain."
        // Bar 0 vs. bar 1 (the same motif) must score highly similar in BOTH the quiet and loud takes,
        // and the similarity score itself must not have shifted with gain.
        var quietSimilarity = FingerprintSimilarity.Cosine(quietSet.Bars[0], quietSet.Bars[1]);
        var loudSimilarity = FingerprintSimilarity.Cosine(loudSet.Bars[0], loudSet.Bars[1]);

        Assert.True(quietSimilarity > 0.99, $"same-motif bars should compare as near-identical; got {quietSimilarity}.");
        Assert.Equal(quietSimilarity, loudSimilarity, precision: 9);

        // The dissimilar control bar (bar 2) must score lower against bar 0 than bar 1 does, in both takes.
        var quietDissimilar = FingerprintSimilarity.Cosine(quietSet.Bars[0], quietSet.Bars[2]);
        var loudDissimilar = FingerprintSimilarity.Cosine(loudSet.Bars[0], loudSet.Bars[2]);
        Assert.True(quietDissimilar < quietSimilarity);
        Assert.Equal(quietDissimilar, loudDissimilar, precision: 9);

        // "retain enough features to explain matches": every feature vector is inspectable, not
        // collapsed to a single opaque score.
        Assert.Equal(quietSet.Bars[0].Features, quietSet.Bars[1].Features);

        // Provenance recorded (Section 4's "every cache key includes algorithm/model/settings versions").
        Assert.Equal(sourceHash, quietSet.SourceHash);
        Assert.Equal(beatGrid.VersionId, quietSet.SourceBeatGridVersionId);
        Assert.Equal(BarFingerprintBuilder.Version, quietSet.AnalyzerVersion);
        Assert.Equal(Settings, quietSet.Settings);
        Assert.True(File.Exists(Path.Combine(_tempDir, "fp-cache", $"bar-fingerprints-{songId.Value:N}.json")));

        // Caching ("with caching", Step 052's own implementation guidance): rebuilding from the exact
        // same context/settings reopens the cached set rather than recomputing a new VersionId.
        var cached = await builder.BuildAsync(context, Settings, CancellationToken.None);
        Assert.Equal(quietSet.VersionId, cached.VersionId);
    }

    [Fact]
    public async Task BuildAsync_SilentBeatGrid_ReturnsEmptyBarsWithoutThrowing_HandlesSilenceExplicitly()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";

        // A beat grid with zero beats -- Step 034's own disclosed "silence" output shape.
        var emptyBeatGrid = new BeatGrid(
            VersionId: Guid.NewGuid(), SongId: songId, SourceHash: sourceHash,
            SourceOnsetAnalysisVersionId: Guid.NewGuid(), SourceTempoAnalysisVersionId: Guid.NewGuid(),
            AnalyzerVersion: "test-fixture/1.0.0", Settings: new BeatGridSettings(4, 0),
            SelectedTempo: null, Beats: [], Candidates: [], CorrectionVersion: 0);

        var energy = BuildFlatEnergyEnvelope(songId, sourceHash, durationSeconds: 1.0, rms: 0f, normalizedEnergy: 0f);
        var melody = new MelodyTrack(Guid.NewGuid(), "melody-hash", [], []);
        var context = new BarFingerprintContext(songId, sourceHash, emptyBeatGrid, energy, melody);

        var builder = new BarFingerprintBuilder(new JsonBarFingerprintStore(Path.Combine(_tempDir, "fp-cache")));
        var set = await builder.BuildAsync(context, Settings, CancellationToken.None);

        Assert.Empty(set.Bars);
        Assert.True(File.Exists(Path.Combine(_tempDir, "fp-cache", $"bar-fingerprints-{songId.Value:N}.json")));
    }

    [Fact]
    public async Task BuildAsync_EmptySourceHash_ThrowsBarFingerprintInvalidException_MissingMediaNegativePath()
    {
        var songId = new SongId(Guid.NewGuid());
        var beatGrid = BuildBeatGrid(songId, sourceHash: "", barCount: 1);
        var energy = BuildFlatEnergyEnvelope(songId, "", durationSeconds: 2.0, rms: 0.1f, normalizedEnergy: 0.5f);
        var melody = new MelodyTrack(Guid.NewGuid(), "melody-hash", [], []);
        var context = new BarFingerprintContext(songId, "", beatGrid, energy, melody);

        var builder = new BarFingerprintBuilder(new JsonBarFingerprintStore(Path.Combine(_tempDir, "fp-cache")));

        var ex = await Assert.ThrowsAsync<BarFingerprintInvalidException>(
            () => builder.BuildAsync(context, Settings, CancellationToken.None));
        Assert.Contains("missing media", ex.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(_tempDir, "fp-cache", $"bar-fingerprints-{songId.Value:N}.json")));
    }

    [Fact]
    public async Task BuildAsync_MismatchedSourceHash_ThrowsBarFingerprintInvalidException_InvalidTimingBoundaryNegativePath()
    {
        var songId = new SongId(Guid.NewGuid());
        var beatGrid = BuildBeatGrid(songId, sourceHash: "hash-a", barCount: 1);
        var energy = BuildFlatEnergyEnvelope(songId, "hash-a", durationSeconds: 2.0, rms: 0.1f, normalizedEnergy: 0.5f);
        var melody = new MelodyTrack(Guid.NewGuid(), "melody-hash", [], []);

        // Context declares a different source hash than the one the BeatGrid/EnergyEnvelope were
        // actually built from -- a stale/mismatched provenance pair, this step's own named failure case.
        var context = new BarFingerprintContext(songId, "hash-b", beatGrid, energy, melody);

        var builder = new BarFingerprintBuilder(new JsonBarFingerprintStore(Path.Combine(_tempDir, "fp-cache")));

        var ex = await Assert.ThrowsAsync<BarFingerprintInvalidException>(
            () => builder.BuildAsync(context, Settings, CancellationToken.None));
        Assert.Contains("SourceHash", ex.Reason);
        Assert.False(File.Exists(Path.Combine(_tempDir, "fp-cache", $"bar-fingerprints-{songId.Value:N}.json")));
    }

    [Fact]
    public async Task BuildAsync_NonPositiveChromaBins_ThrowsBarFingerprintInvalidException_NegativePath()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var beatGrid = BuildBeatGrid(songId, sourceHash, barCount: 1);
        var energy = BuildFlatEnergyEnvelope(songId, sourceHash, durationSeconds: 2.0, rms: 0.1f, normalizedEnergy: 0.5f);
        var melody = new MelodyTrack(Guid.NewGuid(), "melody-hash", [], []);
        var context = new BarFingerprintContext(songId, sourceHash, beatGrid, energy, melody);

        var builder = new BarFingerprintBuilder(new JsonBarFingerprintStore(Path.Combine(_tempDir, "fp-cache")));

        var ex = await Assert.ThrowsAsync<BarFingerprintInvalidException>(
            () => builder.BuildAsync(context, Settings with { ChromaBins = 0 }, CancellationToken.None));
        Assert.Contains("ChromaBins", ex.Reason);
    }

    [Fact]
    public async Task BuildAsync_NullArguments_ThrowArgumentNullException()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var beatGrid = BuildBeatGrid(songId, sourceHash, barCount: 1);
        var energy = BuildFlatEnergyEnvelope(songId, sourceHash, durationSeconds: 2.0, rms: 0.1f, normalizedEnergy: 0.5f);
        var melody = new MelodyTrack(Guid.NewGuid(), "melody-hash", [], []);
        var context = new BarFingerprintContext(songId, sourceHash, beatGrid, energy, melody);

        var builder = new BarFingerprintBuilder(new JsonBarFingerprintStore(Path.Combine(_tempDir, "fp-cache")));

        await Assert.ThrowsAsync<ArgumentNullException>(() => builder.BuildAsync(null!, Settings, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => builder.BuildAsync(context, null!, CancellationToken.None));
    }

    [Fact]
    public void Cosine_DifferentDimensionality_ThrowsArgumentException()
    {
        var a = new BarFingerprint(0, new TimeWindow(0, 1), [1.0, 0.0], 1.0, false);
        var b = new BarFingerprint(0, new TimeWindow(0, 1), [1.0, 0.0, 0.0], 1.0, false);

        Assert.Throws<ArgumentException>(() => FingerprintSimilarity.Cosine(a, b));
    }

    [Fact]
    public void Cosine_EitherFingerprintIsZeroVector_ReturnsZero_NotNaN()
    {
        var silent = new BarFingerprint(0, new TimeWindow(0, 1), [0.0, 0.0, 0.0], 0.0, true);
        var real = new BarFingerprint(1, new TimeWindow(1, 2), [1.0, 0.5, 0.2], 1.0, false);

        Assert.Equal(0.0, FingerprintSimilarity.Cosine(silent, real));
        Assert.Equal(0.0, FingerprintSimilarity.Cosine(silent, silent));
    }
}
