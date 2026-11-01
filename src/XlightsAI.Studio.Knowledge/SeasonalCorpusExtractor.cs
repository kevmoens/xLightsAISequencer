using System.Diagnostics;

namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// Build-plan Phase 17 §2b: exports one historical git commit's full tree into a scratch directory via
/// <c>git archive &lt;commit&gt; | tar -x</c> (no working-tree checkout, no mutation of the real
/// repository -- a read-only export of that commit's blobs), so <see cref="HistoricalCorpusManifestBuilder"/>
/// can then scan it exactly like any other directory. A <c>git worktree add</c> was considered instead
/// (named in the build plan as an alternative) but rejected: a worktree leaves a live, checked-out copy
/// registered against the repository until explicitly removed, which is extra state to clean up and a
/// real risk of accidentally editing a "historical" copy in place; <c>git archive</c> produces an
/// inert, ordinary directory with no git awareness of its own.
/// </summary>
public static class SeasonalCorpusExtractor
{
    /// <summary>
    /// Extracts <paramref name="commit"/>'s full tree from <paramref name="gitRepositoryDirectory"/> into
    /// <c>Path.Combine(scratchRootDirectory, commit)</c>, creating that directory if needed. Idempotent:
    /// if the target directory already exists and is non-empty, extraction is skipped and the existing
    /// directory is returned as-is (re-running a seasonal mining pass should not redundantly re-extract
    /// every commit every time).
    /// </summary>
    /// <returns>The scratch directory the commit's tree was extracted into.</returns>
    public static async Task<string> ExtractAsync(
        string gitRepositoryDirectory, string commit, string scratchRootDirectory, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gitRepositoryDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(commit);
        ArgumentException.ThrowIfNullOrWhiteSpace(scratchRootDirectory);

        var targetDirectory = Path.Combine(scratchRootDirectory, commit);
        if (Directory.Exists(targetDirectory) && Directory.EnumerateFileSystemEntries(targetDirectory).Any())
        {
            return targetDirectory;
        }

        Directory.CreateDirectory(targetDirectory);

        // git archive writes a tar stream to stdout; tar -x -C <dir> reads it from stdin and extracts.
        // Piped via ProcessStartInfo.RedirectStandardOutput/Input rather than shell "|" so this works
        // identically whether invoked from PowerShell or a plain Process launch, and so a non-zero exit
        // from either half is independently observable.
        using var archiveProcess = StartProcess(gitRepositoryDirectory, "git", $"archive \"{commit}\"", redirectStdOut: true);
        using var extractProcess = StartProcess(targetDirectory, "tar", "-x", redirectStdIn: true);

        var pumpTask = PumpAsync(archiveProcess.StandardOutput.BaseStream, extractProcess.StandardInput.BaseStream, ct);

        await Task.WhenAll(
            archiveProcess.WaitForExitAsync(ct),
            pumpTask.ContinueWith(_ => extractProcess.StandardInput.Close(), ct, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default),
            extractProcess.WaitForExitAsync(ct)).ConfigureAwait(false);

        if (archiveProcess.ExitCode != 0)
        {
            var stderr = await archiveProcess.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"'git archive {commit}' failed (exit {archiveProcess.ExitCode}): {stderr}");
        }

        if (extractProcess.ExitCode != 0)
        {
            var stderr = await extractProcess.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"'tar -x' for commit {commit} failed (exit {extractProcess.ExitCode}): {stderr}");
        }

        return targetDirectory;
    }

    private static async Task PumpAsync(Stream source, Stream destination, CancellationToken ct)
    {
        try
        {
            await source.CopyToAsync(destination, ct).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // tar can close its stdin before git archive finishes writing (e.g. tar exits early on a
            // malformed stream); treat that as the extract process's own failure, surfaced via its exit
            // code above, not an unhandled pipe-break exception here.
        }
    }

    private static Process StartProcess(
        string workingDirectory, string fileName, string arguments, bool redirectStdOut = false, bool redirectStdIn = false)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = redirectStdOut,
                RedirectStandardInput = redirectStdIn,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        process.Start();
        return process;
    }
}
