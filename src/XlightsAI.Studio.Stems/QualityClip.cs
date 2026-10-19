using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// One of the full-phase test matrix's own item 7 "listening reference clips": a source song to run
/// through a real <see cref="IStemSeparator"/>, plus the known-correct reference ("ground truth") WAV
/// file path for every stem kind that clip is able to score. A reference path that does not exist on
/// disk is detected and rejected explicitly by <see cref="SeparationQualityEvaluator"/> before any
/// separation work runs -- this step's own "missing media must be handled explicitly" failure/edge case.
/// </summary>
public sealed record QualityClip(
    string ClipId,
    SongAsset Source,
    IReadOnlyDictionary<StemKind, string> ReferenceStemFilePaths);
