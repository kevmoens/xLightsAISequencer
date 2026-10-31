namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Confirmed as a real bug against a real generation: <see cref="DirectorPromptBuilder"/>'s own documented
/// Look schema (<c>"Timescale": "bar|beat|phrase"</c>) tells the AI to write a lowercase, singular word,
/// while <see cref="ChoreographyEngine"/> actually emits the real timing tracks under capitalized, plural
/// names ("Beats"/"Bars"/"Phrases") -- every lookup of <see cref="LookLayer.Timescale"/> against those
/// tracks used an exact, case-sensitive (<see cref="StringComparer.Ordinal"/>) match, so "beat" never
/// matched "Beats". Every Look declaring <c>Timescale: "beat"</c> silently missed <see
/// cref="EmissionStrategySelector.SelectMode"/>'s Bound-mode binding check and fell through
/// <c>LookCompiler.ResolveEventWindows</c>'s own fallback to one event per BAR, not per beat -- the
/// direct cause of a real user-observed symptom ("it feels like it is every other beat that the effect
/// changes," i.e. bar-level cadence where per-beat cadence was intended). This normalizes a free-form,
/// AI-authored Timescale word to the real track name it was clearly meant to reference, so a Look's own
/// declared intent (sync to every beat) actually reaches the compiler instead of silently degrading to a
/// coarser cadence.
/// </summary>
public static class TimescaleResolver
{
    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["beat"] = "Beats",
        ["beats"] = "Beats",
        ["bar"] = "Bars",
        ["bars"] = "Bars",
        ["phrase"] = "Phrases",
        ["phrases"] = "Phrases",
        ["section"] = "Sections",
        ["sections"] = "Sections",
        ["percussion"] = "Percussion",
    };

    /// <summary>
    /// Resolves <paramref name="requested"/> (a Look layer's own declared <see cref="LookLayer.Timescale"/>)
    /// against <paramref name="availableTrackNames"/> (the real tracks this song actually emitted): an
    /// exact match first (an author who already used the real track name, e.g. a lyric-track name like
    /// "Lyrics_Phrases"), then the canonical bar/beat/phrase/section/percussion alias above, then a last
    /// resort case-insensitive match against whatever is actually available. Returns <see
    /// langword="null"/> when nothing matches -- the caller's own existing fallback (e.g. one event per
    /// bar) still applies, unchanged, for a genuinely unresolvable reference.
    /// </summary>
    public static string? Resolve(string? requested, IReadOnlyCollection<string> availableTrackNames)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return null;
        }

        foreach (var name in availableTrackNames)
        {
            if (string.Equals(name, requested, StringComparison.Ordinal))
            {
                return name;
            }
        }

        if (Aliases.TryGetValue(requested, out var canonical) && availableTrackNames.Contains(canonical))
        {
            return canonical;
        }

        foreach (var name in availableTrackNames)
        {
            if (string.Equals(name, requested, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return null;
    }
}
