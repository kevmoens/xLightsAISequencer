using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.WholeSong;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Deterministically synthesizes the per-section <see cref="SongSectionDirectionInput"/> list
/// <see cref="ISongDirectionPlanner.Plan"/> requires. Confirmed before writing this: nothing
/// anywhere in the codebase derives this type from analysis alone — its own doc comment calls it "a
/// user-tunable creative-direction request," and every existing usage (test fixtures) hand-types the
/// values. Per the user's explicit decision (deterministic heuristic over an LLM-assisted proposal or a
/// manual per-section UI), this type fills that gap with simple, documented rules:
/// <list type="bullet">
/// <item><see cref="SongSectionDirectionInput.TargetEnergy"/>/<see cref="SongSectionDirectionInput.TargetContrast"/>
/// come directly from that section's own real <see cref="BarFingerprint.Features"/> (index 0 = mean
/// energy, index 1 = energy variance — already peak-normalized/gain-invariant by
/// <see cref="BarFingerprintBuilder"/>'s own construction), averaged across the section's bars — real
/// measured values, never fabricated.</item>
/// <item><see cref="SongSectionDirectionInput.PaletteId"/> rotates through a small fixed palette list by
/// section index, so consecutive sections never repeat the same color identity back to back for a
/// song with more than <see cref="PaletteRotation"/>'s own length of sections.</item>
/// <item><see cref="SongSectionDirectionInput.FocalRole"/> follows a fixed rule from the section's own
/// classified <see cref="SectionKind"/> (choruses lead, bridges carry harmony, intro/outro recede to
/// atmosphere), falling back to measured energy for the sections <see cref="SectionLabelClassifier"/>
/// itself documents as a first-class non-error outcome (<see cref="SectionKind.Unknown"/>/<see cref="SectionKind.Verse"/>/<see cref="SectionKind.PreChorus"/>/<see cref="SectionKind.Solo"/>).</item>
/// <item><see cref="SongSectionDirectionInput.MotifGroupIds"/> is every real <see cref="MotifGroupCandidate.Id"/>
/// whose <see cref="MotifGroupCandidate.PhraseIds"/> includes a phrase belonging to this section (via
/// <see cref="PhraseCandidate.SectionLabelId"/>) — real structure-analysis provenance, not guessed.</item>
/// </list>
/// </summary>
public static class DeterministicSectionDirectionPlanner
{
    private static readonly string[] PaletteRotation = ["warm", "cool", "vivid", "mono"];

    public static IReadOnlyList<SongSectionDirectionInput> BuildSectionInputs(StructureAnalysisResult structure)
    {
        ArgumentNullException.ThrowIfNull(structure);

        var inputs = new List<SongSectionDirectionInput>();
        for (var i = 0; i < structure.Labels.Labels.Count; i++)
        {
            var section = structure.Labels.Labels[i];
            var bars = structure.FingerprintSet.Bars
                .Where(b => b.BarIndex >= section.StartBarIndex && b.BarIndex < section.EndBarIndexExclusive)
                .ToList();

            var targetEnergy = bars.Count > 0 ? Math.Clamp(bars.Average(b => b.Features[0]), 0.0, 1.0) : 0.5;
            var targetContrast = bars.Count > 0 ? Math.Clamp(bars.Average(b => b.Features[1]), 0.0, 1.0) : 0.0;

            var phraseIdsInSection = structure.Phrases.Phrases
                .Where(p => p.SectionLabelId == section.Id)
                .Select(p => p.Id)
                .ToHashSet();
            var motifGroupIds = structure.Motifs.Groups
                .Where(g => g.PhraseIds.Any(phraseIdsInSection.Contains))
                .Select(g => g.Id)
                .ToArray();

            inputs.Add(new SongSectionDirectionInput(
                SectionId: section.Id,
                TargetEnergy: targetEnergy,
                TargetContrast: targetContrast,
                PaletteId: PaletteRotation[i % PaletteRotation.Length],
                FocalRole: DetermineFocalRole(section.Kind, targetEnergy),
                MotifGroupIds: motifGroupIds));
        }

        return inputs;
    }

    private static MusicalRole DetermineFocalRole(SectionKind kind, double targetEnergy) => kind switch
    {
        SectionKind.Chorus => MusicalRole.Lead,
        SectionKind.Bridge => MusicalRole.Harmony,
        SectionKind.Intro or SectionKind.Outro => MusicalRole.Atmosphere,
        SectionKind.Solo => MusicalRole.Lead,
        _ => targetEnergy >= 0.6 ? MusicalRole.Rhythm : MusicalRole.Atmosphere,
    };
}
