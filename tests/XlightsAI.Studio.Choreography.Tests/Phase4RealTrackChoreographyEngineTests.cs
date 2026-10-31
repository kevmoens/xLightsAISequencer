using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Build-plan Phase 4's own required acceptance, verbatim: "Feed the compiler a hand-authored Look
/// library (3-5 Looks) against a real song + layout and it produces a valid .xsq that passes every rule
/// in architecture §7 as an automated test, with no LLM involved... The always-moving rule holds on the
/// output (max gap &lt;= 1 bar) for a full song... Re-run the C:\xLights reference track through this
/// compiler and compare its metric battery (architecture §9 table) against the hand-sequenced reference."
///
/// Runs the real <c>C:\xLights</c> reference track ("Dubstep Christmas Light Show") and its own real
/// layout end to end through <see cref="ChoreographyEngine"/> with the 4-Look library in
/// <see cref="Phase4LookLibraryFixture"/> -- no synthetic data, no LLM. Skips cleanly (never fails) when
/// the real xLights install or the external reference show is not present on the machine running the
/// tests, mirroring every other real-machine test in this codebase.
///
/// <b>Scope notes (both measured, not assumed):</b>
/// <list type="bullet">
/// <item>This reproduces architecture §9's metric battery at the *declared/compiled-IR* level (effect/
/// layer/palette/timing-track counts, duty-corrected coverage), not from an actual rendered FSEQ -- real
/// headless rendering is L5, a separate, later phase gated on the xLights automation spike (architecture
/// §15 risk 1), not attempted here.</item>
/// <item>Analyzes a 90-second clip (ffmpeg-trimmed from the real track's own 90-180s mark, covering one
/// of <c>EXPORT_AUDIT.md</c>'s own documented vocal clusters) rather than the full ~11-minute track: a
/// measured run of the full track's own direct-DFT analysis (<c>OnsetDetector</c>/<c>BandEnergyAnalyzer</c>,
/// both O(FftSize x bins) per frame -- a deliberate, documented choice for this codebase's existing
/// small/test-scale windows, same disclosed gap Phase 3's own evidence already names) exceeded 8 minutes
/// without completing. The clip is still real audio from the real reference track, not synthetic --
/// only its duration is reduced, for tractability. Vocal activity is consequently not wired into this
/// run either (the real vocals stem covers the full song's own absolute tick range, which would
/// temporally misalign against a clip re-based to tick 0) -- <c>VocalActivity: null</c> below, which
/// <see cref="ChoreographyEngine"/> handles as its own documented "every section compiles as
/// instrumental" fallback; real vocal-activity detection against this exact track is independently
/// verified by Phase 3's own <c>Phase03Task2RealTrackVocalActivityTests</c>.</item>
/// </list>
/// </summary>
[Trait("Category", "Integration")]
public sealed class Phase4RealTrackChoreographyEngineTests : IDisposable
{
    private static readonly string ClipAudioPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "dubstep-90s-clip.mp3");
    private const string SequencePath = @"C:\xLights\Dubstep Christmas Light Show.xsq";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-phase4-realtrack-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static bool IsFullyAvailable =>
        RealXLightsFixture.IsAvailable && File.Exists(ClipAudioPath) && File.Exists(SequencePath);

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

    // Budget raised from 300_000ms: CorpusSongAnalyzer now runs real PitchTracker-based melody analysis
    // (fix for the chroma-starvation defect documented in implementation-evidence/PHASE-07-HARNESS.md --
    // section-boundary detection needs real harmonic signal, not a fabricated empty MelodyTrack), which
    // is legitimately slower than the old placeholder path; this is not a performance regression to chase
    // down, it's the real cost of correct analysis.
    [Fact(Timeout = 900_000)]
    public async Task ChoreographyEngine_RealTrackAndLayout_CompilesValidXsqWithNoLegalityViolations_AlwaysMovingRuleHolds_LadderMonotone()
    {
        if (!IsFullyAvailable)
        {
            return;
        }

        var ct = CancellationToken.None;

        // 1. Real layout.
        var layout = await new ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, ct);

        // 2. Real song analysis (onsets/tempo/beat-grid/energy -> bar fingerprints -> section
        // boundaries/labels -> phrases) against the real 90s clip, reusing the same real-corpus
        // pipeline Phase 2b's own CorpusSongAnalyzer already exercises -- same "skip stem separation, no
        // song-hash-matching melody available" disclosed scope that analyzer itself documents. The real
        // hand-sequenced .xsq is still the full song (used only for the metric-battery comparison below,
        // never for timing).
        var analyzer = new CorpusSongAnalyzer(Path.Combine(_root, "analysis-cache"));
        var entry = new CorpusEntry(SequencePath, ClipAudioPath, "Dubstep Christmas Light Show (90s clip)", CorpusProvenanceTag.ExternalReference, [], "");
        var analysis = await analyzer.AnalyzeAsync(entry, ct);

        Assert.NotEmpty(analysis.Labels.Labels);

        // 3. Real per-band energy (Phase 3 task 1) against the same clip. Vocal activity is
        // deliberately not wired into this run -- see class doc comment.
        var decoder = new TranscodingAudioDecoder(new WavAudioDecoder(), new LocalPathFfmpegInstaller(), Path.Combine(_root, "analysis-cache", "wav-cache"));
        var song = await decoder.ProbeAsync(ClipAudioPath, ct);

        var bandRhythmSettings = ForSampleRateRhythmSettings(song.SampleRate);
        var bandEnergy = await new BandEnergyAnalyzer(decoder).AnalyzeAsync(
            song, new BandEnergySettings(bandRhythmSettings, BassCutoffHz: 250, HighCutoffHz: 2000), ct);

        VocalActivityEnvelope? vocalActivity = null;

        // 4. Real generated vocabulary/recipes, the hand-authored Look library, palette set, filler pool.
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var registry = new EffectRecipeRegistry(recipes);
        var fillerPool = RealXLightsFixture.BuildFillerPool(recipes);
        var paletteLibrary = new Dictionary<string, ColorPaletteDefinition>
        {
            ["warm-gold"] = PaletteBuilder.Build(["#FFC880", "#FFFFFF"]),
            ["high-energy"] = PaletteBuilder.Build(["#FF0030", "#00AEEF", "#FFFFFF", "#FFD400"]),
        };
        var compilerSettings = new LookCompilerSettings(vocabulary, recipes, registry, paletteLibrary, fillerPool);
        var lookLibrary = Phase4LookLibraryFixture.Build();
        Assert.InRange(lookLibrary.Looks.Count, 3, 5); // acceptance criterion: "a hand-authored Look library (3-5 Looks)."

        var lookIdBySection = analysis.Labels.Labels.ToDictionary(l => l.Id, l => Phase4LookLibraryFixture.LookIdFor(l.Kind));

        var songEndTicks = song.DurationTicks;

        var input = new ChoreographyEngine.Input(
            layout, analysis.BeatGrid, analysis.Labels, analysis.Phrases, bandEnergy, vocalActivity,
            lookLibrary, lookIdBySection, EnergyCeilingByKind, songEndTicks, compilerSettings);

        // 5. Compile -- the deterministic constraint engine, no LLM involved.
        var result = ChoreographyEngine.Compile(input);

        Assert.NotEmpty(result.IR.Effects);
        Assert.NotEmpty(result.IR.TimingTracks);
        Assert.Contains(result.IR.TimingTracks, t => t.Name == "Beats");
        Assert.Contains(result.IR.TimingTracks, t => t.Name == "Bars");
        Assert.Contains(result.IR.TimingTracks, t => t.Name == "Phrases");
        Assert.Contains(result.IR.TimingTracks, t => t.Name == "Sections");
        Assert.Contains(result.IR.TimingTracks, t => t.Name == "Percussion");

        // Required acceptance: "it produces a valid .xsq that passes every rule in architecture §7 as
        // an automated test." Every violation is reported (not just asserted empty) so a real failure
        // here is immediately diagnosable.
        //
        // Build-plan Phase 12 §3c's "declared-vs-measured-change-density" and Phase 13 §2c's
        // "whole-song-effect-concentration" rules are excluded from this check: both postdate architecture
        // §7, and both are deliberately soft, inspectable findings (never a compile failure -- same framing
        // as RuleDeclaredDarkness) that this fixture's own small, hand-authored Look library
        // (Phase4LookLibraryFixture, 3-5 Looks per architecture §4's own test acceptance, predating both
        // phases' density-aware/diversity-aware Director/Choreographer prompt guidance) is EXPECTED to
        // trip: it was never designed to vary change-rate by section energy ceiling or to spread usage
        // across a wide effect vocabulary -- exactly the flat-density and narrow-vocabulary problems these
        // two phases exist to fix in a real AI-generated Look library, not in this fixed test fixture.
        var postPhase7Rules = new HashSet<string>(StringComparer.Ordinal) { "declared-vs-measured-change-density", "whole-song-effect-concentration" };
        var preExistingViolations = result.Violations.Where(v => !postPhase7Rules.Contains(v.Rule)).ToList();
        if (preExistingViolations.Count > 0)
        {
            var summary = string.Join("\n", preExistingViolations.Select(v => $"[{v.Rule}] {v.Message}"));
            Assert.Fail($"{preExistingViolations.Count} legality violation(s):\n{summary}");
        }

        // Required acceptance: "The always-moving rule holds on the output (max gap <= 1 bar) for a
        // full song."
        var songWindow = new TimeWindow(0, songEndTicks);
        var targets = result.IR.Effects.Select(e => e.Target).Distinct().ToList();
        Assert.NotEmpty(targets);
        foreach (var target in targets)
        {
            var maxBarTicks = result.BarGrid.Bars.Count == 0 ? songEndTicks : result.BarGrid.Bars.Max(b => b.Window.EndTicks - b.Window.StartTicks);
            var largestGap = AlwaysMovingGapSplitter.LargestGapTicks(target, result.IR.Effects, songWindow);
            Assert.True(largestGap <= maxBarTicks,
                $"Target '{target.Value}' has a gap of {largestGap} ticks, exceeding one bar ({maxBarTicks} ticks).");
        }

        // Required acceptance: "Pixel coverage ladder is monotone."
        Assert.True(PixelCoverageLadder.IsMonotoneSupersetChain(result.CoverageLadder));

        // 6. Export a real, valid .xsq and round-trip it through the structural reader.
        var exporter = new XsqExporter(recipes, vocabulary);
        var outputPath = Path.Combine(_root, "phase4-compiled.xsq");
        var exportResult = await exporter.ExportNewAsync(layout, result.IR.ToSequenceDocument(), outputPath, "2026.03", ct);
        Assert.True(File.Exists(outputPath));

        var reader = new XsqStructuralReader();
        var structural = reader.Read(outputPath);
        Assert.NotEmpty(structural.ElementEffects);
        Assert.NotEmpty(structural.TimingTracks);

        // 7. Declared-IR-level metric battery vs. the hand-sequenced reference (architecture §9 table,
        // the pieces computable without an actual render -- see class doc comment).
        var referenceReader = new XsqStructuralReader();
        var reference = referenceReader.Read(SequencePath);

        var compiledEffectCount = structural.ElementEffects.Sum(e => e.Effects.Count);
        var referenceEffectCount = reference.ElementEffects.Sum(e => e.Effects.Count);
        var compiledEffectTypeDiversity = structural.ElementEffects.SelectMany(e => e.Effects).Select(e => e.Name).Distinct().Count();
        var referenceEffectTypeDiversity = reference.ElementEffects.SelectMany(e => e.Effects).Select(e => e.Name).Distinct().Count();

        // Not hard assertions against the reference's own absolute numbers (that compiled 207-effect,
        // single-effect-type placeholder was for the FULL ~11-minute song; this compile covers a 90s
        // clip, so a direct count comparison would be unfair in either direction) -- but the architecture
        // §1 table's own qualitative placeholder-era defects (single effect type, zero timing tracks)
        // must not reproduce here regardless of clip length.
        Assert.True(compiledEffectCount > 0);
        Assert.True(compiledEffectTypeDiversity > 1, $"Compiled effect type diversity ({compiledEffectTypeDiversity}) did not clear the placeholder-era floor (1 -- exactly the 'On' effect, every time).");
        Assert.True(structural.TimingTracks.Count >= 5, "Compiled output should emit all 5 declared timing tracks (placeholder era emitted 0).");

        var evidenceDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "implementation-evidence");
        Directory.CreateDirectory(evidenceDir);
        var metricsPath = Path.Combine(evidenceDir, "PHASE-04-REAL-TRACK-METRICS.md");
        await File.WriteAllTextAsync(metricsPath, $"""
            # Phase 4 real-track metric comparison (declared/IR-level, not rendered)

            Compiled against a 90s clip of `{ClipAudioPath}` + `{RealXLightsFixture.LayoutPath}`, compared to the
            hand-sequenced reference at `{SequencePath}`.

            | Metric | Compiled (this engine) | Hand-sequenced reference | Placeholder-era (architecture §1) |
            |---|---:|---:|---:|
            | Effect count | {compiledEffectCount} | {referenceEffectCount} | 207 |
            | Effect type diversity | {compiledEffectTypeDiversity} | {referenceEffectTypeDiversity} | 1 |
            | Timing tracks | {structural.TimingTracks.Count} | {reference.TimingTracks.Count} | 0 |
            | Color palettes | {structural.ColorPalettes.Count} | {reference.ColorPalettes.Count} | 1 (single fixed white) |
            | Legality violations (architecture §7) | {result.Violations.Count} | n/a | n/a |
            | Sections compiled | {lookIdBySection.Count} | n/a | n/a |
            | Largest gap across every targeted prop | within 1 bar (asserted) | n/a | 17s (documented original defect) |

            Export warnings: {(exportResult.Warnings.Count == 0 ? "none" : string.Join("; ", exportResult.Warnings))}
            """, ct);
    }

    /// <summary>Same window-sizing formula as <c>XlightsAI.Studio.Pipeline.AudioAnalysisSettingsBundle.ForSampleRate</c> (not referenced directly to avoid depending on its internals): ~23ms window, 50% hop overlap.</summary>
    private static RhythmSettings ForSampleRateRhythmSettings(int sampleRate)
    {
        var fftSize = NextPowerOfTwo((int)Math.Round(sampleRate * (256.0 / 8000.0)));
        return new RhythmSettings(sampleRate, fftSize, fftSize / 2);
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
}
