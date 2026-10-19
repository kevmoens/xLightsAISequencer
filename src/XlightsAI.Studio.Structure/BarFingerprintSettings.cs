namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Step 052's own analysis-recipe configuration an <see cref="IBarFingerprintBuilder"/> run is
/// keyed by. Recorded alongside every <see cref="SongFingerprintSet"/> it produces (Section 4: "every
/// cache key includes algorithm/model/settings versions") so a later re-run with a different recipe can
/// never be silently confused with this one's output.
/// </summary>
/// <param name="ChromaBins">
/// Number of pitch-class bins the melodic/harmonic component of each bar's fingerprint is folded into
/// (<c>12</c> for the standard chromatic scale — a <see cref="XlightsAI.Studio.Melody.NoteEvent.MidiNote"/>
/// is reduced to <c>MidiNote % ChromaBins</c>, discarding octave, so an octave error upstream does not
/// change which motif a bar is judged similar to). Must be positive.
/// </param>
/// <param name="RecipeId">
/// Identifies which declared combination of spectral/harmonic/melodic sources this fingerprint was
/// built from (Section 3 architectural design notes: "Mapping... is a declared policy," applied here to
/// feature composition rather than gesture mapping). This step's own single implemented recipe is
/// <see cref="BarFingerprintBuilder.EnergyPlusChromaRecipeId"/> — declared explicitly, not a silent
/// default, so a future recipe (e.g. adding rhythmic-density features) is a new, distinguishable
/// <see cref="RecipeId"/> rather than a breaking change to this one's meaning.
/// </param>
public sealed record BarFingerprintSettings(int ChromaBins, string RecipeId);
