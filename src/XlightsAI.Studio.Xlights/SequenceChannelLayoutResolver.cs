namespace XlightsAI.Studio.Xlights;

/// <summary>One physical string's own real channel span within one sequence's rendered FSEQ channel buffer.</summary>
/// <param name="GlobalChannelOffset">0-based channel index of this string's first (red) channel within this sequence's own compacted buffer (see <see cref="SequenceChannelLayoutResolver"/> remarks).</param>
/// <param name="NodeCount">Nodes on this one string -- <c>NodeCount * 3</c> contiguous channels starting at <see cref="GlobalChannelOffset"/>.</param>
public sealed record ModelStringChannelSpan(long GlobalChannelOffset, int NodeCount);

/// <summary>
/// Resolves each model's real channel span within one specific exported sequence's own rendered FSEQ
/// channel buffer. PHASE-07 (build-plan "The harness") channel-to-model mapping --
/// <see cref="FseqRenderedFrameDecoder"/>'s own doc comment explicitly deferred this exact problem.
///
/// <para>
/// <b>Confirmed mechanism (empirically verified against the real xLights install, not assumed).</b> A
/// headless render's own FSEQ channel buffer is NOT addressed by the house's absolute
/// <c>xlights_networks.xml</c> controller/universe offsets -- it is a per-sequence, locally **compacted**
/// buffer containing only the models actually referenced in the <c>.xsq</c>'s own <c>&lt;DisplayElements&gt;</c>,
/// each contributing exactly its own real node count &#215; 3 channels, concatenated in
/// <c>&lt;DisplayElements&gt;</c> document order, starting at channel 0. Verified directly:
/// </para>
/// <list type="bullet">
/// <item><description>A sequence referencing only the real "Arches" model group (4 members, 100 nodes
/// each) produced a real decoded FSEQ with exactly 1200 channels (4&#215;100&#215;3) -- not the thousands
/// their individual <c>xlights_networks.xml</c> universe addresses would otherwise imply.</description></item>
/// <item><description>A sequence referencing the real "MiniStar1" (20 nodes) then "Front Door" (100
/// nodes), in that order, produced exactly 360 channels (20&#215;3 + 100&#215;3), with MiniStar1's data at
/// offset 0 and Front Door's at offset 60 -- proving both the compaction and the document-order
/// concatenation.</description></item>
/// </list>
/// <para>
/// An earlier version of this reader resolved each model's channel span from its own
/// <c>StartChannel</c>/<c>StringN</c> attribute plus the house's absolute universe table
/// (<see cref="XlightsNetworksReader"/>/the original <c>ModelStartChannelReader.Resolve</c> shape) --
/// that approach is now known to be wrong for interpreting a rendered <c>.fseq</c>: it produced channel
/// indices far past the real decoded buffer's own length, confirmed by a real end-to-end render
/// projecting zero illuminated pixels before this was corrected. <see cref="XlightsNetworksReader"/>
/// itself remains correct and independently useful (it accurately reports the house's own declared
/// network configuration), but is not the right tool for decoding a specific render's channel layout.
/// </para>
/// </summary>
public static class SequenceChannelLayoutResolver
{
    /// <summary>
    /// Returns each model's own real channel span within this one sequence's rendered FSEQ buffer, keyed
    /// by <see cref="Contracts.ModelId"/>. A <c>&lt;DisplayElements&gt;</c> entry naming a
    /// <c>modelGroup</c> is expanded to its real member models, in the group's own declared member
    /// order (recursively, for a nested group); a member with no imported <see cref="DisplayModel.NodeCount"/>
    /// (unresolved reference, submodel, or zero node count) contributes no channels and is simply
    /// skipped, exactly mirroring what a real headless render itself would have nothing to allocate for.
    /// </summary>
    /// <param name="displayElements">The exported sequence's own <c>&lt;DisplayElements&gt;</c>, in document order -- e.g. <see cref="XsqStructuralDocument.DisplayElements"/>.</param>
    /// <param name="layout">The same show directory's imported layout (model node counts, group membership).</param>
    public static IReadOnlyDictionary<Contracts.ModelId, IReadOnlyList<ModelStringChannelSpan>> Resolve(
        IReadOnlyList<XsqStructuralElement> displayElements, DisplayGraph layout)
    {
        ArgumentNullException.ThrowIfNull(displayElements);
        ArgumentNullException.ThrowIfNull(layout);

        var nodeCountByName = layout.Models.ToDictionary(m => m.XlightsName, m => m.NodeCount, StringComparer.Ordinal);
        var groupsByName = layout.Groups.ToDictionary(g => g.Name, StringComparer.Ordinal);

        var result = new Dictionary<Contracts.ModelId, IReadOnlyList<ModelStringChannelSpan>>();
        var cumulativeChannel = 0L;

        foreach (var element in displayElements)
        {
            if (!string.Equals(element.Type, "model", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var modelName in ExpandToModelNames(element.Name, groupsByName, visitedGroups: []))
            {
                if (!nodeCountByName.TryGetValue(modelName, out var nodeCount) || nodeCount <= 0)
                {
                    continue;
                }

                result[new Contracts.ModelId(modelName)] = [new ModelStringChannelSpan(cumulativeChannel, nodeCount)];
                cumulativeChannel += (long)nodeCount * 3;
            }
        }

        return result;
    }

    private static IEnumerable<string> ExpandToModelNames(
        string name, IReadOnlyDictionary<string, DisplayModelGroup> groupsByName, HashSet<string> visitedGroups)
    {
        if (!groupsByName.TryGetValue(name, out var group))
        {
            yield return name; // Not a known group -- treat as a direct model reference (or an unresolved name, filtered out by the NodeCount lookup above).
            yield break;
        }

        if (!visitedGroups.Add(name))
        {
            yield break; // A group cycle -- never recurse infinitely; already-visited members were already yielded once.
        }

        foreach (var member in group.Members)
        {
            foreach (var expanded in ExpandToModelNames(member.RawName, groupsByName, visitedGroups))
            {
                yield return expanded;
            }
        }
    }
}
