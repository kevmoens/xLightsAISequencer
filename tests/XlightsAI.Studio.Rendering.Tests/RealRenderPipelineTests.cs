using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Rendering.Tests;

/// <summary>
/// End-to-end real-install corroboration of the full PHASE-07 7a render pipeline (probe -> real headless
/// render -> real FSEQ decode -> real channel-to-model projection) against the real house at
/// <c>C:\xLights</c> and the real installed xLights at <c>C:\Program Files\xLights</c>, when both are
/// present. Skips cleanly otherwise (CI, a dev box without either).
/// </summary>
[Trait("Category", "Integration")]
public sealed class RealRenderPipelineTests : IDisposable
{
    private const string RealExecutablePath = @"C:\Program Files\xLights\xlights.exe";
    private const string RealShowDirectory = @"C:\xLights";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-real-renderpipeline-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// Targets the real "Arches" model group (referenced as <c>type="model"</c> per the confirmed
    /// PHASE-0 export-defect finding) with one trivial "On" effect -- the same minimal shape verified by
    /// hand against this real install before being encoded into <see cref="XlightsRenderCapabilityProbe"/>.
    /// </summary>
    private const string MinimalSequenceXml = """
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
            <sequenceDuration>1.000</sequenceDuration>
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
            <Element type="model" name="Arches" visible="1" />
          </DisplayElements>
          <ElementEffects>
            <Element type="model" name="Arches">
              <EffectLayer>
                <Effect ref="0" name="On" startTime="0" endTime="1000" palette="0" />
              </EffectLayer>
            </Element>
          </ElementEffects>
          <TimingTags>
            <Tag number="0" position="-1" />
          </TimingTags>
        </xsequence>
        """;

    [Fact]
    public async Task RealHouse_EndToEnd_RendersDecodesAndProjectsRealFrames()
    {
        if (!File.Exists(RealExecutablePath) || !Directory.Exists(RealShowDirectory))
        {
            return; // No real install/house on this machine -- nothing to corroborate here.
        }

        Directory.CreateDirectory(_root);
        var xsqPath = Path.Combine(_root, "real-pipeline-test.xsq");
        await File.WriteAllTextAsync(xsqPath, MinimalSequenceXml);
        var outputDirectory = Path.Combine(_root, "out");

        var layout = await new ShowLayoutImporter().ImportReadOnlyAsync(RealShowDirectory, CancellationToken.None);

        var probe = new XlightsRenderCapabilityProbe();
        var adapter = new XlightsRenderAdapter(probe, RealExecutablePath);
        var pipeline = new RenderPipeline(adapter, new FseqRenderedFrameDecoder(), new XsqStructuralReader());

        var result = await pipeline.RunAsync(
            new RenderRequest(Guid.NewGuid(), RealShowDirectory, xsqPath, outputDirectory, Selection: null, RenderAuthority.ActualXlights),
            layout, CancellationToken.None);

        Assert.True(result.Artifact.Success, string.Join("; ", result.Artifact.Warnings));
        Assert.NotNull(result.Artifact.FseqPath);
        Assert.True(File.Exists(result.Artifact.FseqPath));
        Assert.NotNull(result.Frames);
        Assert.NotEmpty(result.Frames!);

        // "Arches" (a modelGroup) expands to its real members (Arches-1 Outside/-2/-3/-4, 100 nodes
        // each -- confirmed: a direct render of just this group produced exactly 1200 decoded channels,
        // 4*100*3) via SequenceChannelLayoutResolver reading the actually-rendered .xsq's own
        // <DisplayElements> -- so a non-empty projection here proves every stage (import, real render,
        // real decode, real channel-layout resolution from the real rendered file, real projection) ran
        // end to end against the real member models the "On" effect actually drove.
        Assert.True(
            result.Frames!.Any(f => f.Illuminated.Count > 0),
            $"No illuminated pixels in any frame. ProjectionWarnings: [{string.Join(" | ", result.ProjectionWarnings)}].");
    }
}
