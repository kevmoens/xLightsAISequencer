namespace XlightsAI.Studio.Stems;

/// <summary>
/// Declares which extracted ONNX file inside one verified, pinned archive <see cref="ModelAsset"/>
/// backs each stem's own single-stem session -- never a second independently-pinned <see cref="ModelAsset"/>
/// per stem, since both files are produced by extracting one already hash-verified archive.
///
/// <b>Disclosed <see cref="StemKind"/> mapping decision:</b> the real sherpa-onnx-spleeter-2stems model's
/// two stems are "vocals" and "accompaniment" (the whole non-vocal backing mix). <c>vocals.onnx</c> maps
/// cleanly to the existing <see cref="StemKind.Vocals"/>. "accompaniment" has no clean existing match and
/// is mapped to <see cref="StemKind.Other"/> anyway as the lower-risk, non-breaking choice (no new
/// <see cref="StemKind"/> member added) -- a disclosed semantic overload (see PHASE-06A.md Section 3):
/// a <see cref="StemAsset"/> produced by this recipe with <see cref="StemKind.Other"/> means "the whole
/// undifferentiated backing track," never "miscellaneous leftover instruments" the way it would for a
/// waveform-domain, multi-stem <see cref="StemSeparationRecipe"/>-produced asset.
/// </summary>
/// <param name="Archive">The one verified, pinned archive <see cref="ModelAsset"/> both extracted files come from.</param>
/// <param name="StemArchiveEntryNames">Which archive entry (extracted file) backs each declared stem's own single-stem ONNX session.</param>
public sealed record SpectrogramModelBundle(
    ModelAsset Archive,
    IReadOnlyDictionary<StemKind, string> StemArchiveEntryNames);
