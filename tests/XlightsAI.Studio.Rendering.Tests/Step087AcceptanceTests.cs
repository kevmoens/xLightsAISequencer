using System.Text;
using ZstdSharp;

namespace XlightsAI.Studio.Rendering.Tests;

/// <summary>
/// PHASE-13 Step 087 ("Decode rendered output/frame or channel data into display preview, preserving
/// timing"). Required acceptance, verbatim: "Frame sample matches known pixel/channel values." Required
/// failure/edge case, verbatim: "Invalid input, cancellation or stale persisted state must not corrupt
/// the project."
///
/// <para>
/// Every fixture here is a real, on-disk, byte-for-byte valid FSEQ v2 file -- built by
/// <see cref="BuildFseqFixture"/> using the real <see cref="Compressor"/> this project's own
/// <see cref="FseqRenderedFrameDecoder"/> decompresses with, not a mock of the file format. The byte
/// layout mirrors this repository's own pinned reference,
/// <c>reference/xlights-autosequencer/src/video/fseq.py</c>'s <c>_build_minimal_fseq</c> test helper.
/// </para>
/// </summary>
public sealed class Step087AcceptanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step087-" + Guid.NewGuid().ToString("N"));

    public Step087AcceptanceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string BuildFseqFixture(string stem, int channels, int frames, byte stepMs, Func<int, int, byte> channelValue)
    {
        var payload = new byte[frames * channels];
        for (var f = 0; f < frames; f++)
        {
            for (var c = 0; c < channels; c++)
            {
                payload[(f * channels) + c] = channelValue(f, c);
            }
        }

        using var compressor = new Compressor();
        var compressed = compressor.Wrap(payload).ToArray();

        var blockIndex = new byte[8];
        BitConverter.GetBytes(0u).CopyTo(blockIndex, 0);
        BitConverter.GetBytes((uint)compressed.Length).CopyTo(blockIndex, 4);

        const int fixedHeaderSize = 32;
        var dataOffset = (ushort)(fixedHeaderSize + blockIndex.Length);

        var header = new byte[fixedHeaderSize];
        Encoding.ASCII.GetBytes("PSEQ").CopyTo(header, 0);
        BitConverter.GetBytes(dataOffset).CopyTo(header, 4);
        header[6] = 0; // minor version
        header[7] = 2; // major version
        BitConverter.GetBytes((ushort)fixedHeaderSize).CopyTo(header, 8);
        BitConverter.GetBytes((uint)channels).CopyTo(header, 10);
        BitConverter.GetBytes((uint)frames).CopyTo(header, 14);
        header[18] = stepMs;

        var path = Path.Combine(_root, stem + ".fseq");
        File.WriteAllBytes(path, [.. header, .. blockIndex, .. compressed]);
        return path;
    }

    [Fact]
    public void Step087_Acceptance_Passes()
    {
        // Known fixture: 4 channels, 5 frames, 25ms step; channel c at frame f = (f + c) & 0xFF.
        var path = BuildFseqFixture("known", channels: 4, frames: 5, stepMs: 25, (f, c) => (byte)((f + c) & 0xFF));

        var output = new FseqRenderedFrameDecoder().Decode(path);

        Assert.Equal(4, output.ChannelCount);
        Assert.Equal(25, output.StepMilliseconds);
        Assert.Equal(5, output.Frames.Count);

        for (var f = 0; f < 5; f++)
        {
            // Timing preserved: absolute tick = frame index * step ms, in 100ns ticks.
            Assert.Equal((long)f * 25 * TimeSpan.TicksPerMillisecond, output.Frames[f].TimeTicks);
            Assert.Equal(4, output.Frames[f].Channels.Count);
            for (var c = 0; c < 4; c++)
            {
                Assert.Equal((byte)((f + c) & 0xFF), output.Frames[f].Channels[c]);
            }
        }
    }

    [Fact]
    public void Step087_Acceptance_DifferentStepTime_PreservesDistinctTiming()
    {
        var path = BuildFseqFixture("step40", channels: 2, frames: 3, stepMs: 40, (f, c) => (byte)(f * 10 + c));

        var output = new FseqRenderedFrameDecoder().Decode(path);

        Assert.Equal(40, output.StepMilliseconds);
        Assert.Equal(0L, output.Frames[0].TimeTicks);
        Assert.Equal(400_000L, output.Frames[1].TimeTicks); // 40ms * 10,000 ticks/ms
        Assert.Equal(800_000L, output.Frames[2].TimeTicks);
    }

    [Fact]
    public void NegativePath_MissingFile_ThrowsWithoutPartialOutput()
    {
        var missingPath = Path.Combine(_root, "never-rendered.fseq");

        var ex = Assert.Throws<FseqDecodeInvalidException>(() => new FseqRenderedFrameDecoder().Decode(missingPath));
        Assert.Contains(missingPath, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NegativePath_BadMagicBytes_RejectsRatherThanGuessing()
    {
        var path = Path.Combine(_root, "bad-magic.fseq");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes("NOTFSEQ").Concat(new byte[64]).ToArray());

        var ex = Assert.Throws<FseqDecodeInvalidException>(() => new FseqRenderedFrameDecoder().Decode(path));
        Assert.Contains("PSEQ", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NegativePath_TruncatedHeader_ThrowsBeforeAnyDecode()
    {
        var path = Path.Combine(_root, "truncated.fseq");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes("PSEQ").Concat(new byte[10]).ToArray()); // 14 bytes < 32

        Assert.Throws<FseqDecodeInvalidException>(() => new FseqRenderedFrameDecoder().Decode(path));
    }

    [Fact]
    public void NegativePath_CompressedBlockExtendsPastEndOfFile_RejectsRatherThanReadingGarbage()
    {
        var path = BuildFseqFixture("corrupt", channels: 2, frames: 2, stepMs: 25, (f, c) => (byte)(f + c));

        // Corrupt the block-length field (offset 36, the second half of the one 8-byte block-index entry)
        // to claim a far larger block than the file actually contains.
        var bytes = File.ReadAllBytes(path);
        BitConverter.GetBytes(1_000_000u).CopyTo(bytes, 36);
        File.WriteAllBytes(path, bytes);

        var ex = Assert.Throws<FseqDecodeInvalidException>(() => new FseqRenderedFrameDecoder().Decode(path));
        Assert.Contains("extends past the end of the file", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NegativePath_ZeroDeclaredChannelsOrFrames_RejectsRatherThanProducingEmptyOutput()
    {
        var path = BuildFseqFixture("zero-channels", channels: 1, frames: 1, stepMs: 25, (_, _) => 0);
        var bytes = File.ReadAllBytes(path);
        BitConverter.GetBytes(0u).CopyTo(bytes, 10); // zero out the declared channel count
        File.WriteAllBytes(path, bytes);

        Assert.Throws<FseqDecodeInvalidException>(() => new FseqRenderedFrameDecoder().Decode(path));
    }
}
