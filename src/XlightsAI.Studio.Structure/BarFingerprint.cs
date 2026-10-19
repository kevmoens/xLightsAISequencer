using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// One normalized per-bar feature fingerprint (Step 052's own title, verbatim: "Compute normalized
/// bar/phrase feature fingerprints from audio and events"). A bar is this step's own chosen granularity
/// -- the already-approved <c>BeatGrid</c>'s own downbeat spacing (Phase 05) -- not yet the higher-level
/// <c>MusicalPhrase</c>/<c>SongSection</c> boundaries PHASE-08 Section 3 proposes (those belong to Steps
/// 053/054, which this step's own fingerprints feed into; "retain enough features to explain matches" is
/// this step's own required acceptance, satisfied by keeping every component feature inspectable rather
/// than collapsing straight to a single opaque similarity score).
/// </summary>
/// <param name="BarIndex">The <c>BeatMarker.BarIndex</c> this fingerprint summarizes.</param>
/// <param name="Window">The half-open <c>[Start,End)</c> tick range this bar actually covers -- from its own downbeat up to the next bar's downbeat, or the end of the analyzed audio for the final bar.</param>
/// <param name="Features">
/// A fixed-length, declared-order feature vector: index <c>0</c> is mean peak-normalized energy across
/// the bar, index <c>1</c> is that same energy's variance across the bar, and indices
/// <c>[2, 2+ChromaBins)</c> are a duration-weighted pitch-class histogram (each entry in <c>[0,1]</c>,
/// summing to <c>1</c> whenever at least one melody note sounds in the bar). Every component is already
/// either peak-normalized (energy) or duration-weighted-and-renormalized (chroma) -- never a raw,
/// gain-dependent amplitude -- so two bars that are the same musical idea at different recording gain
/// compare consistently (this step's own required acceptance, verbatim).
/// </param>
/// <param name="Confidence">
/// <c>1.0</c> when at least one real energy-analysis frame actually covers <see cref="Window"/>;
/// <c>0.0</c> for a degenerate bar with no analyzed audio underneath it (e.g. a bar entirely past the end
/// of a decoded, non-silent song) -- "avoid asserting uncertainty away" (the same disclosed convention
/// every prior Rhythm/Melody confidence field already follows), not a fabricated uniform certainty.
/// </param>
/// <param name="IsSilent">
/// <see langword="true"/> when this bar has no measurable energy and no overlapping melody note -- an
/// explicit, inspectable "nothing happens here" marker (Step 052's own "silence... must be handled
/// explicitly" failure/edge case), distinct from a bar that simply has low but real energy.
/// </param>
public sealed record BarFingerprint(
    int BarIndex,
    TimeWindow Window,
    IReadOnlyList<double> Features,
    double Confidence,
    bool IsSilent);
