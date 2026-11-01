using XlightsAI.Studio.Knowledge;
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
public sealed record CorpusSongAnalysis(
    CorpusEntry Entry,
    BeatGrid BeatGrid,
    SongFingerprintSet FingerprintSet,
    SectionLabelAnalysis Labels,
    PhraseSegmentationAnalysis Phrases,
    IReadOnlyList<AlignedEffectInstance> AlignedEffects,
    BandEnergyEnvelope? BandEnergy = null,
    VocalActivityEnvelope? VocalActivity = null);
