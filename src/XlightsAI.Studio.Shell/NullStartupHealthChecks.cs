using XlightsAI.Studio.Distribution;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// No-op <see cref="IStartupHealthChecks"/> used as the default collaborator for
/// <see cref="ShellViewModel"/>'s new Step 095 constructor parameter, same "optional collaborator
/// that needs real external configuration this layer does not yet own" reasoning as
/// <see cref="NullSequenceDocumentStore"/>/<see cref="NullSongAssetStore"/> — the real
/// <see cref="StartupHealthChecks"/> needs a concrete bundle directory and
/// <see cref="ReleaseManifest"/> that only a real published release's own composition root knows,
/// which does not exist yet at the Shell/first-run layer (PHASE-15 Step 094 Section 6 deliberately
/// deferred that wiring). Unlike the throwing Null* stores, this one never needs to fail loudly:
/// an absent asset manifest at this layer means "nothing declared to verify yet," so it reports
/// zero issues rather than throwing — first-run must never crash for a collaborator Step 095 itself
/// does not yet have a real bundle to check.
/// </summary>
public sealed class NullStartupHealthChecks : IStartupHealthChecks
{
    public static readonly NullStartupHealthChecks Instance = new();

    private NullStartupHealthChecks()
    {
    }

    public Task<IReadOnlyList<string>> CheckAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}
