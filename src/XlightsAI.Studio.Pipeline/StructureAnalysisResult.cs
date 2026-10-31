using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline;

/// <summary>Every real analysis artifact <see cref="StructureAnalysisRunner.AnalyzeAsync"/> produces for one song.</summary>
public sealed record StructureAnalysisResult(
    SongFingerprintSet FingerprintSet,
    SectionBoundaryAnalysis Boundaries,
    SectionLabelAnalysis Labels,
    PhraseSegmentationAnalysis Phrases,
    MotifClusteringAnalysis Motifs);
