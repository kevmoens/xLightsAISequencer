using System.IO.Compression;

namespace XlightsAI.Studio.Rendering.Tests;

/// <summary>
/// Test-only inverse of <see cref="MinimalPngEncoder"/> -- decodes exactly the subset of PNG this
/// solution's own encoder produces (8-bit truecolour, filter type 0 on every scanline, single IDAT or
/// concatenated IDATs), so a test can assert the encoder round-trips real pixel data rather than only
/// checking the file's magic bytes. Not a general-purpose PNG reader.
/// </summary>
internal static class MinimalPngDecoderForTests
{
    public static (int Width, int Height, byte[] Rgb) Decode(byte[] png)
    {
        var offset = 8; // skip signature
        var width = 0;
        var height = 0;
        using var idat = new MemoryStream();

        while (offset < png.Length)
        {
            var length = ReadUInt32BigEndian(png, offset);
            var type = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
            var dataStart = offset + 8;

            if (type == "IHDR")
            {
                width = (int)ReadUInt32BigEndian(png, dataStart);
                height = (int)ReadUInt32BigEndian(png, dataStart + 4);
            }
            else if (type == "IDAT")
            {
                idat.Write(png, dataStart, (int)length);
            }
            else if (type == "IEND")
            {
                break;
            }

            offset = dataStart + (int)length + 4; // skip CRC
        }

        idat.Position = 0;
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(idat, CompressionMode.Decompress))
        {
            zlib.CopyTo(inflated);
        }

        var raw = inflated.ToArray();
        var stride = (width * 3) + 1; // +1 filter-type byte per scanline
        var rgb = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
        {
            var rowStart = y * stride;
            // This test decoder only needs to handle filter type 0 ("None"), which is all MinimalPngEncoder ever writes.
            Array.Copy(raw, rowStart + 1, rgb, y * width * 3, width * 3);
        }

        return (width, height, rgb);
    }

    private static uint ReadUInt32BigEndian(byte[] buffer, int offset) =>
        ((uint)buffer[offset] << 24) | ((uint)buffer[offset + 1] << 16) | ((uint)buffer[offset + 2] << 8) | buffer[offset + 3];
}
