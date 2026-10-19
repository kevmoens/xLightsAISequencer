namespace XlightsAI.Studio.Distribution;

/// <summary>
/// Result of <see cref="IUpdateNoticeChecker.CheckForUpdate"/>: whether a newer, signature-verified release
/// is available, realizing this step's own title, verbatim: "...and update notices." Never claims an update
/// is available unless the candidate manifest's signature was actually verified (Section 2: "Do not silently
/// pull remote model updates after initial consent" -- applied here to the notice itself never being trusted
/// unsigned, not to any auto-apply behavior, which this record does not perform at all).
/// </summary>
/// <param name="IsAvailable">Whether a newer, verified release is available.</param>
/// <param name="AvailableVersion">The candidate release's own <see cref="ReleaseManifest.ProductVersion"/>, or <see langword="null"/> when no update is available/verifiable.</param>
/// <param name="Message">A human-readable explanation, always populated regardless of <see cref="IsAvailable"/> (e.g. "already up to date," "manifest could not be verified and was ignored").</param>
public sealed record UpdateNotice(bool IsAvailable, string? AvailableVersion, string Message);
