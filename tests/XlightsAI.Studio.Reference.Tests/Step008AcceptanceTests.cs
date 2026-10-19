using System.Diagnostics;

namespace XlightsAI.Studio.Reference.Tests;

/// <summary>
/// PHASE-01 / Step 008 — "Add CI, unit/integration/golden test projects, formatting and artifact
/// publishing." Required acceptance: clean checkout builds, runs fixtures, and stores test reports.
/// Failure/edge case: invalid input, cancellation or stale persisted state must not corrupt the
/// project.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step008AcceptanceTests
{
    private static readonly (string[] RelativeSegments, string Category)[] TaggedAcceptanceFiles =
    {
        (["tests", "XlightsAI.Studio.Reference.Tests", "Step001AcceptanceTests.cs"], "Golden"),
        (["tests", "XlightsAI.Studio.Reference.Tests", "Step002AcceptanceTests.cs"], "Golden"),
        (["tests", "XlightsAI.Studio.Reference.Tests", "Step003AcceptanceTests.cs"], "Golden"),
        (["tests", "XlightsAI.Studio.Shell.Tests", "Step004AcceptanceTests.cs"], "Unit"),
        (["tests", "XlightsAI.Studio.Shell.Tests", "Step005AcceptanceTests.cs"], "Integration"),
        (["tests", "XlightsAI.Studio.Shell.Tests", "Step006ShellWiringTests.cs"], "Integration"),
        (["tests", "XlightsAI.Studio.Projects.Tests", "Step006AcceptanceTests.cs"], "Integration"),
        (["tests", "XlightsAI.Studio.Projects.Tests", "Step007AcceptanceTests.cs"], "Integration"),
    };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "XlightsAI.Studio.slnx")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException($"Could not locate repository root from '{AppContext.BaseDirectory}'.");
        }

        return dir.FullName;
    }

    [Fact]
    public async Task Step008_Acceptance_Passes()
    {
        var root = RepoRoot();

        // 1. The committed CI workflow is a real, well-shaped file, not a stub.
        var workflowPath = Path.Combine(root, ".github", "workflows", "ci.yml");
        Assert.True(File.Exists(workflowPath), $"CI workflow missing at {workflowPath}");
        var workflowText = await File.ReadAllTextAsync(workflowPath, CancellationToken.None);
        var workflowErrors = CiWorkflowShapeCheck.Validate(workflowText);
        Assert.True(workflowErrors.Count == 0, string.Join("; ", workflowErrors));

        // 2. A formatting-gate config exists ("formatting ... checks to pass").
        var editorConfigPath = Path.Combine(root, ".editorconfig");
        Assert.True(File.Exists(editorConfigPath));
        var editorConfigText = await File.ReadAllTextAsync(editorConfigPath, CancellationToken.None);
        Assert.Contains("root = true", editorConfigText);
        Assert.Contains("[*.cs]", editorConfigText);

        // 3. The formatting gate actually passes right now, for real -- not mocked.
        var formatResult = await RunDotnetAsync(root, "format XlightsAI.Studio.slnx --verify-no-changes", CancellationToken.None);
        Assert.True(formatResult.ExitCode == 0, $"dotnet format --verify-no-changes failed: {formatResult.Output}");

        // 4. Every pre-existing acceptance test class (Steps 001-007) carries the real Category trait
        // that the CI workflow's three filtered test invocations actually rely on -- proves
        // "unit/integration/golden test projects" is wired end-to-end, not just declared in prose.
        foreach (var (relativeSegments, category) in TaggedAcceptanceFiles)
        {
            var path = Path.Combine([root, .. relativeSegments]);
            Assert.True(File.Exists(path), $"expected test file missing: {path}");
            var text = await File.ReadAllTextAsync(path, CancellationToken.None);
            Assert.Contains($"[Trait(\"Category\", \"{category}\")]", text);
        }

        // 5. Test reports are actually stored, not merely claimed: run the real Golden suite and
        // confirm a real .trx report lands on disk at a path shaped like the one CI publishes.
        var resultsDir = Path.Combine(Path.GetTempPath(), "xlai-step008-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(resultsDir);
        try
        {
            var testResult = await RunDotnetAsync(
                root,
                $"test XlightsAI.Studio.slnx --filter \"Category=Golden\" --logger \"trx;LogFileName=golden.trx\" --results-directory \"{resultsDir}\"",
                CancellationToken.None);
            Assert.True(testResult.ExitCode == 0, $"dotnet test (Category=Golden) failed: {testResult.Output}");

            var trxFiles = Directory.GetFiles(resultsDir, "golden.trx", SearchOption.AllDirectories);
            Assert.True(trxFiles.Length > 0, "no .trx test report was written");
            Assert.True(new FileInfo(trxFiles[0]).Length > 0, "the .trx test report is empty");
        }
        finally
        {
            Directory.Delete(resultsDir, recursive: true);
        }
    }

    [Fact]
    public void CiWorkflow_WhenRequiredStepsAreMissing_FailsValidation_NegativePath()
    {
        const string tampered = """
            name: CI
            on: [push]
            jobs:
              build:
                runs-on: windows-latest
                steps:
                  - run: dotnet build
            """;

        var errors = CiWorkflowShapeCheck.Validate(tampered);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("dotnet format"));
        Assert.Contains(errors, e => e.Contains("actions/upload-artifact"));
        Assert.Contains(errors, e => e.Contains("Category=Unit"));
    }

    [Fact]
    public void CiWorkflow_WithEmptyText_FailsSafelyWithoutThrowing_NegativePath()
    {
        var errors = CiWorkflowShapeCheck.Validate(string.Empty);

        Assert.Single(errors);
        Assert.Contains("empty", errors[0]);
    }

    [Fact]
    public async Task ReadCiWorkflow_WithAlreadyCancelledToken_ThrowsWithoutReadingAnything_NegativePath()
    {
        var workflowPath = Path.Combine(RepoRoot(), ".github", "workflows", "ci.yml");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => File.ReadAllTextAsync(workflowPath, cts.Token));
    }

    private static async Task<(int ExitCode, string Output)> RunDotnetAsync(string workingDirectory, string arguments, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo("dotnet", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start 'dotnet' process.");

        var stdOutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stdErrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        var output = await stdOutTask + Environment.NewLine + await stdErrTask;
        return (process.ExitCode, output);
    }
}
