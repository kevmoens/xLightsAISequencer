using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

public sealed class Phase29TimelineGenerationTests
{
    private const long Second = TimeSpan.TicksPerSecond;
    private static readonly string[] Props = ["Tree", "Outline", "Arch1", "Arch2", "Arch3", "Star"];

    [Theory]
    [InlineData("eligible", "rotating")]
    [InlineData("disabled", "none")]
    [InlineData("wrong-kind", "takeover")]
    [InlineData("legacy", "rotating")]
    public async Task WholeSongExportsSharedChorusAndPreservesOwnershipThroughReloadAndRepair(string corpus, string ownership)
    {
        var root = Path.Combine(Path.GetTempPath(), $"phase29-{Guid.NewGuid():N}");
        try
        {
            var song = new SongId(Guid.NewGuid());
            var labels = new[] { SectionKind.Chorus, SectionKind.Verse, SectionKind.Chorus, SectionKind.Bridge }
                .Select((kind, i) => new SectionLabelCandidate(Guid.NewGuid(), Guid.NewGuid(), i * 2, i * 2 + 2, kind, .95, [], false)).ToArray();
            var phrases = labels.SelectMany((s, i) => Enumerable.Range(0, 2).Select(p => new PhraseCandidate(Guid.NewGuid(),
                s.Id, i * 2 + p, i * 2 + p + 1, (4 * i + p * 2) * Second, (4 * i + p * 2 + 2) * Second, 1,
                PhraseBoundarySource.BarHypothesis, .95, [], "test", false))).ToArray();
            var sections = new SectionLabelAnalysis(Guid.NewGuid(), song, "test", Guid.NewGuid(), Guid.NewGuid(), "test",
                new SectionLabelSettings(.9, .5, "test"), labels);
            var phraseAnalysis = new PhraseSegmentationAnalysis(Guid.NewGuid(), song, "test", sections.VersionId, Guid.NewGuid(), null,
                "test", new PhraseSegmentationSettings(2, 1, 0, "test"), phrases);
            var motif = new MotifClusteringAnalysis(Guid.NewGuid(), song, "test", Guid.NewGuid(), Guid.NewGuid(), "test",
                new MotifClusteringSettings(8, .85, "test"), [new(Guid.NewGuid(), [phrases[0].Id, phrases[4].Id], phrases[0].Id, .95, [])], []);
            var beats = new BeatGrid(Guid.NewGuid(), song, "test", Guid.NewGuid(), Guid.NewGuid(), "test", new BeatGridSettings(4, 1000),
                new TempoHypothesis(120, 1, "4/4"), Enumerable.Range(0, 32)
                    .Select(i => new BeatMarker(Guid.NewGuid(), i * Second / 2, i % 4, i / 4, 1, false)).ToArray(), [], 0);
            var sustain = new NoteEvent(Guid.NewGuid(), 5 * Second, 9 * Second, 60, .85, .95, false);
            var energy = new EnergyEnvelope(Guid.NewGuid(), song, "test", "test", "test", new RhythmSettings(100, 64, 32),
                [new(0, 1600, .5f, .5f)]);
            var melody = new MelodyTrack(Guid.NewGuid(), "test", [sustain], []);
            var music = new MusicModel(song, 16 * Second, beats, sections, phraseAnalysis, null, null,
                Melody: melody, Motifs: motif, Energy: energy,
                EnergyTransitions: [new(13 * Second, EnergyTransitionKind.Buildup, .8f)]);
            var layout = new DisplayGraph("fixture", Props.Select(p => new DisplayModel(new ModelId(p), p, p, [], [], NodeCount: 100)).ToArray(),
                [new DisplayModelGroup("ArchPair", [new("Arch1", DisplayGroupMemberKind.Model), new("Arch2", DisplayGroupMemberKind.Model)])], []);
            var effects = RealXLightsFixture.SixCommonEffectNames;
            var vocabulary = new EffectVocabularyCatalog(effects.ToDictionary(n => n, n => new EffectVocabulary(n, "E_", []), StringComparer.Ordinal),
                new EffectVocabulary("Buffer", "B_", []), new EffectVocabulary("Blending", "T_", []), new EffectVocabulary("Color", "C_", []));
            var recipes = new EffectRecipeCatalog();
            GeneratedEffectRecipeFactory.RegisterAll(vocabulary, recipes);
            var look = new Look("base", "Base", "Test", .8, "hold", "gold", "Tree",
                Props.Select((p, i) => new LookLayer(i + 1, p, "On", i == 0 ? LookLayer.RoleSubject : LookLayer.RoleSupport,
                    null, "Normal", "Bars", .9)).ToArray());
            var bible = new ShowBible(Guid.NewGuid(), Guid.NewGuid(), new ShowBibleContent(
                [new("gold", "Gold", [new("#FFC880", PaletteRole.Base)]), new("blue", "Blue", [new("#40A0FF", PaletteRole.Base)])],
                [], new LookLibrary([look, look with { Id = "return", PaletteRef = "blue" }]),
                Enum.GetValues<SectionKind>().Select(k => new EnergyArcEntry(k, .5, .5, 2)).ToArray(), [], [], new WithholdingPlan([], "none")),
                DateTimeOffset.UtcNow);
            var sourceBeats = beats with
            {
                Beats = beats.Beats.Take(4).Select(b => b with { TimeTicks = b.TimeTicks * 2 }).ToArray(),
                SelectedTempo = new TempoHypothesis(60, 1, "4/4"),
            };
            var sourceLabels = sections with { Labels = [labels[0] with { EndBarIndexExclusive = 1,
                Kind = corpus == "wrong-kind" ? SectionKind.Outro : SectionKind.Chorus }] };
            var sourcePhrases = phraseAnalysis with { Phrases = [phrases[0] with { EndTicks = 4 * Second }] };
            var fingerprint = new SongFingerprintSet(Guid.NewGuid(), song, "source", Guid.NewGuid(), "test",
                new BarFingerprintSettings(12, "test"), [new BarFingerprint(0, new TimeWindow(0, 4 * Second), [.5], 1, false)]);
            var sourceEffects = new[]
            {
                new ParsedEffectInstance("Source Mass", "group", "On", 0, 2000, 0, 0, "", ""),
                new ParsedEffectInstance("Source Detail", "model", "Bars", 1000, 1500, 0, 0, "", ""),
            };
            var index = ExemplarIndexBuilder.Build([new CorpusSongAnalysis(new CorpusEntry(@"C:\fixture\source.xsq",
                @"C:\fixture\source.mp3", "Source", CorpusProvenanceTag.UserAuthored, [], "test"), sourceBeats,
                fingerprint, sourceLabels, sourcePhrases, EffectAligner.Align(sourceEffects, sourceBeats, fingerprint, sourceLabels, sourcePhrases))]);
            var exemplar = Assert.Single(index.Entries);
            index = corpus switch
            {
                "disabled" => ExemplarOverrides.ApplyOverrides(index, new Dictionary<string, bool> { [ExemplarOverrides.KeyFor(exemplar.Fingerprint)] = true }),
                "legacy" => index with { SchemaVersion = 0 },
                _ => index,
            };
            var settings = new ChoreographyGenerationSettings(layout, vocabulary, recipes, new EffectRecipeRegistry(recipes),
                new Dictionary<SectionKind, double>(), root);
            var repair = false;
            var reviseReturnLook = false;
            var bogus = $"exemplar:{labels[3].Id:N}:fake:g1";
            var hold = new SectionCue($"sustain-{sustain.Id:N}-start", "group:ArchPair", "On", IntensityTier.Full, LookLayer.RoleSubject,
                $"sustain-{sustain.Id:N}-end", Annotation: "seam", Kind: CueKind.Hold);
            var fake = new FakeChatCompletionClient(Enumerable.Repeat<Func<ChatCompletionRequest, ChatCompletionResult>>(request =>
            {
                var match = Regex.Match(request.UserPrompt, @"(?m)^SectionId: ([a-fA-F0-9-]+)");
                var id = Guid.Parse(match.Groups[1].Value);
                var at = Array.FindIndex(labels, x => x.Id == id);
                IReadOnlyList<SectionCue> cues = at switch
                {
                    1 when ownership == "none" => [hold],
                    1 when ownership == "takeover" => [hold,
                        new("beat-8", "group:ArchPair", "Bars", IntensityTier.Full, LookLayer.RoleSubject,
                            hold.HoldUntilAnchorId, Annotation: "explicit", Kind: CueKind.Takeover)],
                    1 => [hold, new("beat-4", "Arch3", "On", IntensityTier.Bright, LookLayer.RoleSupport, "beat-5", Annotation: "rotate-1"),
                        new("beat-6", "Star", "On", IntensityTier.Bright, LookLayer.RoleSupport, "beat-7", Annotation: "rotate-2")],
                    2 => [new("section-start", "Arch1", "On", IntensityTier.Bright, LookLayer.RoleSupport, Annotation: "collision")],
                    3 => [new("energy-buildup-1", "Star", "On", IntensityTier.Full, LookLayer.RoleSupport, "beat-4",
                        Annotation: repair ? "revised-shift" : bogus, Targets: ["Star", "Arch3", "Outline"])],
                    _ => [],
                };
                var timeline = new SectionTimeline(id, at == 2 && !reviseReturnLook ? "return" : "base", Cues: cues, FillerEffects: effects,
                    PhraseVariations: at == 1 ? [new(PhraseIndex: 1), new(PhraseIndex: 2)] : null);
                return new ChatCompletionResult(JsonSerializer.Serialize(timeline), new AiUsage(10, 5, null, null));
            }, 40));
            var knowledge = new DirectorKnowledgeContext(vocabulary, new CraftRules(DateTimeOffset.UtcNow, .7, 3, [], []), index);
            var pipeline = new ChoreographyGenerationPipeline(knowledge, StylePresetCatalog.TraditionalClassicId, null, null, fake, "scripted", 2000);
            var summary = LayoutSummary.Build(layout);
            var projectSong = Guid.NewGuid();
            var report = await pipeline.GenerateAsync(music, summary, settings, projectSong, bible, CancellationToken.None);
            var before = Read(report.Export.Path);
            var xsq = new XsqStructuralReader().Read(report.Export.Path);
            var tree = Assert.Single(xsq.ElementEffects, e => e.Name == "Tree");
            var hooks = tree.Effects.Where(e => e.Name == "On" && e.StartTimeMs is 0 or 8000 &&
                e.EndTimeMs - e.StartTimeMs == (corpus == "eligible" ? 1000 : e.StartTimeMs == 0 ? 300 : 475)).ToArray();
            Assert.Equal(2, hooks.Length);
            Assert.Equal(xsq.ColorPalettes[hooks[0].Palette], xsq.ColorPalettes[hooks[1].Palette]);
            Assert.Contains("#FFC880", xsq.ColorPalettes[hooks[0].Palette], StringComparison.OrdinalIgnoreCase);
            var heldEnd = ownership == "takeover" ? 7500 : 9000;
            Assert.Contains(before, p => p is { Target: "ArchPair", Effect: "On", Start: 5000 } && p.End == heldEnd);
            Assert.Equal(ownership == "takeover" ? 2 : 1, before.Count(p => p.Target == "ArchPair" && p.Start < 9000 && p.End > 5000));
            if (ownership == "takeover")
            {
                Assert.Contains(before, p => p is { Target: "ArchPair", Effect: "Bars", Start: 7500, End: 9000 });
            }
            Assert.DoesNotContain(before, p => (p.Target is "Arch1" or "Arch2") && p.Start < 9000 && p.End > 5000);
            Assert.DoesNotContain(before, p => p.Target == "Outline" && p.Start >= 5000 && p.Start < 8000);
            Assert.Equal(ownership == "rotating" ? 2 : 0,
                report.CueDecisions!.Count(c => c.Cue.Annotation?.StartsWith("rotate-") == true && c.Placements.Count == 1));
            Assert.Contains(fake.Requests, r => r.UserPrompt.Contains("Incoming holds owned by an earlier section") &&
                r.UserPrompt.Contains("group:ArchPair"));
            Assert.Contains(fake.Requests, r => r.UserPrompt.Contains($"hold-{sustain.Id:N}"[..13] + "-start"));
            Assert.Empty(Assert.Single(report.CueDecisions!, c => c.Cue.Annotation == "collision").Placements);
            Assert.Equal(3, Assert.Single(report.CueDecisions!, c => c.Cue.Annotation == bogus).Placements.Count);
            Assert.Equal(new[] { "Arch3", "Outline", "Star" }, before
                .Where(p => p is { Effect: "On", Start: 13000, End: 13475 })
                .Select(p => p.Target).Order(StringComparer.Ordinal).ToArray());
            Assert.DoesNotContain(report.ExemplarDecisions!, e => e.ExemplarId == "fake");

            var signatures = report.CueDecisions!.Where(c => c.Cue.Annotation?.StartsWith("chorus-signature:") == true).ToArray();
            if (corpus == "eligible")
            {
                Assert.Empty(signatures);
                Assert.Equal(2, report.ExemplarDecisions!.Count(e => e.ExemplarId == exemplar.Id && e.Status == "Applied"));
                var accent = Assert.Single(before, p => p is { Target: "Outline", Effect: "Bars", Start: 500, End: 750 });
                var returnAccent = Assert.Single(before, p => p is { Target: "Outline", Effect: "Bars", Start: 8500, End: 8750 });
                Assert.Contains(before, p => p.Target == "Outline" && p.Effect == "On" &&
                    p.Layer > Math.Max(accent.Layer, returnAccent.Layer));
                Assert.Contains(before, p => p is { Target: "Tree", Effect: "On", Start: 0, End: 1000 });
                Assert.Contains(before, p => p is { Target: "Tree", Effect: "On", Start: 8000, End: 9000 });
                Assert.Contains(before, p => p is { Target: "Star", Effect: "Bars", Start: 8500, End: 8750 });
                Assert.DoesNotContain(before, p => p is { Target: "Star", Effect: "Bars", Start: 500, End: 750 });
            }
            else
            {
                Assert.DoesNotContain(report.ExemplarDecisions!, e => e.Status == "Applied");
                Assert.Equal(2, signatures.Length);
                Assert.All(signatures, s => Assert.Single(s.Placements));
                Assert.Equal(signatures[0].Cue.PaletteSlot, signatures[1].Cue.PaletteSlot);
                Assert.NotEqual(signatures[0].Cue.Intensity, signatures[1].Cue.Intensity);
                Assert.Contains(before, p => p is { Target: "Tree", Effect: "On", Start: 0, End: 300 });
                Assert.Contains(before, p => p is { Target: "Tree", Effect: "On", Start: 8000, End: 8475 });
            }

            if (corpus != "eligible")
            {
                return;
            }
            var scriptsDir = Path.Combine(root, "scripts");
            var persisted = await ChoreographyGenerationPipeline.LoadPersistedTimelinesAsync(scriptsDir, projectSong, CancellationToken.None);
            Assert.Equal("seam", persisted[labels[1].Id].CuesOrEmpty[0].Annotation);
            var scripts = await ChoreographyGenerationPipeline.LoadPersistedScriptsAsync(scriptsDir, projectSong, CancellationToken.None);
            repair = true;
            var repaired = await pipeline.ApplyPostProcessingPromptAsync(music, summary, settings, projectSong, bible, scripts,
                "at 0:13 add a flash", CancellationToken.None);
            Assert.Contains(Read(repaired.Export.Path), p => p is { Target: "ArchPair", Start: 5000, End: 9000 });
            Assert.Equal(3, Assert.Single(repaired.CueDecisions!, c => c.Cue.Annotation == "revised-shift").Placements.Count);
            Assert.DoesNotContain(repaired.CueDecisions!, c => c.Cue.Annotation == bogus);
            Assert.Equal(2, repaired.ExemplarDecisions!.Count(e => e.Status == "Applied"));
            reviseReturnLook = true;
            var revisedChorus = await pipeline.ApplyPostProcessingPromptAsync(music, summary, settings, projectSong, bible, repaired.Scripts,
                "at 0:08 accent the return", CancellationToken.None);
            Assert.Equal("base", revisedChorus.Scripts[labels[2].Id].LookId);
            var revisedPlacements = Read(revisedChorus.Export.Path);
            Assert.Contains(revisedPlacements, p => p is { Target: "Tree", Effect: "On", Start: 8000, End: 9000 });
            Assert.Contains(revisedPlacements, p => p is { Target: "Star", Effect: "Bars", Start: 8500, End: 8750 });
            Assert.Equal(2, revisedChorus.ExemplarDecisions!.Count(e => e.ExemplarId == exemplar.Id && e.Status == "Applied"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed record Placement(string Target, string Effect, int Layer, int Start, int End);

    private static Placement[] Read(string path) => XDocument.Load(path).Root!.Element("ElementEffects")!.Elements("Element")
        .Where(e => (string?)e.Attribute("type") != "timing")
        .SelectMany(e => e.Elements("EffectLayer").SelectMany((layer, index) => layer.Elements("Effect").Select(f =>
            new Placement((string)e.Attribute("name")!, (string)f.Attribute("name")!, index,
                (int)f.Attribute("startTime")!, (int)f.Attribute("endTime")!)))).ToArray();
}
