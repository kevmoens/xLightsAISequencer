namespace XlightsAI.Studio.Release;

/// <summary>
/// Thrown by <see cref="ICompatibilityFixtureCatalog"/> when a <see cref="CompatibilityCase"/>
/// references a layout/audio fixture id this catalog does not know — Step 098's own required failure
/// path, "Invalid model/format/version must fail safely without rewriting source files," applied to
/// an invalid fixture *reference*: no file is read, written, or touched before this is thrown.
/// </summary>
public sealed class CompatibilityFixtureNotFoundException : Exception
{
    public CompatibilityFixtureNotFoundException(string fixtureKind, string fixtureId)
        : base($"No known {fixtureKind} fixture is registered for id '{fixtureId}'.")
    {
        FixtureKind = fixtureKind;
        FixtureId = fixtureId;
    }

    public string FixtureKind { get; }

    public string FixtureId { get; }
}
