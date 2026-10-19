using System.Collections.Concurrent;
using System.Text.Json;

namespace XlightsAI.Studio.Refinement;

/// <summary>
/// JSON-file-backed <see cref="IRefinementRunStore"/>. One file per run
/// (<c>refinement-run-{id}.json</c>), written atomically (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace) -- the identical pattern
/// <see cref="XlightsAI.Studio.Xlights.JsonSequenceDocumentStore"/>/
/// <see cref="XlightsAI.Studio.Editing.JsonEditTransactionHistoryStore"/> already use for every other
/// app-owned aggregate in this repository. A per-run <see cref="SemaphoreSlim"/> serializes this
/// store instance's own concurrent calls against the same run id so a read-modify-write (claim,
/// heartbeat, save state, save attempt) never races itself; cross-process/crash safety is instead
/// provided by the heartbeat-staleness check in <see cref="TryClaimAsync"/>, not by file locking --
/// this is the explicit, disclosed scope of "transactionally claimed work" for a local single-user
/// desktop app (see <see cref="IRefinementRunStore"/>'s own remarks).
/// </summary>
public sealed class JsonRefinementRunStore : IRefinementRunStore
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private readonly string _directory;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public JsonRefinementRunStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public async Task CreateAsync(LoopRun run, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        run.Validate();

        if (run.Status != RefinementStatus.Queued)
        {
            throw new RefinementRunInvalidException(
                $"LoopRun '{run.Id}' must be created with Status={RefinementStatus.Queued} (was {run.Status}).");
        }

        if (run.ClaimToken is not null)
        {
            throw new RefinementRunInvalidException($"LoopRun '{run.Id}' must not be created already claimed.");
        }

        await WithLockAsync(run.Id, async () =>
        {
            var path = PathFor(run.Id);
            if (File.Exists(path))
            {
                throw new RefinementRunInvalidException($"A run with Id '{run.Id}' already exists.");
            }

            await WriteRecordAsync(path, new RefinementRunRecord(run, new Dictionary<int, CandidateAttempt>()), ct).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task<LoopRun?> LoadRunAsync(Guid runId, CancellationToken ct)
    {
        var record = await ReadRecordAsync(PathFor(runId), ct).ConfigureAwait(false);
        return record?.Run;
    }

    public async Task<IReadOnlyList<CandidateAttempt>> LoadAttemptsAsync(Guid runId, CancellationToken ct)
    {
        var record = await ReadRecordAsync(PathFor(runId), ct).ConfigureAwait(false);
        if (record is null)
        {
            return [];
        }

        return record.Attempts.Values.OrderBy(a => a.Attempt).ToList();
    }

    public Task<RefinementClaim?> TryClaimAsync(Guid runId, TimeSpan staleAfter, CancellationToken ct) =>
        WithLockAsync<RefinementClaim?>(runId, async () =>
        {
            var path = PathFor(runId);
            var record = await ReadRecordAsync(path, ct).ConfigureAwait(false);
            if (record is null)
            {
                return null;
            }

            var run = record.Run;
            if (RefinementStatusTransitions.IsTerminal(run.Status))
            {
                return null;
            }

            var now = DateTimeOffset.UtcNow;
            var hasLiveClaim = run.ClaimToken is not null
                && run.HeartbeatAtUtc is not null
                && now - run.HeartbeatAtUtc.Value < staleAfter;

            if (hasLiveClaim)
            {
                return null;
            }

            var token = Guid.NewGuid();
            var claimed = run with { ClaimToken = token, ClaimedAtUtc = now, HeartbeatAtUtc = now };
            await WriteRecordAsync(path, record with { Run = claimed }, ct).ConfigureAwait(false);
            return new RefinementClaim(token, claimed);
        });

    public Task HeartbeatAsync(Guid runId, Guid claimToken, CancellationToken ct) =>
        WithLockAsync(runId, async () =>
        {
            var path = PathFor(runId);
            var record = await RequireRecordAsync(path, runId, ct).ConfigureAwait(false);
            RequireClaim(record.Run, claimToken, runId);

            var updated = record.Run with { HeartbeatAtUtc = DateTimeOffset.UtcNow };
            await WriteRecordAsync(path, record with { Run = updated }, ct).ConfigureAwait(false);
        });

    public Task<LoopRun> SaveRunStateAsync(LoopRun run, Guid claimToken, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        run.Validate();

        return WithLockAsync(run.Id, async () =>
        {
            var path = PathFor(run.Id);
            var record = await RequireRecordAsync(path, run.Id, ct).ConfigureAwait(false);
            var current = record.Run;
            RequireClaim(current, claimToken, run.Id);

            if (!RefinementStatusTransitions.CanTransition(current.Status, run.Status))
            {
                throw new RefinementRunInvalidException(
                    $"LoopRun '{run.Id}' cannot transition from {current.Status} to {run.Status}.");
            }

            if (run.BestCandidateId is { } bestId)
            {
                if (!record.Attempts.Values.Any(a => a.Id == bestId))
                {
                    throw new RefinementRunInvalidException(
                        $"LoopRun '{run.Id}' BestCandidateId '{bestId}' does not name any recorded attempt.");
                }

                var best = record.Attempts.Values.First(a => a.Id == bestId);
                if (best.Violations.Count > 0)
                {
                    throw new RefinementRunInvalidException(
                        $"LoopRun '{run.Id}' BestCandidateId '{bestId}' names attempt {best.Attempt}, which carries {best.Violations.Count} violation(s) and must never be retained as the best candidate.");
                }
            }

            // Claim fields are always derived from the persisted claim, never trusted from the caller's
            // own `run` value -- a terminal status releases the claim (nothing left to resume); any other
            // status keeps the same claim token/claimed-at and refreshes the heartbeat.
            var toSave = RefinementStatusTransitions.IsTerminal(run.Status)
                ? run with { ClaimToken = null, ClaimedAtUtc = null, HeartbeatAtUtc = null }
                : run with { ClaimToken = current.ClaimToken, ClaimedAtUtc = current.ClaimedAtUtc, HeartbeatAtUtc = DateTimeOffset.UtcNow };

            // PHASE-12 Step 083: CancellationRequested is OR-merged with whatever is currently persisted,
            // never blindly overwritten by the caller's own (possibly stale) in-memory `run` value -- a
            // concurrent IRefinementOrchestrator.CancelAsync (via RequestCancellationAsync) can set this
            // flag between this caller's own last read and this save, and that flag must never be silently
            // clobbered back to false by an in-flight save that started before the cancellation request.
            toSave = toSave with { CancellationRequested = toSave.CancellationRequested || current.CancellationRequested };

            await WriteRecordAsync(path, record with { Run = toSave }, ct).ConfigureAwait(false);
            return toSave;
        });
    }

    public Task<CandidateAttempt> SaveAttemptAsync(Guid runId, Guid claimToken, CandidateAttempt attempt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        attempt.Validate();

        if (attempt.RunId != runId)
        {
            throw new RefinementRunInvalidException(
                $"CandidateAttempt '{attempt.Id}' RunId '{attempt.RunId}' does not match the target run '{runId}'.");
        }

        return WithLockAsync(runId, async () =>
        {
            var path = PathFor(runId);
            var record = await RequireRecordAsync(path, runId, ct).ConfigureAwait(false);
            RequireClaim(record.Run, claimToken, runId);

            var attempts = new Dictionary<int, CandidateAttempt>(record.Attempts) { [attempt.Attempt] = attempt };
            await WriteRecordAsync(path, record with { Attempts = attempts }, ct).ConfigureAwait(false);
            return attempt;
        });
    }

    public Task ReleaseClaimAsync(Guid runId, Guid claimToken, CancellationToken ct) =>
        WithLockAsync(runId, async () =>
        {
            var path = PathFor(runId);
            var record = await RequireRecordAsync(path, runId, ct).ConfigureAwait(false);
            RequireClaim(record.Run, claimToken, runId);

            var released = record.Run with { ClaimToken = null, ClaimedAtUtc = null, HeartbeatAtUtc = null };
            await WriteRecordAsync(path, record with { Run = released }, ct).ConfigureAwait(false);
        });

    public Task RequestCancellationAsync(Guid runId, CancellationToken ct) =>
        WithLockAsync(runId, async () =>
        {
            var path = PathFor(runId);
            var record = await RequireRecordAsync(path, runId, ct).ConfigureAwait(false);

            if (RefinementStatusTransitions.IsTerminal(record.Run.Status) || record.Run.CancellationRequested)
            {
                return;
            }

            var updated = record.Run with { CancellationRequested = true };
            await WriteRecordAsync(path, record with { Run = updated }, ct).ConfigureAwait(false);
        });

    private static void RequireClaim(LoopRun run, Guid claimToken, Guid runId)
    {
        if (run.ClaimToken != claimToken)
        {
            throw new RefinementRunInvalidException(
                $"LoopRun '{runId}' is not currently held by claim token '{claimToken}'.");
        }
    }

    private async Task<RefinementRunRecord> RequireRecordAsync(string path, Guid runId, CancellationToken ct)
    {
        var record = await ReadRecordAsync(path, ct).ConfigureAwait(false);
        return record ?? throw new RefinementRunInvalidException($"No run with Id '{runId}' exists.");
    }

    private async Task WithLockAsync(Guid runId, Func<Task> action)
    {
        await WithLockAsync<object?>(runId, async () =>
        {
            await action().ConfigureAwait(false);
            return null;
        }).ConfigureAwait(false);
    }

    private async Task<T> WithLockAsync<T>(Guid runId, Func<Task<T>> action)
    {
        var gate = _locks.GetOrAdd(runId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private string PathFor(Guid runId) => Path.Combine(_directory, $"refinement-run-{runId:N}.json");

    private async Task<RefinementRunRecord?> ReadRecordAsync(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        try
        {
            return await JsonSerializer.DeserializeAsync<RefinementRunRecord>(stream, Options, ct).ConfigureAwait(false)
                ?? throw new RefinementRunCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new RefinementRunCorruptException(path, ex);
        }
    }

    private async Task WriteRecordAsync(string path, RefinementRunRecord record, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var tempPath = $"{path}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(record, Options);
            await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ReadOnlyModelIdSetJsonConverter());
        options.Converters.Add(new ReadOnlyGuidSetJsonConverter());
        return options;
    }
}
