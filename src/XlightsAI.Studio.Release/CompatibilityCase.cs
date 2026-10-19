namespace XlightsAI.Studio.Release;

/// <summary>
/// PHASE-16 Section 3 proposed contract, kept exactly as specified. One cell of the release
/// compatibility matrix (Step 098, implementation guidance: "Test a matrix of Windows versions,
/// xLights supported versions, layouts including groups/submodels, audio types and offline/GPU-less
/// machines"): which OS/architecture/xLights-version combination this case targets, and which real,
/// already-known layout and audio fixture it exercises against that combination. Carries no fixture
/// bytes itself — <see cref="ICompatibilityFixtureCatalog"/> resolves the ids to real on-disk content.
/// </summary>
public sealed record CompatibilityCase(
    string Os,
    string Architecture,
    string XlightsVersion,
    string LayoutFixtureId,
    string AudioFixtureId);
