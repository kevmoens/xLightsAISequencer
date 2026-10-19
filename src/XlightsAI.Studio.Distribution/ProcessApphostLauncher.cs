using System.ComponentModel;
using System.Diagnostics;

namespace XlightsAI.Studio.Distribution;

/// <summary>
/// Real <see cref="IApphostLauncher"/>. Starts the real apphost file directly (no <c>dotnet</c> wrapper, no
/// SDK on the invocation path -- the same real-launch convention Step 094's own evidence already established,
/// §3.4), observes whether it is still running after a short window, then always terminates/disposes it --
/// this is a smoke test, not a user session.
/// </summary>
public sealed class ProcessApphostLauncher(TimeSpan? observationWindow = null) : IApphostLauncher
{
    private readonly TimeSpan _observationWindow = observationWindow ?? TimeSpan.FromSeconds(2);

    public async Task<(bool Launched, string Detail)> TryLaunchAsync(string apphostPath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apphostPath);

        if (!File.Exists(apphostPath))
        {
            return (false, $"Apphost '{apphostPath}' does not exist.");
        }

        Process? process = null;
        try
        {
            process = Process.Start(new ProcessStartInfo(apphostPath)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            if (process is null)
            {
                return (false, $"Process.Start returned null for '{apphostPath}'.");
            }

            await Task.Delay(_observationWindow, ct).ConfigureAwait(false);

            var stillRunning = !process.HasExited;
            return (stillRunning,
                stillRunning
                    ? $"Apphost '{apphostPath}' started and was still running after {_observationWindow.TotalSeconds:0}s."
                    : $"Apphost '{apphostPath}' exited immediately (exit code {process.ExitCode}).");
        }
        catch (Win32Exception ex)
        {
            return (false, $"Apphost launch failed: {ex.Message}");
        }
        finally
        {
            if (process is { HasExited: false })
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // Process already exited between the HasExited check and Kill -- not a reportable failure.
                }
            }

            process?.Dispose();
        }
    }
}
