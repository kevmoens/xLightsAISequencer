using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// PHASE-03 (Analysis extensions) Task 1's own analysis boundary: computes (or reopens a still-valid
/// cached) <see cref="BandEnergyEnvelope"/> for a probed <see cref="SongAsset"/>.
/// </summary>
public interface IBandEnergyAnalyzer
{
    /// <summary>
    /// Produces a <see cref="BandEnergyEnvelope"/> for <paramref name="song"/> using
    /// <paramref name="settings"/>'s windowing and band cutoffs. Validates every input explicitly before
    /// any decode or persistence occurs and never persists a partial/invalid result.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="song"/> or <paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="BandEnergyAnalysisInvalidException"><paramref name="settings"/> is not valid for <paramref name="song"/>, or the decoded signal does not match <paramref name="song"/>'s declared dimensions.</exception>
    Task<BandEnergyEnvelope> AnalyzeAsync(SongAsset song, BandEnergySettings settings, CancellationToken ct);
}
