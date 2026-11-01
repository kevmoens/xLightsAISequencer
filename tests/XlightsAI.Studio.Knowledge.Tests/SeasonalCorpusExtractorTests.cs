using System.Diagnostics;

namespace XlightsAI.Studio.Knowledge.Tests;

/// <summary>
/// Hermetic (own throwaway repo, not the real C:\xLights history) tests for
/// <see cref="SeasonalCorpusExtractor"/>, skipping cleanly if <c>git</c> or <c>tar</c> is not on PATH --
/// same convention as <c>RealXlightsInstallTests</c> for environment-dependent tooling.
/// </summary>
public sealed class SeasonalCorpusExtractorTests
{
    [Fact]
    public async Task ExtractAsync_RecoversAFileThatWasLaterDeletedFromTheWorkingTree()
    {
        if (!IsToolAvailable("git") || !IsToolAvailable("tar"))
        {
            return; // skip cleanly -- git/tar not available in this environment
        }

        var repoDirectory = Path.Combine(Path.GetTempPath(), $"seasonal-extract-repo-{Guid.NewGuid():N}");
        var scratchRoot = Path.Combine(Path.GetTempPath(), $"seasonal-extract-scratch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoDirectory);
        try
        {
            await RunAsync(repoDirectory, "git", "init -q -b main");
            await RunAsync(repoDirectory, "git", "config user.email test@example.com");
            await RunAsync(repoDirectory, "git", "config user.name Test");

            File.WriteAllText(Path.Combine(repoDirectory, "old-show.xsq"), "<xsequence>old season</xsequence>");
            await RunAsync(repoDirectory, "git", "add .");
            await RunAsync(repoDirectory, "git", "commit -q -m \"old season\"");
            var oldSeasonCommit = (await RunAsync(repoDirectory, "git", "rev-parse HEAD")).Trim();

            File.Delete(Path.Combine(repoDirectory, "old-show.xsq"));
            File.WriteAllText(Path.Combine(repoDirectory, "new-show.xsq"), "<xsequence>new season</xsequence>");
            await RunAsync(repoDirectory, "git", "add -A");
            await RunAsync(repoDirectory, "git", "commit -q -m \"new season, remove old show\"");

            Assert.False(File.Exists(Path.Combine(repoDirectory, "old-show.xsq")));

            var extractedDirectory = await SeasonalCorpusExtractor.ExtractAsync(repoDirectory, oldSeasonCommit, scratchRoot, CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(extractedDirectory, "old-show.xsq")));
            Assert.False(File.Exists(Path.Combine(extractedDirectory, "new-show.xsq")));
        }
        finally
        {
            DeleteWithRetry(repoDirectory);
            DeleteWithRetry(scratchRoot);
        }
    }

    /// <summary>
    /// git marks its own loose-object files read-only on disk; <see cref="Directory.Delete(string,bool)"/>
    /// never clears that attribute first and throws <see cref="UnauthorizedAccessException"/> on a
    /// read-only file, so a real git-initialized temp repo must have every file's read-only bit cleared
    /// before deletion -- not a transient lock, so no retry/delay is actually needed once this is done.
    /// </summary>
    private static void DeleteWithRetry(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(directory, recursive: true);
    }

    private static bool IsToolAvailable(string fileName)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            process?.WaitForExit(5000);
            return process is not null && process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> RunAsync(string workingDirectory, string fileName, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"'{fileName} {arguments}' failed (exit {process.ExitCode}): {stderr}");
        }

        return stdout;
    }
}
