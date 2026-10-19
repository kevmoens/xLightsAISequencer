namespace XlightsAI.Studio.Release;

/// <summary>
/// New, phase-local abstraction (disclosed). One required, named release document this step's own
/// implementation guidance calls for, verbatim: "Gate release on ... documentation, security/privacy
/// checks, changelog, support matrix ... and known issues." Carries no file bytes itself —
/// <see cref="IReleaseDocumentationAuditor"/> resolves <see cref="Path"/> to real on-disk content.
/// </summary>
/// <param name="Name">Human-readable name for this requirement (e.g. "Changelog", "Third-party notices").</param>
/// <param name="Path">Real, absolute on-disk path this requirement's content must exist at.</param>
/// <param name="RequiredSubstring">
/// A real, specific substring the file's content must contain (e.g. the version being released inside
/// the changelog) or <see langword="null"/> when mere non-empty existence is the whole requirement.
/// </param>
public sealed record ReleaseDocumentationRequirement(string Name, string Path, string? RequiredSubstring);
