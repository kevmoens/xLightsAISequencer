namespace XlightsAI.Studio.Director;

/// <summary>Mirrors the codebase-wide <c>*CorruptException(path, inner)</c> pattern (e.g. <c>CompositionCorruptException</c>): a persisted role provider config file exists but failed to deserialize.</summary>
public sealed class RoleProviderConfigCorruptException : Exception
{
    public RoleProviderConfigCorruptException(string path, Exception inner)
        : base($"Role provider config cache at '{path}' is corrupt or not a valid serialized RoleProviderConfig.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
