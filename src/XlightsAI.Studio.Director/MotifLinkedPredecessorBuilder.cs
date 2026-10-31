using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director;

/// <summary>Build-plan Phase 10 §3c: the one earlier section (if any) this section's own real motif-clustering data ties it to by shared melodic/phrase content -- not merely a shared <see cref="SectionKind"/>.</summary>
/// <param name="SectionId">The <see cref="SectionLabelCandidate.Id"/> this link describes.</param>
/// <param name="LinkedPredecessorSectionId">The earlier section's own <see cref="SectionLabelCandidate.Id"/> that shares a <see cref="MotifGroupCandidate"/> with this section.</param>
/// <param name="LinkedPredecessorKind">That earlier section's own <see cref="SectionLabelCandidate.Kind"/> -- compared against this section's own Kind so the prompt builder can skip the line when it would be redundant with the existing same-Kind occurrence text.</param>
/// <param name="LinkedPredecessorStartBarIndex">That earlier section's own start bar, for naming it concretely in the prompt.</param>
/// <param name="LinkedPredecessorEndBarIndexExclusive">That earlier section's own end bar (exclusive), for naming it concretely in the prompt.</param>
/// <param name="Confidence">The underlying <see cref="MotifGroupCandidate.Confidence"/> (min pairwise similarity among the group's members) -- carried through for provenance, not currently rendered.</param>
public sealed record SectionMotifLink(
    Guid SectionId,
    Guid LinkedPredecessorSectionId,
    SectionKind LinkedPredecessorKind,
    int LinkedPredecessorStartBarIndex,
    int LinkedPredecessorEndBarIndexExclusive,
    double Confidence);

/// <summary>
/// Build-plan Phase 10 §3c: joins <see cref="MotifClusteringAnalysis.Groups"/> (computed by
/// <c>XlightsAI.Studio.Structure</c>, previously forwarded only to the Shell's Structure Inspector and
/// never reaching either prompt) against <see cref="MusicModel.Sections"/> via each
/// <see cref="PhraseCandidate.SectionLabelId"/>, so a repeat can be told not just that it shares a
/// <see cref="SectionKind"/> with an earlier section, but that it musically echoes one SPECIFIC earlier
/// section -- the "leitmotif" identity §2d's escalation rule needs.
/// </summary>
public static class MotifLinkedPredecessorBuilder
{
    /// <summary>
    /// Computes one <see cref="SectionMotifLink"/> for every section that has an earlier motif-linked
    /// predecessor. Sections with no motif group at all, or whose own group's earliest member already IS
    /// this section (i.e. this section is the original occurrence, not a repeat), are simply omitted from
    /// the result -- never a placeholder entry. Returns an empty list when <see cref="MusicModel.Motifs"/>
    /// is <see langword="null"/> (the same "silence is a valid, inspectable result" convention
    /// <see cref="MelodicContourBriefBuilder"/> uses for a missing <see cref="MusicModel.Melody"/>).
    /// </summary>
    public static IReadOnlyList<SectionMotifLink> ComputeSectionLinks(MusicModel music)
    {
        ArgumentNullException.ThrowIfNull(music);

        if (music.Motifs is null)
        {
            return Array.Empty<SectionMotifLink>();
        }

        var sectionsById = music.Sections.Labels.ToDictionary(s => s.Id);
        var phrasesById = music.Phrases.Phrases.ToDictionary(p => p.Id);

        // Built via first-match: the clusterer constructs Groups as connected components over the
        // similarity graph, so a phrase belongs to at most one group today -- but guard against a future
        // clusterer change allowing overlapping membership rather than throwing on a duplicate key.
        var groupByPhraseId = new Dictionary<Guid, MotifGroupCandidate>();
        foreach (var group in music.Motifs.Groups)
        {
            foreach (var phraseId in group.PhraseIds)
            {
                groupByPhraseId.TryAdd(phraseId, group);
            }
        }

        var links = new List<SectionMotifLink>();
        foreach (var section in music.Sections.Labels)
        {
            var sectionPhrases = music.Phrases.Phrases.Where(p => p.SectionLabelId == section.Id);

            SectionLabelCandidate? bestPredecessor = null;
            var bestConfidence = 0.0;

            foreach (var phrase in sectionPhrases)
            {
                if (!groupByPhraseId.TryGetValue(phrase.Id, out var group))
                {
                    continue;
                }

                if (!phrasesById.TryGetValue(group.RepresentativePhraseId, out var representativePhrase)
                    || !sectionsById.TryGetValue(representativePhrase.SectionLabelId, out var representativeSection))
                {
                    continue;
                }

                if (representativeSection.StartBarIndex >= section.StartBarIndex)
                {
                    continue; // This section IS the representative/original occurrence -- nothing earlier to point to.
                }

                if (bestPredecessor is null || representativeSection.StartBarIndex < bestPredecessor.StartBarIndex)
                {
                    bestPredecessor = representativeSection;
                    bestConfidence = group.Confidence;
                }
            }

            if (bestPredecessor is not null)
            {
                links.Add(new SectionMotifLink(
                    section.Id,
                    bestPredecessor.Id,
                    bestPredecessor.Kind,
                    bestPredecessor.StartBarIndex,
                    bestPredecessor.EndBarIndexExclusive,
                    bestConfidence));
            }
        }

        return links;
    }
}
