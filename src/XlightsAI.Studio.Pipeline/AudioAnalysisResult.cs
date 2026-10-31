using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Pipeline;

/// <summary>Every real analysis artifact <see cref="AudioAnalysisRunner.AnalyzeAsync"/> produces for one song, bundled together since Structure's <c>BarFingerprintContext</c> needs all three at once.</summary>
public sealed record AudioAnalysisResult(
    StemManifest Stems,
    BeatGrid BeatGrid,
    EnergyEnvelope Energy,
    MelodyTrack Melody,
    VocalActivityEnvelope VocalActivity,
    BandEnergyEnvelope BandEnergy,
    IReadOnlyList<VisemeMark> VisemeMarks);
