using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Step 052 -- real implementation of <see cref="IBarFingerprintBuilder"/>: partitions an
/// already-approved <c>BeatGrid</c> into bars and computes one normalized feature fingerprint per bar
/// from the already-approved <c>EnergyEnvelope</c> (energy mean/variance) and <c>MelodyTrack</c>
/// (duration-weighted pitch-class histogram) -- Step 052's own declared recipe, verbatim: "Compute
/// normalized fingerprints per beat/bar from chosen spectral/harmonic/melodic sources with caching;
/// retain enough features to explain matches" (Section 3 architectural design notes: "combining
/// harmony/rhythm/melody").
/// </summary>
/// <remarks>
/// <b>Gain invariance (this step's own required acceptance, verbatim: "Similar motifs compare
/// consistently across changed gain").</b> Every energy component this builder reads is
/// <see cref="XlightsAI.Studio.Rhythm.EnergyFrame.NormalizedEnergy"/> -- already divided by the single
/// largest RMS frame across the whole envelope (Step 031's own "unit normalization" contract) -- never
/// the raw <see cref="XlightsAI.Studio.Rhythm.EnergyFrame.Rms"/>. Scaling an entire recording's gain by a
/// constant factor scales every frame's <c>Rms</c> (and therefore the envelope's own peak) by that same
/// factor, leaving every <c>NormalizedEnergy</c> value numerically unchanged -- so this builder's energy
/// features are gain-invariant by construction, not merely approximately so. The chroma half of each
/// fingerprint is weighted purely by note-overlap duration and then renormalized to sum to <c>1</c>,
/// independent of <see cref="XlightsAI.Studio.Melody.NoteEvent.Velocity"/> for the same reason.
/// <para>
/// <b>Bar windows.</b> A bar's window starts at the earliest <c>BeatMarker.TimeTicks</c> among the beats
/// sharing its <c>BarIndex</c> (its own downbeat) and ends at the next bar's own downbeat, or -- for the
/// final bar -- at the end of the analyzed audio (the last <c>EnergyFrame.EndSample</c> converted to
/// ticks), never an arbitrary fixed duration.
/// </para>
/// </remarks>
public sealed class BarFingerprintBuilder : IBarFingerprintBuilder
{
    /// <summary>Burned into every <see cref="SongFingerprintSet.AnalyzerVersion"/> this builder produces.</summary>
    public const string Version = "XlightsAI.Studio.Structure.BarFingerprintBuilder/1.0.0";

    /// <summary>This step's own single implemented feature recipe -- energy mean/variance plus a duration-weighted chroma histogram. A future recipe is a different, distinguishable <see cref="BarFingerprintSettings.RecipeId"/>, not a silent change to this one's meaning.</summary>
    public const string EnergyPlusChromaRecipeId = "energy+chroma/1.0.0";

    private const long TicksPerSecond = 10_000_000L;

    private readonly IBarFingerprintStore _store;

    public BarFingerprintBuilder(IBarFingerprintStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<SongFingerprintSet> BuildAsync(BarFingerprintContext context, BarFingerprintSettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        ValidateInputs(context, settings);

        ct.ThrowIfCancellationRequested();

        var path = _store.GetPath(context.SongId);

        if (await TryLoadStillValidCacheAsync(path, context, settings, ct).ConfigureAwait(false) is { } cached)
        {
            return cached;
        }

        var bars = ComputeBars(context, settings, ct);

        var set = new SongFingerprintSet(
            VersionId: Guid.NewGuid(),
            SongId: context.SongId,
            SourceHash: context.SourceHash,
            SourceBeatGridVersionId: context.BeatGrid.VersionId,
            AnalyzerVersion: Version,
            Settings: settings,
            Bars: bars);

        await _store.SaveAsync(set, ct).ConfigureAwait(false);
        return set;
    }

    private static void ValidateInputs(BarFingerprintContext context, BarFingerprintSettings settings)
    {
        // "missing media... must be handled explicitly": reject an empty source identity, or an
        // analysis input that does not actually belong to the declared song/source, before any
        // computation or cache lookup.
        if (string.IsNullOrWhiteSpace(context.SourceHash))
        {
            throw new BarFingerprintInvalidException(context.SongId, "SourceHash is empty -- missing media.");
        }

        if (context.BeatGrid.SongId != context.SongId)
        {
            throw new BarFingerprintInvalidException(context.SongId,
                $"BeatGrid.SongId ({context.BeatGrid.SongId.Value}) does not match the supplied SongId.");
        }

        if (context.BeatGrid.SourceHash != context.SourceHash)
        {
            throw new BarFingerprintInvalidException(context.SongId,
                "BeatGrid.SourceHash does not match the supplied SourceHash -- stale or mismatched analysis.");
        }

        if (context.Energy.SongId != context.SongId)
        {
            throw new BarFingerprintInvalidException(context.SongId,
                $"EnergyEnvelope.SongId ({context.Energy.SongId.Value}) does not match the supplied SongId.");
        }

        if (context.Energy.SourceHash != context.SourceHash)
        {
            throw new BarFingerprintInvalidException(context.SongId,
                "EnergyEnvelope.SourceHash does not match the supplied SourceHash -- stale or mismatched analysis.");
        }

        if (string.IsNullOrWhiteSpace(context.Melody.SourceAssetHash))
        {
            throw new BarFingerprintInvalidException(context.SongId, "MelodyTrack.SourceAssetHash is empty -- missing media.");
        }

        if (context.Energy.Settings.AnalysisSampleRate <= 0)
        {
            throw new BarFingerprintInvalidException(context.SongId,
                $"EnergyEnvelope.Settings.AnalysisSampleRate ({context.Energy.Settings.AnalysisSampleRate}) must be positive.");
        }

        if (settings.ChromaBins <= 0)
        {
            throw new BarFingerprintInvalidException(context.SongId,
                $"BarFingerprintSettings.ChromaBins ({settings.ChromaBins}) must be positive.");
        }

        if (string.IsNullOrWhiteSpace(settings.RecipeId))
        {
            throw new BarFingerprintInvalidException(context.SongId, "BarFingerprintSettings.RecipeId must not be empty.");
        }
    }

    private async Task<SongFingerprintSet?> TryLoadStillValidCacheAsync(
        string path, BarFingerprintContext context, BarFingerprintSettings settings, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        SongFingerprintSet existing;
        try
        {
            existing = await _store.LoadAsync(path, ct).ConfigureAwait(false);
        }
        catch (BarFingerprintCorruptException)
        {
            return null;
        }

        var stillValid = existing.SourceHash == context.SourceHash
            && existing.SourceBeatGridVersionId == context.BeatGrid.VersionId
            && existing.AnalyzerVersion == Version
            && existing.Settings == settings;

        return stillValid ? existing : null;
    }

    private static IReadOnlyList<BarFingerprint> ComputeBars(BarFingerprintContext context, BarFingerprintSettings settings, CancellationToken ct)
    {
        if (context.BeatGrid.Beats.Length == 0)
        {
            // "silence... must be handled explicitly": no viable beat grid cannot support any bar
            // partition at all -- an explicit, empty, non-throwing result.
            return [];
        }

        var sampleRate = context.Energy.Settings.AnalysisSampleRate;
        var songEndTicks = context.Energy.Frames.Length == 0
            ? context.BeatGrid.Beats[^1].TimeTicks
            : SamplesToTicks(context.Energy.Frames[^1].EndSample, sampleRate);

        var barStarts = context.BeatGrid.Beats
            .GroupBy(b => b.BarIndex)
            .Select(g => (BarIndex: g.Key, StartTicks: g.Min(b => b.TimeTicks)))
            .OrderBy(b => b.StartTicks)
            .ToList();

        var result = new List<BarFingerprint>(barStarts.Count);
        for (var i = 0; i < barStarts.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var start = barStarts[i].StartTicks;
            var end = i + 1 < barStarts.Count ? barStarts[i + 1].StartTicks : songEndTicks;

            if (end <= start)
            {
                throw new BarFingerprintInvalidException(context.SongId,
                    $"Computed bar window for BarIndex {barStarts[i].BarIndex} is non-positive ({start}-{end}) -- invalid timing boundary.");
            }

            var window = new TimeWindow(start, end);
            result.Add(ComputeBarFingerprint(barStarts[i].BarIndex, window, context, settings, sampleRate));
        }

        return result;
    }

    private static BarFingerprint ComputeBarFingerprint(
        int barIndex, TimeWindow window, BarFingerprintContext context, BarFingerprintSettings settings, int sampleRate)
    {
        double energySum = 0.0;
        double energySumSq = 0.0;
        var frameCount = 0;

        foreach (var frame in context.Energy.Frames)
        {
            var frameStartTicks = SamplesToTicks(frame.StartSample, sampleRate);
            var frameEndTicks = SamplesToTicks(frame.EndSample, sampleRate);
            if (frameEndTicks <= window.StartTicks || frameStartTicks >= window.EndTicks)
            {
                continue;
            }

            energySum += frame.NormalizedEnergy;
            energySumSq += (double)frame.NormalizedEnergy * frame.NormalizedEnergy;
            frameCount++;
        }

        var meanEnergy = frameCount > 0 ? energySum / frameCount : 0.0;
        var energyVariance = frameCount > 0
            ? Math.Max(0.0, (energySumSq / frameCount) - (meanEnergy * meanEnergy))
            : 0.0;

        var chroma = new double[settings.ChromaBins];
        double chromaWeightTotal = 0.0;

        foreach (var note in context.Melody.Notes)
        {
            var overlapStart = Math.Max(note.StartTicks, window.StartTicks);
            var overlapEnd = Math.Min(note.EndTicks, window.EndTicks);
            if (overlapEnd <= overlapStart)
            {
                continue;
            }

            var weight = (double)(overlapEnd - overlapStart);
            var bin = ((note.MidiNote % settings.ChromaBins) + settings.ChromaBins) % settings.ChromaBins;
            chroma[bin] += weight;
            chromaWeightTotal += weight;
        }

        if (chromaWeightTotal > 0.0)
        {
            for (var i = 0; i < chroma.Length; i++)
            {
                chroma[i] /= chromaWeightTotal;
            }
        }

        var features = new double[2 + settings.ChromaBins];
        features[0] = meanEnergy;
        features[1] = energyVariance;
        Array.Copy(chroma, 0, features, 2, chroma.Length);

        var isSilent = meanEnergy <= 0.0 && chromaWeightTotal <= 0.0;
        var confidence = frameCount > 0 ? 1.0 : 0.0;

        return new BarFingerprint(barIndex, window, features, confidence, isSilent);
    }

    private static long SamplesToTicks(long samples, int sampleRate) =>
        samples * TicksPerSecond / sampleRate;
}
