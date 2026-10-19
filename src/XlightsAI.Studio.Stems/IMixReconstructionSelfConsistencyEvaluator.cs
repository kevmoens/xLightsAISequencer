using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06A Step 044E. Separates <paramref name="originalMix"/>'s own content via the real
/// <see cref="ISpectrogramStemSeparator"/>, sums the two produced stems back into a reconstructed mix, and
/// scores how closely that reconstructed mix matches the real original input -- an energy/correlation
/// self-consistency check, explicitly never a separation-quality claim.
/// </summary>
public interface IMixReconstructionSelfConsistencyEvaluator
{
    /// <exception cref="ArgumentNullException">Any required argument is null.</exception>
    /// <exception cref="SeparationQualityInvalidInputException">The real separator produced zero stems, or
    /// the real produced stems/original mix decode to mismatched sample lengths.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
    Task<MixReconstructionSelfConsistencyReport> EvaluateAsync(
        Guid reportId,
        long createdAtTicks,
        ISpectrogramStemSeparator separator,
        SpectrogramModelBundle modelBundle,
        SpectrogramSeparationRecipe recipe,
        SongAsset originalMix,
        SeparationQualityThresholds thresholds,
        CancellationToken ct);
}
