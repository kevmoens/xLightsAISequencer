namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Build-plan Phase 16 §2b: makes one real model call per <see cref="ExemplarEntry"/> to produce a
/// human/AI-readable "why this snippet, what it's good for" description -- a field that did not exist
/// before this phase. Called by <see cref="CorpusMiningRunner.RunAsync(string,string,TimeSpan,Action{string}?,CancellationToken,IExemplarDescriptionClient?)"/>
/// after <see cref="ExemplarIndexBuilder.Build"/> (which stays pure/synchronous and makes no LLM calls of
/// its own -- see its own class remarks) rather than threading an AI client through the builder itself.
///
/// <b>Concurrency default, disclosed:</b> no batching/cost plan has been scoped yet for Phase 17's larger
/// mining run (per the build plan, "likely hundreds to low thousands of entries once Phase 17 lands") --
/// <see cref="DefaultMaxConcurrency"/> picks a conservative, pragmatic default (a handful of calls in
/// flight at once) rather than firing every entry's call simultaneously, consistent with the
/// cost-consciousness <see cref="AiCostEstimator"/>/<see cref="AiCallLogStore"/> already establish for
/// Director/Choreographer calls. A real cost/batching plan can replace this once Phase 17 is scoped.
/// </summary>
public static class ExemplarDescriptionEnricher
{
    public const int DefaultMaxConcurrency = 4;

    /// <summary>
    /// Returns a new <see cref="ExemplarIndex"/> with every entry's <see cref="ExemplarEntry.Description"/>
    /// set from a real <paramref name="client"/> call. A single entry's call throwing does not abort the
    /// whole enrichment pass -- that entry keeps its prior (usually <see langword="null"/>) description,
    /// same "one bad item excludes itself, not the whole run" discipline as <see cref="CorpusMiningRunner"/>'s
    /// own per-song try/catch.
    /// </summary>
    public static async Task<ExemplarIndex> EnrichAsync(
        ExemplarIndex index,
        IExemplarDescriptionClient client,
        int maxConcurrency,
        Action<string>? progress,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(client);
        if (maxConcurrency <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), maxConcurrency, "Must be positive.");
        }

        var entries = index.Entries.ToArray();
        var results = new ExemplarEntry[entries.Length];
        var completed = 0;

        using var gate = new SemaphoreSlim(maxConcurrency);
        var tasks = entries.Select(async (entry, i) =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var description = await client.DescribeAsync(BuildPrompt(entry), ct).ConfigureAwait(false);
                results[i] = entry with { Description = description.Trim() };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results[i] = entry;
                progress?.Invoke($"Exemplar description failed for \"{entry.Fingerprint.SongTitle}\" bars {entry.Fingerprint.StartBarIndex}-{entry.Fingerprint.EndBarIndexExclusive}: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                gate.Release();
                var done = Interlocked.Increment(ref completed);
                if (done % 25 == 0 || done == entries.Length)
                {
                    progress?.Invoke($"Exemplar descriptions: {done}/{entries.Length}.");
                }
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        return index with { Entries = results };
    }

    /// <summary>
    /// Grounds the description in exactly the fields the build plan names: "its placed Effects, bar
    /// range, tempo, energy percentile, and section kind" -- never a fabricated song/section the model
    /// was not actually given.
    /// </summary>
    private static string BuildPrompt(ExemplarEntry entry)
    {
        var fp = entry.Fingerprint;
        var effectNames = entry.Effects.Select(e => e.Effect.EffectName).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
        var effectSummary = effectNames.Count > 0 ? string.Join(", ", effectNames) : "(none placed in this window)";

        return $"""
            You are annotating one real exemplar snippet in a Christmas-light-show choreography knowledge base.
            Write exactly one or two sentences describing why this snippet is a good worked example for an AI Director to reference when designing a similar moment, and what kind of target section it suits.
            Ground your answer only in the facts below -- never invent song lyrics, artist, or effects not listed.

            Section kind: {fp.SectionKind}
            Bar range: {fp.StartBarIndex}-{fp.EndBarIndexExclusive}
            Tempo: {fp.TempoBpm:0} BPM
            Energy percentile (within its own song): {fp.EnergyPercentile:P0}
            Effects placed in this window: {effectSummary}

            Respond with only the description text -- no preamble, no quotation marks, no restating the facts above verbatim.
            """;
    }
}
