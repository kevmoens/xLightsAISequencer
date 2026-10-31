namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-1c (architecture §12 blocker 3, "sequence metadata (<c>author</c>, <c>song</c>,
/// <c>mediaFile</c>, etc.)"): the real xLights <c>&lt;head&gt;</c> metadata fields this build previously
/// always emitted empty for lack of anywhere to source them from. Every field defaults to an empty
/// string so <c>new SequenceMetadata()</c> round-trips to exactly the same empty-head shape
/// <see cref="XsqExporter"/> already emitted before this type existed — the golden fixture
/// (<c>Fixtures/golden/golden-three-effect-sequence.xsq</c>) is unaffected by this type's introduction.
/// </summary>
public sealed record SequenceMetadata(
    string Author = "",
    string AuthorEmail = "",
    string AuthorWebsite = "",
    string Song = "",
    string Artist = "",
    string Album = "",
    string MusicUrl = "",
    string Comment = "",
    string MediaFile = "");
