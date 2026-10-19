using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// Windows Data Protection API (DPAPI) backed <see cref="ICredentialStore"/> -- encrypts each secret
/// with <see cref="ProtectedData"/> under the current Windows user account before it ever touches disk,
/// so a copied file is unreadable off this machine/account. The same atomic temp-file +
/// <see cref="File.Move(string, string, bool)"/> replace pattern <see cref="JsonSelectionContextStore"/>
/// already uses. Guards every member with <see cref="OperatingSystem.IsWindows"/> (the same
/// platform-guard convention <c>WaveOutAudioTransport</c> already establishes) and throws
/// <see cref="PlatformNotSupportedException"/> rather than silently storing a secret unencrypted on any
/// other OS -- there is no cross-platform credential store in this repository yet (an explicitly
/// disclosed gap, not a silent omission).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiCredentialStore : ICredentialStore
{
    private readonly string _directory;

    public DpapiCredentialStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public async Task StoreAsync(string name, string secret, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(secret);
        RequireWindows();

        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_directory);

        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), optionalEntropy: null, DataProtectionScope.CurrentUser);

        var finalPath = GetPath(name);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllBytesAsync(tempPath, protectedBytes, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            File.Move(tempPath, finalPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    public async Task<string?> TryGetAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        RequireWindows();

        ct.ThrowIfCancellationRequested();
        var path = GetPath(name);
        if (!File.Exists(path))
        {
            return null;
        }

        var protectedBytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
        var plainBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public Task DeleteAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        RequireWindows();

        ct.ThrowIfCancellationRequested();
        var path = GetPath(name);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetPath(string name) => Path.Combine(_directory, $"{SanitizeName(name)}.cred");

    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Length == 0 ? "_" : sanitized;
    }

    private static void RequireWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "DpapiCredentialStore requires the Windows Data Protection API; no cross-platform credential store is implemented yet.");
        }
    }
}
