namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// PHASE-2b (build plan §2b.6): one resolved, mineable corpus sequence — a real <c>.xsq</c> with a real,
/// locally-resolved paired audio file. <c>head/mediaFile</c> inside the <c>.xsq</c> is never trusted for
/// this (architecture §8b: "shared sequences arrive with a broken media reference by design" — every
/// <c>.xsq</c> in this corpus was independently verified to carry a stale, non-local
/// <c>mediaFile</c> path); <see cref="AudioPath"/> was instead resolved by hand against the real
/// filesystem, preferring same-directory pairing and falling back to same-song root-level audio reuse
/// when a vendor subfolder copy's own audio could not be found next to it.
/// </summary>
/// <param name="SequencePath">Absolute path to the canonical <c>.xsq</c> for this sequence. For a sequence with more than one on-disk copy (a root-level file and a vendor-named subfolder extraction of the same underlying download), this is the vendor-folder copy — closer to the original download — per architecture §8b's "ship the rules, not the content" licensing stance; see <see cref="DuplicatePaths"/> for the copy not chosen.</param>
/// <param name="AudioPath">Absolute path to the paired audio file this sequence's effects are aligned against.</param>
/// <param name="SongTitle">Human-readable song title, derived from the sequence's own filename (every corpus <c>.xsq</c>'s <c>&lt;head&gt;&lt;song&gt;</c>/<c>&lt;artist&gt;</c> fields were found empty, so the filename is the only real identifying text available).</param>
/// <param name="Provenance">Where this sequence likely came from — see <see cref="CorpusProvenanceTag"/>.</param>
/// <param name="DuplicatePaths">Other on-disk copies of this same sequence found during corpus enumeration (same song, same underlying download), not used for mining but recorded so a later audit can find them.</param>
/// <param name="Notes">Free-text provenance notes — e.g. which <c>orig_zip/</c> entry this was matched against, or why a subfolder's own audio could not be used and a root-level file was substituted instead.</param>
public sealed record CorpusEntry(
    string SequencePath,
    string AudioPath,
    string SongTitle,
    CorpusProvenanceTag Provenance,
    IReadOnlyList<string> DuplicatePaths,
    string Notes);
