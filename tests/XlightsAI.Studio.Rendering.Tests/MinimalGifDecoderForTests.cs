namespace XlightsAI.Studio.Rendering.Tests;

/// <summary>
/// A from-spec, test-only GIF89a decoder -- the read-path mirror of <see cref="AnimatedGifEncoderTests"/>'
/// own <see cref="AnimatedGifEncoder"/> round-trip, same convention as <see cref="MinimalPngDecoderForTests"/>
/// already uses for <see cref="MinimalPngEncoder"/>. Only supports what <see cref="AnimatedGifEncoder"/> itself
/// ever emits (a global colour table, no local colour tables/interlacing, one Graphic Control Extension per
/// image), but implements the real GIF89a block structure and LZW decompression, not a special-cased parser
/// tied to this encoder's exact byte layout.
/// </summary>
public static class MinimalGifDecoderForTests
{
    public sealed record DecodedFrame(int DelayCentiseconds, byte[] Rgb);

    public static (int Width, int Height, List<DecodedFrame> Frames) Decode(byte[] gif)
    {
        var offset = 0;
        var signature = System.Text.Encoding.ASCII.GetString(gif, 0, 6);
        if (signature is not ("GIF89a" or "GIF87a"))
        {
            throw new InvalidOperationException($"Not a GIF file (signature was '{signature}').");
        }

        offset += 6;
        var width = ReadUInt16LittleEndian(gif, ref offset);
        var height = ReadUInt16LittleEndian(gif, ref offset);
        var packed = gif[offset++];
        offset++; // background colour index
        offset++; // pixel aspect ratio

        var hasGlobalColorTable = (packed & 0b1000_0000) != 0;
        var globalColorTableSize = 1 << ((packed & 0x07) + 1);
        (byte R, byte G, byte B)[]? globalPalette = null;
        if (hasGlobalColorTable)
        {
            globalPalette = new (byte, byte, byte)[globalColorTableSize];
            for (var i = 0; i < globalColorTableSize; i++)
            {
                globalPalette[i] = (gif[offset], gif[offset + 1], gif[offset + 2]);
                offset += 3;
            }
        }

        var frames = new List<DecodedFrame>();
        var pendingDelayCentiseconds = 0;

        while (true)
        {
            var introducer = gif[offset++];
            if (introducer == 0x3B)
            {
                break; // trailer
            }

            if (introducer == 0x21)
            {
                var label = gif[offset++];
                if (label == 0xF9)
                {
                    var blockSize = gif[offset++];
                    var gceStart = offset;
                    pendingDelayCentiseconds = gif[gceStart + 1] | (gif[gceStart + 2] << 8);
                    offset += blockSize;
                    SkipSubBlocks(gif, ref offset); // block terminator
                }
                else
                {
                    SkipSubBlocks(gif, ref offset);
                }

                continue;
            }

            if (introducer == 0x2C)
            {
                offset += 4; // left, top
                var imageWidth = ReadUInt16LittleEndian(gif, ref offset);
                var imageHeight = ReadUInt16LittleEndian(gif, ref offset);
                var imagePacked = gif[offset++];
                if ((imagePacked & 0b1000_0000) != 0 || (imagePacked & 0b0100_0000) != 0)
                {
                    throw new NotSupportedException("Local colour tables and interlacing are not supported by this test decoder.");
                }

                var lzwMinCodeSize = gif[offset++];
                var compressed = ReadSubBlocksConcatenated(gif, ref offset);
                var indices = LzwDecompress(compressed, lzwMinCodeSize, imageWidth * imageHeight);

                var palette = globalPalette ?? throw new NotSupportedException("No global colour table present.");
                var rgb = new byte[imageWidth * imageHeight * 3];
                for (var i = 0; i < indices.Length; i++)
                {
                    var color = palette[indices[i]];
                    rgb[i * 3] = color.R;
                    rgb[(i * 3) + 1] = color.G;
                    rgb[(i * 3) + 2] = color.B;
                }

                frames.Add(new DecodedFrame(pendingDelayCentiseconds, rgb));
                continue;
            }

            throw new NotSupportedException($"Unrecognized GIF block introducer 0x{introducer:X2}.");
        }

        return (width, height, frames);
    }

    private static int ReadUInt16LittleEndian(byte[] data, ref int offset)
    {
        var value = data[offset] | (data[offset + 1] << 8);
        offset += 2;
        return value;
    }

    private static void SkipSubBlocks(byte[] data, ref int offset)
    {
        while (true)
        {
            var size = data[offset++];
            if (size == 0)
            {
                return;
            }

            offset += size;
        }
    }

    private static byte[] ReadSubBlocksConcatenated(byte[] data, ref int offset)
    {
        using var buffer = new MemoryStream();
        while (true)
        {
            var size = data[offset++];
            if (size == 0)
            {
                break;
            }

            buffer.Write(data, offset, size);
            offset += size;
        }

        return buffer.ToArray();
    }

    private static byte[] LzwDecompress(byte[] compressed, int minCodeSize, int expectedPixelCount)
    {
        var clearCode = 1 << minCodeSize;
        var endCode = clearCode + 1;

        var reader = new GifBitReader(compressed);
        var output = new List<byte>(expectedPixelCount);

        List<byte[]> dictionary = [];
        int codeSize;
        byte[]? previousEntry = null;

        void ResetDictionary()
        {
            dictionary = [];
            for (var i = 0; i < clearCode; i++)
            {
                dictionary.Add([(byte)i]);
            }

            dictionary.Add([]); // clear code placeholder (never looked up)
            dictionary.Add([]); // end code placeholder (never looked up)
            codeSize = minCodeSize + 1;
            previousEntry = null;
        }

        ResetDictionary();

        while (true)
        {
            var code = reader.ReadCode(codeSize);
            if (code == clearCode)
            {
                ResetDictionary();
                continue;
            }

            if (code == endCode)
            {
                break;
            }

            byte[] entry;
            if (code < dictionary.Count)
            {
                entry = dictionary[code];
            }
            else if (code == dictionary.Count && previousEntry is not null)
            {
                entry = [.. previousEntry, previousEntry[0]];
            }
            else
            {
                throw new InvalidOperationException($"Invalid LZW code {code} encountered (dictionary has {dictionary.Count} entries).");
            }

            output.AddRange(entry);

            if (previousEntry is not null)
            {
                dictionary.Add([.. previousEntry, entry[0]]);
                // The decoder's own table add always lags the encoder's by exactly one code (it cannot
                // know the extending byte for "prefix+pixel" until the *next* code arrives), so the
                // encoder has already allocated one further slot than this decoder's own table physically
                // holds right now -- "+ 1" accounts for that already-allocated-but-not-yet-materialized
                // slot so this decoder's own code-size growth stays in lockstep with the encoder's.
                if (dictionary.Count + 1 > (1 << codeSize) - 1 && codeSize < 12)
                {
                    codeSize++;
                }
            }

            previousEntry = entry;
        }

        return [.. output];
    }

    private sealed class GifBitReader(byte[] data)
    {
        private int _byteIndex;
        private int _bitBuffer;
        private int _bitCount;

        public int ReadCode(int codeSize)
        {
            while (_bitCount < codeSize)
            {
                _bitBuffer |= data[_byteIndex++] << _bitCount;
                _bitCount += 8;
            }

            var code = _bitBuffer & ((1 << codeSize) - 1);
            _bitBuffer >>= codeSize;
            _bitCount -= codeSize;
            return code;
        }
    }
}
