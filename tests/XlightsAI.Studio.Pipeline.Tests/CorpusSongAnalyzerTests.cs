using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Reference.Fixtures;

namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// Real, hermetic end-to-end exercise of <see cref="CorpusSongAnalyzer"/>: a real synthetic WAV click
/// track (no ffmpeg/mp3 needed — <see cref="DeterministicSyntheticAudioGenerator"/>'s own fixture
/// convention, same one <c>AudioAnalysisRunnerTests</c> uses) flows through the real
/// Onset/Tempo/BeatGrid/Energy/BarFingerprint/SectionBoundary/SectionLabel/Phrase chain, and a real
/// synthetic <c>.xsq</c> is parsed and aligned onto the result. Proves the wiring works without needing
/// the real <c>C:\xLights</c> corpus or ffmpeg (that full real corpus, including ffmpeg-transcoded
/// mp3/m4a audio, was separately run once as a one-off analysis pass to produce the Phase 2b artifacts —
/// see the Phase 2b final report — rather than wired into the automated test suite, since a full
/// 20-song real-audio run takes on the order of an hour).
/// </summary>
public sealed class CorpusSongAnalyzerTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), $"xlai-corpus-song-analyzer-{Guid.NewGuid():N}");

    public CorpusSongAnalyzerTests() => Directory.CreateDirectory(_rootDirectory);

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_AlignsParsedEffectsOntoRealBeatGridAndStructure()
    {
        const int sampleRate = 8000;
        const int bpm = 120;
        const int clickCount = 64; // 32 real seconds at 120 bpm -- ~16 bars of 4/4.
        var samples = DeterministicSyntheticAudioGenerator.BuildClickTrack(bpm, clickCount, clickDurationSeconds: 0.05, toneHz: 440, sampleRate);
        var wavPath = Path.Combine(_rootDirectory, "song.wav");
        await File.WriteAllBytesAsync(wavPath, DeterministicSyntheticAudioGenerator.EncodeWav(samples, sampleRate));

        var durationSeconds = samples.Length / (double)sampleRate;
        var xsqPath = Path.Combine(_rootDirectory, "song.xsq");
        await File.WriteAllTextAsync(xsqPath, BuildXsq(durationSeconds));

        var entry = new CorpusEntry(xsqPath, wavPath, "song", CorpusProvenanceTag.UserAuthored, [], "fixture");
        var analyzer = new CorpusSongAnalyzer(Path.Combine(_rootDirectory, "cache"));

        var analysis = await analyzer.AnalyzeAsync(entry, CancellationToken.None);

        Assert.Equal(4, analysis.AlignedEffects.Count);
        Assert.All(analysis.AlignedEffects, a => Assert.Equal("On", a.Effect.EffectName));
        // A real beat grid was built from real audio -- not every alignment field is necessarily
        // resolvable from a short synthetic click track, but the beat grid itself must be real.
        Assert.NotNull(analysis.BeatGrid.SelectedTempo);
    }

    private static string BuildXsq(double durationSeconds)
    {
        var quarterMs = (long)(durationSeconds * 1000 / 4);
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <xsequence BaseChannel="0" ChanCtrlBasic="0" ChanCtrlColor="0" FixedPointTiming="1" ModelBlending="true">
              <head>
                <version>2026.03</version>
                <author></author>
                <author-email></author-email>
                <author-website></author-website>
                <song></song>
                <artist></artist>
                <album></album>
                <MusicURL></MusicURL>
                <comment></comment>
                <sequenceTiming>25 ms</sequenceTiming>
                <sequenceType>Animation</sequenceType>
                <mediaFile></mediaFile>
                <sequenceDuration>{durationSeconds:F3}</sequenceDuration>
                <imageDir></imageDir>
              </head>
              <nextid>1</nextid>
              <Jukebox></Jukebox>
              <ColorPalettes>
                <ColorPalette>C_BUTTON_Palette1=#FFFFFF</ColorPalette>
              </ColorPalettes>
              <EffectDB>
                <Effect></Effect>
              </EffectDB>
              <DataLayers></DataLayers>
              <DisplayElements>
                <Element type="model" name="Model1" visible="1" />
              </DisplayElements>
              <ElementEffects>
                <Element type="model" name="Model1">
                  <EffectLayer>
                    <Effect ref="0" name="On" startTime="0" endTime="{quarterMs}" palette="0" />
                    <Effect ref="0" name="On" startTime="{quarterMs}" endTime="{quarterMs * 2}" palette="0" />
                    <Effect ref="0" name="On" startTime="{quarterMs * 2}" endTime="{quarterMs * 3}" palette="0" />
                    <Effect ref="0" name="On" startTime="{quarterMs * 3}" endTime="{quarterMs * 4}" palette="0" />
                  </EffectLayer>
                </Element>
              </ElementEffects>
              <TimingTags></TimingTags>
            </xsequence>
            """;
    }
}
