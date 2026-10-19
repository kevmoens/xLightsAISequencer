namespace XlightsAI.Studio.Release;

/// <summary>
/// Phase-local contract (disclosed, see <see cref="BlindTrial"/>). Step 099's own implementation
/// guidance, verbatim: "Randomize candidate ordering for human A/B reviews... report sample size
/// honestly" — this is the randomization half; <see cref="AestheticTrialSummaryReporter"/> is the
/// honest-sample-size half.
/// </summary>
public interface IBlindTrialOrderingService
{
    /// <summary>
    /// Creates one new blind trial over <paramref name="candidateIds"/>, assigning a fresh
    /// <see cref="BlindTrial.AnonymousTrialId"/> and a genuinely randomized
    /// <see cref="BlindTrial.PresentationOrder"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="candidateIds"/> is <see langword="null"/>.</exception>
    /// <exception cref="BlindTrialInvalidException">
    /// <paramref name="candidateIds"/> has fewer than two entries, contains a blank id, or contains a
    /// duplicate id.
    /// </exception>
    BlindTrial CreateTrial(IReadOnlyList<string> candidateIds);
}
