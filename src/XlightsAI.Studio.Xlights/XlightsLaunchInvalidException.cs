namespace XlightsAI.Studio.Xlights;

/// <summary>
/// Thrown by <see cref="IXlightsLauncher"/> when a requested launch is malformed, targets a missing or
/// incompatible installation, or the OS refuses to start the process. Always thrown before any process
/// is started — PHASE-02 Step 014's required failure path: "Missing or incompatible model, cancellation
/// and offline mode must yield a recoverable state."
/// </summary>
public sealed class XlightsLaunchInvalidException : Exception
{
    public XlightsLaunchInvalidException(string message)
        : base(message)
    {
    }
}
