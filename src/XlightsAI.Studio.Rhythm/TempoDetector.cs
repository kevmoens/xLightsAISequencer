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

        // PHASE-28 §3.1 — "metrical-level resolution": raw autocorrelation on its own cannot tell a true
        // beat period apart from an equally-periodic bar period (a stronger accent every Nth beat is also,
        // by construction, periodic at the beat period's own integer multiples). Re-score the strongest
        // peak's half/third/quarter and double/triple lags through the same real autocorrelation function
        // and prefer the shortest one that is both nearly as strong and lands in the musically preferred
        // BPM band — never a fabricated or silently-assumed level.
        var strongestPeak = accepted.MaxBy(a => a.Score);
        var resolvedLagBins = ResolveMetricalLevel(impulse, strongestPeak, minLagBins, maxLagBins, binSeconds, settings);

        if (resolvedLagBins != strongestPeak.LagBins)
        {
            var resolvedScore = AutocorrelationScore(impulse, resolvedLagBins);
            accepted.RemoveAll(a => AreLagsNearDuplicate(a.LagBins, resolvedLagBins));
            accepted.Add((resolvedLagBins, resolvedScore));
        }

        // The metrically-resolved candidate is reported first — it is the selected beat period
        // downstream (BeatGridBuilder.BuildAsync takes Candidates[0]) — even though its own raw
        // autocorrelation score (and therefore Confidence) can genuinely be lower than the bar-level
        // peak it was resolved from; the remainder stay ordered by Confidence descending.
        var ordered = accepted
            .Where(a => !AreLagsNearDuplicate(a.LagBins, resolvedLagBins))
            .OrderByDescending(a => a.Score)
            .Prepend(accepted.First(a => AreLagsNearDuplicate(a.LagBins, resolvedLagBins)))
            .Take(settings.MaxCandidates);

        return ordered
            .Select(a =>
            {
                var (meterGuess, meterConfidence) = DetectMeter(impulse, a.LagBins, settings);
                return new TempoHypothesis(
                    Bpm: 60.0 / (a.LagBins * binSeconds),
                    Confidence: globalMaxScore > 0 ? a.Score / globalMaxScore : 0.0,
                    MeterGuess: meterGuess,
                    MeterConfidence: meterConfidence);
            })
            .ToArray();
    }

    /// <summary>
    /// PHASE-28 §3.1 — given the strongest autocorrelation peak's lag, also scores its half/third/quarter
    /// and double/triple lags and reports the shortest one whose score is within
    /// <see cref="TempoDetectionSettings.BeatLevelTolerance"/> of the strongest peak's own score AND whose
    /// resulting BPM falls inside [<see cref="TempoDetectionSettings.PreferredBpmMin"/>,
    /// <see cref="TempoDetectionSettings.PreferredBpmMax"/>]. Falls back to the strongest peak's own lag,
    /// unchanged, when no metrical level satisfies both conditions.
    /// </summary>
    private static int ResolveMetricalLevel(
        double[] impulse, (int LagBins, double Score) strongest, int minLagBins, int maxLagBins, double binSeconds, TempoDetectionSettings settings)
    {
        var candidates = new List<(int LagBins, double Score)> { strongest };
        foreach (var divisor in new[] { 2.0, 3.0, 4.0 })
        {
            // Round, never truncate: e.g. a 95-bin bar lag / 3 truncates to 31 bins (193.5 BPM, just
            // outside a typical preferred-BPM upper bound) instead of the much closer, correctly
            // rounded 32 bins (187.5 BPM) — a real beat period lost to integer-division rounding, not
            // genuinely absent from the data.
            var lagBins = (int)Math.Round(strongest.LagBins / divisor);
            if (lagBins < minLagBins || lagBins > maxLagBins || lagBins == strongest.LagBins)
            {
                continue;
            }

            candidates.Add((lagBins, AutocorrelationScore(impulse, lagBins)));
        }

        foreach (var multiplier in new[] { 2, 3 })
        {
            var lagBins = strongest.LagBins * multiplier;
            if (lagBins < minLagBins || lagBins > maxLagBins || lagBins == strongest.LagBins)
            {
                continue;
            }

            candidates.Add((lagBins, AutocorrelationScore(impulse, lagBins)));
        }

        // BeatLevelTolerance is how much a metrical level is allowed to deviate below the strongest
        // peak's own score, not the fraction it must retain: a bar-level accent is often intrinsically
        // much more self-similar (and therefore much higher-scoring under raw autocorrelation) than its
        // own true beat subdivision, so the threshold must tolerate a real beat period scoring well
        // below the bar period it was derived from, not merely near-equal to it.
        var scoreThreshold = strongest.Score * (1.0 - settings.BeatLevelTolerance);
        var eligible = candidates
            .Where(c => c.Score >= scoreThreshold)
            .Where(c => IsWithinPreferredBpmRange(60.0 / (c.LagBins * binSeconds), settings))
            .ToArray();

        return eligible.Length == 0 ? strongest.LagBins : eligible.MinBy(c => c.LagBins).LagBins;
    }

    private static bool IsWithinPreferredBpmRange(double bpm, TempoDetectionSettings settings) =>
        bpm >= settings.PreferredBpmMin && bpm <= settings.PreferredBpmMax;

    /// <summary>
    /// PHASE-28 §3.1 — real meter detection: for each candidate meter <c>g</c> (beats per bar), compares
    /// mean onset strength at beat positions <c>k ≡ 0 (mod g)</c> (candidate downbeats, counted from a
    /// shared downbeat anchor) against every other beat position (offbeats). The meter with the highest
    /// downbeat/offbeat ratio wins.
    /// <para>
    /// The anchor is resolved once per candidate tempo (<see cref="LoudestOnsetBin"/>), as the single
    /// loudest onset within the first full bar's worth of beats — not simply the first detected onset
    /// chronologically, which for a song with a pickup/anacrusis is not guaranteed to land on the real
    /// downbeat at all — and shared across every candidate meter here. Searching a *separate* phase per
    /// meter was tried and rejected (see implementation evidence): with <c>g</c> phases tried per
    /// candidate, a larger <c>g</c> gets systematically more chances for noise alone to produce a high
    /// ratio, biasing every result toward the largest meter in <see cref="TempoDetectionSettings.CandidateMeters"/>
    /// regardless of the real music. One shared anchor avoids that bias entirely.
    /// </para>
    /// <para>
    /// <see cref="TempoHypothesis.MeterConfidence"/> reports how far the winning ratio actually is from
    /// "no distinction at all" (ratio of 1) — never a bare, undisclosed guess, even when no meter is
    /// genuinely distinguishable (e.g. a perfectly isochronous, unaccented click train, which always
    /// reports confidence 0).
    /// </para>
    /// <para>
    /// <b>Disclosed limitation, measured not assumed:</b> a candidate meter that evenly divides a larger
    /// one is, by construction, also "periodic" at that larger meter (a true 3-beat accent pattern is
    /// mathematically also periodic every 6 beats) — real tracks with a strong phrase-level repeat (an
    /// accent every *other* bar, not just every bar) can therefore make the larger, nested multiple
    /// outscore the musically "correct" smaller meter. Several tie-break heuristics (preferring the
    /// smallest meter within a score tolerance of the best, in both directions) were tried against this
    /// codebase's own real corpus and rejected: each one that fixed the nested-multiple case regressed
    /// at least one genuinely simple real 4/4 track (a true half-note backbeat accent, e.g. a snare on
    /// beats 2 and 4, scores close enough to the full bar that "prefer smaller when close" wrongly picks
    /// 2/4 over 4/4). No tie-break found makes both directions correct at once with this single ratio
    /// signal, so this detector reports the plain highest-ratio meter, honestly, rather than a tuned
    /// heuristic that trades one real failure mode for another.
    /// </para>
    /// </summary>
    private static (string MeterGuess, double MeterConfidence) DetectMeter(double[] impulse, int lagBins, TempoDetectionSettings settings)
    {
        var candidateMeters = settings.EffectiveCandidateMeters;
        var maxMeter = candidateMeters.Length == 0 ? 1 : candidateMeters.Max();
        var anchorBin = LoudestOnsetBin(impulse, 0, Math.Min(impulse.Length, lagBins * maxMeter));

        var ratioByMeter = new Dictionary<int, double>();

        foreach (var meter in candidateMeters)
        {
            if (meter <= 1)
            {
                continue;
            }

            double downbeatSum = 0, offbeatSum = 0;
            var downbeatCount = 0;
            var offbeatCount = 0;

            for (var k = 0; anchorBin + k * (long)lagBins < impulse.Length; k++)
            {
                var strength = impulse[anchorBin + k * lagBins];
                if (k % meter == 0)
                {
                    downbeatSum += strength;
                    downbeatCount++;
                }
                else
                {
                    offbeatSum += strength;
                    offbeatCount++;
                }
            }

            if (downbeatCount == 0 || offbeatCount == 0)
            {
                continue;
            }

            var offbeatMean = offbeatSum / offbeatCount;
            if (offbeatMean <= 0)
            {
                continue;
            }

            ratioByMeter[meter] = (downbeatSum / downbeatCount) / offbeatMean;
        }

        if (ratioByMeter.Count == 0)
        {
            return ("4/4", 0.0);
        }

        var bestMeter = ratioByMeter.MaxBy(kv => kv.Value).Key;
        var bestRatio = ratioByMeter[bestMeter];
        var meterConfidence = Math.Clamp((bestRatio - 1.0) / bestRatio, 0.0, 1.0);
        return ($"{bestMeter}/4", meterConfidence);
    }

    /// <summary>Index of the single highest-strength bin in <c>impulse[startBin, endBinExclusive)</c>, used as the shared downbeat anchor every candidate meter is measured against — ties keep the earliest bin.</summary>
    private static int LoudestOnsetBin(double[] impulse, int startBin, int endBinExclusive)
    {
        var bestBin = startBin;
        var bestStrength = impulse[startBin];
        for (var i = startBin + 1; i < endBinExclusive; i++)
        {
            if (impulse[i] > bestStrength)
            {
                bestStrength = impulse[i];
                bestBin = i;
            }
        }

        return bestBin;
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
