namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Step 042 — the pure, no-I/O solo/mute resolution rule shared by
/// <see cref="StemMixdownRenderer"/> (real audio mixdown) and any UI layer that needs to preview which
/// stems are currently audible without re-rendering audio. Mute always silences its own channel, even
/// if that same channel is also soloed; solo (when at least one channel is soloed) silences every
/// other, non-soloed channel.
/// </summary>
public static class StemMixRules
{
    /// <summary>
    /// Returns the subset of <paramref name="manifest"/>'s stem IDs that should be audible given
    /// <paramref name="channels"/>. A stem with no corresponding <see cref="StemChannelState"/> in
    /// <paramref name="channels"/> defaults to unmuted/unsoloed (plain "not explicitly silenced").
    /// </summary>
    public static IReadOnlyList<Guid> ResolveActiveStemIds(StemManifest manifest, IReadOnlyList<StemChannelState> channels)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(channels);

        var byStemId = channels.ToDictionary(c => c.StemId, c => c);
        var anySoloed = channels.Any(c => c.Soloed);

        var active = new List<Guid>();
        foreach (var stem in manifest.Stems)
        {
            var state = byStemId.GetValueOrDefault(stem.Id, new StemChannelState(stem.Id, Muted: false, Soloed: false));
            if (state.Muted)
            {
                continue;
            }

            if (anySoloed && !state.Soloed)
            {
                continue;
            }

            active.Add(stem.Id);
        }

        return active;
    }
}
