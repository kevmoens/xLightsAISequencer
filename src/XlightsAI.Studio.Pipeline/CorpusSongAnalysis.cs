using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// PHASE-2b (build plan §2b.7-8): one corpus sequence's full analysis bundle — real audio analysis
/// (beat grid, bar fingerprints, section labels, phrases) plus every parsed effect aligned onto it.
/// <see cref="CorpusSongAnalyzer"/>'s own output; <see cref="PatternLibraryBuilder"/>,
/// <see cref="ExemplarIndexBuilder"/> and <see cref="CraftRuleInducer"/> all consume a corpus-wide
/// collection of these.
/// </summary>
public sealed record CorpusSongAnalysis(
    CorpusEntry Entry,
    BeatGrid BeatGrid,
    SongFingerprintSet FingerprintSet,
    SectionLabelAnalysis Labels,
    PhraseSegmentationAnalysis Phrases,
    IReadOnlyList<AlignedEffectInstance> AlignedEffects);
