namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 075's own new credential boundary -- no prior phase persists any secret anywhere in
/// this repository. Holds exactly one named secret per entry (a BYOK cloud provider's API key); never
/// the provider config itself (see <see cref="AiProviderSettings"/>, which deliberately excludes this).
/// Section 4's own rule, verbatim: "Never log API keys or whole lyrics by default" -- no implementation
/// of this interface may write a plaintext secret to disk, a log, or an exception message.
/// </summary>
public interface ICredentialStore
{
    Task StoreAsync(string name, string secret, CancellationToken ct = default);

    /// <summary>Returns the stored secret for <paramref name="name"/>, or <see langword="null"/> if none has been stored.</summary>
    Task<string?> TryGetAsync(string name, CancellationToken ct = default);

    Task DeleteAsync(string name, CancellationToken ct = default);
}
