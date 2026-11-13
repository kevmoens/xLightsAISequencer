using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Shell;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// Build-plan Phase 25 Part D.5: <see cref="ExemplarLibraryEntryViewModel"/> surfaces
/// <see cref="ExemplarFingerprint.PeakConcurrentLayerCount"/> alongside the existing
/// <see cref="ExemplarLibraryEntryViewModel.EffectCount"/> column so curators can see (and selectively
/// enable/disable) genuinely layered entries.
/// </summary>
public sealed class ExemplarLibraryViewModelComplexityTests
{
    [Fact]
    public void PeakConcurrentLayerCount_ReflectsTheFingerprintsOwnValue()
    {
        var entry = Entry(peakConcurrentLayerCount: 4);
        var row = new ExemplarLibraryEntryViewModel(entry, isEnabled: true);

        Assert.Equal(4, row.PeakConcurrentLayerCount);
    }

    [Fact]
    public void IsLayeredComplexity_TrueAtOrAboveTheDefaultThreeLayerFloor()
    {
        Assert.False(new ExemplarLibraryEntryViewModel(Entry(2), isEnabled: true).IsLayeredComplexity);
        Assert.True(new ExemplarLibraryEntryViewModel(Entry(3), isEnabled: true).IsLayeredComplexity);
        Assert.True(new ExemplarLibraryEntryViewModel(Entry(5), isEnabled: true).IsLayeredComplexity);
    }

    private static ExemplarEntry Entry(int peakConcurrentLayerCount) =>
        new(new ExemplarFingerprint("Song", CorpusProvenanceTag.UserAuthored, 0, 8, 120, 0.5, SectionKind.Chorus, null, null, peakConcurrentLayerCount), []);
}
