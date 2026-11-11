namespace XlightsAI.Studio.Stems;

/// <summary>
/// Declares which three extracted files inside one verified, pinned archive <see cref="ModelAsset"/> back
/// a <see cref="WhisperOnnxTranscriber"/> run -- same "never a second independently-pinned ModelAsset per
/// file" reasoning as <see cref="SpectrogramModelBundle"/>, since all three are produced by extracting one
/// already hash-verified archive.
/// </summary>
/// <param name="Archive">The one verified, pinned archive <see cref="ModelAsset"/> all three extracted files come from.</param>
/// <param name="EncoderEntryName">Archive entry for the encoder ONNX graph (mel-spectrogram in, cross-attention key/value tensors out).</param>
/// <param name="DecoderEntryName">Archive entry for the decoder ONNX graph (autoregressive, KV-cached token generation).</param>
/// <param name="TokensEntryName">Archive entry for the token-id-to-text-piece vocabulary file (<see cref="WhisperTokenVocabulary"/>'s own expected format).</param>
/// <param name="MelBins">
/// User-requested real investigation (2026-11-08): this model's own expected mel-bin count. Every model
/// from tiny through medium uses 80 (<see cref="WhisperLogMelSpectrogram.DefaultMelBins"/>, the default
/// here); OpenAI's large-v3 release switched its own encoder to 128 (confirmed directly: feeding it an
/// 80-bin tensor throws a real ONNX Runtime dimension-mismatch error naming "Expected: 128").
/// </param>
/// <param name="TimestampBeginTokenId">
/// User-requested real investigation (2026-11-08): the first real timestamp token's own id --
/// <see cref="WhisperOnnxTranscriber"/> forces this exact id as the chunk's own <c>t=0.00</c> anchor and
/// uses it as the boundary between "this is a timestamp" and "this is real content." Defaults to
/// <see cref="WhisperSpecialTokens.TimestampBegin"/> (50363), empirically confirmed correct for every
/// model sharing tiny.en's own vocabulary layout (which reserves all 99 multilingual language-tag slots
/// even in an English-only export). OpenAI's large-v3 release added a 100th language (Cantonese),
/// shifting every special token after the language-tag block by exactly one id -- confirmed directly:
/// reusing the default id against the real large-v3 model produced only garbage/repeated-punctuation
/// output on a known-good clip, while <c>TimestampBegin + 1</c> (50364) needs independent confirmation
/// before being trusted.
/// </param>
public sealed record WhisperModelBundle(
    ModelAsset Archive,
    string EncoderEntryName,
    string DecoderEntryName,
    string TokensEntryName,
    int MelBins = WhisperLogMelSpectrogram.DefaultMelBins,
    int TimestampBeginTokenId = WhisperSpecialTokens.TimestampBegin);
