namespace XlightsAI.Studio.Stems;

/// <summary>
/// Build-plan "richer analysis tools" Phase 6 (2026-11-02): one word recovered from a song's own isolated
/// vocal stem by <see cref="ILyricTranscriber"/>. <see cref="StartTicks"/>/<see cref="EndTicks"/> are a
/// disclosed approximation, not a true forced alignment: Whisper's own decoder only emits segment-level
/// timestamp tokens (a start/end pair bounding a run of several words), so the words within one segment
/// have their timing evenly distributed across that segment's own measured span, weighted by each word's
/// character length. Word recognition on SUNG vocals (as opposed to plain speech) is also measurably less
/// reliable -- treat a transcript hit as a strong signal for "approximately here," never ground truth.
/// </summary>
/// <param name="Text">The recognized word, as decoded (no punctuation stripped beyond what the tokenizer itself produces).</param>
/// <param name="StartTicks">Approximate start (100ns ticks from song start).</param>
/// <param name="EndTicks">Approximate end (100ns ticks from song start).</param>
/// <param name="Confidence">Carried over from the segment's own decoding confidence (not a per-word score Whisper does not expose at this granularity).</param>
public sealed record TranscribedWord(string Text, long StartTicks, long EndTicks, float Confidence);
