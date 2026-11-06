using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// Build-plan "richer analysis tools" Phase 6 (2026-11-02): speech-to-text over a song's own isolated
/// vocal stem, closing the gap <see cref="VisemeApproximator"/>'s own doc comment already discloses ("no
/// ASR... exists"). Real implementation is <see cref="WhisperOnnxTranscriber"/>; a not-yet-installed model
/// surfaces as <see cref="ModelAssetConsentRequiredException"/>, the same "never a hidden download"
/// convention <see cref="OnnxStemSeparator"/> already established.
/// </summary>
public interface ILyricTranscriber
{
    /// <param name="vocalsStem">The already-isolated vocals <see cref="SongAsset"/> (see <see cref="StemSongAssetFactory.ProbeAsync"/>) to transcribe.</param>
    Task<IReadOnlyList<TranscribedWord>> TranscribeAsync(SongAsset vocalsStem, CancellationToken ct);
}
