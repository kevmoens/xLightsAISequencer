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
public sealed record WhisperModelBundle(
    ModelAsset Archive,
    string EncoderEntryName,
    string DecoderEntryName,
    string TokensEntryName);
