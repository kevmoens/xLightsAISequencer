using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director;

internal sealed record AdaptedGesture(ExemplarGesture Source, string Target, long Start, long End);

internal sealed record ExemplarAdaptation(string Id, Guid SectionId, string Palette, IReadOnlyList<AdaptedGesture> Gestures,
    string? EchoTarget = null)
{
    internal string Brief => $"Exemplar {Id}, palette {Palette}. These gestures are placed automatically; vary the remaining layers around them: " +
        string.Join("; ", Gestures.Select(g =>
            $"{g.Source.Id} {g.Source.Role}: {g.Source.Effect} on {g.Target} at beat +{g.Source.OnsetBeats:0.##} for {g.Source.DurationBeats:0.##} beats")) +
        (EchoTarget is null ? "" : $"; this return adds one supporting echo on {EchoTarget}");

    internal (SectionTimeline Timeline, SectionTimingCatalog Catalog) Apply(
        SectionTimeline timeline, SectionTimingCatalog catalog, string intensity)
    {
        var marks = catalog.Marks.ToDictionary(p => p.Key, p => p.Value);
        var cues = new List<SectionCue>();
        foreach (var gesture in Gestures)
        {
            var tag = $"exemplar:{SectionId:N}:{Id}:{gesture.Source.Id}";
            var start = $"{tag}:start";
            var end = $"{tag}:end";
            marks[start] = gesture.Start;
            marks[end] = gesture.End;
            var foundation = gesture.Source.Role == "foundation";
            cues.Add(new SectionCue(start, gesture.Target, gesture.Source.Effect, intensity,
                foundation ? LookLayer.RoleSubject : LookLayer.RoleSupport, end, Annotation: tag,
                Kind: foundation ? CueKind.Hold : CueKind.Accent, PaletteSlot: Palette)
            {
                TraceId = tag,
                IsMeasuredLitSpan = true,
            });
        }
        if (EchoTarget is not null)
        {
            var support = cues.First(c => c.Kind == CueKind.Accent);
            cues.Add(support with { Props = EchoTarget, Annotation = "chorus-exemplar-echo", TraceId = null, Intensity = IntensityTier.Bright });
        }
        return (timeline with { Cues = [.. timeline.CuesOrEmpty, .. cues] }, catalog with { Marks = marks });
    }

    internal bool IsRealized(ChoreographyIR ir, DisplayGraph layout, EffectRecipeCatalog recipes)
    {
        // Verify actual owned placements, not a model's annotation or an effect-name coincidence.
        var roles = Gestures.Where(g => ir.Effects.Any(e =>
            e.CueTraceId == $"exemplar:{SectionId:N}:{Id}:{g.Source.Id}" &&
            e.RecipeId == LookCompiler.RecipeIdFor(g.Source.Effect, recipes) &&
            LookCompiler.TryResolveModels(e.Target.Value, layout).ToHashSet()
                .SetEquals(LookCompiler.TryResolveModels(g.Target, layout)) &&
            e.StartTicks == g.Start &&
            (e.EndTicks - e.StartTicks) / (double)(g.End - g.Start) is >= .75 and <= 1.25))
            .Select(g => g.Source.Role).Distinct();
        return roles.Count() >= 2;
    }
}

internal static class ExemplarAdaptationPlanner
{
    internal static IReadOnlyDictionary<Guid, ExemplarAdaptation> Build(
        MusicModel music, BarGrid grid, ExemplarIndex index, DisplayGraph layout, EffectRecipeCatalog recipes,
        LookLibrary looks, IReadOnlyDictionary<Guid, ChorusSignature> signatures)
    {
        var result = new Dictionary<Guid, ExemplarAdaptation>();
        if (index.SchemaVersion != ExemplarIndex.GestureSchemaVersion || music.Energy?.Frames is not { Length: > 0 } frames ||
            music.SongDurationTicks <= 0)
        {
            return result;
        }
        var axis = new ExemplarBeatAxis(music.BeatGrid.Beats.Select(b => b.TimeTicks));
        if (!axis.IsUsable)
        {
            return result;
        }
        var bars = grid.Bars.Select(b => MeanEnergy(b.Window)).ToArray();
        if (bars.Length == 0)
        {
            return result;
        }
        var sections = music.Sections.Labels.OrderBy(s => s.StartBarIndex).ToArray();
        foreach (var family in sections.GroupBy(s => signatures.GetValueOrDefault(s.Id)?.FamilyId ?? s.Id))
        {
            var members = family.Select(s => (Section: s, Window: grid.WindowForBarRange(
                new BarIndex(s.StartBarIndex), new BarIndex(s.EndBarIndexExclusive)))).ToArray();
            if (members.Any(x => x.Window is null))
            {
                continue;
            }
            var signature = signatures.GetValueOrDefault(family.First().Id);
            var look = looks.Looks.FirstOrDefault(l => !l.DeclaredDarkness &&
                (l.AllowedSectionKindsOrEmpty.Count == 0 || l.AllowedSectionKindsOrEmpty.Contains(family.First().Kind)));
            if (look is null)
            {
                continue;
            }
            var palette = signature?.Palette ?? look.PaletteRef;
            var targets = new[] { signature?.Target ?? look.Subject }.Concat(look.Layers.Select(l => l.Props))
                .Concat(layout.Models.Select(m => m.XlightsName)).Distinct(StringComparer.Ordinal)
                .Select(t => (Target: t, Members: LookCompiler.TryResolveModels(t, layout).ToHashSet()))
                .Where(t => t.Members.Count > 0).ToArray();
            var contexts = members.Select(m =>
            {
                var w = m.Window!.Value;
                var energy = MeanEnergy(w);
                var origin = axis.BeatAt(w.StartTicks);
                var tempo = (axis.BeatAt(w.EndTicks) - origin) * TimeSpan.TicksPerMinute / (w.EndTicks - w.StartTicks);
                return (m.Section, Window: w, Origin: origin, Tempo: tempo,
                    Rank: bars.Count(b => b <= energy) / (double)bars.Length,
                    Vocal: SectionMusicContext.IsVocalActiveMajority(music.VocalActivity, w));
            }).ToArray();
            var candidates = index.Entries.Where(e => e.Id is not null && e.Gestures is { Count: > 0 } &&
                    contexts.All(c => e.Fingerprint.SectionKind == c.Section.Kind &&
                        TempoDistance(e.Fingerprint.TempoBpm, c.Tempo) <= .15 &&
                        Math.Abs(e.Fingerprint.EnergyPercentile - c.Rank) <= .25))
                .OrderBy(e => contexts.Sum(c => TempoDistance(e.Fingerprint.TempoBpm, c.Tempo) +
                    Math.Abs(e.Fingerprint.EnergyPercentile - c.Rank) +
                    (e.Fingerprint.VocalActivityHint is { } vocal && vocal != c.Vocal ? .2 : 0)))
                .ThenBy(e => e.Id, StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                var common = candidate.Gestures!.Where(g => g.Role is "foundation" or "accent" or "support" &&
                    double.IsFinite(g.OnsetBeats) && double.IsFinite(g.DurationBeats) && g.OnsetBeats >= 0 && g.DurationBeats > 0 &&
                    recipes.TryGet(LookCompiler.RecipeIdFor(g.Effect, recipes), out _) &&
                    contexts.All(c =>
                        axis.TicksAt(c.Origin + g.OnsetBeats + g.DurationBeats) <= c.Window.EndTicks &&
                        axis.TicksAt(c.Origin + g.OnsetBeats + g.DurationBeats) -
                        axis.TicksAt(c.Origin + g.OnsetBeats) >= TimeSpan.TicksPerMillisecond * 50))
                    .OrderBy(g => g.OnsetBeats).ToArray();
                var roles = new[] { "foundation", "accent", "support" }.Where(role => common.Any(g => g.Role == role)).ToArray();
                var first = targets.FirstOrDefault(a => targets.Any(b => !a.Members.Overlaps(b.Members)));
                if (roles.Length < 2 || first.Target is null)
                {
                    continue;
                }
                var second = targets.First(b => !first.Members.Overlaps(b.Members));
                var third = targets.FirstOrDefault(c => !first.Members.Overlaps(c.Members) && !second.Members.Overlaps(c.Members));
                var mapped = roles.Zip(new[] { first.Target, second.Target, third.Target })
                    .Where(x => x.Second is not null).ToDictionary(x => x.First, x => x.Second!);
                var gestures = common.Where(g => mapped.ContainsKey(g.Role)).GroupBy(g => g.Role).Select(g => g.First()).ToArray();
                var used = mapped.Values.SelectMany(t => LookCompiler.TryResolveModels(t, layout)).ToHashSet();
                foreach (var c in contexts)
                {
                    result[c.Section.Id] = new ExemplarAdaptation(candidate.Id!, c.Section.Id, palette, gestures.Select(g =>
                        new AdaptedGesture(g, mapped[g.Role], axis.TicksAt(c.Origin + g.OnsetBeats),
                            axis.TicksAt(c.Origin + g.OnsetBeats + g.DurationBeats))).ToArray(),
                        signatures.GetValueOrDefault(c.Section.Id)?.Intensity == IntensityTier.Full
                            ? targets.LastOrDefault(t => !t.Members.Overlaps(used)).Target : null);
                }
                break;
            }
        }
        return result;

        double MeanEnergy(TimeWindow w)
        {
            var sampleRate = music.Energy!.Settings.AnalysisSampleRate;
            var startSample = w.StartTicks * (double)sampleRate / TimeSpan.TicksPerSecond;
            var endSample = w.EndTicks * (double)sampleRate / TimeSpan.TicksPerSecond;
            double sum = 0, weight = 0;
            foreach (var frame in frames)
            {
                if (frame.StartSample >= endSample)
                {
                    break;
                }
                var overlap = Math.Max(0, Math.Min(endSample, frame.EndSample) - Math.Max(startSample, frame.StartSample));
                sum += overlap * frame.NormalizedEnergy;
                weight += overlap;
            }
            return weight > 0 ? sum / weight : 0;
        }
    }

    private static double TempoDistance(double source, double target) =>
        target > 0 ? new[] { .5, 1, 2 }.Min(scale => Math.Abs(source * scale / target - 1)) : double.PositiveInfinity;
}
