namespace XlightsAI.Studio.Reference.Fixtures;

/// <summary>
/// The kinds of baseline fixture this Step 003 corpus must contain. Spec wording (Step 003
/// implementation guidance): "synthetic notes, a known click track, an isolated piano phrase,
/// a short mixed-source audio fixture with rights, and at least two layout XML shapes."
/// </summary>
public enum FixtureKind
{
    SyntheticNotes,
    DrumPulseClickTrack,
    InstrumentalPhrase,
    ComplexMixedSong,
    LayoutShape,
}

/// <summary>
/// One fixture in the corpus: where it lives, its recorded hash, its usage rights, the reference
/// (spec/tool) version it was produced against, and the expected outputs a reader can check without
/// re-deriving them (duration, sample rate, note/click counts, etc.).
/// </summary>
public sealed record FixtureEntry(
    string Id,
    FixtureKind Kind,
    string RelativePath,
    string Sha256,
    string PermittedUsage,
    string LicenseNote,
    string ReferenceVersion,
    IReadOnlyDictionary<string, string> ExpectedOutputs);

/// <summary>
/// Deserialized form of <c>fixtures/FIXTURE-CORPUS-MANIFEST.json</c>: the Step 003 deliverable,
/// "a baseline fixture corpus" with recorded input hashes, permitted usage, outputs and a
/// repeatable invocation (<see cref="GeneratedByTool"/> + <see cref="GeneratedWithToolVersion"/>
/// identify the exact deterministic generator that can reproduce every audio fixture byte-for-byte).
/// </summary>
public sealed record FixtureCorpusManifest(
    int SchemaVersion,
    string GeneratedByTool,
    string GeneratedWithToolVersion,
    DateTimeOffset GeneratedUtc,
    IReadOnlyList<FixtureEntry> Fixtures);
