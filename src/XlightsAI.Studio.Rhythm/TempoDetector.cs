namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// PHASE-05 Step 033 — real implementation of <see cref="ITempoDetector"/>: an onset-autocorrelation
/// tempo detector (Section 3 architectural design notes: "evaluate autocorrelation/periodicity and
/// known half/double-time signals"). Consumes an already-built <see cref="OnsetAnalysis"/> (never
/// re-decodes audio), quantizes onset strengths into fixed-width time bins, and scores every candidate
/// lag in the requested BPM range by its normalized autocorrelation — the same periodicity that makes an
/// isochronous beat's own integer multiples (half-time, double-time) genuinely score as alternate peaks,
/// not a fabricated list.
/// </summary>
/// <remarks>
/// <b>Half/double-time alternatives</b> (this step's own title): for a perfectly isochronous source, the
/// true period's own integer multiples are mathematically also periods of the same impulse train, so
/// they naturally surface as additional (lower, but non-trivial) autocorrelation peaks. This detector
/// does not rely on that alone — after ranking the peaks found by the full lag scan, it explicitly also
/// evaluates the half-time and double-time lag of the single strongest peak (via the same real
/// autocorrelation function, not a fabricated score) and folds them into the candidate pool if they fall
/// inside the requested <see cref="TempoDetectionSettings.MinBpm"/>/<see cref="TempoDetectionSettings.MaxBpm"/>
/// range and are not already present, so an ambiguous tempo is represented even on sparser or less
/// perfectly isochronous input.
/// </remarks>
public sealed class TempoDetector : ITempoDetector
{
    /// <summary>Burned into every <see cref="TempoAnalysis.AnalyzerVersion"/> this detector produces.</summary>
    public const string Version = "XlightsAI.Studio.Rhythm.TempoDetector/1.0.0";

    /// <summary>This step's own disclosed simplification (Section 3: downbeat/meter estimation is "optional... with confidence and manual labeling," not solved here) — every candidate reports the same placeholder meter.</summary>
    private const string DefaultMeterGuess = "4/4";

    /// <summary>100 ns ticks/s, same canonical unit conversion every other <c>Rhythm</c> type uses.</summary>
    private const long TicksPerSecond = 10_000_000L;

    private readonly ITempoAnalysisStore _store;

    public TempoDetector(ITempoAnalysisStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<TempoAnalysis> DetectAsync(OnsetAnalysis onsets, TempoDetectionSettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(onsets);
        ArgumentNullException.ThrowIfNull(settings);

        // "Invalid timing boundaries... must be handled explicitly" (Step 033's own failure/edge case):
        // reject a scan range/resolution that cannot produce a meaningful result before any computation
        // or cache lookup.
        if (settings.MinBpm <= 0)
        {
            throw new TempoAnalysisInvalidException(onsets.SongId, $"TempoDetectionSettings.MinBpm ({settings.MinBpm}) must be positive.");
        }

        if (settings.MaxBpm <= settings.MinBpm)
        {
            throw new TempoAnalysisInvalidException(onsets.SongId, $"TempoDetectionSettings.MaxBpm ({settings.MaxBpm}) must be greater than MinBpm ({settings.MinBpm}).");
        }

        if (settings.TimeResolutionSeconds <= 0)
        {
            throw new TempoAnalysisInvalidException(onsets.SongId, $"TempoDetectionSettings.TimeResolutionSeconds ({settings.TimeResolutionSeconds}) must be positive.");
        }

        if (settings.MaxCandidates <= 0)
        {
            throw new TempoAnalysisInvalidException(onsets.SongId, $"TempoDetectionSettings.MaxCandidates ({settings.MaxCandidates}) must be positive.");
        }

        ct.ThrowIfCancellationRequested();

        var path = _store.GetPath(onsets.SongId);

        if (await TryLoadStillValidCacheAsync(path, onsets, settings, ct).ConfigureAwait(false) is { } cached)
        {
            return cached;
        }

        // "Silence... must be handled explicitly": zero onsets (true silence, or missing/empty media
        // that decoded to no detectable transients) cannot support any periodicity measurement at all —
        // report that plainly as an empty, non-throwing candidate list rather than guessing or crashing.
        var candidates = onsets.Onsets.Length == 0
            ? []
            : ComputeCandidates(onsets.Onsets, settings, ct);

        var analysis = new TempoAnalysis(
            VersionId: Guid.NewGuid(),
            SongId: onsets.SongId,
            SourceHash: onsets.SourceHash,
            SourceOnsetAnalysisVersionId: onsets.VersionId,
            AnalyzerVersion: Version,
            Settings: settings,
            Candidates: candidates);

        await _store.SaveAsync(analysis, ct).ConfigureAwait(false);
        return analysis;
    }

    private async Task<TempoAnalysis?> TryLoadStillValidCacheAsync(
        string path, OnsetAnalysis onsets, TempoDetectionSettings settings, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        TempoAnalysis existing;
        try
        {
            existing = await _store.LoadAsync(path, ct).ConfigureAwait(false);
        }
        catch (TempoAnalysisCorruptException)
        {
            return null;
        }

        var stillValid = existing.SourceHash == onsets.SourceHash
            && existing.SourceOnsetAnalysisVersionId == onsets.VersionId
            && existing.AnalyzerVersion == Version
            && existing.Settings == settings;

        return stillValid ? existing : null;
    }

    private static TempoHypothesis[] ComputeCandidates(OnsetEvent[] onsetEvents, TempoDetectionSettings settings, CancellationToken ct)
    {
        var binSeconds = settings.TimeResolutionSeconds;
        var ticksPerBin = binSeconds * TicksPerSecond;

        var maxTimeTicks = onsetEvents.Max(o => o.TimeTicks);
        var binCount = (int)(maxTimeTicks / ticksPerBin) + 1;

        var impulse = new double[binCount];
        foreach (var onset in onsetEvents)
        {
            var bin = Math.Clamp((int)(onset.TimeTicks / ticksPerBin), 0, binCount - 1);
            impulse[bin] += onset.Strength;
        }

        var minLagBins = Math.Max(1, (int)Math.Round(60.0 / settings.MaxBpm / binSeconds));
        var maxLagBins = Math.Min(binCount - 1, (int)Math.Round(60.0 / settings.MinBpm / binSeconds));

        if (minLagBins > maxLagBins)
        {
            // The scanned BPM range cannot be measured against a signal this short at this resolution
            // (every candidate period would exceed the available data) — an explicit empty result, not
            // a crash or a fabricated guess.
            return [];
        }

        ct.ThrowIfCancellationRequested();

        var lagCount = maxLagBins - minLagBins + 1;
        var scores = new double[lagCount];
        for (var i = 0; i < lagCount; i++)
        {
            scores[i] = AutocorrelationScore(impulse, minLagBins + i);
        }

        ct.ThrowIfCancellationRequested();

        var peaks = new List<(int LagBins, double Score)>();
        for (var i = 0; i < lagCount; i++)
        {
            if (scores[i] <= 0)
            {
                continue;
            }

            var isPeak = (i == 0 || scores[i] >= scores[i - 1]) && (i == lagCount - 1 || scores[i] >= scores[i + 1]);
            if (isPeak)
            {
                peaks.Add((minLagBins + i, scores[i]));
            }
        }

        // "known half/double-time signals": explicitly also evaluate the half-time and double-time lag
        // of the single strongest peak found, via the same real autocorrelation score, in case they did
        // not already surface as their own local maxima.
        if (peaks.Count > 0)
        {
            var strongest = peaks.MaxBy(p => p.Score);
            foreach (var multiple in new[] { strongest.LagBins * 2, strongest.LagBins / 2 })
            {
                if (multiple < minLagBins || multiple > maxLagBins || multiple == strongest.LagBins)
                {
                    continue;
                }

                var score = AutocorrelationScore(impulse, multiple);
                if (score > 0 && !peaks.Any(p => AreLagsNearDuplicate(p.LagBins, multiple)))
                {
                    peaks.Add((multiple, score));
                }
            }
        }

        if (peaks.Count == 0)
        {
            return [];
        }

        var globalMaxScore = scores.Max();

        // Merge near-duplicate lags (a flat autocorrelation plateau can otherwise register as several
        // adjacent "peaks" a fraction of a bin apart) — strongest-first, same refractory-style
        // acceptance Step 032's onset peak-picking already uses.
        var accepted = new List<(int LagBins, double Score)>();
        foreach (var candidate in peaks.OrderByDescending(p => p.Score))
        {
            if (accepted.Any(a => AreLagsNearDuplicate(a.LagBins, candidate.LagBins)))
            {
                continue;
            }

            accepted.Add(candidate);
        }

        return accepted
            .OrderByDescending(a => a.Score)
            .Take(settings.MaxCandidates)
            .Select(a => new TempoHypothesis(
                Bpm: 60.0 / (a.LagBins * binSeconds),
                Confidence: globalMaxScore > 0 ? a.Score / globalMaxScore : 0.0,
                MeterGuess: DefaultMeterGuess))
            .ToArray();
    }

    /// <summary>Normalized (per-overlap-count) autocorrelation of <paramref name="impulse"/> at <paramref name="lagBins"/>.</summary>
    private static double AutocorrelationScore(double[] impulse, int lagBins)
    {
        var overlapCount = impulse.Length - lagBins;
        if (overlapCount <= 0)
        {
            return 0.0;
        }

        double sum = 0;
        for (var t = 0; t < overlapCount; t++)
        {
            sum += impulse[t] * impulse[t + lagBins];
        }

        return sum / overlapCount;
    }

    /// <summary>Two lags are the "same" candidate, within a 5%-of-magnitude (minimum 1 bin) tolerance.</summary>
    private static bool AreLagsNearDuplicate(int a, int b) =>
        Math.Abs(a - b) <= Math.Max(1, Math.Min(a, b) / 20);
}
