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
        string Notes);

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
            SequenceRelativePath: @"Mashup-LoraJoslyn\2022\Mashup.xsq",
            AudioRelativePath: @"Mashup-LoraJoslyn\2022\Music\LalalaGirl - Christmas Mashup 2016-short.mp3",
            Provenance: CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            DuplicateRelativePaths: ["LalalaGirl - Christmas Mashup 2016-short.xsq"],
            Notes: "orig_zip/Mashup-LoraJoslyn.zip."),
        new(
            SequenceRelativePath: @"Nutcracker Dubstep\2020 Christmas Sequences\2020 Nutcracker Dubstep\Nutcracker Dubstep.xsq",
            AudioRelativePath: @"Nutcracker Dubstep\2020 Christmas Sequences\2020 Nutcracker Dubstep\Nutcracker Dubstep.mp3",
            Provenance: CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            DuplicateRelativePaths: ["Nutcracker Dubstep.xsq"],
            Notes: "orig_zip/Nutcracker Dubstep.zip."),
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
            SequenceRelativePath: @"49 The Weeknd AG - Save Your Tears\The Weeknd AG - Save Your Tears HD.xsq",
            AudioRelativePath: "The Weeknd & Ariana Grande - Save Your Tears Remix HD.mp3",
            Provenance: CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            DuplicateRelativePaths: ["The Weeknd & Ariana Grande - Save Your Tears Remix HD.xsq"],
            Notes: "orig_zip/The Weeknd AG - Save Your Tears HD.zip. No 'Music' folder exists next to this " +
                   "vendor copy (only Images/avi/xml); resolved to the root-level same-song audio instead."),
        new(
            SequenceRelativePath: @"55 The Weeknd - Blinding Lights\The Weeknd - Blinding Lights3.xsq",
            AudioRelativePath: "The Weeknd - Blinding Lights Lite.mp3",
            Provenance: CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            DuplicateRelativePaths: ["The Weeknd - Blinding Lights Lite.xsq"],
            Notes: "orig_zip/The Weeknd - Blinding Lights HD3.zip. No 'Music' folder exists next to this " +
                   "vendor copy; resolved to the root-level same-song audio instead."),
        new(
            SequenceRelativePath: @"Christmas Eve in Sarajevo--TSO--Carolina Mountain Lights\XLights\Christmas Eve Sarajevo.xsq",
            AudioRelativePath: @"Christmas Eve in Sarajevo--TSO--Carolina Mountain Lights\XLights\MP3 Christmas Music\Christmas Sarajevo.mp3",
            Provenance: CorpusProvenanceTag.LikelyVendorOrCommunityDownload,
            DuplicateRelativePaths: ["TSO - Christmas Sarajevo.xsq"],
            Notes: "orig_zip/Christmas Eve in Sarajevo--TSO--Carolina Mountain Lights.zip."),

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
    ];

    /// <summary>
    /// Builds the manifest by verifying every <see cref="KnownResolutions"/> entry against
    /// <paramref name="rootDirectory"/>'s real filesystem, and classifying every other real
    /// <c>.xsq</c> found under it (backup/cache/this-app's-own-output exclusions, or "not part of the
    /// known resolution set" for anything genuinely new since this audit was last done).
    /// </summary>
    public static CorpusManifest Build(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        if (!Directory.Exists(rootDirectory))
        {
            throw new DirectoryNotFoundException($"Corpus root directory not found: '{rootDirectory}'.");
        }

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
                Notes: resolution.Notes));
        }

        foreach (var xsqPath in Directory.EnumerateFiles(rootDirectory, "*.xsq", SearchOption.AllDirectories))
        {
            if (resolvedSequencePaths.Contains(Path.GetFullPath(xsqPath)))
            {
                continue;
            }

            excluded[xsqPath] = ClassifyUnresolvedPath(xsqPath);
        }

        return new CorpusManifest(
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            RootDirectory: Path.GetFullPath(rootDirectory),
            Entries: entries,
            ExcludedPaths: excluded);
    }

    private static string ClassifyUnresolvedPath(string xsqPath)
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

        if (normalized.Contains(@"Bad Feelings\exports", StringComparison.OrdinalIgnoreCase))
        {
            return "under Bad Feelings/exports -- this app's own prior output, not a corpus sequence";
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
