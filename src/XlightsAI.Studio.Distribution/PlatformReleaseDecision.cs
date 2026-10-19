namespace XlightsAI.Studio.Distribution;

/// <summary>
/// PHASE-15 Step 097. The actual release-claim decision derived from a set of real
/// <see cref="PlatformSmokeTestReport"/>s -- Section 3's own architectural design note, verbatim: "Every
/// blocker must be fixed, deferred behind feature flag with documentation, or removed from supported claims."
/// A platform never appears in <see cref="AdvertisedRuntimeIdentifiers"/> unless every one of its own checks
/// was both runnable and passing; anything short of that is recorded in <see cref="UnsupportedReasons"/> with
/// the real, specific reason -- never silently dropped.
/// </summary>
/// <param name="AdvertisedRuntimeIdentifiers">Runtime identifiers eligible to be publicly claimed as supported.</param>
/// <param name="UnsupportedReasons">Every other evaluated runtime identifier, mapped to its own real, human-readable reasons.</param>
public sealed record PlatformReleaseDecision(
    IReadOnlyList<string> AdvertisedRuntimeIdentifiers,
    IReadOnlyDictionary<string, IReadOnlyList<string>> UnsupportedReasons);
