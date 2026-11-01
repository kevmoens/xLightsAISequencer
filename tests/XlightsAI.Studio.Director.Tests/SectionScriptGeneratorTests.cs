using System.Text.Json;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Knowledge;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>Build-plan Phase 6 task 1: the same "validate, retry with correction, exhaust after N attempts" discipline as <see cref="ShowBibleGeneratorTests"/>, scoped to one <see cref="SectionScript"/> call.</summary>
public sealed class SectionScriptGeneratorTests
{
    private static EffectVocabularyCatalog FakeVocabulary() => new(
        RealXLightsFixture.SixCommonEffectNames.ToDictionary(name => name, name => new EffectVocabulary(name, "E_", []), StringComparer.Ordinal),
        buffer: new EffectVocabulary("Buffer", "B_", []),
        blending: new EffectVocabulary("Blending", "T_", []),
        color: new EffectVocabulary("Color", "C_", []));

    private static async Task<(XlightsAI.Studio.Xlights.DisplayGraph Layout, LookLibrary Looks)> LoadFixturesAsync()
    {
        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        return (layout, BuildLookLibrary());
    }

    /// <summary>A single real-group Look, mirroring <see cref="ShowBibleContentFixture"/>'s own established "real group names, never fabricated" convention -- kept local rather than referencing <c>XlightsAI.Studio.Choreography.Tests.Phase4LookLibraryFixture</c>, since this test project does not (and should not) take a project reference on another test project.</summary>
    private static LookLibrary BuildLookLibrary() => new([
        new Look(
            Id: "golden-hold",
            Name: "Golden Hold",
            Intent: "Warm full-house steady state.",
            EnergyCeiling: 0.45,
            Motion: "hold",
            PaletteRef: "warm-gold",
            Subject: "group:Outline",
            Layers: [new LookLayer(1, "group:Outline", "On", LookLayer.RoleSubject, new Dictionary<string, string>(), "Normal", "Bars", 0.9)]),
    ]);

    private static string ToModelResponseText(SectionScript script) => $"```json\n{JsonSerializer.Serialize(script)}\n```";

    [Fact]
    public async Task GenerateAsync_FirstResponseValid_ValidatesOnFirstAttempt()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (layout, looks) = await LoadFixturesAsync();
        var sectionId = Guid.NewGuid();
        var validScript = new SectionScript(sectionId, "golden-hold", FillerEffects: RealXLightsFixture.SixCommonEffectNames);

        var client = new FakeChatCompletionClient([_ => new ChatCompletionResult(ToModelResponseText(validScript), new AiUsage(500, 100, 0, 0))]);
        var generator = new SectionScriptGenerator(client);

        var report = await generator.GenerateAsync("STABLE", "VOLATILE", sectionId, looks, layout, FakeVocabulary(), 1000, CancellationToken.None);

        Assert.NotNull(report.Script);
        Assert.Equal("golden-hold", report.Script!.LookId);
        Assert.Equal(1, report.AttemptCount);
        Assert.True(report.ValidatedOnFirstAttempt);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_FirstResponseInvalid_RetriesWithCorrection_ThenSucceeds()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (layout, looks) = await LoadFixturesAsync();
        var sectionId = Guid.NewGuid();
        var invalidScript = new SectionScript(sectionId, "no-such-look");
        var validScript = new SectionScript(sectionId, "golden-hold", FillerEffects: RealXLightsFixture.SixCommonEffectNames);

        var client = new FakeChatCompletionClient([
            _ => new ChatCompletionResult(ToModelResponseText(invalidScript), new AiUsage(500, 100, 0, 0)),
            _ => new ChatCompletionResult(ToModelResponseText(validScript), new AiUsage(500, 100, 450, 0)),
        ]);
        var generator = new SectionScriptGenerator(client);

        var report = await generator.GenerateAsync("STABLE", "VOLATILE", sectionId, looks, layout, FakeVocabulary(), 1000, CancellationToken.None);

        Assert.NotNull(report.Script);
        Assert.Equal(2, report.AttemptCount);
        Assert.False(report.ValidatedOnFirstAttempt);
        Assert.Contains("Correction needed", client.Requests[1].UserPrompt);
        Assert.Contains("look-ref", client.Requests[1].UserPrompt);
        Assert.Equal(client.Requests[0].SystemPrompt, client.Requests[1].SystemPrompt);
    }

    [Fact]
    public async Task GenerateAsync_EveryAttemptInvalid_ExhaustsRetries_ReturnsNullScript()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (layout, looks) = await LoadFixturesAsync();
        var sectionId = Guid.NewGuid();
        var invalidScript = new SectionScript(sectionId, "no-such-look");

        var client = new FakeChatCompletionClient(Enumerable.Repeat<Func<ChatCompletionRequest, ChatCompletionResult>>(
            _ => new ChatCompletionResult(ToModelResponseText(invalidScript), new AiUsage(500, 100, 0, 0)), 3));
        var generator = new SectionScriptGenerator(client, maxAttempts: 3);

        var report = await generator.GenerateAsync("STABLE", "VOLATILE", sectionId, looks, layout, FakeVocabulary(), 1000, CancellationToken.None);

        Assert.Null(report.Script);
        Assert.Equal(3, report.AttemptCount);
        Assert.NotEmpty(report.FinalViolations);
    }

    [Fact]
    public async Task GenerateAsync_WithCallLog_AppendsOneEntryPerAttempt()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (layout, looks) = await LoadFixturesAsync();
        var sectionId = Guid.NewGuid();
        var songId = Guid.NewGuid();
        var validScript = new SectionScript(sectionId, "golden-hold", FillerEffects: RealXLightsFixture.SixCommonEffectNames);
        var root = Path.Combine(Path.GetTempPath(), "xlai-phase6-calllog-" + Guid.NewGuid().ToString("N"));
        try
        {
            var client = new FakeChatCompletionClient([_ => new ChatCompletionResult(ToModelResponseText(validScript), new AiUsage(500, 100, 0, 0))]);
            var callLog = new JsonAiCallLogStore(root);
            var generator = new SectionScriptGenerator(client, callLog: callLog, modelName: "claude-sonnet-5");

            await generator.GenerateAsync("STABLE", "VOLATILE", songId, looks, layout, FakeVocabulary(), 1000, CancellationToken.None);

            var entries = await callLog.ListForSongAsync(songId, CancellationToken.None);
            Assert.Single(entries);
            Assert.Equal(AiRole.Choreographer, entries[0].Role);
            Assert.Equal("claude-sonnet-5", entries[0].Model);
            Assert.NotNull(entries[0].EstimatedCostUsd);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
