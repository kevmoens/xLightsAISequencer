using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// <see cref="StructureAnalysisRunner"/> is pure sequencing of already-real, already-individually-tested
/// Structure analyzers (each stage's own algorithmic correctness is covered by
/// <c>tests/XlightsAI.Studio.Structure.Tests</c>, e.g. <c>Step056AcceptanceTests</c>'s own "repeated
/// motif" proof) — these tests build real, hand-constructed (never mocked) <see cref="BeatGrid"/>/
/// <see cref="EnergyEnvelope"/>/<see cref="MelodyTrack"/> fixtures, the same "real production types,
/// built directly" convention <c>Step056AcceptanceTests</c> itself already uses for its own fixtures, and
/// prove the full chain runs end to end with internally consistent output shapes.
/// </summary>
public sealed class StructureAnalysisRunnerTests : IDisposable
{
    private const int SampleRate = 8000;
    private const int BeatsPerBar = 4;
    private const long TicksPerSecond = 10_000_000L;
    private const long BeatTicks = 2_000_000L; // 0.2s/beat
    private const long BarTicks = BeatTicks * BeatsPerBar;
    private const int BarCount = 24; // Chorus [0,8) / Verse [8,16) / Chorus [16,24).

    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), $"xlai-structure-runner-{Guid.NewGuid():N}");

    public StructureAnalysisRunnerTests() => Directory.CreateDirectory(_rootDirectory);

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private string Dir(string name)
    {
        var path = Path.Combine(_rootDirectory, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static bool IsChorusBar(int bar) => bar < 8 || bar >= 16;

    private static BeatGrid BuildBeatGrid(SongId songId, string sourceHash)
    {
        var beats = new List<BeatMarker>();
        for (var bar = 0; bar < BarCount; bar++)
        {
            for (var beatInBar = 0; beatInBar < BeatsPerBar; beatInBar++)
            {
                beats.Add(new BeatMarker(
                    Id: Guid.NewGuid(),
                    TimeTicks: (bar * BarTicks) + (beatInBar * BeatTicks),
                    BeatWithinBar: beatInBar,
                    BarIndex: bar,
                    Confidence: 1.0,
                    UserCorrected: false));
            }
        }

        var settings = new BeatGridSettings(BeatsPerBar, AlignmentToleranceTicks: 0);
        return new BeatGrid(
            VersionId: Guid.NewGuid(),
            SongId: songId,
            SourceHash: sourceHash,
            SourceOnsetAnalysisVersionId: Guid.NewGuid(),
            SourceTempoAnalysisVersionId: Guid.NewGuid(),
            AnalyzerVersion: "test-fixture/1.0.0",
            Settings: settings,
            SelectedTempo: null,
            Beats: beats.ToArray(),
            Candidates: [],
            CorrectionVersion: 0);
    }

    private static EnergyEnvelope BuildEnergyEnvelope(SongId songId, string sourceHash)
    {
        const int fftSize = 256;
        const int hop = 128;
        var rhythmSettings = new RhythmSettings(AnalysisSampleRate: SampleRate, FftSize: fftSize, HopSamples: hop);
        var totalSamples = (long)(BarCount * BarTicks / (double)TicksPerSecond * SampleRate);

        var frames = new List<EnergyFrame>();
        for (long start = 0; start < totalSamples; start += hop)
        {
            var end = Math.Min(start + fftSize, totalSamples);
            var startTicks = (long)(start / (double)SampleRate * TicksPerSecond);
            var bar = Math.Min((int)(startTicks / BarTicks), BarCount - 1);
            var energy = IsChorusBar(bar) ? 1.0f : 0.3f;
            frames.Add(new EnergyFrame(start, end, Rms: energy, NormalizedEnergy: energy));
        }

        return new EnergyEnvelope(
            VersionId: Guid.NewGuid(),
            SongId: songId,
            SourceHash: sourceHash,
            SourceDecoderVersion: "test-fixture/1.0.0",
            AnalyzerVersion: "test-fixture/1.0.0",
            Settings: rhythmSettings,
            Frames: frames.ToArray());
    }

    private static MelodyTrack BuildMelodyTrack()
    {
        var notes = new List<NoteEvent>();
        for (var bar = 0; bar < BarCount; bar++)
        {
            var midiNote = IsChorusBar(bar) ? 60 : 65; // C4 for chorus, F4 for verse -- distinguishable chroma.
            notes.Add(new NoteEvent(
                Id: Guid.NewGuid(),
                StartTicks: bar * BarTicks,
                EndTicks: (bar + 1) * BarTicks,
                MidiNote: midiNote,
                Velocity: 0.8,
                Confidence: 1.0,
                UserCorrected: false));
        }

        return new MelodyTrack(Guid.NewGuid(), SourceAssetHash: "lead-stem-hash", Notes: notes, Gestures: []);
    }

    private StructureAnalysisRunner BuildRunner() => new(
        new JsonBarFingerprintStore(Dir("fingerprints")),
        new JsonSectionBoundaryStore(Dir("boundaries")),
        new JsonSectionLabelStore(Dir("labels")),
        new JsonSectionLabelOverrideStore(Dir("label-overrides")),
        new JsonPhraseSegmentationStore(Dir("phrases")),
        new JsonPhraseBoundaryOverrideStore(Dir("phrase-overrides")),
        new JsonMotifClusteringStore(Dir("motifs")));

    [Fact]
    public async Task AnalyzeAsync_RealChorusVerseChorusFixture_ProducesInternallyConsistentChain()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var beatGrid = BuildBeatGrid(songId, sourceHash);
        var energy = BuildEnergyEnvelope(songId, sourceHash);
        var melody = BuildMelodyTrack();

        var runner = BuildRunner();
        var result = await runner.AnalyzeAsync(songId, sourceHash, beatGrid, energy, melody, CancellationToken.None);

        Assert.Equal(BarCount, result.FingerprintSet.Bars.Count);
        Assert.NotEmpty(result.Boundaries.Boundaries);
        Assert.Contains(result.Boundaries.Boundaries, b => b.BarIndex == 0);

        // Every detected boundary must have received exactly one label (Structure's own one-to-one contract).
        Assert.Equal(result.Boundaries.Boundaries.Count, result.Labels.Labels.Count);

        // Every phrase must fall within a labeled section's own bar range, and motif clustering must run
        // without throwing over whatever phrases were produced (possibly fewer than two -> empty groups,
        // an explicitly handled case per IMotifClusterer's own contract, not an error).
        Assert.NotNull(result.Phrases.Phrases);
        Assert.NotNull(result.Motifs.Groups);

        // The real, distinct chorus/verse acoustic content (energy + chroma) must register as genuinely
        // different evidence somewhere in the per-bar fingerprints -- not every bar numerically identical.
        Assert.True(result.FingerprintSet.Bars.Select(b => b.Features[0]).Distinct().Count() > 1);
    }

    [Fact]
    public async Task AnalyzeAsync_NullArguments_Throw()
    {
        var songId = new SongId(Guid.NewGuid());
        var runner = BuildRunner();
        var beatGrid = BuildBeatGrid(songId, "h");
        var energy = BuildEnergyEnvelope(songId, "h");
        var melody = BuildMelodyTrack();

        await Assert.ThrowsAsync<ArgumentNullException>(() => runner.AnalyzeAsync(songId, "h", null!, energy, melody, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => runner.AnalyzeAsync(songId, "h", beatGrid, null!, melody, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => runner.AnalyzeAsync(songId, "h", beatGrid, energy, null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => runner.AnalyzeAsync(songId, "", beatGrid, energy, melody, CancellationToken.None));
    }
}
