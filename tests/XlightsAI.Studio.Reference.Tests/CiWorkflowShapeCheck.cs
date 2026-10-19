namespace XlightsAI.Studio.Reference.Tests;

/// <summary>
/// Test-only validator for Step 008's committed CI workflow shape. Deliberately a plain substring
/// check, not a full YAML parser: the step requires specific commands/markers to exist in the
/// workflow text, not a generic YAML-schema engine this phase does not otherwise need.
/// </summary>
internal static class CiWorkflowShapeCheck
{
    public static readonly IReadOnlyList<string> RequiredMarkers = new[]
    {
        "runs-on: windows-latest",
        "dotnet restore",
        "dotnet format",
        "--verify-no-changes",
        "dotnet build",
        "dotnet test",
        "Category=Unit",
        "Category=Integration",
        "Category=Golden",
        "actions/upload-artifact",
        "if: always()",
    };

    public static IReadOnlyList<string> Validate(string? workflowText)
    {
        if (string.IsNullOrWhiteSpace(workflowText))
        {
            return new[] { "workflow file is empty" };
        }

        var errors = new List<string>();
        foreach (var marker in RequiredMarkers)
        {
            if (!workflowText.Contains(marker, StringComparison.Ordinal))
            {
                errors.Add($"missing required marker: {marker}");
            }
        }

        return errors;
    }
}
