using XlightsAI.Studio.Reference.Fixtures;

namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// Build-plan Phase 25 Part C: a real, hermetic end-to-end exercise of the training-only ingestion
/// path -- <see cref="CorpusTrainingRunner"/> discovers an external sequence folder via
/// <see cref="Knowledge.ExternalTrainingManifestBuilder"/> (deliberately never touching
/// <see cref="Knowledge.CorpusManifestBuilder.KnownResolutions"/>), runs the real
/// <see cref="CorpusSongAnalyzer"/> over it, and writes an aggregate-only
/// <c>corpus-training-report.json</c>. Uses the same synthetic WAV click-track/xsq fixture convention as
/// <see cref="CorpusSongAnalyzerTests"/> so this stays fast and ffmpeg-free.
/// </summary>
public sealed class CorpusTrainingRunnerTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), $"xlai-corpus-training-{Guid.NewGuid():N}");

    public CorpusTrainingRunnerTests() => Directory.CreateDirectory(_rootDirectory);

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_DiscoversExternalSequenceNotInKnownResolutions_AnalyzesIt_AndWritesAggregateOnlyReport()
    {
        const int sampleRate = 8000;
        const int bpm = 120;
        const int clickCount = 64; // 32 real seconds at 120 bpm -- ~16 bars of 4/4.
        var samples = DeterministicSyntheticAudioGenerator.BuildClickTrack(bpm, clickCount, clickDurationSeconds: 0.05, toneHz: 440, sampleRate);

        var showDirectory = Path.Combine(_rootDirectory, "Some Downloaded Show");
        Directory.CreateDirectory(showDirectory);

        var wavPath = Path.Combine(showDirectory, "totally-unaudited-song.wav");
        await File.WriteAllBytesAsync(wavPath, DeterministicSyntheticAudioGenerator.EncodeWav(samples, sampleRate));

        var durationSeconds = samples.Length / (double)sampleRate;
        var xsqPath = Path.Combine(showDirectory, "totally-unaudited-song.xsq");
        await File.WriteAllTextAsync(xsqPath, BuildXsq(durationSeconds));

        // A real xlights_rgbeffects.xml alongside the .xsq -- not guaranteed for every external
        // training sequence, but when present (as here), coverage ratios are computed from it.
        await File.WriteAllTextAsync(Path.Combine(showDirectory, "xlights_rgbeffects.xml"), BuildRgbEffects());

        var knowledgeDirectory = Path.Combine(_rootDirectory, "training-knowledge");
        var messages = new List<string>();

        var result = await CorpusTrainingRunner.RunAsync(showDirectory, knowledgeDirectory, messages.Add, CancellationToken.None);

        // Discovered despite never appearing in CorpusManifestBuilder.KnownResolutions.
        Assert.Equal(1, result.Report.DiscoveredSequenceCount);
        Assert.Equal(1, result.Report.AnalyzedSequenceCount);
        Assert.Equal(0, result.Report.FailedSequenceCount);
        Assert.Single(result.Manifest.Entries);
        Assert.Equal(Knowledge.CorpusProvenanceTag.ExternalReference, result.Manifest.Entries[0].Provenance);

        // Real group geometry: "Everything" covers both models (100%), "HalfHouse" covers only the
        // tree (50%).
        var distribution = result.Report.CatchAllCoverageRatioDistribution;
        Assert.Equal(1, distribution.SongsWithLayoutCount);
        Assert.Equal(2, distribution.GroupSampleCount);
        Assert.Equal(0.5, distribution.Min);
        Assert.Equal(1.0, distribution.Max);
        Assert.Equal(2, distribution.Histogram.Values.Sum());

        // Build-plan Phase 25 Part C §3 bullets 3/4: layer-complexity and reuse-rate stats are always
        // present on the report (even when empty) -- this fixture's single model's effects never overlap,
        // so no window reaches the 3+ layer-complexity floor.
        Assert.Equal(0, result.Report.LayerComplexity.ThreePlus.WindowCount);
        Assert.Equal(0, result.Report.LayerComplexity.FourPlus.WindowCount);
        Assert.Equal(0, result.Report.LayerComplexity.FivePlus.WindowCount);
        Assert.True(result.Report.ReuseRate.TotalBucketCount >= 0);
        Assert.True(result.Report.ReuseRate.TotalWindowCount >= 0);

        // Timer/progress reporting: at least one elapsed-time progress line was reported.
        Assert.Contains(messages, m => m.Contains("elapsed"));

        // The persisted artifact is corpus-training-report.json, never exemplar-index.json/pattern-library.json.
        Assert.Equal(Path.Combine(knowledgeDirectory, "corpus-training-report.json"), result.ReportPath);
        Assert.True(File.Exists(result.ReportPath));

        // Respect the license boundary (build-plan Phase 25 Part C §4): the persisted report never
        // carries a raw model/group/effect name or settings string from the source sequence.
        var reportJson = await File.ReadAllTextAsync(result.ReportPath);
        Assert.DoesNotContain("MegaTree", reportJson);
        Assert.DoesNotContain("Arch-01", reportJson);
        Assert.DoesNotContain("Everything", reportJson);
        Assert.DoesNotContain("HalfHouse", reportJson);
        Assert.DoesNotContain("Tree 360", reportJson);
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
                <Element type="model" name="MegaTree-Center" visible="1" />
              </DisplayElements>
              <ElementEffects>
                <Element type="model" name="MegaTree-Center">
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

    private static string BuildRgbEffects() => """
        <xrgb FormatVersion="2" BaseChannel="0" ModelBlacklist="">
          <models>
            <model
              name="MegaTree-Center"
              DisplayAs="Tree 360"
              StringType="RGB Nodes"
              parm1="16"
              parm2="50"
              parm3="1"
              WorldPosX="0"
              WorldPosY="0" />
            <model
              name="Arch-01"
              DisplayAs="Arches"
              StringType="RGB Nodes"
              parm1="16"
              parm2="50"
              parm3="1"
              WorldPosX="10"
              WorldPosY="0" />
          </models>
          <modelGroups>
            <modelGroup name="Everything" models="MegaTree-Center,Arch-01" />
            <modelGroup name="HalfHouse" models="MegaTree-Center" />
          </modelGroups>
        </xrgb>
        """;
}
