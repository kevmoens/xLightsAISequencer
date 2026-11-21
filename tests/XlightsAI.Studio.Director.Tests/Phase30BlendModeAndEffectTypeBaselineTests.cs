using System.Text.Json;
using System.Text.RegularExpressions;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Phase 30 step 30.0's other gate (besides the blend-mode fixture confirmation itself): "Measure
/// baseline blend-mode usage in current generator output (expect ~100% default) and current
/// [pre-family-grouping raw effect-type distribution] per section on the existing fixture songs, for
/// later comparison."
///
/// Reuses the same real-clip/real-xLights-install/hand-authored-Show-Bible shape as
/// <see cref="Phase6EndToEndRealTrackTests"/>, but swaps its real local-Ollama Choreographer client for a
/// deterministic <see cref="FakeChatCompletionClient"/> (same "scripted, no network call" convention
/// <see cref="Phase29TimelineGenerationTests"/> already established) -- this measurement needs a real,
/// exported XSQ from the two baseline clips prior phases already used
/// (<c>carrie-30s-clip.mp3</c>/<c>chappell-30s-clip.mp3</c>, per
/// <c>implementation-evidence/PHASE-29-baseline-and-blockers.md</c>'s own baseline table), not a live-LLM
/// dependency, and not Ollama's own non-determinism. Skips cleanly (never fails) when the real xLights
/// install is not present, matching every other real-fixture test in this project.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Phase30BlendModeAndEffectTypeBaselineTests : IDisposable
{
    private static readonly (string ClipFileName, string SongName, string RealSequencePath)[] Songs =
    [
        ("carrie-30s-clip.mp3", "Carrie Underwood - Away In A Manger Lite (30s clip)", @"C:\xLights\Carrie Underwood - Away In A Manger Lite.xsq"),
        ("chappell-30s-clip.mp3", "Christmas Tree Hot to Go - Chappell Roan (30s clip)", @"C:\xLights\Christmas Tree Hot to Go - Chappell Roan.xsq"),
    ];

    private static readonly Dictionary<SectionKind, double> EnergyCeilingByKind = new()
    {
        [SectionKind.Intro] = 0.20,
        [SectionKind.Verse] = 0.40,
        [SectionKind.PreChorus] = 0.70,
        [SectionKind.Chorus] = 0.85,
        [SectionKind.Bridge] = 0.25,
        [SectionKind.Solo] = 0.80,
        [SectionKind.Outro] = 0.15,
        [SectionKind.Unknown] = 0.50,
    };

    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-phase30-baseline-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact(Timeout = 600_000)]
    public async Task GenerateEndToEnd_RealBaselineSongs_ZeroBlendModeSettings_RecordsRawEffectTypeDistribution()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var ct = CancellationToken.None;
        var layout = await new ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, ct);
        var layoutSummary = LayoutSummary.Build(layout);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var registry = new EffectRecipeRegistry(recipes);

        var knowledge = new DirectorKnowledgeContext(
            vocabulary,
            new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []),
            new ExemplarIndex(DateTimeOffset.UtcNow, []));

        var bible = BuildHandAuthoredShowBible();
        Assert.Empty(ShowBibleValidator.Validate(bible.Content, layout, vocabulary, recipes));

        var rows = new List<string>();
        var totalBlendModeSettings = 0;
        var completedSongs = 0;

        foreach (var (clipFileName, songName, realSequencePath) in Songs)
        {
            var clipPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", clipFileName);
            if (!File.Exists(clipPath) || !File.Exists(realSequencePath))
            {
                continue;
            }

            var analyzed = await AnalyzeAsync(clipPath, realSequencePath, songName, ct);

            // Same fix PHASE-29-baseline-and-blockers.md records: the analysed beat grid can extrapolate
            // one beat past a trimmed clip's own audio end (Carrie: 30.142s; Chappell: 30.256s on that
            // run), which the 25ms xLights frame grid then rounds to a non-positive-duration timing mark.
            // Drop only the out-of-bounds beats from the generation input; keep the saved analysis intact.
            var inBoundsBeats = analyzed.BeatGrid.Beats.Where(b => b.TimeTicks >= 0 && b.TimeTicks < analyzed.SongDurationTicks).ToArray();
            var music = inBoundsBeats.Length < analyzed.BeatGrid.Beats.Length
                ? analyzed with { BeatGrid = analyzed.BeatGrid with { Beats = inBoundsBeats } }
                : analyzed;

            // A deterministic, no-network scripted Choreographer: round-robins the Show Bible's own Looks
            // across whichever real SectionIds this song's real analysis produces, with no cues (pure
            // base-Look coverage) -- same "scripted, no real provider" discipline as
            // Phase29TimelineGenerationTests' own FakeChatCompletionClient, generalized to an arbitrary
            // real section count instead of a hand-enumerated fixed one.
            var sectionOrder = music.Sections.Labels.Select(l => l.Id).ToList();
            var lookIdBySection = sectionOrder
                .Select((id, i) => (id, lookId: bible.Content.Looks.Looks[i % bible.Content.Looks.Looks.Count].Id))
                .ToDictionary(x => x.id, x => x.lookId);

            // Same reply every time a section's id comes up, repeated generously -- covers
            // SectionScriptBatchGenerator's own internal per-section retries, same as
            // Phase29TimelineGenerationTests' own Enumerable.Repeat(..., 40) convention.
            Func<ChatCompletionRequest, ChatCompletionResult> respond = request =>
            {
                var match = Regex.Match(request.UserPrompt, @"(?m)^SectionId: ([a-fA-F0-9-]+)");
                var id = Guid.Parse(match.Groups[1].Value);
                var timeline = new SectionTimeline(id, lookIdBySection[id], Cues: [], FillerEffects: RealXLightsFixture.SixCommonEffectNames);
                return new ChatCompletionResult(JsonSerializer.Serialize(timeline), new AiUsage(10, 5, null, null));
            };
            var fake = new FakeChatCompletionClient(Enumerable.Repeat(respond, Math.Max(40, sectionOrder.Count * 10)));

            var pipeline = new ChoreographyGenerationPipeline(
                knowledge, "High-Energy / EDM",
                directorClient: null, directorModelName: null,
                fake, "scripted",
                maxOutputTokens: 600);
            var settings = new ChoreographyGenerationSettings(layout, vocabulary, recipes, registry, EnergyCeilingByKind, _root);

            var report = await pipeline.GenerateAsync(music, layoutSummary, settings, Guid.NewGuid(), bible, ct);
            Assert.True(File.Exists(report.Export.Path));

            var structural = new XsqStructuralReader().Read(report.Export.Path);

            // Blend-mode baseline: the exporter never serializes T_CHOICE_LayerMethod today
            // (XsqExporter.BuildSettingsString's own documented discipline) -- confirm that empirically
            // against this real exported XSQ rather than just trusting the doc comment.
            var blendModeSettings = structural.EffectDb.Count(s => s.Contains("T_CHOICE_LayerMethod", StringComparison.Ordinal));
            totalBlendModeSettings += blendModeSettings;

            // Raw effect-type distribution (NOT "effect-family concentration" -- EffectVisualCharacter
            // carries no family-grouping field yet, confirmed by Phase 30 research; that regrouping is
            // step 30.4's own job once the field exists). One count per effect-name placement across every
            // model/section this song compiled.
            var effectTypeCounts = structural.ElementEffects
                .SelectMany(e => e.Effects)
                .GroupBy(e => e.Name, StringComparer.Ordinal)
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());
            var totalPlacements = effectTypeCounts.Values.Sum();

            rows.Add($"| {songName} | {totalPlacements} | {blendModeSettings} | {string.Join(", ", effectTypeCounts.Select(kv => $"{kv.Key}={kv.Value}"))} |");
            completedSongs++;

            Assert.Equal(0, blendModeSettings);
        }

        Assert.True(completedSongs > 0, "No baseline song completed end-to-end -- see test output for per-song skip reasons.");
        Assert.Equal(0, totalBlendModeSettings);

        var evidenceDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "implementation-evidence");
        Directory.CreateDirectory(evidenceDir);
        await File.WriteAllTextAsync(Path.Combine(evidenceDir, "PHASE-30-BASELINE-RUN.md"), $"""
            # Phase 30 step 30.0 baseline run (auto-generated by Phase30BlendModeAndEffectTypeBaselineTests)

            Real layout: `{RealXLightsFixture.LayoutPath}`. Choreographer: deterministic scripted client (no network, no Ollama) --
            round-robins the hand-authored Show Bible's own Looks across each song's real analyzed sections, no cues.

            | Song | Total effect placements | T_CHOICE_LayerMethod settings (blend-mode usage) | Raw effect-type counts (pre-family-grouping) |
            |---|---:|---:|---|
            {string.Join("\n", rows)}

            Blend-mode baseline: 0 `T_CHOICE_LayerMethod` settings across {completedSongs} song(s) -- 100% implicit default ("Normal"),
            confirmed empirically against real exported XSQ output, consistent with `XsqExporter.BuildSettingsString`'s own
            documented "BlendMode is IR-only, never serialized" discipline.

            Effect-type distribution is raw per-effect-name counts, not grouped by family -- `EffectVisualCharacter` carries no
            family-grouping field yet (confirmed by Phase 30 research); step 30.4 adds that field and regroups these same counts
            by family. This table is this step's "for later comparison" baseline for that future regrouping.
            """, ct);
    }

    private async Task<MusicModel> AnalyzeAsync(string audioPath, string realSequencePath, string songName, CancellationToken ct)
    {
        var analyzer = new CorpusSongAnalyzer(Path.Combine(_root, "analysis-cache", songName));
        var entry = new CorpusEntry(realSequencePath, audioPath, songName, CorpusProvenanceTag.ExternalReference, [], "");
        var analysis = await analyzer.AnalyzeAsync(entry, ct);

        var decoder = new TranscodingAudioDecoder(new WavAudioDecoder(), new LocalPathFfmpegInstaller(), Path.Combine(_root, "analysis-cache", songName, "wav-cache"));
        var song = await decoder.ProbeAsync(audioPath, ct);

        var fftSize = NextPowerOfTwo((int)Math.Round(song.SampleRate * (256.0 / 8000.0)));
        var rhythmSettings = new RhythmSettings(song.SampleRate, fftSize, fftSize / 2);
        var bandEnergy = await new BandEnergyAnalyzer(decoder).AnalyzeAsync(song, new BandEnergySettings(rhythmSettings, BassCutoffHz: 250, HighCutoffHz: 2000), ct);

        return new MusicModel(analysis.BeatGrid.SongId, song.DurationTicks, analysis.BeatGrid, analysis.Labels, analysis.Phrases, bandEnergy, VocalActivity: null);
    }

    private static int NextPowerOfTwo(int value)
    {
        var power = 1;
        while (power < value)
        {
            power <<= 1;
        }

        return power;
    }

    /// <summary>Same hand-authored, schema-valid 10-Look/3-palette Show Bible shape as <see cref="Phase6EndToEndRealTrackTests"/>'s own fixture (duplicated locally for the same reason that class's own doc comment gives).</summary>
    private static ShowBible BuildHandAuthoredShowBible()
    {
        string[] subjects = ["group:MiniTrees", "group:Matrixes", "group:Outline", "group:Arches", "Tree"];
        var palettes = new List<ShowBiblePalette>
        {
            new("warm-gold", "Warm Gold", [new ShowBiblePaletteColor("#FFC880", PaletteRole.Base), new ShowBiblePaletteColor("#FFFFFF", PaletteRole.Key)]),
            new("high-energy", "High Energy", [new ShowBiblePaletteColor("#FF0030", PaletteRole.Base), new ShowBiblePaletteColor("#00AEEF", PaletteRole.Accent)]),
            new("cool-blue", "Cool Blue", [new ShowBiblePaletteColor("#0030FF", PaletteRole.Base)]),
        };

        var looks = new List<Look>();
        for (var i = 0; i < 10; i++)
        {
            var subject = subjects[i % subjects.Length];
            var secondary = subjects[(i + 1) % subjects.Length];
            var energyCeiling = 0.15 + (0.08 * (i % 10));
            var layers = new List<LookLayer>
            {
                new(1, secondary, "Twinkle", LookLayer.RoleSecondary, new Dictionary<string, string>(), "Normal", "Bars", 0.3),
                new(2, subject, "On", LookLayer.RoleSubject, new Dictionary<string, string>(), "Normal", "Bars", 0.9),
            };

            if (energyCeiling >= ShowBibleValidator.PeakLookEnergyCeilingThreshold)
            {
                var tertiary = subjects[(i + 2) % subjects.Length];
                layers.Add(new LookLayer(0, tertiary, "Color Wash", LookLayer.RoleSupport, new Dictionary<string, string>(), "Additive", "Bars", 0.5));
            }

            looks.Add(new Look(
                Id: $"look-{i}",
                Name: $"Look {i}",
                Intent: $"Phase 30 baseline Look {i}.",
                EnergyCeiling: energyCeiling,
                Motion: i % 3 == 0 ? "hold" : "chase",
                PaletteRef: palettes[i % palettes.Count].Id,
                Subject: subject,
                Layers: layers));
        }

        var lookIds = looks.Select(l => l.Id).ToList();
        var content = new ShowBibleContent(
            Palettes: palettes,
            PropRoles: [new PropRoleAssignment("Tree", MusicalRole.Melody), new PropRoleAssignment("group:Matrixes", MusicalRole.Bass)],
            Looks: new LookLibrary(looks),
            EnergyArc: Enum.GetValues<SectionKind>().Select(k => new EnergyArcEntry(k, 0.5, 0.5, 2)).ToList(),
            PeakMoments: [new PeakMoment(lookIds[0], "The only Look reserved for this moment.")],
            SignatureGestures: [new SignatureGesture("Pulse", "A one-bar brightness pulse on the subject.")],
            Withholding: new WithholdingPlan([lookIds[^1]], "The final Look, held back until the last chorus."));

        return new ShowBible(Guid.NewGuid(), Guid.NewGuid(), content, DateTimeOffset.UtcNow);
    }
}
