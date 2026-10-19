// PHASE-13 Step 086 test fixture only -- NOT xLights and never claimed to be. A real, separately-compiled
// OS executable (apphost) that stands in for "an installed target xLights build" purely to prove
// XlightsRenderCapabilityProbe/XlightsRenderAdapter's real process-launch plumbing (argument list, stdout/
// stderr capture, timeout/kill, exit code, on-disk .fseq artifact) end to end, mirroring the same disclosed
// convention PHASE-02 Step 014's own tests use with the real `where.exe`. Behavior is selected entirely by
// literal argument tokens (probe calls) or by substrings in the trailing sequence file's own name (render
// calls) -- never by environment variables, so parallel test runs stay deterministic and isolated.

using System.Text;

if (args.Contains("--hang"))
{
    // Simulates a build that never answers a probe/render request -- the caller is expected to kill this
    // process tree after its own timeout elapses, not wait for this sleep to finish.
    Thread.Sleep(TimeSpan.FromMinutes(5));
    return 0;
}

if (args.Contains("--help"))
{
    Console.WriteLine("Usage: FakeXlightsHost [--headless] [-s <showdir>] [--outputdir <dir>] <sequence.xsq>");
    Console.WriteLine("Supports --headless and --outputdir.");
    return 0;
}

if (args.Contains("--help-limited"))
{
    Console.WriteLine("Usage: FakeXlightsHost <sequence>");
    Console.WriteLine("This build does not document any additional render flags.");
    return 0;
}

// Anything else is treated as a render invocation: the real upstream-documented shape is
// `--headless -s <showdir> [--outputdir <dir>] <sequence.xsq>`, with the sequence file always last.
if (args.Length == 0)
{
    Console.Error.WriteLine("No sequence file supplied.");
    return 1;
}

var xsqPath = args[^1];
var stem = Path.GetFileNameWithoutExtension(xsqPath);

if (stem.Contains("-hang-", StringComparison.OrdinalIgnoreCase))
{
    Thread.Sleep(TimeSpan.FromMinutes(5));
    return 0;
}

if (stem.Contains("-fail-", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Simulated render failure: unsupported effect encountered.");
    return 3;
}

var outputDirIndex = Array.IndexOf(args, "--outputdir");
var outputDirectory = outputDirIndex >= 0 && outputDirIndex + 1 < args.Length
    ? args[outputDirIndex + 1]
    : Path.GetDirectoryName(Path.GetFullPath(xsqPath)) ?? Directory.GetCurrentDirectory();

Directory.CreateDirectory(outputDirectory);
var fseqPath = Path.Combine(outputDirectory, stem + ".fseq");
File.WriteAllBytes(fseqPath, Encoding.ASCII.GetBytes("FAKE-XLIGHTS-HOST-FSEQ-EVIDENCE: not a real FSEQ file."));
Console.WriteLine($"Rendered '{xsqPath}' to '{fseqPath}'.");
return 0;
