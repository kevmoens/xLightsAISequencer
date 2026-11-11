namespace XlightsAI.Studio.Rendering;

/// <summary>
/// Standard GIF89a LZW compression (Appendix F of the spec -- the same variable-code-size LZW every GIF
/// decoder in existence implements), reproduced clean-room for <see cref="AnimatedGifEncoder"/>. No "clever"
/// clear-code timing: a Clear code is emitted once up front and again only when the 4096-entry code table
/// fills up, which is the simplest spec-compliant behaviour and decodes correctly in any real viewer.
/// </summary>
internal static class GifLzwEncoder
{
    private const int MaxCodeSizeBits = 12;
    private const int MaxCodeCount = 1 << MaxCodeSizeBits;

    public static byte[] Compress(byte[] indices, int minCodeSize)
    {
        var clearCode = 1 << minCodeSize;
        var endCode = clearCode + 1;
        var nextCode = endCode + 1;
        var codeSize = minCodeSize + 1;
        var dictionary = new Dictionary<(int Prefix, byte Pixel), int>();
        var writer = new GifBitWriter();

        writer.WriteCode(clearCode, codeSize);

        if (indices.Length == 0)
        {
            writer.WriteCode(endCode, codeSize);
            return writer.ToArray();
        }

        var prefix = -1;
        foreach (var pixel in indices)
        {
            if (prefix == -1)
            {
                prefix = pixel; // the initial "dictionary" entries 0..clearCode-1 are literally the pixel values.
                continue;
            }

            if (dictionary.TryGetValue((prefix, pixel), out var combinedCode))
            {
                prefix = combinedCode;
                continue;
            }

            writer.WriteCode(prefix, codeSize);

            if (nextCode < MaxCodeCount)
            {
                dictionary[(prefix, pixel)] = nextCode;
                nextCode++;
                if (nextCode > (1 << codeSize) - 1 && codeSize < MaxCodeSizeBits)
                {
                    codeSize++;
                }
            }
            else
            {
                writer.WriteCode(clearCode, codeSize);
                dictionary.Clear();
                nextCode = endCode + 1;
                codeSize = minCodeSize + 1;
            }

            prefix = pixel;
        }

        writer.WriteCode(prefix, codeSize);
        writer.WriteCode(endCode, codeSize);
        return writer.ToArray();
    }

    private sealed class GifBitWriter
    {
        private readonly List<byte> _bytes = [];
        private int _bitBuffer;
        private int _bitCount;

        public void WriteCode(int code, int codeSize)
        {
            _bitBuffer |= code << _bitCount;
            _bitCount += codeSize;
            while (_bitCount >= 8)
            {
                _bytes.Add((byte)(_bitBuffer & 0xFF));
                _bitBuffer >>= 8;
                _bitCount -= 8;
            }
        }

        public byte[] ToArray()
        {
            if (_bitCount > 0)
            {
                _bytes.Add((byte)(_bitBuffer & 0xFF));
                _bitBuffer = 0;
                _bitCount = 0;
            }

            return _bytes.ToArray();
        }
    }
}
