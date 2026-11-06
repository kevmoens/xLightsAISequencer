namespace XlightsAI.Studio.Stems;

/// <summary>
/// Build-plan "richer analysis tools" Phase 6 (2026-11-02): reads sherpa-onnx's own
/// <c>&lt;model&gt;-tokens.txt</c> format -- one <c>base64(utf8 piece) id</c> pair per line, covering only
/// the base byte-pair-encoding vocabulary (ids <c>0..VocabularySize-1</c>). The handful of special/control
/// tokens above that range (end-of-text, start-of-transcript, timestamp tokens, ...) are never listed in
/// this file -- <see cref="WhisperSpecialTokens"/> names the fixed ids this specific model
/// (<see cref="SherpaOnnxWhisperTinyEnModelAsset"/>, an English-only "tiny.en" Whisper export) uses for
/// those, independently confirmed against the real model's own decoding behavior (see that type's own doc
/// comment), not re-derived from first principles here.
/// </summary>
public sealed class WhisperTokenVocabulary
{
    private readonly IReadOnlyDictionary<int, byte[]> _pieceBytesById;

    private WhisperTokenVocabulary(IReadOnlyDictionary<int, byte[]> pieceBytesById)
    {
        _pieceBytesById = pieceBytesById;
    }

    public int VocabularySize => _pieceBytesById.Count;

    public static async Task<WhisperTokenVocabulary> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var pieceBytesById = new Dictionary<int, byte[]>();
        foreach (var line in await File.ReadAllLinesAsync(path, ct).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !int.TryParse(parts[1], out var id))
            {
                continue;
            }

            pieceBytesById[id] = Convert.FromBase64String(parts[0]);
        }

        return new WhisperTokenVocabulary(pieceBytesById);
    }

    /// <summary>
    /// Decodes a run of base-vocabulary token ids (every id strictly below
    /// <see cref="WhisperSpecialTokens.EndOfText"/> for this model) into text -- bytes are concatenated
    /// across every token BEFORE a single final UTF-8 decode, since one UTF-8 codepoint can legitimately
    /// span more than one BPE token's own byte-piece. An id this vocabulary has no entry for (should never
    /// happen for a genuine base-vocabulary id) is silently skipped rather than throwing mid-decode.
    /// </summary>
    public string Decode(IEnumerable<int> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        using var buffer = new MemoryStream();
        foreach (var id in ids)
        {
            if (_pieceBytesById.TryGetValue(id, out var bytes))
            {
                buffer.Write(bytes, 0, bytes.Length);
            }
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }
}

/// <summary>
/// Build-plan "richer analysis tools" Phase 6 (2026-11-02): the fixed special-token ids for
/// <see cref="SherpaOnnxWhisperTinyEnModelAsset"/> specifically (an English-only "tiny.en" Whisper export,
/// whose own base vocabulary is exactly <see cref="EndOfText"/> tokens wide) -- independently confirmed
/// against the real model's own decoding behavior (forcing <see cref="NoTimestamps"/> off at the first
/// decode step reliably produced a <see cref="TimestampBegin"/>-relative token, matching the real audio's
/// own measured duration; see <see cref="SherpaOnnxWhisperTinyEnModelAsset"/>'s own doc comment for the
/// full verification). Not applicable to any other Whisper model/vocabulary size.
/// </summary>
public static class WhisperSpecialTokens
{
    public const int EndOfText = 50256;
    public const int StartOfTranscript = 50257;
    public const int NoTimestamps = 50362;
    public const int TimestampBegin = 50363;

    /// <summary>Seconds represented by one increment between adjacent timestamp token ids.</summary>
    public const double TimestampSecondsPerToken = 0.02;
}
