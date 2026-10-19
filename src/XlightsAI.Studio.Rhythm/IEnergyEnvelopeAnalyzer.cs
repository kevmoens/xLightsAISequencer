using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Step 031's own analysis boundary: computes (or reopens a still-valid cached) <see cref="EnergyEnvelope"/>
/// for a probed <see cref="SongAsset"/>. A narrower, concrete precursor to Section 3's proposed
/// <c>IRhythmAnalyzer.AnalyzeAsync</c> (which returns a full <c>BeatGrid</c> with onsets/tempo
/// candidates) — Step 031's own scope is only the RMS/energy envelope those later steps consume, per
/// this step's own "Implement RMS/energy envelope with baseline comparison" title.
/// </summary>
public interface IEnergyEnvelopeAnalyzer
{
    /// <summary>
    /// Produces an <see cref="EnergyEnvelope"/> for <paramref name="song"/> using <paramref name="settings"/>'s
    /// windowing. Validates <paramref name="song"/>/<paramref name="settings"/> explicitly before any
    /// decode or persistence occurs (Step 031's own "invalid input... must not corrupt the project"
    /// failure/edge case) and never persists a partial/invalid result.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="song"/> or <paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="EnergyEnvelopeInvalidException"><paramref name="settings"/> is not valid for <paramref name="song"/>, or the decoded signal does not match <paramref name="song"/>'s declared dimensions.</exception>
    Task<EnergyEnvelope> AnalyzeAsync(SongAsset song, RhythmSettings settings, CancellationToken ct);
}
