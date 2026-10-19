using System.Text.RegularExpressions;

namespace XlightsAI.Studio.Reference;

/// <summary>Outcome of validating an <see cref="UpstreamReferenceManifest"/>. Never throws for bad data.</summary>
public sealed record UpstreamReferenceValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static UpstreamReferenceValidationResult Success { get; } = new(true, []);

    public static UpstreamReferenceValidationResult Failure(IReadOnlyList<string> errors) => new(false, errors);
}

/// <summary>
/// Pure domain rules for what makes a pinned upstream reference manifest acceptable:
/// a resolvable repository URL, a full (not abbreviated, not a branch name) commit SHA,
/// a recorded license and attribution notice, and at least one audited component.
/// This step's "never rely on main at build time" rule is enforced here by rejecting
/// anything that isn't a full 40-character hex commit SHA.
/// </summary>
public static class UpstreamReferenceValidator
{
    private static readonly Regex FullCommitShaPattern = new("^[0-9a-f]{40}$", RegexOptions.Compiled);

    public static UpstreamReferenceValidationResult Validate(UpstreamReferenceManifest? manifest)
    {
        if (manifest is null)
        {
            return UpstreamReferenceValidationResult.Failure(["Manifest is null."]);
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(manifest.RepositoryUrl) ||
            !Uri.TryCreate(manifest.RepositoryUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            errors.Add($"RepositoryUrl must be an absolute http(s) URL. Got: '{manifest.RepositoryUrl}'.");
        }

        if (string.IsNullOrWhiteSpace(manifest.CommitSha) || !FullCommitShaPattern.IsMatch(manifest.CommitSha))
        {
            errors.Add($"CommitSha must be a full 40-character lowercase hex git commit SHA, not a branch name or abbreviated SHA. Got: '{manifest.CommitSha}'.");
        }

        if (string.IsNullOrWhiteSpace(manifest.License.SpdxId))
        {
            errors.Add("License.SpdxId must be recorded.");
        }

        if (string.IsNullOrWhiteSpace(manifest.License.CopyrightHolder))
        {
            errors.Add("License.CopyrightHolder must be recorded.");
        }

        if (string.IsNullOrWhiteSpace(manifest.License.VendoredLicenseCopy))
        {
            errors.Add("License.VendoredLicenseCopy path must be recorded so the license text survives even if the submodule is not checked out.");
        }

        if (string.IsNullOrWhiteSpace(manifest.AttributionNotice))
        {
            errors.Add("AttributionNotice must be recorded.");
        }

        if (manifest.Components.Count == 0)
        {
            errors.Add("Components must list at least one audited upstream source boundary.");
        }
        else if (manifest.Components.Any(c => string.IsNullOrWhiteSpace(c.Path)))
        {
            errors.Add("Every component must have a non-empty Path.");
        }

        if (manifest.PinnedUtc == default)
        {
            errors.Add("PinnedUtc must be recorded.");
        }

        return errors.Count == 0
            ? UpstreamReferenceValidationResult.Success
            : UpstreamReferenceValidationResult.Failure(errors);
    }
}
