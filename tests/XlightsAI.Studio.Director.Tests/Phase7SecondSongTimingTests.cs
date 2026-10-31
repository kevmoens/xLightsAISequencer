using System.Diagnostics;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Pipeline;

namespace XlightsAI.Studio.Director.Tests;

[Trait("Category", "Integration")]
public sealed class Phase7SecondSongTimingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-phase7-secondsong-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact(Timeout = 1_700_000)]
    public async Task SecondRealSong_SectionCount_WithMelodyFix()
    {
        const string audioPath = @"C:\xLights\Cher-DJ Play a Christmas Song.mp3";
        const string sequencePath = @"C:\xLights\Cher-DJ Play a Christmas Song.xsq";
        var reportPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "implementation-evidence", "PHASE-07-SECONDSONG-TIMING.md");
        if (!File.Exists(audioPath) || !File.Exists(sequencePath))
        {
            await File.WriteAllTextAsync(reportPath, "BLOCKED: real audio/sequence not found.\n");
            return;
        }

        var sw = Stopwatch.StartNew();
        var analyzer = new CorpusSongAnalyzer(Path.Combine(_root, "analysis-cache", "cher"));
        var entry = new CorpusEntry(sequencePath, audioPath, "Cher-DJ Play a Christmas Song (full length)", CorpusProvenanceTag.ExternalReference, [], "");
        var analysis = await analyzer.AnalyzeAsync(entry, CancellationToken.None);
        sw.Stop();

        var report = $"""
            # Phase 7 second real song section-count verification

            Song: Cher-DJ Play a Christmas Song (full length).
            Elapsed: {sw.Elapsed}.
            Sections detected: {analysis.Labels.Labels.Count}.
            """;
        await File.WriteAllTextAsync(reportPath, report);
        Assert.True(analysis.Labels.Labels.Count > 1, "Expected more than one section with the real-melody fix.");
    }
}
