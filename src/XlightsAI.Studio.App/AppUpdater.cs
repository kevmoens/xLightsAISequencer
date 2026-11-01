using System.Runtime.InteropServices;
using Velopack;
using Velopack.Sources;
using XlightsAI.Studio.Shell;

namespace XlightsAI.Studio.App;

/// <summary>
/// Background auto-update via Velopack against this repository's GitHub Releases. Does nothing unless the
/// app was installed by a Velopack installer (a plain <c>dotnet run</c> or an extracted build has no
/// update feed). A found update is downloaded in the background and applied when the app next exits, so the
/// user is never interrupted mid-session. Failures are logged and never block startup.
/// </summary>
internal static class AppUpdater
{
    private const string RepositoryUrl = "https://github.com/kevmoens/xLightsAISequencer";

    public static async Task CheckAndStageAsync(AppDiagnostics diagnostics)
    {
        try
        {
            var manager = new UpdateManager(
                new GithubSource(RepositoryUrl, accessToken: null, prerelease: false),
                new UpdateOptions { ExplicitChannel = ReleaseChannel() });

            if (!manager.IsInstalled)
            {
                return;
            }

            var update = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (update is null)
            {
                return;
            }

            await manager.DownloadUpdatesAsync(update).ConfigureAwait(false);
            manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: false);
        }
        catch (Exception ex)
        {
            diagnostics.RecordFailure("auto-update", ex);
        }
    }

    /// <summary>Must match the <c>--channel</c> the release workflow packs with; Windows uses Velopack's default channel.</summary>
    private static string? ReleaseChannel()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
    }
}
