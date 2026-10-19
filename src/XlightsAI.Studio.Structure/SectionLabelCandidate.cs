namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Step 054's own title, verbatim: "Assign candidate intro/verse/chorus/bridge labels and
/// support overrides." One classified segment -- the half-open bar range between two adjacent
/// <see cref="SectionBoundaryCandidate"/>s (or the final boundary and the end of the song) -- with its
/// proposed <see cref="SectionKind"/>, <see cref="Confidence"/> and human-readable <see cref="Evidence"/>
/// (Step 054's own required acceptance, verbatim: "UI shows provenance/confidence"). A section *label* is
/// deliberately distinct from the underlying segment *boundary* (Section 3: "Distinguish segment boundary
/// from section label and motif identity") -- this type only answers "what is this segment," never "where
/// does it start," which remains <see cref="SectionBoundaryCandidate"/>'s own sole authority.
/// </summary>
/// <param name="Id">Stable identity for this one classified segment.</param>
/// <param name="BoundaryId">The <see cref="SectionBoundaryCandidate.Id"/> this segment starts at -- the stable key a user override (<see cref="ISectionLabelOverrideStore"/>) and a later step's UI both address this segment by, independent of bar-index drift from a boundary re-analysis.</param>
/// <param name="StartBarIndex">The first bar (inclusive) this segment covers.</param>
/// <param name="EndBarIndexExclusive">One past the last bar this segment covers (half-open, Section 2 convention).</param>
/// <param name="Kind">The classified label. <see cref="SectionKind.Unknown"/> is a valid, non-error outcome when the evidence does not clear <see cref="SectionLabelSettings.MinConfidenceToLabel"/> (Step 054's own "Unknown is valid; never force classical song structure," verbatim).</param>
/// <param name="Confidence">Unit-normalized confidence in <c>[0, 1]</c> in <see cref="Kind"/> -- never fabricated as uniformly certain. <c>1.0</c> whenever <see cref="UserCorrected"/> is <see langword="true"/> (a human decision is never second-guessed by the automatic recipe's own confidence scale).</param>
/// <param name="Evidence">Human-readable, non-throwing reasons this specific <see cref="Kind"/>/<see cref="Confidence"/> was chosen -- "with confidence and evidence" (Step 054's own implementation guidance, verbatim). Never empty.</param>
/// <param name="UserCorrected">Whether a human has overridden the automatic classification for this segment via <see cref="ISectionLabelClassifier.SetOverrideAsync"/> (Step 054's own required acceptance, verbatim: "support overrides"). Overrides are read from a separately versioned store (<see cref="ISectionLabelOverrideStore"/>) and re-applied on every <see cref="ISectionLabelClassifier.ClassifyAsync"/> call, including across an application restart and across an upstream re-analysis that invalidates the automatic classification cache.</param>
public sealed record SectionLabelCandidate(
    Guid Id,
    Guid BoundaryId,
    int StartBarIndex,
    int EndBarIndexExclusive,
    SectionKind Kind,
    double Confidence,
    IReadOnlyList<string> Evidence,
    bool UserCorrected);
