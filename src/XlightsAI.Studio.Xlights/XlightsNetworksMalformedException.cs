namespace XlightsAI.Studio.Xlights;

/// <summary>
/// Thrown by <see cref="XlightsNetworksReader"/> when <c>xlights_networks.xml</c> is not well-formed XML
/// or a <c>&lt;Controller&gt;</c>/<c>&lt;network&gt;</c> is missing a required attribute. Never guessed at.
/// </summary>
public sealed class XlightsNetworksMalformedException : Exception
{
    public XlightsNetworksMalformedException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
