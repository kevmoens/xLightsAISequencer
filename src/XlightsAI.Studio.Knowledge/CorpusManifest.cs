namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// PHASE-2b (build plan §2b.6): the persisted result of one corpus enumeration pass — every resolved
/// <see cref="CorpusEntry"/>, plus enough honest accounting that a later phase never has to re-derive
/// (or silently trust) which sequences were actually usable. See <see cref="CorpusManifestBuilder"/> for
/// how this is built and <see cref="JsonCorpusManifestStore"/> for how it is persisted.
/// </summary>
/// <param name="GeneratedAtUtc">When this manifest was built.</param>
/// <param name="RootDirectory">The corpus root directory this manifest was enumerated from (e.g. <c>C:\xLights</c>).</param>
/// <param name="Entries">Every resolved, mineable sequence — one per distinct song, duplicates already collapsed (see <see cref="CorpusEntry.DuplicatePaths"/>).</param>
/// <param name="ExcludedPaths">Every <c>.xsq</c> path considered during enumeration but deliberately excluded, paired with the reason (e.g. "RenderCache", "backup file", "this app's own prior output", "no resolvable paired audio") — so a reviewer can see the exclusion was a real decision, not an omission.</param>
public sealed record CorpusManifest(
    DateTimeOffset GeneratedAtUtc,
    string RootDirectory,
    IReadOnlyList<CorpusEntry> Entries,
    IReadOnlyDictionary<string, string> ExcludedPaths);
