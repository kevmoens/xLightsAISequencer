using System.Diagnostics;
using System.Runtime.Versioning;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// macOS Keychain backed <see cref="ICredentialStore"/>, driven through the built-in <c>security</c> CLI so
/// no native dependency is needed. Secrets live in the user's login keychain, never in a file this app writes.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacKeychainCredentialStore : ICredentialStore
{
    private const string Service = "XlightsAI.Studio";
    private const int ItemNotFoundExitCode = 44;

    public async Task StoreAsync(string name, string secret, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(secret);

        var (exitCode, _) = await RunSecurityAsync(ct, "add-generic-password", "-U", "-s", Service, "-a", name, "-w", secret).ConfigureAwait(false);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Could not store credential '{name}' in the macOS Keychain (exit code {exitCode}).");
        }
    }

    public async Task<string?> TryGetAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var (exitCode, output) = await RunSecurityAsync(ct, "find-generic-password", "-s", Service, "-a", name, "-w").ConfigureAwait(false);
        if (exitCode == ItemNotFoundExitCode)
        {
            return null;
        }

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Could not read credential '{name}' from the macOS Keychain (exit code {exitCode}).");
        }

        return output.TrimEnd('\r', '\n');
    }

    public async Task DeleteAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var (exitCode, _) = await RunSecurityAsync(ct, "delete-generic-password", "-s", Service, "-a", name).ConfigureAwait(false);
        if (exitCode != 0 && exitCode != ItemNotFoundExitCode)
        {
            throw new InvalidOperationException($"Could not delete credential '{name}' from the macOS Keychain (exit code {exitCode}).");
        }
    }

    private static async Task<(int ExitCode, string Output)> RunSecurityAsync(CancellationToken ct, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("/usr/bin/security")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start /usr/bin/security.");
        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        var output = await outputTask.ConfigureAwait(false);
        await errorTask.ConfigureAwait(false);
        return (process.ExitCode, output);
    }
}
