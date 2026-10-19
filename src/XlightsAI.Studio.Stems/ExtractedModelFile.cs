namespace XlightsAI.Studio.Stems;

/// <summary>
/// One verified, extracted file inside a declared, already hash-verified archive <see cref="ModelAsset"/>
/// -- not an independently-pinned <see cref="ModelAsset"/> itself (no individual SHA-256 exists for an
/// individually extracted file; only the archive's own hash is pinned), so its provenance is always
/// traced back to the verified archive it came from.
/// </summary>
/// <param name="ArchiveEntryName">The entry's path inside the archive (e.g. <c>sherpa-onnx-spleeter-2stems/vocals.onnx</c>).</param>
/// <param name="LocalPath">Absolute local path the entry was extracted to.</param>
/// <param name="Length">Extracted file length in bytes.</param>
public sealed record ExtractedModelFile(string ArchiveEntryName, string LocalPath, long Length);
