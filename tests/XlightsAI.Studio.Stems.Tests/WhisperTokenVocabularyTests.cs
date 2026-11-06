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

    [Fact]
    public async Task VocabularySize_ReflectsLoadedEntryCount()
    {
        await WriteTokensFileAsync((" a", 0), (" b", 1), (" c", 2));
        var vocabulary = await WhisperTokenVocabulary.LoadAsync(_path, CancellationToken.None);

        Assert.Equal(3, vocabulary.VocabularySize);
    }
}
