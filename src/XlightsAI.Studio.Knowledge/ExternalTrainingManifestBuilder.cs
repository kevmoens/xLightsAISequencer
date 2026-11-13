namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// Build-plan Phase 25 Part C: discovers an arbitrary external folder of <c>.xsq</c> sequences for
/// *training-only* statistics mining — deliberately separate from <see cref="CorpusManifestBuilder"/>'s
/// hand-audited <c>KnownResolutions</c> allowlist, which this type never reads or modifies. A sequence
/// found here does not need to appear in that allowlist at all; every discovered sequence is tagged
/// <see cref="CorpusProvenanceTag.ExternalReference"/> (the same enum value <see cref="CorpusManifestBuilder"/>
/// already uses for its own single external reference show), so every existing downstream consumer that
/// already filters <see cref="CorpusProvenanceTag.ExternalReference"/> out of the exemplar-eligible
/// pipelines (<c>ExemplarIndexBuilder</c>, <c>MultiLayerStackMiner</c>, <c>PatternLibraryBuilder</c>,
/// <c>CraftRuleInducer</c>) keeps excluding these entries by construction — this discovery path only
/// ever feeds <c>CorpusTrainingRunner</c>'s aggregate-statistics report, never the exemplar-eligible
/// corpus.
/// </summary>
public static class ExternalTrainingManifestBuilder
{
    private static readonly string[] AudioExtensions = [".mp3", ".m4a", ".wav", ".ogg", ".flac"];

    /// <summary>
    /// Enumerates every <c>.xsq</c> under <paramref name="rootDirectory"/>, pairing each with an audio
    /// file in the same directory (preferring a same-stem match; otherwise the sole audio candidate
    /// present — an ambiguous multi-candidate directory is excluded rather than guessed). Render-cache
    /// and backup directories are skipped using the same hygiene rules as <see cref="CorpusManifestBuilder"/>,
    /// but there is no "not part of the known-resolution set" exclusion here — that's the entire point
    /// of this separate training-only path.
    /// </summary>
    public static CorpusManifest Build(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        if (!Directory.Exists(rootDirectory))
        {
            throw new DirectoryNotFoundException($"External training root directory not found: '{rootDirectory}'.");
        }

        var entries = new List<CorpusEntry>();
        var excluded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var xsqPath in Directory.EnumerateFiles(rootDirectory, "*.xsq", SearchOption.AllDirectories))
        {
            var normalized = xsqPath.Replace('/', '\\');
            if (normalized.Contains(@"\RenderCache\", StringComparison.OrdinalIgnoreCase))
            {
                excluded[xsqPath] = "under RenderCache/ -- render-iteration artifact, not a trainable sequence";
                continue;
            }

            if (normalized.Contains(@"\Backup\", StringComparison.OrdinalIgnoreCase))
            {
                excluded[xsqPath] = "under Backup/ -- xLights auto-backup copy, not a distinct trainable sequence";
                continue;
            }

            var audioPath = FindPairedAudio(xsqPath);
            if (audioPath is null)
            {
                excluded[xsqPath] = "no unambiguous paired audio file found in the same directory -- cannot be analyzed";
                continue;
            }

            entries.Add(new CorpusEntry(
                SequencePath: xsqPath,
                AudioPath: audioPath,
                SongTitle: Path.GetFileNameWithoutExtension(xsqPath),
                Provenance: CorpusProvenanceTag.ExternalReference,
                DuplicatePaths: [],
                Notes: "Training-only ingestion (build-plan Phase 25 Part C) -- not audited against CorpusManifestBuilder's exemplar-eligible KnownResolutions allowlist."));
        }

        return new CorpusManifest(DateTimeOffset.UtcNow, Path.GetFullPath(rootDirectory), entries, excluded);
    }

    private static string? FindPairedAudio(string xsqPath)
    {
        var directory = Path.GetDirectoryName(xsqPath);
        if (directory is null)
        {
            return null;
        }

        var stem = Path.GetFileNameWithoutExtension(xsqPath);
        var candidates = Directory.EnumerateFiles(directory)
            .Where(f => AudioExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var stemMatch = candidates.FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), stem, StringComparison.OrdinalIgnoreCase));
        if (stemMatch is not null)
        {
            return stemMatch;
        }

        // Exactly one real candidate even without a stem match is still unambiguous; two or more with
        // no stem winner is a real ambiguity, not guessed at.
        return candidates.Count == 1 ? candidates[0] : null;
    }
}
