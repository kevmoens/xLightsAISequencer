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
    Task<WholeSongGenerationResult> GenerateAsync(SongAsset song, DisplayGraph layout, CancellationToken ct = default, CustomGenerationPrompt? customPrompt = null);
}
