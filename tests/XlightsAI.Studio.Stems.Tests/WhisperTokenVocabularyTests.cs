namespace XlightsAI.Studio.Stems.Tests;

/// <summary>Build-plan "richer analysis tools" Phase 6 (2026-11-02): <see cref="WhisperTokenVocabulary"/>.</summary>
public sealed class WhisperTokenVocabularyTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"xlai-whisper-tokens-{Guid.NewGuid():N}.txt");

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private async Task WriteTokensFileAsync(params (string Text, int Id)[] entries)
    {
        var lines = entries.Select(e => $"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(e.Text))} {e.Id}");
        await File.WriteAllLinesAsync(_path, lines);
    }

    [Fact]
    public async Task Decode_SingleTokenPiece_ReturnsItsText()
    {
        await WriteTokensFileAsync((" Hello", 0));
        var vocabulary = await WhisperTokenVocabulary.LoadAsync(_path, CancellationToken.None);

        Assert.Equal(" Hello", vocabulary.Decode([0]));
    }

    [Fact]
    public async Task Decode_MultipleTokens_ConcatenatesBytesBeforeFinalUtf8Decode()
    {
        // Confirms bytes are joined BEFORE decoding -- a real BPE vocabulary can split one UTF-8
        // codepoint's bytes across adjacent tokens, which an independent per-token decode would corrupt.
        await WriteTokensFileAsync((" wor", 0), ("ld", 1), ("!", 2));
        var vocabulary = await WhisperTokenVocabulary.LoadAsync(_path, CancellationToken.None);

        Assert.Equal(" world!", vocabulary.Decode([0, 1, 2]));
    }

    [Fact]
    public async Task Decode_UnknownId_SkippedRatherThanThrowing()
    {
        await WriteTokensFileAsync((" Hi", 0));
        var vocabulary = await WhisperTokenVocabulary.LoadAsync(_path, CancellationToken.None);

        Assert.Equal(" Hi", vocabulary.Decode([0, 999]));
    }

    /// <summary>
    /// User-requested real investigation (2026-11-08): pushing to a larger Whisper variant (large-v3)
    /// surfaced a real file, <c>sherpa-onnx-whisper-large-v3</c>'s own <c>large-v3-tokens.txt</c>, whose
    /// line 50256 reads literally <c>"= 50256"</c> -- a reserved/placeholder entry (sitting exactly where
    /// EndOfText belongs) whose own "base64" field is not valid base64 at all, crashing the whole
    /// vocabulary load with <see cref="FormatException"/> before this fix. That id is never looked up by
    /// <see cref="WhisperTokenVocabulary.Decode"/> in practice (every id passed to it is a real content
    /// token the decode loop already excluded from being a timestamp or EndOfText), so skipping the
    /// unparseable entry -- rather than crashing the whole load -- is always safe.
    /// </summary>
    [Fact]
    public async Task LoadAsync_LineWithInvalidBase64Field_SkipsThatLineRatherThanThrowing()
    {
        await File.WriteAllLinesAsync(_path,
        [
            $"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(" Hi"))} 0",
            "= 1",
        ]);

        var vocabulary = await WhisperTokenVocabulary.LoadAsync(_path, CancellationToken.None);

        Assert.Equal(1, vocabulary.VocabularySize);
        Assert.Equal(" Hi", vocabulary.Decode([0]));
        Assert.Equal(string.Empty, vocabulary.Decode([1])); // id 1 was never loaded -- silently skipped, not decoded as garbage.
    }

    [Fact]
    public async Task VocabularySize_ReflectsLoadedEntryCount()
    {
        await WriteTokensFileAsync((" a", 0), (" b", 1), (" c", 2));
        var vocabulary = await WhisperTokenVocabulary.LoadAsync(_path, CancellationToken.None);

        Assert.Equal(3, vocabulary.VocabularySize);
    }
}
