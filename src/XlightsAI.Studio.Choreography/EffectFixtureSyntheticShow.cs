using XlightsAI.Studio.Reference.Fixtures;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 18 §3 (Phase B)'s own "one fixture = one small show with a DisplayGraph containing just
/// its two fixture models" -- a real, on-disk show directory (an <c>xlights_rgbeffects.xml</c>) declaring
/// exactly <see cref="EffectFixturePlanner.LowResolutionModel"/>/<see cref="EffectFixturePlanner.HighResolutionModel"/>
/// by the same names, so a real headless xLights render against this show directory can actually resolve
/// every fixture's own exported <c>.xsq</c> target. Built once per renderer run (not once per fixture --
/// every fixture's own isolated output is just its own exported <c>.xsq</c>/rendered <c>.fseq</c> inside this
/// one shared show's own render-output tree). The Low tier keeps the "Single Line" shape
/// <see cref="Rendering.SyntheticShowFixture"/> already uses to probe real render capability; the High tier
/// is a real "Matrix" (<c>NumStrings</c>/<c>NodesPerString</c> -- the same real attribute names a real
/// exported <c>C:\xLights</c> Matrix model uses, preferred over the older <c>parm1</c>/<c>parm2</c> pair by
/// <c>ShowLayoutImporter</c>'s own real-corpus finding) -- a genuine 2D grid, not a 1-row string, after
/// several real effects (Shape/Kaleidoscope/Circles/Warp/Lines/Morph/Life/Fireworks) turned out to need real
/// vertical extent to render anything at all on a verified-1-row buffer.
/// </summary>
public static class EffectFixtureSyntheticShow
{
    /// <summary>
    /// Writes a real <c>xlights_rgbeffects.xml</c> into <paramref name="showDirectory"/> declaring both
    /// <see cref="EffectFixturePlanner.LowResolutionModel"/> (Single Line) and
    /// <see cref="EffectFixturePlanner.HighResolutionModel"/> (a real <see cref="EffectFixturePlanner.HighResolutionMatrixColumns"/>×<see cref="EffectFixturePlanner.HighResolutionMatrixRows"/>
    /// Matrix) with the exact same node counts Phase A's own <see cref="DisplayGraph"/> uses -- only the real
    /// node count (and, for the Matrix, its real string/pixel-per-string split) drives the real rendered
    /// channel layout, so this is what actually matters for <see cref="Xlights.SequenceChannelLayoutResolver"/>
    /// to agree with the real rendered <c>.fseq</c>.
    /// </summary>
    public static void Write(string showDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(showDirectory);
        Directory.CreateDirectory(showDirectory);
        File.WriteAllText(Path.Combine(showDirectory, "xlights_rgbeffects.xml"), BuildRgbEffectsXml());
    }

    /// <summary>
    /// Build-plan Phase 18 §5 follow-up: Music/VU Meter/Guitar-style effects rendered completely dark
    /// because this synthetic show never carried a real media file for xLights' own real-time audio
    /// analysis to read during render -- confirmed via each effect's own real metadata (e.g. Music/VU
    /// Meter's declared audio-track/note-range properties) and the exported <c>.xsq</c>'s own empty
    /// <c>&lt;mediaFile&gt;</c>. Writes a real, playable WAV built entirely from
    /// <see cref="DeterministicSyntheticAudioGenerator"/> -- a pure sine/harmonic synthesizer with "no
    /// copyrighted recording involved" (that type's own doc comment) -- so every fixture gets a real,
    /// license-free audio signal with genuine rhythmic and frequency variation (a constant tone or silence
    /// would drive an audio-reactive effect no more usefully than no media file at all) rather than
    /// reaching for one of this repo's existing short test clips (too short to cover a fixture's own
    /// duration) or a real song fixture (rights-cleared for test use, not for an openly-rerunnable tool).
    /// </summary>
    public static string WriteAudioTrack(string showDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(showDirectory);
        Directory.CreateDirectory(showDirectory);
        var path = Path.Combine(showDirectory, "fixture-audio.wav");
        File.WriteAllBytes(path, BuildSyntheticAudioWav());
        return path;
    }

    /// <summary>Generous fixed length -- comfortably exceeds every duration <see cref="EffectFixturePlanner"/> currently uses (default 6s), with real margin for a future override, rather than threading a duration parameter through just for this.</summary>
    private const double AudioDurationSeconds = 15.0;

    private const int AudioSampleRate = 44_100;

    private static byte[] BuildSyntheticAudioWav()
    {
        const int bpm = 140;
        var beatIntervalSeconds = 60.0 / bpm;
        var clickCount = (int)Math.Ceiling(AudioDurationSeconds / beatIntervalSeconds) + 1;
        var clicks = DeterministicSyntheticAudioGenerator.BuildClickTrack(bpm, clickCount, clickDurationSeconds: 0.08, toneHz: 220.0, AudioSampleRate);

        double[] melodyHz = [220.00, 277.18, 329.63, 392.00, 440.00, 392.00, 329.63, 277.18]; // A3-based scale run, real frequency variety over time.
        var noteDurationSeconds = AudioDurationSeconds / melodyHz.Length;
        var melody = DeterministicSyntheticAudioGenerator.BuildNoteSequence(melodyHz, noteDurationSeconds, AudioSampleRate);

        var mixed = DeterministicSyntheticAudioGenerator.MixDown(clicks, melody);
        return DeterministicSyntheticAudioGenerator.EncodeWav(mixed, AudioSampleRate);
    }

    private static string BuildRgbEffectsXml() =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <xrgb FormatVersion="2" BaseChannel="0" ModelBlacklist="">
          <models>
            <model
              name="{EffectFixturePlanner.LowResolutionModel.XlightsName}"
              DisplayAs="Single Line"
              StringType="RGB Nodes"
              parm1="1"
              parm2="{EffectFixturePlanner.LowResolutionModel.NodeCount}"
              parm3="1"
              Dir="L"
              PixelSize="2"
              Transparency="0"
              PreviewWidth="0"
              PreviewHeight="0"
              StartSide="L"
              Antialiasing="1"
              LayoutGroup="Default"
              WorldPosX="0.000000"
              WorldPosY="0.000000"
              WorldPosZ="0.000000"
              X2="100.000000"
              Y2="0.000000"
              Z2="0.000000" />
            <model
              name="{EffectFixturePlanner.HighResolutionModel.XlightsName}"
              DisplayAs="Matrix"
              StringType="RGB Nodes"
              NumStrings="{EffectFixturePlanner.HighResolutionMatrixColumns}"
              NodesPerString="{EffectFixturePlanner.HighResolutionMatrixRows}"
              StrandsPerString="1"
              Dir="L"
              StartSide="B"
              Vertical="true"
              PixelSize="2"
              Transparency="0"
              PreviewWidth="0"
              PreviewHeight="0"
              Antialiasing="1"
              LayoutGroup="Default"
              WorldPosX="150.000000"
              WorldPosY="0.000000"
              WorldPosZ="0.000000"
              ScaleX="50.000000"
              ScaleY="50.000000"
              ScaleZ="1.000000" />
          </models>
        </xrgb>
        """;
}
