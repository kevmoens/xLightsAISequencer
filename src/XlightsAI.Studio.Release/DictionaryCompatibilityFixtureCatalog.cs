namespace XlightsAI.Studio.Release;

/// <summary>
/// Default <see cref="ICompatibilityFixtureCatalog"/>: a plain, explicit, caller-supplied mapping from
/// fixture id to real on-disk path — the same explicit-registration convention
/// <c>XlightsAI.Studio.Distribution.KnownPlatforms</c> already established for known platform
/// definitions. Never infers a fixture location from a naming convention; an unregistered id is
/// reported via <see cref="CompatibilityFixtureNotFoundException"/>, never silently resolved to a
/// guessed path.
/// </summary>
public sealed class DictionaryCompatibilityFixtureCatalog : ICompatibilityFixtureCatalog
{
    private readonly IReadOnlyDictionary<string, string> _layoutShowDirectoriesByFixtureId;
    private readonly IReadOnlyDictionary<string, string> _audioFilePathsByFixtureId;

    public DictionaryCompatibilityFixtureCatalog(
        IReadOnlyDictionary<string, string> layoutShowDirectoriesByFixtureId,
        IReadOnlyDictionary<string, string> audioFilePathsByFixtureId)
    {
        ArgumentNullException.ThrowIfNull(layoutShowDirectoriesByFixtureId);
        ArgumentNullException.ThrowIfNull(audioFilePathsByFixtureId);

        _layoutShowDirectoriesByFixtureId = layoutShowDirectoriesByFixtureId;
        _audioFilePathsByFixtureId = audioFilePathsByFixtureId;
    }

    public string ResolveLayoutShowDirectory(string layoutFixtureId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutFixtureId);

        return _layoutShowDirectoriesByFixtureId.TryGetValue(layoutFixtureId, out var directory)
            ? directory
            : throw new CompatibilityFixtureNotFoundException("layout", layoutFixtureId);
    }

    public string ResolveAudioFilePath(string audioFixtureId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audioFixtureId);

        return _audioFilePathsByFixtureId.TryGetValue(audioFixtureId, out var path)
            ? path
            : throw new CompatibilityFixtureNotFoundException("audio", audioFixtureId);
    }
}
