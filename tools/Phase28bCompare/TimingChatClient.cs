using System.Diagnostics;
using System.Collections.Concurrent;
using XlightsAI.Studio.Director;

namespace XlightsAI.Studio.Phase28bCompare;

/// <summary>Build-plan Phase 28b §8 item 4c: records real per-call wall-clock latency and token usage for every Choreographer call issued through it, since <see cref="AiCallLogEntry"/> itself carries no latency field.</summary>
public sealed class CallTimingRecorder
{
    public sealed record CallRecord(TimeSpan Latency, int? InputTokens, int? OutputTokens);

    private readonly ConcurrentBag<CallRecord> _records = [];

    public void Record(CallRecord record) => _records.Add(record);

    public void Reset() => _records.Clear();

    public Snapshot TakeSnapshot() => new(_records.ToList());

    public sealed record Snapshot(IReadOnlyList<CallRecord> Records)
    {
        public string FormatAgainstBaseline()
        {
            if (Records.Count == 0)
            {
                return "No real Choreographer calls recorded for this leg.";
            }

            var withInput = Records.Where(r => r.InputTokens is not null).Select(r => r.InputTokens!.Value).ToList();
            var withOutput = Records.Where(r => r.OutputTokens is not null).Select(r => r.OutputTokens!.Value).ToList();
            var avgInput = withInput.Count > 0 ? withInput.Average() : (double?)null;
            var avgOutput = withOutput.Count > 0 ? withOutput.Average() : (double?)null;
            var avgLatency = Records.Average(r => r.Latency.TotalSeconds);
            var maxLatency = Records.Max(r => r.Latency.TotalSeconds);

            return $"{Records.Count} real call(s): avg input={(avgInput is { } i ? $"{i:0}" : "n/a")} tokens, " +
                   $"avg output={(avgOutput is { } o ? $"{o:0}" : "n/a")} tokens, avg latency={avgLatency:0.0}s, max latency={maxLatency:0.0}s.";
        }
    }
}

/// <summary>Thin pass-through decorator over a real <see cref="IChatCompletionClient"/> that times every call -- no retry/caching/behavior change of its own.</summary>
public sealed class TimingChatClient(IChatCompletionClient inner, CallTimingRecorder recorder) : IChatCompletionClient
{
    public async Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var result = await inner.CompleteAsync(request, ct).ConfigureAwait(false);
            watch.Stop();
            recorder.Record(new CallTimingRecorder.CallRecord(watch.Elapsed, result.Usage.InputTokens, result.Usage.OutputTokens));
            return result;
        }
        catch
        {
            watch.Stop();
            recorder.Record(new CallTimingRecorder.CallRecord(watch.Elapsed, null, null));
            throw;
        }
    }
}
