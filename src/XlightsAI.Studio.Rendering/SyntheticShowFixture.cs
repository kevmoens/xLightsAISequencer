namespace XlightsAI.Studio.Rendering;

/// <summary>
/// A minimal, self-contained, hand-authored show directory shape (one trivial "Single Line" model, one
/// "On" effect) used only by <see cref="XlightsRenderCapabilityProbe"/> to actually attempt a real
/// headless render against an unknown installed xLights build, without depending on any user's real show.
/// Verified directly against the real install at <c>C:\Program Files\xLights\xlights.exe</c> before being
/// encoded here: a temp directory containing exactly this <see cref="RgbEffectsXml"/> plus
/// <see cref="SequenceXml"/> rendered with <c>--headless -s &lt;dir&gt; --outputdir &lt;out&gt; &lt;xsq&gt;</c>
/// exited 0 and produced a real, non-empty <c>.fseq</c> in ~20ms.
/// </summary>
internal static class SyntheticShowFixture
{
    public const string RgbEffectsXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <xrgb FormatVersion="2" BaseChannel="0" ModelBlacklist="">
          <models>
            <model
              name="ProbeLine"
              DisplayAs="Single Line"
              StringType="RGB Nodes"
              parm1="1"
              parm2="4"
              parm3="1"
              Dir="L"
              PixelSize="2"
              Transparency="0"
              PreviewWidth="0"
              PreviewHeight="0"
              StartSide="L"
              Antialiasing="1" />
          </models>
        </xrgb>
        """;

    /// <summary>
    /// The <c>.xsq</c> body only; the real file name (and therefore its <c>.fseq</c> stem) is chosen by the
    /// caller at write time so tests can drive deterministic stand-in behavior by stem substring.
    /// </summary>
    public const string SequenceXml = """
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
            <Element type="model" name="ProbeLine" visible="1" />
          </DisplayElements>
          <ElementEffects>
            <Element type="model" name="ProbeLine">
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
}
