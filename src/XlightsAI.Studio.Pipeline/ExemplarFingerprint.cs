using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// PHASE-2b (build plan §2b.9; architecture §8b): the musical fingerprint of one indexed exemplar
/// window. <see cref="ExemplarIndexBuilder"/> uses <see cref="PhraseCandidate"/>s (not labeled sections)
/// as the exemplar granularity — see that type's own doc comment for why. <see cref="BandBalance"/> and
/// <see cref="VocalActivityHint"/> are architecture §8b's own named fingerprint fields
/// ("tempo, energy percentile, band balance, vocal activity, section kind"); build-plan Phase 16 §2c
/// populates both for real for every newly mined exemplar (see <see cref="CorpusSongAnalyzer"/>'s own
/// band-energy/vocal-activity analysis stages) — both remain <see langword="null"/> only for an entry
/// mined before this phase shipped, never fabricated.
/// </summary>
/// <param name="SongTitle">Which corpus song this exemplar came from.</param>
/// <param name="Provenance">The source song's provenance — carried through so a future redistribution decision can filter exemplars by license without re-deriving it.</param>
/// <param name="StartBarIndex">First bar (inclusive) of the exemplar window.</param>
/// <param name="EndBarIndexExclusive">One past the last bar of the exemplar window.</param>
/// <param name="TempoBpm">The song's selected tempo (<c>BeatGrid.SelectedTempo.Bpm</c>), or <c>0</c> if the song had no viable tempo candidate.</param>
/// <param name="EnergyPercentile">This window's mean per-bar energy (<c>BarFingerprint.Features[0]</c>), expressed as its percentile rank (<c>[0,1]</c>) within its own song's full per-bar energy distribution — "energy percentile," architecture §8b, verbatim, computed per-song since raw energy is not comparable across differently-mastered recordings.</param>
/// <param name="SectionKind">The majority labeled <see cref="SectionKind"/> covering this window's bars, or <see cref="SectionKind.Unknown"/> if none/mixed.</param>
/// <param name="BandBalance">This window's mean normalized [bass, mid, high] energy shares (<c>BandEnergyFrame</c>), or <see langword="null"/> for an exemplar mined before build-plan Phase 16 §2c.</param>
/// <param name="VocalActivityHint">Whether the window's bars overlap a detected vocal-active region, or <see langword="null"/> for an exemplar mined before build-plan Phase 16 §2c.</param>
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
    /// bounds), energy percentile, a band-balance proxy, and a vocal-activity proxy, all four equally
    /// weighted. Build-plan Phase 16 §2c: <see cref="BandBalance"/> contributes only its own mid-band
    /// share (index 1 of the bass/mid/high triple) rather than all three raw bands — the mid band is
    /// where melody/vocal content concentrates, and including bass+mid+high separately would both
    /// double-count overall loudness already captured by <see cref="EnergyPercentile"/> and overweight
    /// band balance's influence on the vector's direction relative to tempo/energy. <see cref="VocalActivityHint"/>
    /// maps <see langword="true"/>/<see langword="false"/> to 1.0/0.0. A <see langword="null"/> value for
    /// either (not yet analyzed -- still possible for an exemplar mined before this phase) maps to 0.5,
    /// the difference-free midpoint: it never actively attracts or repels a match on a dimension that is
    /// simply absent, which a fabricated 0.0 ("definitely bass-heavy"/"definitely no vocal") would.
    /// </summary>
    public double[] ToComparisonVector()
    {
        const double minBpm = 60.0;
        const double maxBpm = 200.0;
        var normalizedTempo = Math.Clamp((TempoBpm - minBpm) / (maxBpm - minBpm), 0.0, 1.0);
        var midBandShare = BandBalance is { Count: 3 } bandBalance ? bandBalance[1] : 0.5;
        var vocalActivity = VocalActivityHint switch { true => 1.0, false => 0.0, null => 0.5 };
        return [normalizedTempo, EnergyPercentile, midBandShare, vocalActivity];
    }
}
