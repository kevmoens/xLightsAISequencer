namespace XlightsAI.Studio.Stems;

/// <summary>
/// Step 039's own required outcome, verbatim: "Implement model asset manifest". The declared,
/// persisted inventory of every known downloadable <see cref="ModelAsset"/> -- the "verified download
/// manifest (content hash, URL, model/version, license)" Section 3 requires -- kept separate from any
/// individual download attempt (<see cref="ModelAssetManager"/>) so the manifest itself can be inspected,
/// versioned and audited independently of whether any entry has actually been downloaded yet.
/// </summary>
/// <param name="Id">Stable identity for this manifest revision.</param>
/// <param name="CreatedAtTicks">Caller-supplied wall-clock ticks this manifest was produced -- supplied
/// explicitly, not sampled internally, keeping <see cref="ModelAssetManifestValidator"/> a pure function.</param>
/// <param name="Assets">Every declared asset entry. May be empty (no assets declared yet is a valid,
/// inspectable manifest state, distinct from a corrupt or missing file).</param>
public sealed record ModelAssetManifest(
    Guid Id,
    long CreatedAtTicks,
    IReadOnlyList<ModelAsset> Assets);
