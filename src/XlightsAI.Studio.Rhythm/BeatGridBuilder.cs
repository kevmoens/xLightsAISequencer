namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// PHASE-05 Step 034 — real implementation of <see cref="IBeatGridBuilder"/>: generates an evenly
/// spaced beat/bar grid from the top-ranked <see cref="TempoHypothesis"/> of an already built
/// <see cref="TempoAnalysis"/>, anchored to the first detected onset so the grid aligns with real
/// rhythmic evidence rather than an arbitrary song-relative zero (Section 3 architectural design
/// notes: "Represent beats with absolute ticks and bar indices" — Step 034's own title, verbatim).
/// </summary>
/// <remarks>
/// <b>Anchor, resync and coverage.</b> The grid starts exactly at the earliest onset's own
/// <see cref="OnsetEvent.TimeTicks"/> (beat 0, bar 0, always the first bar's downbeat) and steps
/// forward by the selected tempo's period. Whenever a real onset falls within
/// <see cref="BeatGridSettings.AlignmentToleranceTicks"/> of a predicted beat position, that beat is
/// placed exactly at the real onset's own timestamp and the *next* beat is predicted from that
/// corrected position — a standard phase-lock correction that prevents a small per-period tempo
/// quantization error from silently accumulating into a large drift over many beats (this step's own
/// required acceptance, verbatim: "Tempo fixture aligns events within configured threshold," which a
/// pure fixed-period extrapolation from a single anchor cannot guarantee over a long fixture). A beat
/// with no nearby onset is left at its predicted (uncorrected) position and does not move the phase.
/// Generation stops half a period past the last detected onset — covering every real onset without
/// extrapolating indefinitely past the end of the analyzed rhythmic content.
/// <para>
/// <b>Confidence (this step's own "avoid asserting uncertainty away" guidance).</b> A generated beat
/// is never reported as uniformly certain. If a real onset falls within
/// <see cref="BeatGridSettings.AlignmentToleranceTicks"/> of the beat's predicted position, confidence
/// is the selected tempo's own confidence scaled into the upper half of <c>[0, 1]</c> by that onset's
/// strength (<see cref="AlignedConfidenceFloor"/> plus a strength-weighted remainder) — real
/// corroborating evidence always raises confidence at least to that floor, regardless of exactly how
/// strong the matched onset was. Otherwise the beat is purely interpolated from the tempo period with
/// no nearby onset to support it, and confidence is scaled well below that same floor
/// (<see cref="UnalignedConfidenceDecay"/>) to disclose that lower certainty — the two bands never
/// overlap, so "aligned" vs. "unaligned" is always a real, visible confidence distinction, not one that
/// chance onset-strength variation could erase.
/// </para>
/// </remarks>
public sealed class BeatGridBuilder : IBeatGridBuilder
{
    /// <summary>Burned into every <see cref="BeatGrid.AnalyzerVersion"/> this builder produces.</summary>
    public const string Version = "XlightsAI.Studio.Rhythm.BeatGridBuilder/1.0.0";

    /// <summary>Minimum confidence multiplier (of the selected tempo's own confidence) an aligned beat always receives, regardless of the matched onset's own strength — the floor a real corroborating onset guarantees.</summary>
    private const double AlignedConfidenceFloor = 0.6;

    /// <summary>Confidence multiplier applied to a beat with no corroborating onset inside <see cref="BeatGridSettings.AlignmentToleranceTicks"/> — a purely interpolated, less certain position. Deliberately well below <see cref="AlignedConfidenceFloor"/> so the two bands never overlap.</summary>
    private const double UnalignedConfidenceDecay = 0.3;

    private readonly IBeatGridStore _store;

    public BeatGridBuilder(IBeatGridStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<BeatGrid> BuildAsync(OnsetAnalysis onsets, TempoAnalysis tempo, BeatGridSettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(onsets);
        ArgumentNullException.ThrowIfNull(tempo);
        ArgumentNullException.ThrowIfNull(settings);

        // "Invalid timing boundaries... must be handled explicitly" (Step 034's own failure/edge
        // case): reject a meter/tolerance that cannot produce a meaningful result, and reject a
        // tempo/onset pair that does not actually belong together, before any computation or cache
        // lookup.
        if (settings.BeatsPerBar <= 0)
        {
            throw new BeatGridInvalidException(onsets.SongId, $"BeatGridSettings.BeatsPerBar ({settings.BeatsPerBar}) must be positive.");
        }

        if (settings.AlignmentToleranceTicks < 0)
        {
            throw new BeatGridInvalidException(onsets.SongId, $"BeatGridSettings.AlignmentToleranceTicks ({settings.AlignmentToleranceTicks}) must not be negative.");
        }

        if (onsets.SongId != tempo.SongId)
        {
            throw new BeatGridInvalidException(onsets.SongId, $"TempoAnalysis.SongId ({tempo.SongId.Value}) does not match the supplied OnsetAnalysis.SongId ({onsets.SongId.Value}).");
        }

        if (tempo.SourceOnsetAnalysisVersionId != onsets.VersionId)
        {
            throw new BeatGridInvalidException(onsets.SongId, $"TempoAnalysis.SourceOnsetAnalysisVersionId ({tempo.SourceOnsetAnalysisVersionId}) does not match the supplied OnsetAnalysis.VersionId ({onsets.VersionId}) — stale or mismatched tempo analysis.");
        }

        ct.ThrowIfCancellationRequested();

        var path = _store.GetPath(onsets.SongId);

        if (await TryLoadStillValidCacheAsync(path, onsets, tempo, settings, ct).ConfigureAwait(false) is { } cached)
        {
            return cached;
        }

        // Step 035's own "dependency invalidation": a stale-by-reanalysis cache that already carries
        // real user corrections (CorrectionVersion > 0) must never be silently discarded and
        // overwritten by a fresh algorithmic rebuild (Section 3: "changes invalidate downstream
        // generated gestures but not manually accepted effects"; quality-gate matrix row "Algorithm
        // cache invalidation does not delete user-approved grid"). An uncorrected cache (CorrectionVersion
        // == 0) still rebuilds exactly as Step 034 already did.
        if (await TryLoadExistingAsync(path, ct).ConfigureAwait(false) is { CorrectionVersion: > 0 } corrected)
        {
            throw new BeatGridRequiresRebaseException(onsets.SongId, corrected.VersionId, corrected.CorrectionVersion);
        }

        // "Silence... must be handled explicitly": no viable tempo candidate (and therefore, by
        // TempoDetector's own construction, no onsets either) cannot support a beat grid at all —
        // report that plainly as a null selected tempo and an empty, non-throwing beat list.
        var selectedTempo = tempo.Candidates.Length > 0 ? tempo.Candidates[0] : null;
        var beats = selectedTempo is null || onsets.Onsets.Length == 0
            ? []
            : ComputeBeats(onsets.Onsets, selectedTempo, settings, ct);

        var grid = new BeatGrid(
            VersionId: Guid.NewGuid(),
            SongId: onsets.SongId,
            SourceHash: onsets.SourceHash,
            SourceOnsetAnalysisVersionId: onsets.VersionId,
            SourceTempoAnalysisVersionId: tempo.VersionId,
            AnalyzerVersion: Version,
            Settings: settings,
            SelectedTempo: selectedTempo,
            Beats: beats,
            Candidates: tempo.Candidates,
            CorrectionVersion: 0);

        await _store.SaveAsync(grid, ct).ConfigureAwait(false);
        return grid;
    }

    private async Task<BeatGrid?> TryLoadStillValidCacheAsync(
        string path, OnsetAnalysis onsets, TempoAnalysis tempo, BeatGridSettings settings, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        BeatGrid existing;
        try
        {
            existing = await _store.LoadAsync(path, ct).ConfigureAwait(false);
        }
        catch (BeatGridCorruptException)
        {
            return null;
        }

        var stillValid = existing.SourceHash == onsets.SourceHash
            && existing.SourceOnsetAnalysisVersionId == onsets.VersionId
            && existing.SourceTempoAnalysisVersionId == tempo.VersionId
            && existing.AnalyzerVersion == Version
            && existing.Settings == settings;

        return stillValid ? existing : null;
    }

    /// <summary>Reopens whatever grid currently exists at <paramref name="path"/> regardless of staleness — used only to decide whether a stale-by-reanalysis cache already carries user corrections that must not be silently discarded (see <see cref="BuildAsync"/>). A corrupt or missing file is treated as "nothing to protect," identical to <see cref="TryLoadStillValidCacheAsync"/>'s own handling.</summary>
    private async Task<BeatGrid?> TryLoadExistingAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return await _store.LoadAsync(path, ct).ConfigureAwait(false);
        }
        catch (BeatGridCorruptException)
        {
            return null;
        }
    }

    private static BeatMarker[] ComputeBeats(OnsetEvent[] onsetEvents, TempoHypothesis tempo, BeatGridSettings settings, CancellationToken ct)
    {
        const long TicksPerSecond = 10_000_000L;

        var periodTicks = (long)Math.Round(60.0 / tempo.Bpm * TicksPerSecond);
        if (periodTicks <= 0)
        {
            // A degenerate (non-positive after rounding) period cannot be measured against — an
            // explicit empty result, not a crash or an infinite generation loop.
            return [];
        }

        var ordered = onsetEvents.OrderBy(o => o.TimeTicks).ToArray();
        var coverageEndTicks = ordered[^1].TimeTicks + (periodTicks / 2);

        var beats = new List<BeatMarker>();
        var k = 0;
        var predictedTicks = ordered[0].TimeTicks;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (predictedTicks > coverageEndTicks)
            {
                break;
            }

            var nearest = FindNearestOnset(ordered, predictedTicks);
            var distanceTicks = Math.Abs(nearest.TimeTicks - predictedTicks);
            var isAligned = distanceTicks <= settings.AlignmentToleranceTicks;

            // Phase-lock resync (see class remarks): an aligned beat is placed exactly at the real
            // onset it corroborates, and the next prediction steps forward from that corrected
            // position — never from the uncorrected prediction — so quantization error cannot
            // accumulate across many beats.
            var beatTicks = isAligned ? nearest.TimeTicks : predictedTicks;

            var confidence = isAligned
                ? Math.Clamp(tempo.Confidence * (AlignedConfidenceFloor + ((1.0 - AlignedConfidenceFloor) * nearest.Strength)), 0.0, 1.0)
                : Math.Clamp(tempo.Confidence * UnalignedConfidenceDecay, 0.0, 1.0);

            beats.Add(new BeatMarker(
                Id: Guid.NewGuid(),
                TimeTicks: beatTicks,
                BeatWithinBar: k % settings.BeatsPerBar,
                BarIndex: k / settings.BeatsPerBar,
                Confidence: confidence,
                UserCorrected: false));

            predictedTicks = beatTicks + periodTicks;
            k++;
        }

        return [.. beats];
    }

    private static OnsetEvent FindNearestOnset(OnsetEvent[] ordered, long ticks)
    {
        var best = ordered[0];
        var bestDistance = Math.Abs(best.TimeTicks - ticks);

        for (var i = 1; i < ordered.Length; i++)
        {
            var distance = Math.Abs(ordered[i].TimeTicks - ticks);
            if (distance < bestDistance)
            {
                best = ordered[i];
                bestDistance = distance;
            }
        }

        return best;
    }
}
