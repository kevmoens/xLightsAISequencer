namespace XlightsAI.Studio.Knowledge.Tests;

/// <summary>
/// Acceptance-criteria check against a real xLights install, when one happens to be present on the
/// machine running the tests ("all 56 effects produce a generated, validated parameter schema with no
/// exceptions/crashes"). Skips cleanly everywhere else (CI, a machine without xLights installed) —
/// the deterministic fixture-based tests in this project are what every build actually depends on.
/// </summary>
public sealed class RealXlightsInstallTests
{
    private const string RealMetadataRoot = @"C:\Program Files\xLights\effectmetadata";

    [Fact]
    public void AllRealEffects_BuildValidateAndSchemaGenerateWithoutError()
    {
        if (!Directory.Exists(RealMetadataRoot))
        {
            // No real xLights install on this machine (CI, a dev box without xLights) — nothing to
            // check here. The fixture-based tests in this project are what every build depends on.
            return;
        }

        var catalog = EffectVocabularyBuilder.BuildFromDirectory(RealMetadataRoot);
        Assert.Equal(56, catalog.Effects.Count);

        var validator = new EffectSettingsValidator(catalog);
        var schemaGenerator = new EffectVocabularySchemaGenerator(catalog);

        foreach (var (effectName, vocabulary) in catalog.Effects)
        {
            // Every own-property default must itself validate as a supported value — a generated
            // schema that rejects its own declared defaults would indicate a builder bug.
            foreach (var property in vocabulary.Properties)
            {
                if (property.DefaultValue is { } defaultValue)
                {
                    Assert.True(
                        validator.IsSupportedParameterValue(effectName, property.SettingKey, defaultValue),
                        $"{effectName}.{property.SettingKey}'s own default '{defaultValue}' failed validation.");
                }
            }

            var schema = schemaGenerator.GenerateSchema(effectName);
            Assert.NotNull(schema["properties"]);
        }
    }
}
