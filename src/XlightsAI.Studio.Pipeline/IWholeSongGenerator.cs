using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Seam for <see cref="WholeSongGenerator"/> so the Shell layer can default to a throwing stub (same
/// "needs real configuration this layer does not yet own" convention as <c>IAssetInstaller</c>/
/// <c>IFfmpegInstaller</c>) when no real, fully-wired instance has been supplied yet.
/// </summary>
public interface IWholeSongGenerator
{
    /// <summary>
    /// Returns the real, exportable <see cref="XlightsAI.Studio.Xlights.SequenceDocument"/> bundled
    /// with the intermediate melody/phrase/motif analysis the pipeline always computes to build it —
    /// see <see cref="WholeSongGenerationResult"/>'s own doc comment for why both are returned rather
    /// than just the document.
    /// </summary>
    /// <param name="customPrompt">
    /// Build-plan Phase 14 §2c: the user's own free-text creative direction for this run, or
    /// <see langword="null"/>/empty for the pre-existing default behavior every call site before this
    /// phase keeps getting unchanged. Only <c>AiChoreographyWholeSongGenerator</c> actually acts on
    /// this; every other implementation (<c>WholeSongGenerator</c>, <c>NullWholeSongGenerator</c>)
    /// accepts and ignores it.
    /// </param>
    /// <param name="showDirectory">
    /// Build-plan "richer analysis tools" Phase 3 (2026-11-02): the real, on-disk xLights show directory
    /// for the currently loaded show -- only used by <c>AiChoreographyWholeSongGenerator</c>, and only
    /// when <paramref name="customPrompt"/>'s own <see cref="CustomGenerationPrompt.EnableQualityPass"/>
    /// is requested; every other implementation ignores it, same as <paramref name="customPrompt"/> itself.
    /// </param>
    /// <param name="xlightsExecutablePath">
    /// Build-plan "richer analysis tools" Phase 3 (2026-11-02): the configured xLights executable path --
    /// same usage/scope as <paramref name="showDirectory"/>.
    /// </param>
    Task<WholeSongGenerationResult> GenerateAsync(
        SongAsset song, DisplayGraph layout, CancellationToken ct = default, CustomGenerationPrompt? customPrompt = null,
        string? showDirectory = null, string? xlightsExecutablePath = null);

    /// <summary>
    /// Build-plan "richer analysis tools" post-processing phase (2026-11-02): applies a time-ranged
    /// touch-up prompt (e.g. "at 0:24-0:25, make it flash harder") to a PRIOR generation's own persisted
    /// Show Bible/SectionScripts, changing only the section(s) the prompt's own timestamps overlap and
    /// leaving every other section exactly as it was -- the user's own explicit ask: "only fix what I ask
    /// and leave the rest alone." Only <c>AiChoreographyWholeSongGenerator</c> implements this for real
    /// (it is the only generator that persists a reloadable Show Bible/SectionScripts in the first place);
    /// every other implementation throws, same "needs real configuration this layer does not yet own"
    /// convention as <see cref="NullWholeSongGenerator"/>'s own <see cref="GenerateAsync"/>.
    /// </summary>
    /// <param name="generationRunId">The prior run's own <see cref="WholeSongGenerationResult.GenerationRunId"/> to touch up.</param>
    /// <param name="postProcessingPromptText">Free text with one or more <c>mm:ss</c>/<c>hh:mm:ss</c>(-range) tokens naming what to change and when.</param>
    Task<WholeSongGenerationResult> ApplyPostProcessingPromptAsync(
        SongAsset song, DisplayGraph layout, Guid generationRunId, string postProcessingPromptText, CancellationToken ct = default);
}
