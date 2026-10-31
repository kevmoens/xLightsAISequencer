using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// PHASE-2b (build plan §2b.8; architecture §8b): the corpus-wide statistical priors mined from every
/// non-<see cref="XlightsAI.Studio.Knowledge.CorpusProvenanceTag.ExternalReference"/> entry's aligned
/// effects. See <see cref="PatternLibraryBuilder"/> for how this is computed.
/// </summary>
/// <param name="GeneratedAtUtc">When this library was built.</param>
/// <param name="SongCount">Number of corpus songs actually included (the external-reference show and any song whose analysis failed are excluded — see the Phase 2b final report for exact exclusions).</param>
/// <param name="TotalEffectCount">Total aligned effect instances this library was computed from.</param>
/// <param name="EffectFrequency">Effect name → instance count, overall across every included song — the corpus-wide vocabulary tally architecture §8b's own table reports.</param>
/// <param name="EffectFrequencyBySection">Effect name → instance count, conditioned on the labeled <see cref="SectionKind"/> the effect's start bar falls inside. A <see cref="SectionKind.Unknown"/> bucket is included (effects whose section genuinely could not be classified) rather than silently dropped.</param>
/// <param name="DensityBySection">Mean effects-started-per-second, by <see cref="SectionKind"/>, averaged across every labeled section of that kind in the corpus (not weighted by section length) — "density curves by section kind," architecture §8b, reduced to one scalar per kind rather than a full sub-second curve (a disclosed simplification; see the Phase 2b final report).</param>
/// <param name="LayerCountPerModelDistribution">Distinct <c>&lt;EffectLayer&gt;</c> count actually used → number of (song, model) pairs using that many layers.</param>
/// <param name="BlendModeUsage">Blend mode value (<c>T_CHOICE_LayerMethod</c>, authoritative over any legacy <c>B_</c> duplicate per architecture §8a) → instance count, for every effect that set one explicitly (defaults are omitted from the settings string entirely, architecture §8a, so "no entry" is this library's own implicit "Normal/default" bucket, never fabricated as an explicit value).</param>
/// <param name="PaletteActiveSlotDistribution">Active palette slot count (number of ticked <c>C_CHECKBOX_PaletteN</c> checkboxes in the resolved palette string) → instance count.</param>
/// <param name="ModeACount">Effects classified Mode A — bound to a labeled timing track (architecture §6's 7-effect table; see <see cref="PatternLibraryBuilder.IsModeA"/> for the exact key match).</param>
/// <param name="ModeBCount">Every other effect — discrete, per-event emission.</param>
public sealed record PatternLibrary(
    DateTimeOffset GeneratedAtUtc,
    int SongCount,
    int TotalEffectCount,
    IReadOnlyDictionary<string, int> EffectFrequency,
    IReadOnlyDictionary<SectionKind, IReadOnlyDictionary<string, int>> EffectFrequencyBySection,
    IReadOnlyDictionary<SectionKind, double> DensityBySection,
    IReadOnlyDictionary<int, int> LayerCountPerModelDistribution,
    IReadOnlyDictionary<string, int> BlendModeUsage,
    IReadOnlyDictionary<int, int> PaletteActiveSlotDistribution,
    int ModeACount,
    int ModeBCount)
{
    public double ModeARatio => ModeACount + ModeBCount == 0 ? 0 : ModeACount / (double)(ModeACount + ModeBCount);
}
