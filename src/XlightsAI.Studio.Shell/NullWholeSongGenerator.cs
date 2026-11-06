using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// Throwing default <see cref="IWholeSongGenerator"/>, same "needs a real install/output directory this
/// layer does not yet own" reasoning as <see cref="NullAssetInstaller"/>/<see cref="NullFfmpegInstaller"/>
/// — a real <see cref="WholeSongGenerator"/> needs real, directory-backed stores for every Rhythm/Melody/
/// Structure analysis stage plus a real stem-separation model, which only a real composition root can
/// supply.
/// </summary>
public sealed class NullWholeSongGenerator : IWholeSongGenerator
{
    public static readonly NullWholeSongGenerator Instance = new();

    private NullWholeSongGenerator()
    {
    }

    public Task<WholeSongGenerationResult> GenerateAsync(
        SongAsset song, DisplayGraph layout, CancellationToken ct = default, CustomGenerationPrompt? customPrompt = null,
        string? showDirectory = null, string? xlightsExecutablePath = null)
    {
        throw new InvalidOperationException(
            "No real whole-song generator is configured for this composition root yet (no analysis-cache directories/stem-separation model wired up).");
    }

    public Task<WholeSongGenerationResult> ApplyPostProcessingPromptAsync(
        SongAsset song, DisplayGraph layout, Guid generationRunId, string postProcessingPromptText, CancellationToken ct = default) =>
        throw new InvalidOperationException(
            "No real whole-song generator is configured for this composition root yet (no analysis-cache directories/stem-separation model wired up).");
}
