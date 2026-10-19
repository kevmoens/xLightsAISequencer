namespace XlightsAI.Studio.Reference;

/// <summary>One audited source boundary (directory) inside the pinned upstream checkout.</summary>
public sealed record UpstreamComponent(string Path, string Description);

/// <summary>License facts for the pinned upstream reference, as recorded in the manifest.</summary>
public sealed record UpstreamLicense(
    string SpdxId,
    string CopyrightHolder,
    string CopyrightYear,
    string UpstreamLicenseFile,
    string VendoredLicenseCopy,
    string ScopeNote);

/// <summary>
/// Deserialized form of a pinned-upstream-reference manifest: the immutable-commit pin, license and
/// attribution record, and component inventory for an upstream reference project consulted for
/// behavior/data-shape comparison only (never compiled into or shipped with this application). This is
/// a pure data contract; <see cref="UpstreamReferenceValidator"/> owns the rules.
/// </summary>
public sealed record UpstreamReferenceManifest(
    int SchemaVersion,
    string Name,
    string RepositoryUrl,
    string CommitSha,
    string CommitMessage,
    DateTimeOffset CommitDateUtc,
    DateTimeOffset PinnedUtc,
    string VendoringStrategy,
    string LocalReferencePath,
    UpstreamLicense License,
    string AttributionNotice,
    IReadOnlyList<UpstreamComponent> Components,
    IReadOnlyList<string> ThirdPartyDependencyNotes,
    IReadOnlyList<string> KnownLicenseRisks);
