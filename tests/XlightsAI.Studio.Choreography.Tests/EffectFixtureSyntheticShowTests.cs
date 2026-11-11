namespace XlightsAI.Studio.Choreography.Tests;

public sealed class EffectFixtureSyntheticShowTests : IDisposable
{
    private readonly string _showDirectory = Path.Combine(Path.GetTempPath(), "xlai-synthetic-show-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_showDirectory))
        {
            Directory.Delete(_showDirectory, recursive: true);
        }
    }

    [Fact]
    public void Write_DeclaresHighTierAsARealMatrixWithMatchingNodeCount()
    {
        EffectFixtureSyntheticShow.Write(_showDirectory);

        var xml = File.ReadAllText(Path.Combine(_showDirectory, "xlights_rgbeffects.xml"));

        Assert.Contains($"name=\"{EffectFixturePlanner.HighResolutionModel.XlightsName}\"", xml, StringComparison.Ordinal);
        Assert.Contains("DisplayAs=\"Matrix\"", xml, StringComparison.Ordinal);
        Assert.Contains($"NumStrings=\"{EffectFixturePlanner.HighResolutionMatrixColumns}\"", xml, StringComparison.Ordinal);
        Assert.Contains($"NodesPerString=\"{EffectFixturePlanner.HighResolutionMatrixRows}\"", xml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Regression test for a real defect found investigating why Music/VU Meter/Guitar-style fixtures
    /// rendered completely dark: the exported .xsq always carried an empty &lt;mediaFile&gt;, so xLights'
    /// own real-time audio analysis had nothing to read. The written track must be a real, playable WAV
    /// long enough to cover every duration this phase currently uses.
    /// </summary>
    [Fact]
    public void WriteAudioTrack_ProducesARealPlayableWavLongEnoughForAnyFixtureDuration()
    {
        var path = EffectFixtureSyntheticShow.WriteAudioTrack(_showDirectory);

        Assert.True(File.Exists(path));
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 44); // more than just a bare WAV header.
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(bytes, 8, 4));

        // dataSize (bytes 40-43, little-endian) / (sampleRate * 2 bytes/sample) >= the longest duration
        // EffectFixturePlanner.Build currently uses (its own DefaultFixtureDurationSeconds).
        var dataSize = BitConverter.ToInt32(bytes, 40);
        var sampleRate = BitConverter.ToInt32(bytes, 24);
        var durationSeconds = dataSize / 2.0 / sampleRate;
        Assert.True(durationSeconds >= EffectFixturePlanner.DefaultFixtureDurationSeconds);
    }
}
