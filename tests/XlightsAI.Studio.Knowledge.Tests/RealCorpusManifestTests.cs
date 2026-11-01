namespace XlightsAI.Studio.Knowledge.Tests;

/// <summary>
/// Acceptance-criteria check against this developer's real <c>C:\xLights</c> corpus, when present — same
/// skip-cleanly-elsewhere convention as <c>RealXlightsInstallTests</c>. The exact entry count/provenance
/// split asserted here is what the Phase 2b manual corpus audit actually found on this machine (see the
/// Phase 2b final report for the full reconciliation against the architecture doc's "31 sequences"
/// figure) — a real, machine-specific fact, not a portable algorithm property.
/// </summary>
public sealed class RealCorpusManifestTests
{
    private const string RealCorpusRoot = @"C:\xLights";

    [Fact]
    public void Build_AgainstRealCorpus_FindsTwentyEntriesWithCorrectProvenanceSplit()
    {
        if (!Directory.Exists(RealCorpusRoot))
        {
            return;
        }

        var manifest = CorpusManifestBuilder.Build(RealCorpusRoot);

        // Build-plan Phase 17 §1b/§2b: 7 of the user's 12 selected historical recoveries parse cleanly
        // and were registered as real UserAuthored KnownResolutions entries (2026-10-31) -- 5 UserAuthored
        // from before this phase plus 7 newly recovered = 12; LikelyVendorOrCommunityDownload/
        // ExternalReference counts are unchanged (Phase 16 §1c's 5 broken-copy swaps were swaps within
        // that same category, not additions).
        Assert.Equal(27, manifest.Entries.Count);
        Assert.Equal(14, manifest.Entries.Count(e => e.Provenance == CorpusProvenanceTag.LikelyVendorOrCommunityDownload));
        Assert.Equal(1, manifest.Entries.Count(e => e.Provenance == CorpusProvenanceTag.ExternalReference));
        Assert.Equal(12, manifest.Entries.Count(e => e.Provenance == CorpusProvenanceTag.UserAuthored));

        foreach (var entry in manifest.Entries)
        {
            Assert.True(File.Exists(entry.SequencePath), entry.SequencePath);
            Assert.True(File.Exists(entry.AudioPath), entry.AudioPath);
        }

        // Bad Feelings/exports is this app's own prior output, never a corpus member.
        Assert.DoesNotContain(manifest.Entries, e => e.SequencePath.Contains("Bad Feelings"));
    }
}
