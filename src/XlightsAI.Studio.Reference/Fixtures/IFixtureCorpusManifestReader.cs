namespace XlightsAI.Studio.Reference.Fixtures;

/// <summary>Reads a <see cref="FixtureCorpusManifest"/> from storage. Never writes.</summary>
public interface IFixtureCorpusManifestReader
{
    Task<FixtureCorpusManifest> ReadAsync(string path, CancellationToken ct);
}
