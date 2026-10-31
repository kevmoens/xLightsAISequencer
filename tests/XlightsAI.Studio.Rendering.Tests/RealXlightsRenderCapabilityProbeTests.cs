namespace XlightsAI.Studio.Rendering.Tests;

/// <summary>
/// Corroborates Phase 0's de-risk spike (docs/PHASE0-FINDINGS.md section 4/5) against a real installed
/// xLights build, when one happens to be present on the machine running the tests. Skips cleanly
/// everywhere else (CI, a dev box without xLights installed) -- <see cref="Step086AcceptanceTests"/>'s
/// FakeXlightsHost-based tests are what every build actually depends on.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RealXlightsRenderCapabilityProbeTests
{
    private const string RealExecutablePath = @"C:\Program Files\xLights\xlights.exe";

    [Fact]
    public async Task RealInstall_SupportsHeadlessAndOutputDirectory()
    {
        if (!File.Exists(RealExecutablePath))
        {
            // No real xLights install on this machine -- nothing to corroborate here.
            return;
        }

        var probe = new XlightsRenderCapabilityProbe();
        var capability = await probe.ProbeAsync(RealExecutablePath, CancellationToken.None);

        // Phase 0 finding: this real install's own --help text never mentions --headless/--outputdir (it
        // launches the GUI instead of printing usage), which is exactly the false negative that motivated
        // replacing text-sniffing with a real trivial render attempt. The real attempt must now report true.
        Assert.True(capability.SupportsHeadless, "Expected the real install to support --headless (Phase 0 finding, section 4/5).");
        Assert.True(capability.SupportsOutputDirectory, "Expected the real install to support --outputdir (Phase 0 finding, section 4/5).");
        Assert.Equal(RealExecutablePath, capability.ExecutablePath);
    }
}
