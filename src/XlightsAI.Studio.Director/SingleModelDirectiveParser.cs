using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan "richer analysis tools" Phase 8 (2026-11-02): a narrow, mechanical extractor for a user's
/// own custom-prompt request that a specific real model be targeted individually, never as (only) part of
/// a group -- same "deliberately NOT general NLP, only an unambiguous literal match" discipline
/// <see cref="CustomPromptTimedCueParser"/> already established for timestamp tokens. A line qualifies
/// only when it names a real model from <paramref name="layout"/> AND carries one of a small, fixed set of
/// "not a group" phrases; anything else in the custom prompt (free-text creative direction that happens to
/// mention a prop name with no such phrase) is left alone, exactly as it always has been.
/// </summary>
public static class SingleModelDirectiveParser
{
    private static readonly string[] NotAGroupPhrases =
    [
        "not a group", "not as a group", "not the group", "not grouped",
        "individually", "as an individual model", "not part of a group", "never as a group",
    ];

    public static IReadOnlyList<ModelId> Parse(string? text, DisplayGraph layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var modelIds = new List<ModelId>();
        foreach (var line in text.Split('\n'))
        {
            if (!NotAGroupPhrases.Any(phrase => line.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            foreach (var model in layout.Models)
            {
                if (line.Contains(model.XlightsName, StringComparison.OrdinalIgnoreCase) && !modelIds.Contains(model.Id))
                {
                    modelIds.Add(model.Id);
                }
            }
        }

        return modelIds;
    }
}
