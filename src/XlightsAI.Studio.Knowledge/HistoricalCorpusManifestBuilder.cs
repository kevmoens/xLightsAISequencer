namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// Build-plan Phase 17 §2b/§2c: builds a <see cref="CorpusManifest"/> from a scratch directory a caller
/// has already extracted one historical git commit's tree into (see
/// <see cref="SeasonalCorpusExtractor"/>) — a generic, non-curated resolution, unlike
/// <see cref="CorpusManifestBuilder"/>'s hand-verified <c>KnownResolutions</c> table. Hand-curating every
/// season's own audio pairing the same way the live corpus was curated is not practical at this scale (one
/// audit per season, repeated every time a new season commit is added), so this instead applies one real,
/// disclosed heuristic per <c>.xsq</c> file: prefer a same-directory audio file sharing its filename stem,
/// otherwise fall back to the sole audio file in that directory when exactly one exists. Anything more
/// ambiguous than that is excluded with a real reason rather than guessed — the same "never silently
/// fabricate a resolution" discipline <see cref="CorpusManifestBuilder"/> already follows.
/// </summary>
public static class HistoricalCorpusManifestBuilder
{
    private static readonly string[] AudioExtensions = [".mp3", ".m4a", ".wav", ".wma"];

    /// <param name="scratchDirectory">The extracted historical tree's root (see <see cref="SeasonalCorpusExtractor.ExtractAsync"/>).</param>
    /// <param name="sourceCommit">Recorded on every produced <see cref="CorpusEntry.SourceCommit"/> (build-plan Phase 17 §2c).</param>
    /// <param name="knownProvenanceByTitle">
    /// Optional cross-reference (keyed by <see cref="CorpusEntry.SongTitle"/>, ordinal-ignore-case) --
    /// typically every live-tree <see cref="CorpusManifestBuilder.KnownResolutions"/> entry's own
    /// <see cref="CorpusEntry.Provenance"/>, so a historical copy of an already-known vendor/community
    /// download is tagged the same way instead of defaulting to <see cref="CorpusProvenanceTag.UserAuthored"/>.
    /// A title with no match defaults to <see cref="CorpusProvenanceTag.UserAuthored"/>, matching this
    /// corpus's own established convention (see <see cref="CorpusManifestBuilder.KnownResolutions"/>'s
    /// doc comment) that an unmatched title with no contradicting authorship evidence is treated as the
    /// user's own sequence rather than silently assumed vendor content.
    /// </param>
    public static CorpusManifest Build(
        string scratchDirectory, string sourceCommit, IReadOnlyDictionary<string, CorpusProvenanceTag>? knownProvenanceByTitle = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scratchDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCommit);
        if (!Directory.Exists(scratchDirectory))
        {
            throw new DirectoryNotFoundException($"Historical extraction scratch directory not found: '{scratchDirectory}'.");
        }

        var provenanceByTitle = knownProvenanceByTitle ?? new Dictionary<string, CorpusProvenanceTag>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<CorpusEntry>();
        var excluded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var xsqPath in Directory.EnumerateFiles(scratchDirectory, "*.xsq", SearchOption.AllDirectories))
        {
            var normalized = xsqPath.Replace('/', '\\');
            if (normalized.Contains(@"\RenderCache\", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains(@"\Backup\", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains(@"\exports\", StringComparison.OrdinalIgnoreCase) ||
                xsqPath.Contains(".xsq.bak", StringComparison.OrdinalIgnoreCase))
            {
                excluded[xsqPath] = "under RenderCache/Backup/exports, or a backup file -- not a distinct historical sequence";
                continue;
            }

            var directory = Path.GetDirectoryName(xsqPath)!;
            var stem = Path.GetFileNameWithoutExtension(xsqPath);
            var audioCandidates = AudioExtensions
                .Select(ext => Path.Combine(directory, stem + ext))
                .Where(File.Exists)
                .ToList();

            string? audioPath = audioCandidates.Count > 0 ? audioCandidates[0] : null;

            if (audioPath is null)
            {
                var allAudioInDirectory = Directory.EnumerateFiles(directory)
                    .Where(f => AudioExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                    .ToList();
                if (allAudioInDirectory.Count == 1)
                {
                    audioPath = allAudioInDirectory[0];
                }
            }

            if (audioPath is null)
            {
                excluded[xsqPath] = "no resolvable paired audio in this historical extraction (no same-stem file, and not exactly one audio file in its directory)";
                continue;
            }

            var songTitle = stem;
            var provenance = provenanceByTitle.TryGetValue(songTitle, out var known)
                ? known
                : CorpusProvenanceTag.UserAuthored;

            entries.Add(new CorpusEntry(
                SequencePath: xsqPath,
                AudioPath: audioPath,
                SongTitle: songTitle,
                Provenance: provenance,
                DuplicatePaths: [],
                Notes: $"Historical extraction from commit {sourceCommit}; audio paired by same-directory heuristic (see HistoricalCorpusManifestBuilder).",
                SourceCommit: sourceCommit));
        }

        return new CorpusManifest(
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            RootDirectory: Path.GetFullPath(scratchDirectory),
            Entries: entries,
            ExcludedPaths: excluded);
    }
}
