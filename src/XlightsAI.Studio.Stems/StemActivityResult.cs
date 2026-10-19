namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Step 043's own persisted, versioned output: the complete, inspectable result of one
/// activity-detection run against a single <see cref="StemAsset"/> -- mirrors
/// <c>XlightsAI.Studio.Rhythm.EnergyEnvelope</c>/<c>OnsetAnalysis</c>'s own provenance shape exactly.
/// Pure algorithm output, rebuildable at any time from <see cref="SourceHash"/>/<see cref="Settings"/>;
/// never itself a place a user correction or an accepted <c>EditTransaction</c> is recorded (Section 4:
/// preview/analysis artifacts "cannot update the accepted document directly").
/// </summary>
/// <param name="VersionId">Identity of this particular computed run -- changes every time the result is rebuilt, even if the resulting regions are numerically identical.</param>
/// <param name="StemId">The <see cref="StemAsset.Id"/> this result was computed from.</param>
/// <param name="SourceHash">The source <see cref="StemAsset.Sha256"/> at detection time -- a changed/re-separated stem never silently serves a stale result built from the old content.</param>
/// <param name="DetectorVersion">Identifies which detector (and its version) produced this result -- Section 4's "every cache key includes algorithm/model/settings versions" rule, applied to the detection algorithm itself.</param>
/// <param name="Settings">The exact <see cref="StemActivityDetectionSettings"/> this run used -- a second, independent cache-key component alongside <see cref="DetectorVersion"/>.</param>
/// <param name="Activities">Detected regions, ordered by <see cref="StemActivity.StartTicks"/>. May be empty (e.g. a fully silent stem) -- an empty list is a valid, inspectable result, not an error.</param>
public sealed record StemActivityResult(
    Guid VersionId,
    Guid StemId,
    string SourceHash,
    string DetectorVersion,
    StemActivityDetectionSettings Settings,
    StemActivity[] Activities);
