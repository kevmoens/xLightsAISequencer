namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// PHASE-2b (build plan §2b.6): enumerates a real <c>C:\xLights</c>-shaped corpus root into a
/// <see cref="CorpusManifest"/>. Audio pairing for the 9 sequences whose own <c>.xsq</c> directory had
/// no obviously-named paired audio file (every vendor subfolder extraction) was resolved by hand against
/// the real filesystem — resolving by song identity to a root-level audio file reused across copies, or
/// to a confirmed same-directory file the initial shallow listing missed — and is recorded here as data
/// (<see cref="KnownResolutions"/>) rather than re-derived by filename-matching heuristics at build time,
/// since that correlation (including which vendor zip each root-level sequence actually came from) is
/// genuinely external knowledge no amount of local re-scanning recovers on its own. What this builder
/// *does* do at build time, every run, is verify every one of those resolutions still exists on disk —
/// a moved or deleted file surfaces as an <see cref="CorpusManifest.ExcludedPaths"/> entry, never a
/// silently stale manifest.
/// </summary>
public static class CorpusManifestBuilder
{
    private const string DubstepReferenceShowRelativePath = "Dubstep Christmas Light Show.xsq";

    /// <summary>
    /// One known-good sequence resolution. <see cref="SequenceRelativePath"/> is the canonical copy
    /// (preferred: a vendor-named subfolder extraction, closer to the original download, per
    /// architecture §8b; a plain root-level file otherwise). <see cref="AudioRelativePath"/> is resolved
    /// against the real filesystem (same directory when present; a root-level same-song file reused by
    /// song identity otherwise — see <see cref="Notes"/> for which). <see cref="DuplicateRelativePaths"/>
    /// are other on-disk copies of the exact same song/download found during enumeration.
    /// </summary>
    private sealed record Resolution(
        string SequenceRelativePath,
        string AudioRelativePath,
        CorpusProvenanceTag Provenance,
        string[] DuplicateRelativePaths,
        string Notes,
        string? SourceCommit = null,
        bool IsFavorite = false);

    /// <summary>
    /// The real, hand-verified corpus resolution (see class doc comment). 20 distinct sequences total:
    /// 14 matched by filename correlation against a preserved <c>orig_zip/</c> entry (or an equivalent
    /// vendor-named project subfolder) → <see cref="CorpusProvenanceTag.LikelyVendorOrCommunityDownload"/>;
    /// 1 explicit external reference show; 5 with no zip/vendor match and nothing in their <c>.xsq</c>
    /// &lt;head&gt; author field to contradict user authorship →
    /// <see cref="CorpusProvenanceTag.UserAuthored"/>. This is materially fewer than the architecture
    /// doc's claimed "31 distinct sequences" figure — see <see cref="CorpusManifestBuilder"/>'s own
    /// remarks in the Phase 2b final report for the reconciliation: two <c>orig_zip/</c> entries
    /// (Chipmunks, Taylor Swift 2022) were never extracted/sequenced into any live <c>.xsq</c> on this
    /// machine, and every "(also in X/)" vendor subfolder noted below collapses to the *same* song as
    /// its root-level counterpart rather than adding a new one.
    /// </summary>
    private static readonly Resolution[] KnownResolutions =
    [
        new(
            SequenceRelativePath: @"All I Want for Christmas--Mariah Carey--Michael Bourassa\All i want for christmas 2.xsq",
            AudioRelativePath: "All I Want For Christmas Is You shortened.mp3",
            Provenance: CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            DuplicateRelativePaths: ["All I Want For Christmas Is You shortened.xsq"],
            Notes: "orig_zip/All I Want for Christmas--Mariah Carey--Michael Bourassa.zip. Own directory's only " +
                   "audio candidate is under a folder literally named '_lost' (a broken-media-reference backup), " +
                   "so the confirmed root-level audio for the same song is used instead."),
        new(
            SequenceRelativePath: "Carrie Underwood - Away In A Manger Lite.xsq",
            AudioRelativePath: "Carrie Underwood - Away In A Manger Lite.mp3",
            Provenance: CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            DuplicateRelativePaths: [],
            Notes: "orig_zip/Carrie Underwood - Away In A Manger HD.zip."),
        new(
            SequenceRelativePath: "Chuck Berry - Run Rudolph  Run.xsq",
            AudioRelativePath: "Chuck Berry - Run Rudolph  Run.m4a",
            Provenance: CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            DuplicateRelativePaths: [],
            Notes: "orig_zip/Chuck Berry - Run Rudolph Run SD Layout.zip."),
        new(
            SequenceRelativePath: @"Dance of the Sugar Plum Fairy\Dance Of The Sugar Plum Fairy (JayKode Trap Remix) HD Layout.xsq",
            AudioRelativePath: "Dance Of The Sugar Plum Fairy (JayKode Trap Remix).mp3",
            Provenance: CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            DuplicateRelativePaths: ["Dance Of The Sugar Plum Fairy (JayKode Trap Remix).xsq"],
            Notes: "orig_zip/Dance Of The Sugar Plum Fairy (JayKode Trap Remix) HD Layout.zip. The vendor " +
                   "subfolder has no audio file of its own; the root-level same-song file is used."),
        new(
            SequenceRelativePath: "homealone.xsq",
            AudioRelativePath: "homealone.mp3",
            Provenance: CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            DuplicateRelativePaths: [],
            Notes: "orig_zip/Home Alone SD.zip."),
        new(
            SequenceRelativePath: "Joe Bygraves - Stand As One Lite.xsq",
            AudioRelativePath: "Joe Bygraves - Stand As One Lite.mp3",
            Provenance: CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            DuplicateRelativePaths: [],
            Notes: "orig_zip/Joe Bygraves - Stand As One HD.zip."),
        new(
            SequenceRelativePath: "LalalaGirl - Christmas Mashup 2016-short.xsq",
            AudioRelativePath: "LalalaGirl - Christmas Mashup 2016-short.mp3",
            Provenance: CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            DuplicateRelativePaths: [@"Mashup-LoraJoslyn\2022\Mashup.xsq"],
            Notes: "orig_zip/Mashup-LoraJoslyn.zip. Build-plan Phase 16 §1c/Phase 17 §1c (real 2026-10-31 " +
                   "corpus-mining run): the vendor subfolder's own 'Mashup.xsq' (previously canonical here) " +
                   "fails real XsqStructuralReader parsing with a zero-width 'Beats' timing-track mark -- " +
                   "swapped to this root-level duplicate, confirmed to parse cleanly (115 effects), as the " +
                   "canonical copy instead; the broken vendor copy is now just a recorded duplicate."),
        new(
            SequenceRelativePath: "Nutcracker Dubstep.xsq",
            AudioRelativePath: "Nutcracker Dubstep.mp3",
            Provenance: CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            DuplicateRelativePaths: [@"Nutcracker Dubstep\2020 Christmas Sequences\2020 Nutcracker Dubstep\Nutcracker Dubstep.xsq"],
            Notes: "orig_zip/Nutcracker Dubstep.zip. Build-plan Phase 16 §1c/Phase 17 §1c: the vendor " +
                   "subfolder's own 'Nutcracker Dubstep.xsq' (previously canonical here) fails real " +
                   "XsqStructuralReader parsing with a zero-width 'Beat Count' timing-track mark -- swapped " +
                   "to this root-level duplicate, confirmed to parse cleanly (6103 effects), as the canonical " +
                   "copy instead; the broken vendor copy is now just a recorded duplicate."),
        new(
            SequenceRelativePath: "Ode to Joy remix - Epic.xsq",
            AudioRelativePath: "Ode to Joy remix - Epic.mp3",
            Provenance: CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            DuplicateRelativePaths: [],
            Notes: "orig_zip/Ode To Joy.zip."),
        new(
            SequenceRelativePath: @"Rockin' Around the Christmas Tree\Christmas 2023\Sequences\Rockin' Around the Christmas Tree.xsq",
            AudioRelativePath: @"Rockin' Around the Christmas Tree\Christmas 2023\Audio\01 Rockin' Around the Christmas Tree.mp3",
            Provenance: CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            DuplicateRelativePaths: ["Rockin Around the Christmas Tree.xsq"],
            Notes: "orig_zip/Rockin' Around the Christmas Tree.zip."),
        new(
            SequenceRelativePath: @"Sleigh Ride\Christmas 2023\Sequences\Sleigh Ride.xsq",
            AudioRelativePath: @"Sleigh Ride\Christmas 2023\Audio\05 Sleigh Ride Edited.mp3",
            Provenance: CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            DuplicateRelativePaths: ["Sleigh Ride.xsq"],
            Notes: "orig_zip/Sleigh Ride.zip."),
        new(
            SequenceRelativePath: "The Weeknd & Ariana Grande - Save Your Tears Remix HD.xsq",
            AudioRelativePath: "The Weeknd & Ariana Grande - Save Your Tears Remix HD.mp3",
            Provenance: CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            DuplicateRelativePaths: [@"49 The Weeknd AG - Save Your Tears\The Weeknd AG - Save Your Tears HD.xsq"],
            Notes: "orig_zip/The Weeknd AG - Save Your Tears HD.zip. Build-plan Phase 16 §1c/Phase 17 §1c: " +
                   "the vendor subfolder's own 'The Weeknd AG - Save Your Tears HD.xsq' (previously canonical " +
                   "here) fails real XsqStructuralReader parsing with a zero-width 'Abel2' timing-track mark -- " +
                   "swapped to this root-level duplicate, confirmed to parse cleanly (712 effects), as the " +
                   "canonical copy instead; the broken vendor copy is now just a recorded duplicate."),
        new(
            SequenceRelativePath: "The Weeknd - Blinding Lights Lite.xsq",
            AudioRelativePath: "The Weeknd - Blinding Lights Lite.mp3",
            Provenance: CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            DuplicateRelativePaths: [@"55 The Weeknd - Blinding Lights\The Weeknd - Blinding Lights3.xsq"],
            Notes: "orig_zip/The Weeknd - Blinding Lights HD3.zip. Build-plan Phase 16 §1c/Phase 17 §1c: " +
                   "the vendor subfolder's own 'The Weeknd - Blinding Lights3.xsq' (previously canonical " +
                   "here) fails real XsqStructuralReader parsing with a zero-width 'Beats' timing-track mark -- " +
                   "swapped to this root-level duplicate, confirmed to parse cleanly (1727 effects), as the " +
                   "canonical copy instead; the broken vendor copy is now just a recorded duplicate."),
        new(
            SequenceRelativePath: "TSO - Christmas Sarajevo.xsq",
            AudioRelativePath: "TSO - Christmas Sarajevo.mp3",
            Provenance: CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            DuplicateRelativePaths: [@"Christmas Eve in Sarajevo--TSO--Carolina Mountain Lights\XLights\Christmas Eve Sarajevo.xsq"],
            Notes: "orig_zip/Christmas Eve in Sarajevo--TSO--Carolina Mountain Lights.zip. Build-plan Phase " +
                   "16 §1c/Phase 17 §1c: the vendor subfolder's own 'Christmas Eve Sarajevo.xsq' (previously " +
                   "canonical here) fails real XsqStructuralReader parsing with a zero-width 'Sarajevo' " +
                   "timing-track mark -- swapped to this root-level duplicate, confirmed to parse cleanly " +
                   "(716 effects), as the canonical copy instead; the broken vendor copy is now just a " +
                   "recorded duplicate."),

        // Explicit external reference (architecture §8b / C:\xLights\CLAUDE.md) -- never a mined
        // corpus member, excluded from every aggregate statistic.
        new(
            SequenceRelativePath: DubstepReferenceShowRelativePath,
            AudioRelativePath: "Dubstep Christmas Light Show.mp3",
            Provenance: CorpusProvenanceTag.ExternalReference,
            DuplicateRelativePaths: [],
            Notes: "The Johnson Family reference show discussed throughout the architecture doc; excluded from aggregate statistics."),

        // User-authored: no matching orig_zip/ entry found, and every one of these .xsq <head><author>
        // fields was confirmed empty (no contradicting vendor name).
        new(
            SequenceRelativePath: "Cher-DJ Play a Christmas Song.xsq",
            AudioRelativePath: "Cher-DJ Play a Christmas Song.mp3",
            Provenance: CorpusProvenanceTag.UserAuthored,
            DuplicateRelativePaths: [@"Cher-DJ Play a Christmas Song\Xlights Generic Layout\DJ play a Christmas song\Cher-DJ Play a Christmas Song.xsq"],
            Notes: "No orig_zip/ match; <head><author> empty."),
        new(
            SequenceRelativePath: "Christmas Tree Hot to Go - Chappell Roan.xsq",
            AudioRelativePath: "Christmas Tree Hot to Go - Chappell Roan.mp3",
            Provenance: CorpusProvenanceTag.UserAuthored,
            DuplicateRelativePaths: [],
            Notes: "No orig_zip/ match; <head><author> empty."),
        new(
            SequenceRelativePath: "PinkPonyClub.xsq",
            AudioRelativePath: @"PinkPonyClub\PinkPonyClub.mp3",
            Provenance: CorpusProvenanceTag.UserAuthored,
            DuplicateRelativePaths: [],
            Notes: "No orig_zip/ match; <head><author> empty. <head><mediaFile> names this same relative " +
                   "subfolder path, just rooted at a stale absolute prefix -- the local resolution agrees with it."),
        new(
            SequenceRelativePath: "Ronettes - Sleigh Bells (PhatCap! Trap Remix).xsq",
            AudioRelativePath: "Ronettes - Sleigh Bells (PhatCap! Trap Remix).mp3",
            Provenance: CorpusProvenanceTag.UserAuthored,
            DuplicateRelativePaths: [],
            Notes: "No orig_zip/ match; <head><author> empty."),
        new(
            SequenceRelativePath: "WISCANTHEM.xsq",
            AudioRelativePath: @"Wiscanthem\WISCANTHEM.mp3",
            Provenance: CorpusProvenanceTag.UserAuthored,
            DuplicateRelativePaths: [],
            Notes: "No orig_zip/ match; <head><author> empty. <head><mediaFile> names this same relative " +
                   "subfolder path (case differs only in casing of the folder name), just rooted at a stale " +
                   "absolute prefix -- the local resolution agrees with it."),

        // Build-plan Phase 17 §1b/§2b/§2c: recovered from git history (no longer present in any earlier
        // working-tree commit's deletion sense -- these were deleted from the tree as later seasons
        // replaced them, then re-extracted here via `git cat-file blob` from their real source commit and
        // copied back into the live corpus root, 2026-10-31). User-selected from the full 87-file
        // recovered-sequences list via the picker artifact. Each Notes field records its real source
        // commit for traceability (§2c) -- no content-hash dedup pass has been run yet against the other
        // 19 KnownResolutions entries above (§2b's own open question), but none of these 12 titles
        // collide with any existing entry's SongTitle.
        new(
            SequenceRelativePath: "Existence (VIP) - Shortened.xsq",
            AudioRelativePath: "Existence (VIP) - Shortened.mp3",
            Provenance: CorpusProvenanceTag.UserAuthored,
            DuplicateRelativePaths: [],
            Notes: "Recovered from git history commit 749d18c (\"2023 show\"); deleted from the working tree by a later season. The user's named favorite -- see Phase 17 §2f (favorite-song CraftRuleInducer weighting).",
            SourceCommit: "749d18c",
            IsFavorite: true),
        new(
            SequenceRelativePath: "Dubstep 2019.xsq",
            AudioRelativePath: "Dubstep 2019.mp3",
            Provenance: CorpusProvenanceTag.UserAuthored,
            DuplicateRelativePaths: [],
            Notes: "Recovered from git history commit 372e9b9 (\"macair\", 2022); deleted from the working tree by a later season.",
            SourceCommit: "372e9b9"),
        new(
            SequenceRelativePath: "Eye of the Tiger DubStep.xsq",
            AudioRelativePath: "Eye of the Tiger DubStep.mp3",
            Provenance: CorpusProvenanceTag.UserAuthored,
            DuplicateRelativePaths: [],
            Notes: "Recovered from git history commit 749d18c (\"2023 show\"); deleted from the working tree by a later season.",
            SourceCommit: "749d18c"),
        new(
            SequenceRelativePath: "AC_DC - Thunderstruck (Official Video).xsq",
            AudioRelativePath: "AC_DC - Thunderstruck (Official Video).mp3",
            Provenance: CorpusProvenanceTag.UserAuthored,
            DuplicateRelativePaths: [],
            Notes: "Recovered from git history commit 749d18c (\"2023 show\"); deleted from the working tree by a later season.",
            SourceCommit: "749d18c"),
        new(
            SequenceRelativePath: "E1 - Eye of the Tiger.xsq",
            AudioRelativePath: "E1 - Eye of the Tiger.mp3",
            Provenance: CorpusProvenanceTag.UserAuthored,
            DuplicateRelativePaths: [],
            Notes: "Recovered from git history commit 372e9b9 (\"macair\", 2022), original nested path " +
                   "'Completed Sequences/DDL Audio Tracks - Openings and Closings/E1 - Eye of the Tiger.xsq' " +
                   "paired with 'Music/E1 - Eye of the Tiger DubStep.mp3' (different directory in that " +
                   "commit) -- both copied flat to the corpus root; deleted from the working tree by a later season.",
            SourceCommit: "372e9b9"),
        new(
            SequenceRelativePath: "Mr Grinch2018final.xsq",
            AudioRelativePath: "Mr Grinch2018final.mp3",
            Provenance: CorpusProvenanceTag.UserAuthored,
            DuplicateRelativePaths: [],
            Notes: "Recovered from git history commit 749d18c (\"2023 show\"); its own real audio pairing " +
                   "in that commit was \"Mr Grinch/02 You're a Mean One, Mr. Grinch (feat. Sabrina " +
                   "Carpenter).mp3\" -- a different real recording from the 'Mr Grinch - Lindsey Stirling " +
                   "Logan Carpenter.mp3' already mined elsewhere in the live corpus, not a duplicate of it. " +
                   "Deleted from the working tree by a later season.",
            SourceCommit: "749d18c"),
        new(
            SequenceRelativePath: "Thriller.xsq",
            AudioRelativePath: "Thriller.mp3",
            Provenance: CorpusProvenanceTag.UserAuthored,
            DuplicateRelativePaths: [],
            Notes: "Recovered from git history commit cfc240e (\"2024 halloween show\"); deleted from the working tree by a later season.",
            SourceCommit: "cfc240e"),

        // Five of the 12 user-selected historical titles (7 song medley updated, Into the Unknown,
        // First 7 songs Christmas, Memories - Maroon 5, Run Run Rudolph) are still NOT registered here.
        // They originally failed real XsqStructuralReader parsing with the same zero-width-timing-mark
        // bug class as the Phase 16 §1c findings above; XsqStructuralReader now tolerates a zero-width
        // mark by skipping it (see that type's own doc comment), so these may now parse cleanly -- they
        // have not been re-extracted/re-verified against the real git history blobs yet (this table only
        // records resolutions this audit has actually confirmed against real files, never a guess), so
        // they remain unregistered until that re-check happens.
    ];

    /// <summary>
    /// Builds the manifest by verifying every <see cref="KnownResolutions"/> entry against
    /// <paramref name="rootDirectory"/>'s real filesystem, and classifying every other real
    /// <c>.xsq</c> found under it (backup/cache/this-app's-own-output exclusions, or "not part of the
    /// known resolution set" for anything genuinely new since this audit was last done).
    /// </summary>
    /// <param name="rootDirectory">The corpus root to enumerate (a live working tree, or a scratch directory a caller has already extracted a historical git commit into -- see build-plan Phase 17 §2b; this method itself stays git-unaware).</param>
    /// <param name="excludedProjectRootDirectories">
    /// Build-plan Phase 17 §2a: absolute root directories of this app's own prior projects (e.g. every
    /// <c>ProjectSummary.RootDirectory</c> from <c>IRecentProjectsStore.Load()</c>), so a generated
    /// <c>.xsq</c> that was later copied or moved outside its project's own <c>exports/</c> folder is
    /// still excluded from corpus mining, not just the ones still sitting directly under <c>exports/</c>.
    /// This method deliberately takes the list as data rather than depending on
    /// <c>XlightsAI.Studio.Projects</c> itself, to keep this a pure, dependency-light directory scanner.
    /// Optional; omitted (or empty) for a corpus root with no overlap against this app's own projects.
    /// </param>
    public static CorpusManifest Build(string rootDirectory, IReadOnlyList<string>? excludedProjectRootDirectories = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        if (!Directory.Exists(rootDirectory))
        {
            throw new DirectoryNotFoundException($"Corpus root directory not found: '{rootDirectory}'.");
        }

        var excludedProjectRoots = (excludedProjectRootDirectories ?? [])
            .Select(Path.GetFullPath)
            .ToList();

        var entries = new List<CorpusEntry>();
        var excluded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var resolvedSequencePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var resolution in KnownResolutions)
        {
            var sequencePath = Path.Combine(rootDirectory, resolution.SequenceRelativePath);
            var audioPath = Path.Combine(rootDirectory, resolution.AudioRelativePath);
            resolvedSequencePaths.Add(Path.GetFullPath(sequencePath));
            foreach (var dup in resolution.DuplicateRelativePaths)
            {
                resolvedSequencePaths.Add(Path.GetFullPath(Path.Combine(rootDirectory, dup)));
            }

            if (!File.Exists(sequencePath))
            {
                excluded[sequencePath] = "known corpus entry, but the .xsq no longer exists at its recorded path";
                continue;
            }

            if (!File.Exists(audioPath))
            {
                excluded[sequencePath] = $"known corpus entry, but its resolved paired audio '{audioPath}' no longer exists";
                continue;
            }

            var duplicatePaths = resolution.DuplicateRelativePaths
                .Select(p => Path.Combine(rootDirectory, p))
                .Where(File.Exists)
                .ToList();

            entries.Add(new CorpusEntry(
                SequencePath: sequencePath,
                AudioPath: audioPath,
                SongTitle: DeriveSongTitle(sequencePath),
                Provenance: resolution.Provenance,
                DuplicatePaths: duplicatePaths,
                Notes: resolution.Notes,
                SourceCommit: resolution.SourceCommit,
                IsFavorite: resolution.IsFavorite));
        }

        foreach (var xsqPath in Directory.EnumerateFiles(rootDirectory, "*.xsq", SearchOption.AllDirectories))
        {
            if (resolvedSequencePaths.Contains(Path.GetFullPath(xsqPath)))
            {
                continue;
            }

            excluded[xsqPath] = ClassifyUnresolvedPath(xsqPath, excludedProjectRoots);
        }

        return new CorpusManifest(
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            RootDirectory: Path.GetFullPath(rootDirectory),
            Entries: entries,
            ExcludedPaths: excluded);
    }

    /// <summary>
    /// Build-plan Phase 17 §2a: excludes this app's own generated output via two independent rules --
    /// (1) any path with a segment literally named <c>exports</c> (<c>ChoreographyGenerationPipeline</c>'s
    /// own <c>exportsDir</c>/<c>XsqExporter</c> output convention, generic across every project this app
    /// creates, not just one hard-coded project name), and (2) any path lying under one of
    /// <paramref name="excludedProjectRoots"/> (this app's own recorded project directories), which also
    /// catches a generated file later copied or moved outside its original <c>exports/</c> folder. Either
    /// rule alone would miss cases the other catches.
    /// </summary>
    private static string ClassifyUnresolvedPath(string xsqPath, IReadOnlyList<string> excludedProjectRoots)
    {
        var normalized = xsqPath.Replace('/', '\\');
        if (normalized.Contains(@"\RenderCache\", StringComparison.OrdinalIgnoreCase))
        {
            return "under RenderCache/ -- render-iteration artifact, not a corpus sequence";
        }

        if (normalized.Contains(@"\Backup\", StringComparison.OrdinalIgnoreCase))
        {
            return "under Backup/ -- xLights auto-backup copy, not a distinct corpus sequence";
        }

        if (normalized.Contains(@"\exports\", StringComparison.OrdinalIgnoreCase))
        {
            return "under an exports/ folder -- this app's own generated output convention (ChoreographyGenerationPipeline), not a corpus sequence";
        }

        var fullXsqPath = Path.GetFullPath(xsqPath);
        foreach (var projectRoot in excludedProjectRoots)
        {
            if (fullXsqPath.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fullXsqPath, projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                return $"under this app's own recorded project directory '{projectRoot}' -- this app's own prior output, not a corpus sequence";
            }
        }

        if (xsqPath.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ||
            xsqPath.Contains(".xsq.bak", StringComparison.OrdinalIgnoreCase))
        {
            return "backup file (.bak)";
        }

        return "not part of the known-resolution set audited for this manifest (re-audit needed if this is a new real sequence)";
    }

    private static string DeriveSongTitle(string sequencePath) =>
        Path.GetFileNameWithoutExtension(sequencePath);
}
