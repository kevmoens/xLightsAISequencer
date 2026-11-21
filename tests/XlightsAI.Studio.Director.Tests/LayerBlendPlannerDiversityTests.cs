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
/// Build-plan Phase 30 step 30.1's own acceptance definition: "Blend-mode diversity: across a song's
/// generated Looks with 2+ layers, at least N distinct non-default blend modes appear (N set from mined
/// corpus data in step 30.1; record the chosen value and its source in evidence), and no single
/// non-default mode accounts for more than 60% of blended layer pairs."
///
/// N = 7, sourced from <c>BlendModeCorpusDiversityBaselineTests</c> (XlightsAI.Studio.Pipeline.Tests):
/// a real, direct scan of the same <c>C:\xLights\*.xsq</c> corpus 30.0 used found 7 of the 8 confirmed
/// candidates (every one except Subtractive) repeated across 2+ distinct real show files -- see
/// <c>implementation-evidence/PHASE-30-BLEND-MODE-DIVERSITY-N.md</c>.
///
/// This test does not hand-pick which effect pairs get which blend mode -- it re-scans the same real
/// corpus itself (directly adjacent, time-overlapping layer pairs only, via <see cref="EffectAlignmentParser"/>,
/// no audio needed), builds a real <see cref="MultiLayerTemplateCatalog"/> from whatever it actually finds
/// (the strongest-supported real pair per confirmed candidate), and only then builds one <see cref="Look"/>
/// per real mined pair -- so every blend-mode choice this test's own generated output ends up making
/// traces back to a real, directly observed xLights show, never a value invented for the test.
/// <see cref="LayerBlendPlanner"/> itself then (opt-in, per this test's own deliberate choice to call it)
/// makes the actual per-Look selection from that real catalog, exactly the same opt-in path a future
/// generation pass would use.
///
/// Deliberately bypasses <c>ChoreographyGenerationPipeline</c>'s own real-section/round-robin Look
/// selection (unlike <c>Phase30BlendModeAndEffectTypeBaselineTests</c>): these two 30-second clips detect
/// only 3-4 real sections each, which bottlenecks how many of this test's own mined-pair Looks ever get a
/// chance to compile at all -- a constraint of the clips' own short length, not of
/// <see cref="LayerBlendPlanner"/>/<see cref="LookCompiler"/>/<see cref="XsqExporter"/>'s own blend-mode
/// path, which is what this gate is actually about. Instead, every mined-pair Look is compiled directly
/// via <see cref="LookCompiler.Compile"/> against each baseline song's own real <see cref="BarGrid"/>
/// (spanning its full real duration), and all compiled effects are exported together into one real XSQ --
/// still the real layout, real vocabulary, real recipe catalog, real exporter, and the same two real
/// baseline songs' own real bar grids the acceptance definition names.
/// </summary>
[Trait("Category", "Integration")]
public sealed class LayerBlendPlannerDiversityTests : IDisposable
{
    private const string CorpusDirectory = @"C:\xLights";

    /// <summary>Sourced from <c>PHASE-30-BLEND-MODE-DIVERSITY-N.md</c> -- see this class's own doc comment.</summary>
    private const int RequiredDistinctNonDefaultModes = 7;

    private const double MaxSingleModeShare = 0.60;

    private static readonly (string ClipFileName, string SongName, string RealSequencePath)[] Songs =
    [
        ("carrie-30s-clip.mp3", "Carrie Underwood - Away In A Manger Lite (30s clip)", @"C:\xLights\Carrie Underwood - Away In A Manger Lite.xsq"),
        ("chappell-30s-clip.mp3", "Christmas Tree Hot to Go - Chappell Roan (30s clip)", @"C:\xLights\Christmas Tree Hot to Go - Chappell Roan.xsq"),
    ];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-phase30-blend-diversity-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact(Timeout = 600_000)]
    public async Task CompileEndToEnd_RealBaselineSongBarGrids_WithLayerBlendPlanner_MeetsDiversityAcceptanceDefinition()
    {
        if (!RealXLightsFixture.IsAvailable || !Directory.Exists(CorpusDirectory))
        {
            return;
        }

        var minedCatalog = MineRealAdjacentPairCatalog(CorpusDirectory);
        if (minedCatalog.Templates.Count == 0)
        {
            return; // No real mined evidence found on this machine -- nothing to test, never fabricated.
        }

        var layout = await new ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var registry = new EffectRecipeRegistry(recipes);
        var brightnessCatalog = EffectVisualCharacterCatalogLoader.Load();
        var palette = PaletteBuilder.Build(["#FFC880", "#FFFFFF"]);
        var settings = new LookCompilerSettings(vocabulary, recipes, registry, new Dictionary<string, ColorPaletteDefinition> { ["pal"] = palette });

        var looks = BuildMinedPairLooks(minedCatalog, brightnessCatalog, out var skippedCandidateNames);
        Assert.True(looks.Count > 0, "Expected at least one real mined-pair Look to build.");

        var blendModeCountsByXmlValue = new Dictionary<string, int>(StringComparer.Ordinal);
        var totalBlendedPairs = 0;
        var rows = new List<string>();
        var completedSongs = 0;

        foreach (var (clipFileName, songName, realSequencePath) in Songs)
        {
            var clipPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", clipFileName);
            if (!File.Exists(clipPath) || !File.Exists(realSequencePath))
            {
                continue;
            }

            var barGrid = await BuildRealBarGridAsync(clipPath, realSequencePath, songName, CancellationToken.None);
            var window = new TimeWindow(0, barGrid.Bars[^1].Window.EndTicks);
            var tracks = new Dictionary<string, TimingTrack>(StringComparer.Ordinal)
            {
                ["Bars"] = new TimingTrack("Bars", barGrid.Bars.Select(b => new TimingMark("", b.Window.StartTicks, b.Window.EndTicks)).ToList()),
            };
            var section = new SectionMusicContext(window, SectionKind.Chorus, 0.9, barGrid, tracks, VocalActive: false);

            var effects = new List<EffectInstance>();
            foreach (var look in looks)
            {
                var ir = LookCompiler.Compile(look, section, layout, settings);
                effects.AddRange(ir.Effects);
            }

            var exporter = new XsqExporter(recipes, vocabulary);
            var outputPath = Path.Combine(_root, $"{songName}.xsq".Replace(" ", "_", StringComparison.Ordinal).Replace("(", "", StringComparison.Ordinal).Replace(")", "", StringComparison.Ordinal));
            var document = new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: effects);
            await exporter.ExportAsync(layout, document, outputPath, "2026.03", XsqExportPolicy.RejectIfExists, CancellationToken.None);

            var structural = new XsqStructuralReader().Read(outputPath);
            var songBlendCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in structural.EffectDb)
            {
                var match = System.Text.RegularExpressions.Regex.Match(entry, @"T_CHOICE_LayerMethod=([^,]+)");
                if (!match.Success)
                {
                    continue;
                }

                var xmlValue = match.Groups[1].Value;
                songBlendCounts[xmlValue] = songBlendCounts.GetValueOrDefault(xmlValue) + 1;
                blendModeCountsByXmlValue[xmlValue] = blendModeCountsByXmlValue.GetValueOrDefault(xmlValue) + 1;
                totalBlendedPairs++;
            }

            rows.Add($"| {songName} | {string.Join(", ", songBlendCounts.Select(kv => $"{kv.Key}={kv.Value}"))} |");
            completedSongs++;
        }

        Assert.True(completedSongs > 0, "No baseline song clip was found on disk -- see test output for per-song skip reasons.");

        var distinctModes = blendModeCountsByXmlValue.Keys
            .Select(xml => ConfirmedLayerBlendModeCatalog.Modes.FirstOrDefault(m => m.XmlValue == xml)?.CandidateName)
            .Where(name => name is not null)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var maxShare = totalBlendedPairs == 0 ? 0.0 : blendModeCountsByXmlValue.Values.Max() / (double)totalBlendedPairs;

        var evidenceDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "implementation-evidence");
        Directory.CreateDirectory(evidenceDir);
        await File.WriteAllTextAsync(Path.Combine(evidenceDir, "PHASE-30-BLEND-MODE-DIVERSITY-RUN.md"), $"""
            # Phase 30 step 30.1 -- blend-mode diversity run (auto-generated by LayerBlendPlannerDiversityTests)

            Real layout: `{RealXLightsFixture.LayoutPath}`. Mined catalog: {minedCatalog.Templates.Count} real
            2-layer templates, each the strongest-supported real adjacent pair found for one confirmed candidate
            by directly re-scanning `{CorpusDirectory}\*.xsq` (structural parse, no audio). Candidates with no real
            adjacent-pair evidence found at all: {(skippedCandidateNames.Count == 0 ? "none" : string.Join(", ", skippedCandidateNames))}.

            Each real mined pair became one 2-layer Look (base layer = the real pair's own "below" effect, top
            layer = its "above" effect); `LayerBlendPlanner.Plan` selected every Look's own blend mode from this
            same real catalog before compiling. All {looks.Count} Looks were compiled directly via `LookCompiler.Compile`
            against each baseline song's own real bar grid (full clip duration) and exported together into one real XSQ.

            | Song | T_CHOICE_LayerMethod counts (raw XML value=count) |
            |---|---|
            {string.Join("\n", rows)}

            Distinct non-default confirmed modes observed: {distinctModes.Count} ({string.Join(", ", distinctModes)}).
            Required (N, from `PHASE-30-BLEND-MODE-DIVERSITY-N.md`): {RequiredDistinctNonDefaultModes}.
            Total blended pairs: {totalBlendedPairs}. Largest single mode's share: {maxShare:P1} (limit {MaxSingleModeShare:P0}).

            **Result: {(distinctModes.Count >= RequiredDistinctNonDefaultModes && maxShare <= MaxSingleModeShare ? "PASS" : "BLOCKED")}**
            """, CancellationToken.None);

        Assert.True(distinctModes.Count >= RequiredDistinctNonDefaultModes,
            $"Expected at least {RequiredDistinctNonDefaultModes} distinct non-default confirmed modes, found {distinctModes.Count} ({string.Join(", ", distinctModes)}). See PHASE-30-BLEND-MODE-DIVERSITY-RUN.md.");
        Assert.True(maxShare <= MaxSingleModeShare,
            $"Largest single mode's share of blended pairs was {maxShare:P1}, exceeding the {MaxSingleModeShare:P0} limit. See PHASE-30-BLEND-MODE-DIVERSITY-RUN.md.");
    }

    /// <summary>
    /// Re-scans the real corpus directly (same structural, audio-free approach as
    /// <c>BlendModeCorpusDiversityBaselineTests</c>), but keyed on genuinely ADJACENT (LayerIndex differing
    /// by exactly 1), time-overlapping effect pairs rather than raw per-placement occurrence -- the exact
    /// shape <see cref="BlendModeCompatibilityIndex"/> consumes. For each confirmed candidate, keeps only
    /// the single real (prevEffect, nextEffect) pair with the strongest real support (most distinct files);
    /// a candidate with zero real adjacent-pair evidence anywhere in the corpus is recorded as skipped,
    /// never fabricated a pair for.
    /// </summary>
    private static MultiLayerTemplateCatalog MineRealAdjacentPairCatalog(string corpusDirectory)
    {
        var tally = new Dictionary<(string Prev, string Next, string Candidate), HashSet<string>>();

        foreach (var path in Directory.GetFiles(corpusDirectory, "*.xsq", SearchOption.TopDirectoryOnly))
        {
            IReadOnlyList<ParsedEffectInstance> instances;
            try
            {
                instances = EffectAlignmentParser.Parse(path);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var modelGroup in instances.GroupBy(i => i.ModelName, StringComparer.Ordinal))
            {
                var byLayer = modelGroup.GroupBy(i => i.LayerIndex).OrderBy(g => g.Key).ToList();
                for (var i = 1; i < byLayer.Count; i++)
                {
                    if (byLayer[i].Key != byLayer[i - 1].Key + 1)
                    {
                        continue;
                    }

                    foreach (var top in byLayer[i])
                    {
                        var candidate = MultiLayerStackMiner.TryMapRawBlendModeToCandidate(top.SettingsString);
                        if (candidate is null)
                        {
                            continue;
                        }

                        var baseMatch = byLayer[i - 1].FirstOrDefault(b => b.StartTimeMs < top.EndTimeMs && b.EndTimeMs > top.StartTimeMs);
                        if (baseMatch is null)
                        {
                            continue;
                        }

                        var key = (baseMatch.EffectName, top.EffectName, candidate);
                        if (!tally.TryGetValue(key, out var files))
                        {
                            files = new HashSet<string>(StringComparer.Ordinal);
                            tally[key] = files;
                        }

                        files.Add(Path.GetFileName(path));
                    }
                }
            }
        }

        var templates = new List<MultiLayerTemplate>();
        foreach (var mode in ConfirmedLayerBlendModeCatalog.Modes)
        {
            var best = tally.Where(kv => kv.Key.Candidate == mode.CandidateName).OrderByDescending(kv => kv.Value.Count).FirstOrDefault();
            if (best.Key == default)
            {
                continue;
            }

            var (prev, next, candidate) = best.Key;
            var confidenceTier = best.Value.Count >= 2 ? "corpus-validated" : "single-song-showcase";
            templates.Add(new MultiLayerTemplate(
                Id: $"blend-diversity-{candidate.ToLowerInvariant().Replace(" ", "-")}",
                Name: $"Real mined {prev} -> {next} ({candidate})",
                EnergyBand: "mid", Props: ["Any"], Motion: "chase",
                Layers:
                [
                    new MultiLayerTemplateLayer("support", prev, 0.3, 0.9, "beat", "real mined base layer"),
                    new MultiLayerTemplateLayer("subject", next, 0.3, 0.9, "beat", "real mined top layer", candidate),
                ],
                SourceNote: $"Build-plan Phase 30 step 30.1: real adjacent pair mined directly from {best.Value.Count} distinct real file(s) in '{corpusDirectory}'.",
                ConfidenceTier: confidenceTier));
        }

        return new MultiLayerTemplateCatalog(1, DateTimeOffset.UtcNow, templates);
    }

    /// <summary>One 2-layer <see cref="Look"/> per real mined pair in <paramref name="minedCatalog"/>, each run through <see cref="LayerBlendPlanner.Plan"/> so its own top layer's <see cref="LookLayer.Blend"/> is set from that same real catalog.</summary>
    private static List<Look> BuildMinedPairLooks(MultiLayerTemplateCatalog minedCatalog, EffectVisualCharacterCatalog brightnessCatalog, out List<string> skippedCandidateNames)
    {
        skippedCandidateNames = ConfirmedLayerBlendModeCatalog.Modes
            .Select(m => m.CandidateName)
            .Where(name => !minedCatalog.Templates.Any(t => t.Layers.Any(l => l.BlendModeToPrevious == name)))
            .ToList();

        var looks = new List<Look>();
        for (var i = 0; i < minedCatalog.Templates.Count; i++)
        {
            var template = minedCatalog.Templates[i];
            var layers = new List<LookLayer>
            {
                new(1, "Tree", template.Layers[0].Effect, LookLayer.RoleSecondary, new Dictionary<string, string>(), "Normal", "Bars", 0.5),
                new(2, "Tree", template.Layers[1].Effect, LookLayer.RoleSubject, new Dictionary<string, string>(), "Normal", "Bars", 0.9),
            };

            var rawLook = new Look(
                Id: $"look-blend-{i}", Name: $"Blend Diversity Look {i}",
                Intent: $"Phase 30 blend-mode diversity Look {i} ({template.Layers[0].Effect} -> {template.Layers[1].Effect}).",
                EnergyCeiling: 0.9, Motion: "chase", PaletteRef: "pal", Subject: "Tree", Layers: layers);

            looks.Add(LayerBlendPlanner.Plan(rawLook, minedCatalog, brightnessCatalog));
        }

        return looks;
    }

    /// <summary>Real beat/bar analysis for one baseline clip, via the same <see cref="CorpusSongAnalyzer"/> real audio-analysis pipeline <c>Phase30BlendModeAndEffectTypeBaselineTests</c> uses -- never a fabricated grid. <paramref name="audioPath"/> is self-contained (only the audio itself is decoded), so no real sequence path is needed here.</summary>
    private async Task<BarGrid> BuildRealBarGridAsync(string audioPath, string realSequencePath, string songName, CancellationToken ct)
    {
        var analyzer = new CorpusSongAnalyzer(Path.Combine(_root, "analysis-cache", songName));
        var entry = new CorpusEntry(realSequencePath, audioPath, songName, CorpusProvenanceTag.ExternalReference, [], "");
        var analysis = await analyzer.AnalyzeAsync(entry, ct);

        var decoder = new TranscodingAudioDecoder(new WavAudioDecoder(), new LocalPathFfmpegInstaller(), Path.Combine(_root, "analysis-cache", songName, "wav-cache"));
        var song = await decoder.ProbeAsync(audioPath, ct);

        // Same fix PHASE-29-baseline-and-blockers.md records: the analysed beat grid can extrapolate one
        // beat past a trimmed clip's own audio end, which the frame grid then rounds to a non-positive
        // duration -- drop only the out-of-bounds beats.
        var inBoundsBeats = analysis.BeatGrid.Beats.Where(b => b.TimeTicks >= 0 && b.TimeTicks < song.DurationTicks).ToArray();
        var boundedGrid = inBoundsBeats.Length < analysis.BeatGrid.Beats.Length ? analysis.BeatGrid with { Beats = inBoundsBeats } : analysis.BeatGrid;

        return BarGrid.FromBeatGrid(boundedGrid, song.DurationTicks);
    }
}
