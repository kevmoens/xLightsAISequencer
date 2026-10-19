namespace XlightsAI.Studio.Release;

/// <summary>
/// The full, real checksum manifest for one published release bundle directory — this step's own
/// user-visible outcome, verbatim: "Installer/bundle artifact with clean-machine smoke results and
/// checksums." <see cref="ManifestSha256"/> is a single, stable, deterministic hash over every entry
/// (sorted by <see cref="ReleaseArtifactManifestEntry.RelativePath"/>, ordinal) — this step's own
/// required acceptance, verbatim: "Reproducible artifacts..."; re-running
/// <see cref="ReleaseArtifactChecksumBuilder.Build"/> against byte-identical bundle contents always
/// reproduces the same <see cref="ManifestSha256"/>, regardless of the real filesystem's own directory
/// enumeration order.
/// </summary>
public sealed record ReleaseArtifactManifest(IReadOnlyList<ReleaseArtifactManifestEntry> Entries, string ManifestSha256);
