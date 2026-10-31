namespace XlightsAI.Studio.Director;

/// <summary>
/// Extracts a single JSON value from a model's raw text output. Real models (verified directly against
/// a real local Ollama call on this machine) routinely wrap structured output in a markdown code fence
/// (<c>```json ... ```</c>) or add a short prose preamble/epilogue even when explicitly instructed to
/// "respond only with JSON" -- this strips a fence first, then falls back to locating the first balanced
/// top-level <c>{...}</c>/<c>[...]</c> span (tracking string/escape state so a brace inside a JSON string
/// literal is never mistaken for a structural one), rather than assuming the whole response is clean JSON.
/// </summary>
public static class StructuredJsonExtractor
{
    /// <exception cref="AiCallRejectedException"><see cref="AiCallRejectionReason.MalformedJson"/> -- no JSON value could be located in <paramref name="text"/>.</exception>
    public static string ExtractJson(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var trimmed = StripCodeFence(text.Trim());

        var firstBraceOrBracket = -1;
        for (var i = 0; i < trimmed.Length; i++)
        {
            if (trimmed[i] is '{' or '[')
            {
                firstBraceOrBracket = i;
                break;
            }
        }

        if (firstBraceOrBracket < 0)
        {
            throw new AiCallRejectedException(AiCallRejectionReason.MalformedJson, "No JSON object/array found in the model's response text.");
        }

        var end = FindMatchingClose(trimmed, firstBraceOrBracket);
        if (end < 0)
        {
            throw new AiCallRejectedException(AiCallRejectionReason.MalformedJson, "The model's response text contained an unterminated JSON value.");
        }

        return trimmed[firstBraceOrBracket..(end + 1)];
    }

    private static string StripCodeFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstNewline = text.IndexOf('\n');
        if (firstNewline < 0)
        {
            return text;
        }

        var closingFence = text.IndexOf("```", firstNewline, StringComparison.Ordinal);
        return closingFence < 0 ? text[(firstNewline + 1)..] : text[(firstNewline + 1)..closingFence];
    }

    private static int FindMatchingClose(string text, int openIndex)
    {
        var open = text[openIndex];
        var close = open == '{' ? '}' : ']';
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = openIndex; i < text.Length; i++)
        {
            var c = text[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
            }
            else if (c == open)
            {
                depth++;
            }
            else if (c == close)
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }
}
