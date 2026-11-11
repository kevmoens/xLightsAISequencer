using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// PHASE-2b (build plan §2b.7-8): one corpus sequence's full analysis bundle — real audio analysis
/// (beat grid, bar fingerprints, section labels, phrases) plus every parsed effect aligned onto it.
/// <see cref="CorpusSongAnalyzer"/>'s own output; <see cref="PatternLibraryBuilder"/>,
/// <see cref="ExemplarIndexBuilder"/> and <see cref="CraftRuleInducer"/> all consume a corpus-wide
/// collection of these.
/// </summary>
/// <param name="BandEnergy">
/// Build-plan Phase 16 §2c: real per-band (bass/mid/high) energy analysis, reusing the same
/// <see cref="Rhythm.BandEnergyAnalyzer"/> <c>AudioAnalysisRunner</c> already runs for real generation
/// -- defaults to <see langword="null"/> so a hand-constructed test fixture predating this phase keeps
/// compiling; <see cref="CorpusSongAnalyzer.AnalyzeAsync"/> itself always populates a real one.
/// </param>
/// <param name="VocalActivity">
/// Build-plan Phase 16 §2c: real vocal-activity analysis -- see <see cref="CorpusSongAnalyzer"/>'s own
/// doc comment for the disclosed full-mix substitution this uses in place of an isolated vocals stem.
/// Defaults to <see langword="null"/> for the same back-compat reason as <see cref="BandEnergy"/>.
/// </param>
/// <param name="KeyChanges">
/// Build-plan Phase 19 §2b: real, measured musical key changes (<see cref="XlightsAI.Studio.Structure.KeyDetector.DetectKeyChanges"/>)
/// over this song's own already-computed <see cref="FingerprintSet"/> chroma -- no new analysis, just the
/// same key-profile correlation live generation (<c>AiChoreographyWholeSongGenerator</c>) already runs,
/// now also captured for corpus mining so a craft rule can actually correlate a real effect/palette choice
/// against a real detected key (previously impossible: this field never existed on this type before).
/// Defaults to <see langword="null"/> for the same back-compat reason as <see cref="BandEnergy"/>.
/// </param>
/// <param name="Melody">
/// Build-plan Phase 19 §2b: the same full-mix <see cref="MelodyTrack"/> <see cref="CorpusSongAnalyzer"/>
/// already computes for <c>BarFingerprintBuilder</c>'s own chroma input, now also kept on the result
/// instead of being discarded -- real pitch/note data (reduced fidelity vs. an isolated vocal stem, see
/// <see cref="CorpusSongAnalyzer"/>'s own class doc comment for why corpus mining never runs stem
/// separation), never a fabricated/empty melody. Defaults to <see langword="null"/> for the same
/// back-compat reason as <see cref="BandEnergy"/>.
/// </param>
/// <param name="TransientHits">
/// Build-plan Phase 23 §2a/§2b: real, loud percussive hits (<see cref="Rhythm.TransientHitDetector"/>),
/// promoted from this song's own already-computed <see cref="Rhythm.OnsetAnalysis"/>/<see cref="BandEnergy"/>
/// exactly the same way real generation already does (<c>AiChoreographyWholeSongGenerator</c>) -- a pure,
/// deterministic post-filter over data <see cref="CorpusSongAnalyzer"/> already computed for the beat
/// grid, so capturing it here needs no new corpus-wide re-analysis. <see cref="CraftRuleInducer"/> uses
/// this to check whether a real hard hit's immediately-preceding effect measurably contrasts in brightness
/// tier with the one at/after it, and whether hard-hit-adjacent moments skew toward short/discrete
/// effects. Defaults to <see langword="null"/> for the same back-compat reason as <see cref="BandEnergy"/>.
/// </param>
public sealed record CorpusSongAnalysis(
    CorpusEntry Entry,
    BeatGrid BeatGrid,
    SongFingerprintSet FingerprintSet,
    SectionLabelAnalysis Labels,
    PhraseSegmentationAnalysis Phrases,
    IReadOnlyList<AlignedEffectInstance> AlignedEffects,
    BandEnergyEnvelope? BandEnergy = null,
    VocalActivityEnvelope? VocalActivity = null,
    IReadOnlyList<KeyChange>? KeyChanges = null,
    MelodyTrack? Melody = null,
    IReadOnlyList<Rhythm.TransientHit>? TransientHits = null);
