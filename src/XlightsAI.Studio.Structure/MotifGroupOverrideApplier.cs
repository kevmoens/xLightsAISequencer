using System.Security.Cryptography;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Step 058 -- merges a persisted <see cref="IMotifLinkOverrideStore"/> decision set onto whichever
/// automatic <see cref="MotifClusteringAnalysis.Links"/> is current, recomputing effective
/// <see cref="MotifGroupCandidate"/> membership the same way <see cref="PhraseSegmenter"/> already merges
/// <see cref="IPhraseBoundaryOverrideStore"/> entries onto an automatic phrase segmentation -- applied at
/// read time, never baked into <see cref="MotifClusteringAnalysis"/> itself (Step 056's own already-shipped,
/// unmodified cache). A user "unlink" removes one specific pairwise edge; a user "link" adds one, regardless
/// of (or in the absence of) any automatic proposal. The same connected-component closure
/// <see cref="MotifClusterer"/> already uses is then re-run over the resulting edge set -- a phrase left
/// with no remaining edge to any other phrase is simply absent from every group (the same "distinct passage
/// does not share a motif ID" rule, now also reachable by explicit user unlink, not only by an automatic
/// recipe never proposing a link in the first place).
/// </summary>
/// <remarks>
/// <b>Deterministic group identity.</b> Unlike <see cref="MotifClusterer"/>'s own <c>Guid.NewGuid()</c> group
/// ids (fresh on every automatic recompute, immaterial because nothing downstream yet referenced them at
/// that cache's own layer), this applier's own <see cref="MotifGroupCandidate.Id"/> is a stable hash of the
/// group's own sorted member phrase ids. The same override set applied to the same automatic links always
/// reproduces the same group ids across an application restart or a brand-new store instance over the same
/// directory -- "whole-song persistence after reopen" (Step 058's own UI behavior line, verbatim) requires
/// this: a `ChoreographyTemplate.MotifGroupId` a user selected before closing the app must still resolve to
/// the same group after reopening, provided the same link/unlink decisions are still on disk. A link/unlink
/// decision that changes a group's own membership necessarily changes its id too -- this is disclosed,
/// deliberate behavior, not a bug: Section 3's own "a user may reassign the canonical source" is not
/// implemented by this step either, so a <see cref="ChoreographyTemplate"/> keyed to a group whose membership
/// has since changed is not automatically migrated; the user redefines the template against the new
/// effective group after settling their link/unlink decisions, the same disclosed, narrow scope boundary
/// every prior Phase 08 step's own evidence already established for what it does not (yet) do.
/// </remarks>
public static class MotifGroupOverrideApplier
{
    /// <summary>
    /// Recomputes effective motif groups for <paramref name="phrases"/> from <paramref name="automaticLinks"/>
    /// (Step 056's own unmodified clusterer output) with <paramref name="overrides"/> (this step's own
    /// persisted link/unlink decisions) applied on top.
    /// </summary>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="MotifClusteringInvalidException">
    /// An override references the same phrase twice, or a phrase id not present in <paramref name="phrases"/>
    /// -- "Invalid input... must not corrupt the project" (Step 058's own failure/edge case, verbatim).
    /// </exception>
    public static IReadOnlyList<MotifGroupCandidate> Apply(
        SongId songId,
        IReadOnlyList<PhraseCandidate> phrases,
        IReadOnlyList<MotifLinkCandidate> automaticLinks,
        IReadOnlyList<MotifLinkOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(phrases);
        ArgumentNullException.ThrowIfNull(automaticLinks);
        ArgumentNullException.ThrowIfNull(overrides);

        var phraseIds = phrases.Select(p => p.Id).ToHashSet();

        foreach (var ov in overrides)
        {
            if (ov.PhraseAId == ov.PhraseBId)
            {
                throw new MotifClusteringInvalidException(songId,
                    $"MotifLinkOverride references the same phrase twice ('{ov.PhraseAId}') -- malformed decision.");
            }

            if (!phraseIds.Contains(ov.PhraseAId) || !phraseIds.Contains(ov.PhraseBId))
            {
                throw new MotifClusteringInvalidException(songId,
                    $"MotifLinkOverride references a phrase id not present in the supplied phrase list ('{ov.PhraseAId}'/'{ov.PhraseBId}').");
            }
        }

        if (phrases.Count == 0)
        {
            return [];
        }

        var similarityByPair = new Dictionary<(Guid, Guid), double>();
        var edges = new HashSet<(Guid, Guid)>();
        foreach (var link in automaticLinks)
        {
            var key = Key(link.SourcePhraseId, link.TargetPhraseId);
            edges.Add(key);
            similarityByPair[key] = link.Similarity;
        }

        // Replace-all semantics: the caller's own supplied override list is this song's full, current
        // decision set (IMotifLinkOverrideStore.SaveAsync replaces wholesale), so each entry is applied
        // once, independent of any prior state.
        foreach (var ov in overrides)
        {
            var key = Key(ov.PhraseAId, ov.PhraseBId);
            if (ov.ForceLinked)
            {
                edges.Add(key);
            }
            else
            {
                edges.Remove(key);
            }
        }

        var orderedPhrases = phrases.OrderBy(p => p.StartBarIndex).ToList();
        var indexById = new Dictionary<Guid, int>();
        for (var i = 0; i < orderedPhrases.Count; i++)
        {
            indexById[orderedPhrases[i].Id] = i;
        }

        var parent = Enumerable.Range(0, orderedPhrases.Count).ToArray();

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }

            return x;
        }

        void Union(int a, int b)
        {
            var rootA = Find(a);
            var rootB = Find(b);
            if (rootA != rootB)
            {
                parent[rootB] = rootA;
            }
        }

        foreach (var (a, b) in edges)
        {
            if (indexById.TryGetValue(a, out var ia) && indexById.TryGetValue(b, out var ib))
            {
                Union(ia, ib);
            }
        }

        var componentsByRoot = new Dictionary<int, List<int>>();
        for (var i = 0; i < orderedPhrases.Count; i++)
        {
            var root = Find(i);
            if (!componentsByRoot.TryGetValue(root, out var members))
            {
                members = [];
                componentsByRoot[root] = members;
            }

            members.Add(i);
        }

        var groups = new List<MotifGroupCandidate>();
        foreach (var members in componentsByRoot.Values)
        {
            if (members.Count < 2)
            {
                continue;
            }

            var orderedMembers = members.OrderBy(i => orderedPhrases[i].StartBarIndex).ToList();
            var memberPhraseIds = orderedMembers.Select(i => orderedPhrases[i].Id).ToList();

            var minConfidence = double.MaxValue;
            var anyForced = false;
            for (var a = 0; a < orderedMembers.Count; a++)
            {
                for (var b = a + 1; b < orderedMembers.Count; b++)
                {
                    var idA = orderedPhrases[orderedMembers[a]].Id;
                    var idB = orderedPhrases[orderedMembers[b]].Id;
                    var key = Key(idA, idB);
                    if (!edges.Contains(key))
                    {
                        continue; // No direct edge between this specific pair; connectivity flows through another member.
                    }

                    if (similarityByPair.TryGetValue(key, out var sim))
                    {
                        minConfidence = Math.Min(minConfidence, sim);
                    }
                    else
                    {
                        // A user-approved manual link with no automatic similarity score -- full confidence,
                        // the same "always 1.0 when user-approved" convention PhraseCandidate.Confidence
                        // already uses for a user-corrected phrase boundary.
                        minConfidence = Math.Min(minConfidence, 1.0);
                        anyForced = true;
                    }
                }
            }

            if (minConfidence == double.MaxValue)
            {
                minConfidence = 0.0;
            }

            var evidence = new List<string>
            {
                $"{memberPhraseIds.Count} phrases connected via automatic similarity and/or user-approved manual links (minimum pairwise confidence in this group: {minConfidence:F3}).",
            };
            if (anyForced)
            {
                evidence.Add("Includes at least one user-approved manual link with no automatic similarity score.");
            }

            groups.Add(new MotifGroupCandidate(
                Id: DeterministicGroupId(memberPhraseIds),
                PhraseIds: memberPhraseIds,
                RepresentativePhraseId: memberPhraseIds[0],
                Confidence: minConfidence,
                Evidence: evidence));
        }

        return groups.OrderBy(g => orderedPhrases.First(p => p.Id == g.RepresentativePhraseId).StartBarIndex).ToList();
    }

    private static (Guid, Guid) Key(Guid a, Guid b) => a.CompareTo(b) <= 0 ? (a, b) : (b, a);

    /// <summary>Stable across repeated computation for the same member set -- see remarks above.</summary>
    private static Guid DeterministicGroupId(IReadOnlyList<Guid> memberPhraseIds)
    {
        var sorted = memberPhraseIds.OrderBy(id => id).ToList();
        var buffer = new byte[16 * sorted.Count];
        for (var i = 0; i < sorted.Count; i++)
        {
            sorted[i].TryWriteBytes(buffer.AsSpan(i * 16, 16));
        }

        var hash = SHA256.HashData(buffer);
        return new Guid(hash.AsSpan(0, 16));
    }
}
