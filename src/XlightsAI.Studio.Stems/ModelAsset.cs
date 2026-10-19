namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Section 3's own proposed signature: one declared, downloadable model asset entry. This is
/// the type Step 038 explicitly deferred (its own evidence §3/§9: "a Step 039 implementer will reconcile
/// or supersede this step's narrower, phase-local shapes explicitly, not silently inherit them") — Step
/// 039 ("Implement model asset manifest, verified download, consent and checksum validation") is exactly
/// where it is introduced.
/// </summary>
/// <param name="Id">Stable identifier for this model asset (e.g. upstream repo/package name).</param>
/// <param name="Version">Pinned version/revision this declared asset entry describes.</param>
/// <param name="Source">Absolute HTTPS URL the verified bytes are downloaded from. Never HTTP — enforced by <see cref="ModelAssetValidator"/>, not merely documented.</param>
/// <param name="Sha256">Declared, pinned SHA-256 of the full downloaded file's bytes, lower-case hex. The sole basis for "verified" — never trusted from the response alone.</param>
/// <param name="LicenseId">SPDX-style license identifier for the model weights.</param>
/// <param name="InferenceBackend">Concrete .NET-reachable inference backend this asset is declared for (e.g. "ONNX Runtime (Microsoft.ML.OnnxRuntime, CPU)").</param>
public sealed record ModelAsset(
    string Id,
    string Version,
    Uri Source,
    string Sha256,
    string LicenseId,
    string InferenceBackend);
