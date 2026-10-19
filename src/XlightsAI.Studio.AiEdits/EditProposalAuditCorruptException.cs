namespace XlightsAI.Studio.AiEdits;

/// <summary>Mirrors <see cref="SelectionContextCorruptException"/>'s own convention: a persisted <see cref="EditProposalAuditRecord"/> file exists but failed to deserialize.</summary>
public sealed class EditProposalAuditCorruptException : Exception
{
    public EditProposalAuditCorruptException(string path, Exception inner)
        : base($"Proposal audit record at '{path}' is corrupt or not a valid serialized EditProposalAuditRecord.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
