namespace XlightsAI.Studio.Stems;

/// <summary>
/// The exact, real, verbatim-confirmed STFT configuration the sherpa-onnx Spleeter conversion requires
/// (from the project's own published <c>scripts/spleeter/separate_onnx.py</c>): 44100 Hz, n_fft=4096,
/// hop_length=1024, win_length=4096, Hann window, <c>center=False</c> (no edge padding/reflection — frame
/// <c>i</c> covers raw samples <c>[i*hop, i*hop+n_fft)</c>).
/// </summary>
public sealed record StftConfig(int FftSize, int HopSize, int WindowSize)
{
    public static StftConfig SpleeterReference { get; } = new(FftSize: 4096, HopSize: 1024, WindowSize: 4096);

    /// <summary>2049 for FftSize=4096 (n_fft/2 + 1 real-STFT frequency bins).</summary>
    public int FullFrequencyBinCount => FftSize / 2 + 1;
}
