namespace XlightsAI.Studio.Composition;

/// <summary>Mirrors <see cref="XlightsAI.Studio.Display.SpatialDisplayGraphCorruptException"/>'s own convention: a persisted <see cref="MusicalRoleAssignmentSet"/> file exists but failed to deserialize.</summary>
public sealed class CompositionCorruptException : Exception
{
    public CompositionCorruptException(string path, Exception inner)
        : base($"Composition cache at '{path}' is corrupt or not a valid serialized MusicalRoleAssignmentSet.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
