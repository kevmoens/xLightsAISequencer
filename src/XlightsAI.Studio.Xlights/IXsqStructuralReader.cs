namespace XlightsAI.Studio.Xlights;

/// <summary>
/// The "reference reader" PHASE-02 Step 013 explicitly calls for ("Write a structural roundtrip test
/// with a reference reader"): parses an on-disk <c>.xsq</c> file back into an independently-inspectable
/// <see cref="XsqStructuralDocument"/>, so a round-trip test can assert the exporter's own output is
/// well-formed and internally consistent without re-using any of the exporter's own in-memory state.
/// </summary>
public interface IXsqStructuralReader
{
    /// <exception cref="XsqStructuralReadException">
    /// The file is not well-formed XML, is missing a required section, or contains a dangling
    /// <c>EffectDB</c>/palette reference.
    /// </exception>
    XsqStructuralDocument Read(string path);
}
