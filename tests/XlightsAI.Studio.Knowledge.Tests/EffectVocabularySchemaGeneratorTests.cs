using System.Text.Json.Nodes;

namespace XlightsAI.Studio.Knowledge.Tests;

public sealed class EffectVocabularySchemaGeneratorTests
{
    private static string FixturesRoot => Path.Combine(AppContext.BaseDirectory, "Fixtures", "effectmetadata");

    private readonly EffectVocabularyCatalog _catalog = EffectVocabularyBuilder.BuildFromDirectory(FixturesRoot);
    private readonly EffectVocabularySchemaGenerator _generator;

    public EffectVocabularySchemaGeneratorTests()
    {
        _generator = new EffectVocabularySchemaGenerator(_catalog);
    }

    [Fact]
    public void GenerateSchema_IncludesTheEffectsOwnNumericPropertyWithBounds()
    {
        var schema = _generator.GenerateSchema("Fire");
        var properties = (JsonObject)schema["properties"]!;
        var height = (JsonObject)properties["E_SLIDER_Fire_Height"]!;

        Assert.Equal("number", (string?)height["type"]);
        Assert.Equal(1, (double?)height["minimum"]);
        Assert.Equal(100, (double?)height["maximum"]);
    }

    [Fact]
    public void GenerateSchema_IncludesEnumChoicesAsAStringEnum()
    {
        var schema = _generator.GenerateSchema("Fire");
        var properties = (JsonObject)schema["properties"]!;
        var location = (JsonObject)properties["E_CHOICE_Fire_Location"]!;

        Assert.Equal("string", (string?)location["type"]);
        var options = location["enum"]!.AsArray().Select(n => (string)n!).ToList();
        Assert.Equal(["Bottom", "Top", "Left", "Right"], options);
    }

    [Fact]
    public void GenerateSchema_MergesInTheSharedPanels()
    {
        var schema = _generator.GenerateSchema("Fire");
        var properties = (JsonObject)schema["properties"]!;

        Assert.True(properties.ContainsKey("B_SLIDER_Blur"));
        Assert.True(properties.ContainsKey("T_CHOICE_LayerMethod"));
        Assert.True(properties.ContainsKey("C_BUTTON_Palette1"));
    }

    [Fact]
    public void GenerateAllSchemas_ProducesOneSchemaPerEffect()
    {
        var schemas = _generator.GenerateAllSchemas();

        Assert.Equal(_catalog.Effects.Count, schemas.Count);
        Assert.Contains("Bars", schemas.Keys);
    }

    [Fact]
    public void GenerateSchema_UnknownEffect_Throws()
    {
        Assert.Throws<ArgumentException>(() => _generator.GenerateSchema("DoesNotExist"));
    }
}
