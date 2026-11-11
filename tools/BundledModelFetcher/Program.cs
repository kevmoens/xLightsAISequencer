// User-requested real feature (2026-11-08): "all models should ship with our app." Run once at
// build/release time (before `vpk pack`), never at app runtime -- downloads and hash-verifies the two
// real pinned model archives (Whisper transcription + Spleeter stem separation) via the exact same
// ModelAssetManager/HttpModelAssetDownloader the shipped app itself uses, and places them under the
// given output directory using that same content-addressed naming convention. The shipped app's own
// ModelAssetManager instances are wired (see Program.cs's RegisterServices) to search this same
// "models" folder next to the executable before ever touching the network, so a bundled hit here means
// the end user never sees a runtime download/consent prompt at all.
//
// Spleeter's pretrained-weight redistribution license is disclosed by SherpaOnnxSpleeterModelAsset's
// own doc comment as genuinely contested/unresolved (Deezer has never clarified it). Bundling it here
// is a formal, explicit, user/maintainer-granted waiver of that residual risk -- see
// implementation-evidence/PHASE-06A/STEP-044G.md for the full record of that decision. Whisper's
// weights are MIT-licensed; bundling it needed no waiver.
using XlightsAI.Studio.Stems;

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Usage: BundledModelFetcher <output-models-directory>");
    return 1;
}

var outputDirectory = args[0];
Directory.CreateDirectory(outputDirectory);

var manager = new ModelAssetManager(outputDirectory, new HttpModelAssetDownloader());

var assets = new[]
{
    ("Whisper tiny.en (speech-to-text)", SherpaOnnxWhisperTinyEnModelAsset.Archive),
    ("Spleeter 2-stem (source separation)", SherpaOnnxSpleeterModelAsset.Archive),
};

foreach (var (label, asset) in assets)
{
    Console.WriteLine($"Fetching {label}: {asset.Source}");
    var path = await manager.EnsureVerifiedAsync(asset, userConsent: true, CancellationToken.None);
    var sizeBytes = new FileInfo(path).Length;
    Console.WriteLine($"  Verified at {path} ({sizeBytes:N0} bytes).");
}

Console.WriteLine($"Done. Bundled models directory: {outputDirectory}");
return 0;
