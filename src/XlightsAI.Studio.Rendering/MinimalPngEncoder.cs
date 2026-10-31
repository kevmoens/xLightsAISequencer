using System.IO.Compression;

namespace XlightsAI.Studio.Rendering;

/// <summary>
/// A minimal, dependency-free 8-bit RGB PNG encoder. No imaging package (SkiaSharp/ImageSharp/
/// System.Drawing) exists anywhere in this solution (verified by grep before writing this) -- rather
/// than add a new third-party dependency for a prompt-sized, non-display-quality raster
/// (<see cref="PreviewFrameRasterizer"/>'s only consumer is a vision-model request), this hand-rolls the
/// PNG container directly on top of <see cref="System.IO.Compression.ZLibStream"/>, which the .NET base
/// class library has shipped since .NET 6 and which is exactly the compressed-stream format a PNG
/// <c>IDAT</c> chunk requires (RFC 2083 §2.2 / RFC 1950) -- no custom deflate/zlib implementation needed.
/// </summary>
public static class MinimalPngEncoder
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Encodes <paramref name="rgb"/> (row-major, 3 bytes/pixel, no padding) as a non-interlaced,
    /// 8-bit-depth, colour-type-2 (truecolour) PNG.
    /// </summary>
    public static byte[] Encode(int width, int height, byte[] rgb)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width and height must be positive.");
        }

        var expectedLength = width * height * 3;
        if (rgb.Length != expectedLength)
        {
            throw new ArgumentException($"Expected {expectedLength} RGB bytes for a {width}x{height} image, got {rgb.Length}.", nameof(rgb));
        }

        using var output = new MemoryStream();
        output.Write(Signature);

        WriteChunk(output, "IHDR", BuildIhdr(width, height));
        WriteChunk(output, "IDAT", BuildIdat(width, height, rgb));
        WriteChunk(output, "IEND", []);

        return output.ToArray();
    }

    private static byte[] BuildIhdr(int width, int height)
    {
        var ihdr = new byte[13];
        WriteUInt32BigEndian(ihdr, 0, (uint)width);
        WriteUInt32BigEndian(ihdr, 4, (uint)height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 2; // colour type: truecolour (RGB)
        ihdr[10] = 0; // compression method: deflate
        ihdr[11] = 0; // filter method: adaptive (per-scanline filter byte, always 0 "None" here)
        ihdr[12] = 0; // interlace method: none
        return ihdr;
    }

    private static byte[] BuildIdat(int width, int height, byte[] rgb)
    {
        var stride = width * 3;
        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0); // filter type 0 ("None") for every scanline -- correctness over compression ratio.
            raw.Write(rgb, y * stride, stride);
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(zlib);
        }

        return compressed.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);

        var lengthBytes = new byte[4];
        WriteUInt32BigEndian(lengthBytes, 0, (uint)data.Length);
        output.Write(lengthBytes);

        output.Write(typeBytes);
        output.Write(data);

        var crc = Crc32(typeBytes, data);
        var crcBytes = new byte[4];
        WriteUInt32BigEndian(crcBytes, 0, crc);
        output.Write(crcBytes);
    }

    private static void WriteUInt32BigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    // RFC 2083 Annex D's own reference CRC-32 implementation (standard PNG CRC), reproduced directly --
    // no BCL API exposes the raw CRC-32 PNG chunks require.
    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in type)
        {
            c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        }

        foreach (var b in data)
        {
            c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        }

        return c ^ 0xFFFFFFFFu;
    }
}
