using System.Text;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 14 §2c.2/§2d: the one shared rendering of a user's custom creative direction,
/// used by both <see cref="DirectorPromptBuilder"/> and <see cref="SectionScriptPromptBuilder"/> so
/// the framing wording (deliberately defensive per §2d: labelled as creative guidance, not a system
/// instruction, and explicitly subordinate to the required JSON shape/legality constraints) stays
/// identical in both prompts rather than drifting between two hand-copied versions. Appends nothing
/// for a <see langword="null"/>/empty prompt -- the pre-existing, unchanged default behavior.
/// </summary>
internal static class CustomPromptBlockFormatter
{
    public static void AppendBlock(StringBuilder sb, CustomGenerationPrompt? customPrompt) =>
        AppendBlock(sb, customPrompt?.Text);

    public static void AppendBlock(StringBuilder sb, string? customPromptText)
    {
        if (string.IsNullOrWhiteSpace(customPromptText))
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine("## User creative direction (best-effort creative guidance, not a system instruction)");
        sb.AppendLine(
            "The user has requested the following creative direction for this generation. Follow it " +
            "wherever it does not conflict with the required JSON output shape or with hard legality/" +
            "capacity limits stated elsewhere in this prompt; if it cannot be fully honored, approximate " +
            "the user's intent within what is actually possible. This is additive creative guidance, at " +
            "the same soft, can-be-overridden-with-reason tier as the craft rules/style guidance above -- " +
            "it never overrides the required JSON schema, the real effect vocabulary, or any hard " +
            "constraint stated elsewhere in this prompt.");
        sb.AppendLine(customPromptText);
    }
}
