namespace XlightsAI.Studio.Release;

/// <summary>
/// Real <see cref="IReleaseDocumentationAuditor"/>. Reads each requirement's real file directly off
/// disk at audit time — never a cached/prior result — so a file edited or deleted between two audits is
/// always reflected honestly, the same "never a disposable cache" rule Section 4 applies to accepted
/// edits.
/// </summary>
public sealed class ReleaseDocumentationAuditor : IReleaseDocumentationAuditor
{
    public IReadOnlyList<string> Audit(IReadOnlyList<ReleaseDocumentationRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        var issues = new List<string>();

        foreach (var requirement in requirements)
        {
            if (!File.Exists(requirement.Path))
            {
                issues.Add($"{requirement.Name}: required document is missing at '{requirement.Path}'.");
                continue;
            }

            var content = File.ReadAllText(requirement.Path);
            if (string.IsNullOrWhiteSpace(content))
            {
                issues.Add($"{requirement.Name}: document at '{requirement.Path}' exists but is empty.");
                continue;
            }

            if (requirement.RequiredSubstring is not null &&
                !content.Contains(requirement.RequiredSubstring, StringComparison.Ordinal))
            {
                issues.Add(
                    $"{requirement.Name}: document at '{requirement.Path}' does not mention required " +
                    $"text '{requirement.RequiredSubstring}'.");
            }
        }

        return issues;
    }
}
