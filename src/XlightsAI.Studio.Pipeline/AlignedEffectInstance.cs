using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// PHASE-2b (build plan §2b.7-8; architecture §8b): one <see cref="ParsedEffectInstance"/> aligned onto
/// its song's own beat/bar/section grid — <see cref="CorpusSongAnalyzer"/>'s own output shape, and the
/// unit <see cref="PatternLibraryBuilder"/>/<see cref="ExemplarIndexBuilder"/> both tabulate statistics
/// over.
/// </summary>
/// <param name="Effect">The raw parsed placement this alignment was computed for.</param>
/// <param name="StartBarIndex">The bar this effect starts on, or <c>-1</c> if it starts before the first detected downbeat.</param>
/// <param name="StartBeatWithinBar">The beat-within-bar this effect starts on (<see cref="XlightsAI.Studio.Rhythm.BeatMarker.BeatWithinBar"/> convention; <c>0</c> is the downbeat), or <c>-1</c> if no beat grid was available.</param>
/// <param name="OffsetFromNearestBeatMs">Signed offset in milliseconds from this effect's own start time to the nearest beat marker (negative = started early), or <see langword="null"/> if the song had no detected beats at all.</param>
/// <param name="SectionKind">The <see cref="SectionKind"/> of the labeled section this effect's start time falls inside, or <see langword="null"/> if no section covers this time (e.g. past the last detected boundary) or the song's section labeling produced no usable boundaries.</param>
/// <param name="PhraseStartBarIndex">The <see cref="PhraseCandidate.StartBarIndex"/> of the phrase this effect's start time falls inside, or <see langword="null"/> if none.</param>
/// <param name="PhraseBars">The bar length of that same phrase, or <see langword="null"/> if none — this is what the 2/2/4 eight-bar-phrase CraftRule checks against.</param>
public sealed record AlignedEffectInstance(
    ParsedEffectInstance Effect,
    int StartBarIndex,
    int StartBeatWithinBar,
    double? OffsetFromNearestBeatMs,
    SectionKind? SectionKind,
    int? PhraseStartBarIndex,
    int? PhraseBars);
