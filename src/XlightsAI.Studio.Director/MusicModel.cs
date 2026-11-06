using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Architecture §3's L0 analysis output, bundled -- no such aggregate type exists yet anywhere in this
/// codebase (confirmed by prior research: every real consumer, e.g. <c>ChoreographyEngine.Input</c>,
/// takes the same five analysis types as separate constructor parameters). Phase 5 is the first
/// consumer that genuinely needs one value to build a prompt from, so it is introduced here rather than
/// forcing every prompt-construction call site to re-list five parameters.
/// </summary>
public sealed record MusicModel(
    SongId SongId,
    long SongDurationTicks,
    BeatGrid BeatGrid,
    SectionLabelAnalysis Sections,
    PhraseSegmentationAnalysis Phrases,
    BandEnergyEnvelope? BandEnergy,
    VocalActivityEnvelope? VocalActivity,
    IReadOnlyList<VisemeMark>? VisemeMarks = null,
    MelodyTrack? Melody = null,
    MotifClusteringAnalysis? Motifs = null,
    IReadOnlyList<TransientHit>? TransientHits = null,
    IReadOnlyList<EnergyTransition>? EnergyTransitions = null,
    EnergyEnvelope? Energy = null,
    IReadOnlyList<KeyChange>? KeyChanges = null,
    IReadOnlyList<TranscribedWord>? Transcript = null);
