using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// PHASE-05 Section 3's proposed signature, with disclosed additions carried over from the same
/// provenance convention Steps 032/033 already established: Step 034's own persisted, versioned
/// output — a generated beat/bar grid built from an already-selected <see cref="TempoHypothesis"/>
/// against its source <see cref="OnsetAnalysis"/>. Still a raw <i>analyzed</i> artifact, not yet a
/// user-approved correction (Section 2: "Separate raw audio features from human-approved beat
/// grid") — Step 035 is where a human accepts/corrects individual <see cref="Beats"/> entries.
/// </summary>
/// <param name="VersionId">Identity of this particular computed run — changes every time the grid is rebuilt, even if the resulting beats are numerically identical.</param>
/// <param name="SongId">The <c>SongAsset.Id</c> this grid was ultimately computed from.</param>
/// <param name="SourceHash">The source <c>SongAsset.Sha256</c> the consumed <see cref="OnsetAnalysis"/>/<see cref="TempoAnalysis"/> were themselves built from.</param>
/// <param name="SourceOnsetAnalysisVersionId">The exact <see cref="OnsetAnalysis.VersionId"/> this grid's beat positions were aligned against.</param>
/// <param name="SourceTempoAnalysisVersionId">The exact <see cref="TempoAnalysis.VersionId"/> <see cref="SelectedTempo"/> was chosen from — a rebuilt tempo analysis (even with the same <see cref="SourceHash"/>) always invalidates this cache.</param>
/// <param name="AnalyzerVersion">Identifies which builder (and its version) produced this grid.</param>
/// <param name="Settings">The exact <see cref="BeatGridSettings"/> this run used.</param>
/// <param name="SelectedTempo">The single <see cref="TempoHypothesis"/> this grid's beat spacing is generated from — the top-ranked (highest-confidence) entry of <see cref="Candidates"/>, per this step's own disclosed scope (§ <see cref="IBeatGridBuilder"/>). <see langword="null"/> when the source tempo analysis carried no viable candidate (e.g. silence) — see <see cref="Beats"/>.</param>
/// <param name="Beats">Generated beat markers, ordered by <see cref="BeatMarker.TimeTicks"/>. Empty, never an exception, when <see cref="SelectedTempo"/> is <see langword="null"/> or the source onset analysis carried no onsets to anchor a grid against — this step's own "silence... must be handled explicitly" edge case.</param>
/// <param name="Candidates">The full ranked tempo candidate list carried through unchanged from the source <see cref="TempoAnalysis"/> (Section 3's proposed <c>BeatGrid.Candidates</c> shape, verbatim) — so a consumer can see every alternate hypothesis this grid's <see cref="SelectedTempo"/> was chosen among, not just the one that won.</param>
/// <param name="CorrectionVersion">Reserved for Step 035's own manual-correction tracking (Section 4: "...and user correction version"). Always <c>0</c> for every grid this step (034) produces; this step never increments it. Declared now, not retrofitted later, because <see cref="BeatGrid"/> is the one contract Step 035 extends rather than redefines (Section 2: "Treat all preceding completed step contracts as immutable").</param>
public sealed record BeatGrid(
    Guid VersionId,
    SongId SongId,
    string SourceHash,
    Guid SourceOnsetAnalysisVersionId,
    Guid SourceTempoAnalysisVersionId,
    string AnalyzerVersion,
    BeatGridSettings Settings,
    TempoHypothesis? SelectedTempo,
    BeatMarker[] Beats,
    TempoHypothesis[] Candidates,
    long CorrectionVersion);
