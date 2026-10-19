namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Step 053's own title, verbatim: "Detect candidate novelty/section boundaries with
/// confidence." One proposed boundary between two adjacent bars (or the trivial start-of-song
/// boundary at <see cref="BarIndex"/> <c>0</c>) -- a segment boundary only, never yet a section
/// label (Section 3: "Distinguish segment boundary from section label and motif identity"; labeling
/// is Step 054's own later scope, not fabricated here).
/// </summary>
/// <param name="Id">Stable identity for this one candidate -- required so a later step (054+) or a human can address/approve/move a single boundary without re-identifying it by position.</param>
/// <param name="BarIndex">The <see cref="BarFingerprint.BarIndex"/> this boundary falls on -- the bar that begins the new segment. <c>0</c> always denotes the song's own start, not a detected novelty event.</param>
/// <param name="TimeTicks">The boundary's absolute tick position -- always the corresponding bar's own <see cref="BarFingerprint.Window"/> start, which is itself an already-approved <c>BeatMarker</c> downbeat (Step 052's own contract); never an independently invented timestamp.</param>
/// <param name="NoveltyScore">The raw bar-to-bar dissimilarity (<c>1 - cosine similarity</c> against the immediately preceding bar) this candidate was detected from, in <c>[0, 1]</c> for the non-negative feature vectors this recipe produces. <c>0</c> for the trivial <see cref="BarIndex"/> <c>0</c> entry, which has no preceding bar to compare against.</param>
/// <param name="Confidence">
/// Unit-normalized confidence in <c>[0, 1]</c> that this is a real section change rather than
/// incidental variation -- "represent uncertain and human-adjustable boundaries" (Step 053's own
/// implementation guidance, verbatim), never fabricated as uniformly certain. Equal to
/// <see cref="NoveltyScore"/> for a detected boundary (the stronger the novelty, the more confident
/// the proposal); always <c>1.0</c> for the trivial start-of-song entry.
/// </param>
/// <param name="SnappedToBeatGrid">
/// Always <see langword="true"/> for every candidate this step produces -- "snap only when evidence
/// supports beat grid" (Step 053's own implementation guidance, verbatim) is satisfied structurally:
/// every candidate's <see cref="TimeTicks"/> is inherited directly from an already-approved bar
/// window boundary (itself an already-approved beat-grid downbeat), never an unsnapped, freely
/// floating timestamp a lower-evidence detector might propose.
/// </param>
/// <param name="UserApproved">Whether a human has explicitly accepted this specific boundary (a later step's own scope -- boundary review/override UI is Step 058's own scope). Always <see langword="false"/> for every candidate this step produces.</param>
public sealed record SectionBoundaryCandidate(
    Guid Id,
    int BarIndex,
    long TimeTicks,
    double NoveltyScore,
    double Confidence,
    bool SnappedToBeatGrid,
    bool UserApproved);
