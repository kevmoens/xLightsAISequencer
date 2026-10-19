using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// The already-built, already-approved-up-to-this-point analysis inputs an <see cref="IBarFingerprintBuilder"/>
/// run is computed from -- a narrower, concrete precursor to PHASE-08 Section 3's proposed
/// <c>SongAnalysisContext</c> (which additionally names <c>IReadOnlyList&lt;StemActivity&gt; Activity</c>
/// for a later step's own section/motif scope). This step's own declared recipe (Section 3 architectural
/// design notes: "combining harmony/rhythm/melody") only consumes <see cref="BeatGrid"/> (for bar
/// windows), <see cref="Energy"/> (spectral/energy proxy) and <see cref="Melody"/> (melodic/harmonic
/// content) -- per-stem activity weighting is a disclosed, deferred scope, not fabricated here.
/// </summary>
/// <param name="SongId">The song every supplied analysis input must share.</param>
/// <param name="SourceHash">The source audio hash <see cref="BeatGrid"/> and <see cref="Energy"/> must both have been built from -- "missing media" is an empty value here.</param>
/// <param name="BeatGrid">The approved beat/bar grid whose downbeats partition the song into bars.</param>
/// <param name="Energy">The raw energy envelope the spectral/energy half of each bar's fingerprint is computed from.</param>
/// <param name="Melody">The analyzed (and/or user-corrected) melody track the harmonic/melodic half of each bar's fingerprint is computed from.</param>
public sealed record BarFingerprintContext(
    SongId SongId,
    string SourceHash,
    BeatGrid BeatGrid,
    EnergyEnvelope Energy,
    MelodyTrack Melody);
