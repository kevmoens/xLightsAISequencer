namespace XlightsAI.Studio.AiEdits;

/// <summary>Picks the OS-appropriate <see cref="ICredentialStore"/>: DPAPI on Windows, Keychain on macOS.</summary>
public static class CredentialStoreFactory
{
    public static ICredentialStore Create(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            return new DpapiCredentialStore(directory);
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacKeychainCredentialStore();
        }

        throw new PlatformNotSupportedException("Credential storage is only implemented for Windows (DPAPI) and macOS (Keychain).");
    }
}
