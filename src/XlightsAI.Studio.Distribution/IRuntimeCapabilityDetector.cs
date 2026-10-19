namespace XlightsAI.Studio.Distribution;

/// <summary>
/// PHASE-15 Step 094 ("Publish self-contained signed/unsigned-labeled Windows x64 app with verified native
/// assets"). Probes the real, currently running machine -- never a declared/assumed value -- so a
/// first-run/startup flow can detect "Fresh Windows x64 with no developer toolchain" (phase test matrix row
/// 1) and "OS/arch incompatibility" (phase test matrix row 8) before attempting any feature that depends on
/// either.
/// </summary>
public interface IRuntimeCapabilityDetector
{
    /// <summary>
    /// Never throws for an unsupported or unexpected OS/architecture -- an unsupported machine is a reportable
    /// <see cref="RuntimeCapability"/> value, not a crash (Section 2: "detect actual versions/capabilities").
    /// </summary>
    Task<RuntimeCapability> ProbeAsync(CancellationToken ct);
}
