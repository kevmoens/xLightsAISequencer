namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Step 055's own title, verbatim: "Segment sections into phrase units with manual
/// corrections." A phrase-local, concrete narrowing of PHASE-08 Section 3's proposed
/// <c>MusicalPhrase(Guid Id, Guid SectionId, long StartTicks, long EndTicks, int? Bars, string
/// SignatureHash)</c> -- same identity/provenance fields, adapted to this step's own bar-based recipe
/// (<see cref="Bars"/> is never <see langword="null"/> here: every phrase this step produces is bar-
/// aligned by construction, "Phrase boundaries align to bars or user-chosen events," this step's own
/// required acceptance, verbatim -- there is no non-bar-aligned phrase this recipe can ever propose).
/// </summary>
/// <param name="Id">Stable identity for this one phrase.</param>
/// <param name="SectionLabelId">The owning <see cref="SectionLabelCandidate.Id"/> (Section 3's own <c>SectionId</c>) this phrase belongs to -- the stable key <see cref="IPhraseSegmenter.SetManualBoundariesAsync"/> and a later step's UI both address this phrase's containing section by.</param>
/// <param name="StartBarIndex">The first bar (inclusive) this phrase covers -- always either the owning section's own start bar, or a boundary this step itself proposed/accepted.</param>
/// <param name="EndBarIndexExclusive">One past the last bar this phrase covers (half-open, Section 2 convention) -- always either the next phrase's own <see cref="StartBarIndex"/> or the owning section's own end bar. Phrases within one section never overlap and never leave a gap (Step 055's own implementation guidance, verbatim: "permit irregular length and phrase overlaps only by deliberate policy" -- this recipe's own declared policy is "never overlap, ever").</param>
/// <param name="StartTicks">The phrase's absolute start tick -- always the corresponding bar's own <see cref="BarFingerprint.Window"/> start.</param>
/// <param name="EndTicks">The phrase's absolute end tick (half-open) -- always the corresponding end bar's own <see cref="BarFingerprint.Window"/> start, or the end of the source fingerprint set's final bar.</param>
/// <param name="Bars">The exact bar count this phrase spans (<c>EndBarIndexExclusive - StartBarIndex</c>). Deliberately non-nullable (narrower than Section 3's proposed <c>int? Bars</c>): every phrase this recipe produces is bar-aligned by construction, so the bar count is always known.</param>
/// <param name="Source">How this phrase's own <see cref="StartBarIndex"/> was determined.</param>
/// <param name="Confidence">Unit-normalized confidence in <c>[0, 1]</c> that <see cref="StartBarIndex"/> is the musically correct phrase start -- never fabricated as uniformly certain. Always <c>1.0</c> when <see cref="UserCorrected"/> is <see langword="true"/>, or when this phrase's own start is simply the owning section's already-approved start/end boundary (inherited certainty, not a guess).</param>
/// <param name="Evidence">Human-readable, non-throwing reasons this specific boundary/confidence was chosen. Never empty.</param>
/// <param name="SignatureHash">A stable SHA-256 hex digest of this phrase's own averaged <see cref="BarFingerprint.Features"/> -- Section 3's own proposed field, carried forward unused by this step's own narrower scope but populated so Step 056's own later motif-clustering work has it available without recomputation (Section 3: "Distinguish segment boundary from section label and motif identity" -- this step never itself compares <see cref="SignatureHash"/> values across phrases).</param>
/// <param name="UserCorrected">Whether a human has set this phrase's containing section's boundaries via <see cref="IPhraseSegmenter.SetManualBoundariesAsync"/> (Step 055's own required acceptance, verbatim: "...with manual corrections"). Overrides are read from a separately versioned store and re-applied on every <see cref="IPhraseSegmenter.SegmentAsync"/> call, including across an application restart and across an upstream re-analysis that invalidates the automatic segmentation cache.</param>
public sealed record PhraseCandidate(
    Guid Id,
    Guid SectionLabelId,
    int StartBarIndex,
    int EndBarIndexExclusive,
    long StartTicks,
    long EndTicks,
    int Bars,
    PhraseBoundarySource Source,
    double Confidence,
    IReadOnlyList<string> Evidence,
    string SignatureHash,
    bool UserCorrected);
