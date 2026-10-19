namespace XlightsAI.Studio.Stems;

/// <summary>
/// Real, deterministic archive expansion of an already hash-verified <see cref="ModelAsset"/> archive
/// into its constituent files. The concrete, real (tar+bzip2-capable) implementation and its own
/// disclosed third-party dependency choice is Step 044D's own scope -- this step declares the contract
/// only, so <see cref="ISpectrogramStemSeparator"/>'s composition can be defined and proven against a
/// test double, independent of that later, real archive-format decision.
/// </summary>
public interface IModelArchiveExpander
{
    Task<IReadOnlyList<ExtractedModelFile>> ExpandAsync(string verifiedArchivePath, string destinationDirectory, CancellationToken ct);
}
