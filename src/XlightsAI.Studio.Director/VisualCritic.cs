using System.Text.Json;
using XlightsAI.Studio.Rendering;

namespace XlightsAI.Studio.Director;

/// <summary>Which side of a pairwise presentation won, from the model's own verdict.</summary>
public enum PairwiseWinner
{
    Candidate,
    Reference,
}

/// <summary>
/// One pairwise visual-critique call's raw outcome, before the two orderings are reconciled by
/// <see cref="VisualCritic.CompareAsync"/>.
/// </summary>
/// <param name="Winner">Which side the model picked, translated back out of whichever left/right position it actually occupied in that one call.</param>
/// <param name="Confidence">The model's own self-reported confidence in <c>[0,1]</c>.</param>
public sealed record PairwiseVerdict(PairwiseWinner Winner, double Confidence);

/// <summary>
/// Build-plan Phase 7c visual critique result for one section: both orderings' own verdicts plus the
/// reconciled outcome. Architecture §9's judging discipline, applied directly: "randomize order and run
/// both" -- <see cref="FlippedBetweenOrderings"/> is the low-confidence signal that discipline exists to
/// surface (position bias "is worth 5-15 points" per the same section), not something to hide by silently
/// picking one ordering's answer.
/// </summary>
/// <param name="SectionId">Which section this comparison was for.</param>
/// <param name="FirstOrdering">The verdict when the candidate frame was presented first.</param>
/// <param name="SecondOrdering">The verdict when the reference frame was presented first.</param>
/// <param name="FlippedBetweenOrderings"><see langword="true"/> when the two orderings disagreed on the winner -- a real low-confidence signal, never averaged away.</param>
/// <param name="AgreedWinner">The winner both orderings agreed on, or <see langword="null"/> when they flipped.</param>
public sealed record SectionVisualCritique(
    Guid SectionId, PairwiseVerdict FirstOrdering, PairwiseVerdict SecondOrdering, bool FlippedBetweenOrderings, PairwiseWinner? AgreedWinner);

/// <summary>
/// Build-plan Phase 7c: "extract frames at section boundaries and peak moments and hand them to a vision
/// model with the Show Bible's stated intent for that section." Pairwise only (never absolute scoring),
/// randomized order run both ways, no requested chain-of-thought -- architecture §9's own three judging-
/// discipline rules, applied verbatim ("on LitBench... requiring a rationale decreased agreement" is why
/// the prompt below asks for a verdict and confidence only, nothing else).
/// </summary>
public sealed class VisualCritic
{
    /// <summary>
    /// The point radius <see cref="CompareAsync"/> rasterizes frames at -- larger than
    /// <see cref="PreviewFrameRasterizer"/>'s own default, since a judge assessing overall colour/
    /// brightness character needs visible colour mass rather than faithfully-scaled dots (a real
    /// house typically has far more addressable points than this prompt-sized raster has pixels).
    /// Public so a test asserting against the exact bytes <see cref="CompareAsync"/> sends can
    /// rasterize identically rather than duplicating a magic number.
    /// </summary>
    public const int FrameRasterPointRadiusPixels = 14;

    private readonly IVisionCapableChatCompletionClient _client;
    private readonly string _modelName;
    private readonly Random _random;

    public VisualCritic(IVisionCapableChatCompletionClient client, string modelName, Random? random = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        _modelName = modelName;
        _random = random ?? new Random();
    }

    /// <summary>
    /// Compares <paramref name="candidateFrame"/> against <paramref name="referenceFrame"/> (the previous
    /// iteration's frame at the same timestamp, or a retrieved corpus exemplar) for whether it better
    /// delivers <paramref name="sectionIntent"/> (<c>Look.Intent</c>'s own one-sentence text). Issues two
    /// calls -- left/right order randomized for the first, reversed for the second -- never one.
    /// </summary>
    /// <exception cref="AiCallRejectedException">Either call failed, or the model's response could not be parsed as a verdict.</exception>
    public async Task<SectionVisualCritique> CompareAsync(
        Guid sectionId, PreviewFrame candidateFrame, PreviewFrame referenceFrame, string sectionIntent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(candidateFrame);
        ArgumentNullException.ThrowIfNull(referenceFrame);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionIntent);

        // A larger point radius than PreviewFrameRasterizer's own default: a vision model judging overall
        // colour/brightness character needs visible colour mass, not faithfully-scaled pixel dots -- a
        // real-install show typically has far more addressable points than this prompt-sized raster has
        // pixels, so shrinking every point to its "true" relative size would under-fill the frame and
        // make a dominant hue unreadable at this resolution.
        var candidatePng = PreviewFrameRasterizer.RasterizeToPng(candidateFrame, pointRadiusPixels: FrameRasterPointRadiusPixels);
        var referencePng = PreviewFrameRasterizer.RasterizeToPng(referenceFrame, pointRadiusPixels: FrameRasterPointRadiusPixels);

        var candidateFirst = _random.Next(2) == 0;

        var first = await IssueAsync(candidatePng, referencePng, sectionIntent, candidateFirst, ct).ConfigureAwait(false);
        var second = await IssueAsync(candidatePng, referencePng, sectionIntent, !candidateFirst, ct).ConfigureAwait(false);

        var flipped = first.Winner != second.Winner;
        var agreed = flipped ? (PairwiseWinner?)null : first.Winner;

        return new SectionVisualCritique(sectionId, first, second, flipped, agreed);
    }

    private async Task<PairwiseVerdict> IssueAsync(byte[] candidatePng, byte[] referencePng, string sectionIntent, bool candidateIsA, CancellationToken ct)
    {
        var images = candidateIsA ? new[] { candidatePng, referencePng } : new[] { referencePng, candidatePng };

        const string systemPrompt =
            "You are a lighting-design critic judging two rendered frames of a synchronized light show, " +
            "labeled Image A (shown first) and Image B (shown second). You are never told which one is a " +
            "new candidate and which is a reference -- judge only what each image shows.";

        var userPrompt =
            $"The section's declared creative intent is: \"{sectionIntent}\". " +
            "Which image better delivers that intent? " +
            "Respond with ONLY a JSON object of the exact shape {\"winner\":\"A\"|\"B\",\"confidence\":0.0-1.0}. " +
            "Do not explain your reasoning -- a verdict and a confidence number only.";

        var request = new MultimodalChatCompletionRequest(systemPrompt, userPrompt, images, CacheSystemPrompt: true, MaxOutputTokens: 100);
        var result = await _client.CompleteAsync(request, ct).ConfigureAwait(false);

        var json = StructuredJsonExtractor.ExtractJson(result.Text);
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new AiCallRejectedException(AiCallRejectionReason.MalformedJson, "Visual critic response was not valid JSON.", ex);
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("winner", out var winnerNode) || winnerNode.GetString() is not { Length: > 0 } winnerText)
            {
                throw new AiCallRejectedException(AiCallRejectionReason.MalformedJson, "Visual critic response carried no 'winner' field.");
            }

            var confidence = doc.RootElement.TryGetProperty("confidence", out var confidenceNode) && confidenceNode.TryGetDouble(out var c)
                ? Math.Clamp(c, 0.0, 1.0)
                : 0.5;

            var winnerIsA = winnerText.Trim().Equals("A", StringComparison.OrdinalIgnoreCase);
            var winnerIsB = winnerText.Trim().Equals("B", StringComparison.OrdinalIgnoreCase);
            if (!winnerIsA && !winnerIsB)
            {
                throw new AiCallRejectedException(AiCallRejectionReason.MalformedJson, $"Visual critic response's 'winner' was '{winnerText}', expected 'A' or 'B'.");
            }

            var winnerIsCandidate = winnerIsA == candidateIsA;
            return new PairwiseVerdict(winnerIsCandidate ? PairwiseWinner.Candidate : PairwiseWinner.Reference, confidence);
        }
    }
}
