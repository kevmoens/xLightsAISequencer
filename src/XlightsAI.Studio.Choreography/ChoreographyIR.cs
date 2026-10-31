using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 4 task 7 ("A Look -&gt; ChoreographyIR compiler") / architecture §3's L4 diagram
/// ("ChoreographyIR -&gt; timing tracks + effects + palettes -&gt; CONSTRAINT ENGINE -&gt; .xsq"): the
/// compiled-but-not-yet-exported output of <see cref="LookCompiler"/>, before the constraint engine's
/// own always-moving/legality/coverage passes run against it. Deliberately NOT a new parallel type to
/// <see cref="SequenceDocument"/> -- that type's own <see cref="SequenceDocument.Effects"/>/
/// <see cref="SequenceDocument.TimingTracks"/>/<see cref="SequenceDocument.Palettes"/> already carry
/// exactly this shape (Phase 1c built it for exactly this purpose) -- this record exists only to give
/// the Phase 4 compiler pipeline its own named, metadata-free intermediate value (no <c>Id</c>/<c>Revision</c>/
/// document identity concerns belong at this stage) and a clear seam before <see cref="ToSequenceDocument"/>
/// attaches real document identity for export.
/// </summary>
public sealed record ChoreographyIR(
    IReadOnlyList<TimingTrack> TimingTracks,
    IReadOnlyList<EffectInstance> Effects,
    IReadOnlyList<ColorPaletteDefinition> Palettes)
{
    public static ChoreographyIR Empty { get; } = new([], [], []);

    /// <summary>
    /// Merges this IR with <paramref name="other"/> -- used to accumulate per-section compiles into one
    /// whole-song IR. Palettes are concatenated and effects re-pointed at the shifted palette indices so
    /// no cross-section palette index collides. Timing tracks are merged **by name** (marks unioned and
    /// re-sorted) rather than concatenated as separate elements -- a song-spanning track like
    /// <c>Beats</c> must stay one track across every section's own compile, never duplicate into several
    /// same-named tracks (which <see cref="IXsqExporter"/> rejects outright).
    /// </summary>
    public ChoreographyIR Merge(ChoreographyIR other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var paletteOffset = Palettes.Count;
        var shiftedOtherEffects = other.Effects.Select(e => e with { PaletteIndex = (e.PaletteIndex ?? 0) + paletteOffset }).ToList();

        var tracksByName = new Dictionary<string, List<TimingMark>>(StringComparer.Ordinal);
        var trackOrder = new List<string>();
        foreach (var track in TimingTracks.Concat(other.TimingTracks))
        {
            if (!tracksByName.TryGetValue(track.Name, out var marks))
            {
                marks = [];
                tracksByName[track.Name] = marks;
                trackOrder.Add(track.Name);
            }

            marks.AddRange(track.Marks);
        }

        var mergedTracks = trackOrder
            .Select(name => new TimingTrack(name, tracksByName[name].OrderBy(m => m.StartTicks).ToList()))
            .ToList();

        return new ChoreographyIR(
            mergedTracks,
            [.. Effects, .. shiftedOtherEffects],
            [.. Palettes, .. other.Palettes]);
    }

    /// <summary>Attaches document identity so this IR can be exported via <see cref="IXsqExporter"/>.</summary>
    public SequenceDocument ToSequenceDocument(SequenceMetadata? metadata = null) =>
        new(Guid.NewGuid(), Revision: 1, Effects, metadata, Palettes, TimingTracks);
}
