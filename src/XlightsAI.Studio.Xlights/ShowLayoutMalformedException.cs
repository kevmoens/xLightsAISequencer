namespace XlightsAI.Studio.Xlights;

/// <summary>
/// Thrown by <see cref="IShowLayoutImporter"/> when <c>xlights_rgbeffects.xml</c> cannot be parsed
/// safely: not well-formed XML, not a recognized <c>&lt;xrgbEffects&gt;</c> document, a disallowed
/// DOCTYPE/entity declaration, or a document larger than the configured safety limit. Always thrown
/// before any output is produced — the source file is only ever read, never rewritten, so this
/// exception never indicates partial/corrupted on-disk state (PHASE-02 Step 010 required failure
/// path: "Invalid model/format/version must fail safely without rewriting source files.").
/// </summary>
public sealed class ShowLayoutMalformedException : Exception
{
    public ShowLayoutMalformedException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
