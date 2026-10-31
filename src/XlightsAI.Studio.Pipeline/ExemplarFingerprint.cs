using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// PHASE-2b (build plan §2b.9; architecture §8b): the musical fingerprint of one indexed exemplar
/// window. <see cref="ExemplarIndexBuilder"/> uses <see cref="PhraseCandidate"/>s (not labeled sections)
/// as the exemplar granularity — see that type's own doc comment for why. <see cref="BandBalance"/> and
/// <see cref="VocalActivityHint"/> are architecture §8b's own named fingerprint fields
/// ("tempo, energy percentile, band balance, vocal activity, section kind"); both are
/// <see langword="null"/> for every exemplar this builder produces, a disclosed Phase 3 gap (see class
/// remarks on <see cref="ExemplarIndexBuilder"/>) rather than a fabricated value.
/// </summary>
/// <param name="SongTitle">Which corpus song this exemplar came from.</param>
/// <param name="Provenance">The source song's provenance — carried through so a future redistribution decision can filter exemplars by license without re-deriving it.</param>
/// <param name="StartBarIndex">First bar (inclusive) of the exemplar window.</param>
/// <param name="EndBarIndexExclusive">One past the last bar of the exemplar window.</param>
/// <param name="TempoBpm">The song's selected tempo (<c>BeatGrid.SelectedTempo.Bpm</c>), or <c>0</c> if the song had no viable tempo candidate.</param>
/// <param name="EnergyPercentile">This window's mean per-bar energy (<c>BarFingerprint.Features[0]</c>), expressed as its percentile rank (<c>[0,1]</c>) within its own song's full per-bar energy distribution — "energy percentile," architecture §8b, verbatim, computed per-song since raw energy is not comparable across differently-mastered recordings.</param>
/// <param name="SectionKind">The majority labeled <see cref="SectionKind"/> covering this window's bars, or <see cref="SectionKind.Unknown"/> if none/mixed.</param>
/// <param name="BandBalance">Per-band energy balance — <see langword="null"/> for every exemplar (see class remarks): <c>EnergyEnvelopeAnalyzer</c> only produces broadband RMS, no per-band split.</param>
/// <param name="VocalActivityHint">Whether a vocal is active during this window — <see langword="null"/> for every exemplar (see class remarks): no vocal-activity detector exists yet in this codebase.</param>
public sealed record ExemplarFingerprint(
    string SongTitle,
    CorpusProvenanceTag Provenance,
    int StartBarIndex,
    int EndBarIndexExclusive,
    double TempoBpm,
    double EnergyPercentile,
    SectionKind SectionKind,
    IReadOnlyList<double>? BandBalance,
    bool? VocalActivityHint)
{
    /// <summary>
    /// The comparison vector <see cref="ExemplarIndex.FindSimilar"/> uses — tempo (normalized against a
    /// generous 60-200 BPM corpus range, matching <c>AudioAnalysisSettingsBundle</c>'s own tempo-scan
    /// bounds) and energy percentile, equally weighted. Band balance/vocal activity are omitted (both
    /// <see langword="null"/> for every real exemplar today); extending this vector once Phase 3 supplies
    /// them is the natural place to do it, not fabricating zeros for an unmeasured dimension today.
    /// </summary>
    public double[] ToComparisonVector()
    {
        const double minBpm = 60.0;
        const double maxBpm = 200.0;
        var normalizedTempo = Math.Clamp((TempoBpm - minBpm) / (maxBpm - minBpm), 0.0, 1.0);
        return [normalizedTempo, EnergyPercentile];
    }
}
