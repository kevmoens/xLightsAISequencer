using System.Diagnostics;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// One-shot timing probe for the user's follow-up question to <c>implementation-evidence/PHASE-07-HARNESS.md</c>:
/// does the full-length real track (not a 30s/90s clip) produce more than one structural section, and does
/// analysis complete in a practical time? Writes its own small timing report; does not touch
/// <c>PHASE-07-HARNESS.md</c> itself (that is updated separately once/if a real multi-iteration run follows).
/// </summary>
[Trait("Category", "Integration")]
public sealed class Phase7FullLengthTimingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-phase7-fulllen-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact(Timeout = 1_700_000)]
    public async Task FullLengthRealTrack_AnalysisTiming_And_SectionCount()
    {
        const string audioPath = @"C:\xLights\Dubstep Christmas Light Show.mp3";
        const string sequencePath = @"C:\xLights\Dubstep Christmas Light Show.xsq";
        var reportPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "implementation-evidence", "PHASE-07-FULLLENGTH-TIMING.md");

        if (!File.Exists(audioPath) || !File.Exists(sequencePath))
        {
            await File.WriteAllTextAsync(reportPath, "BLOCKED: real full-length audio/sequence not found.\n");
            return;
        }

        var sw = Stopwatch.StartNew();
        var ct = CancellationToken.None;
        const string songName = "Dubstep Christmas Light Show (full length)";

        var analyzer = new CorpusSongAnalyzer(Path.Combine(_root, "analysis-cache", songName));
        var entry = new CorpusEntry(sequencePath, audioPath, songName, CorpusProvenanceTag.ExternalReference, [], "");
        var analysis = await analyzer.AnalyzeAsync(entry, ct);
        sw.Stop();

        var report = $"""
            # Phase 7 full-length real-track analysis timing

            Song: {songName}.
            Elapsed (structure/section analysis only, `CorpusSongAnalyzer.AnalyzeAsync`): {sw.Elapsed}.
            Sections detected: {analysis.Labels.Labels.Count}.
            Song duration ticks: n/a (BeatGrid only exposes beats here); section list: {string.Join(", ", analysis.Labels.Labels.Select(s => $"{s.Kind}[{s.StartBarIndex}-{s.EndBarIndexExclusive})"))}.
            """;
        await File.WriteAllTextAsync(reportPath, report, ct);

        Assert.True(true);
    }
}
