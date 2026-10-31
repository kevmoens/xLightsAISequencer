# Third-party notices

This application is built on the following third-party NuGet packages. Each package is obtained
unmodified from its official NuGet feed; none of their source is vendored into this repository. This
list was compiled by enumerating every `PackageReference` actually present in `src/**/*.csproj` at
release time (`implementation-evidence/PHASE-16/STEP-100.md` records the exact command used) — it is
not a hand-maintained guess.

| Package | Version | License | Publisher |
|---|---|---|---|
| Avalonia | 11.3.2 | MIT | AvaloniaUI |
| Avalonia.Desktop | 11.3.2 | MIT | AvaloniaUI |
| Avalonia.Themes.Fluent | 11.3.2 | MIT | AvaloniaUI |
| Avalonia.Fonts.Inter | 11.3.2 | MIT | AvaloniaUI |
| Avalonia.Diagnostics | 11.3.2 | MIT | AvaloniaUI (Debug-configuration only; not bundled in Release) |
| Tmds.DBus.Protocol | 0.95.1 | MIT | Tom Deseyn |
| ZstdSharp.Port | 0.8.6 | MIT | oozcitak |
| System.Security.Cryptography.ProtectedData | 9.0.0 | MIT | Microsoft |
| Microsoft.Data.Sqlite | 10.0.9 | MIT | Microsoft |
| SQLitePCLRaw.bundle_e_sqlite3 | 2.1.12 | Apache-2.0 | Eric Sink / SQLitePCL.raw contributors |
| Microsoft.ML.OnnxRuntime | 1.24.4 | MIT | Microsoft |
| SharpCompress | 0.48.0 | MIT | Adam Hathcock |

No upstream reference project is vendored, submoduled, or otherwise present in this repository.

## Runtime-downloaded (not bundled) third-party assets

Unlike the NuGet packages above, none of the following is bundled with the app install. Each is
downloaded to the user's own machine only after an explicit, per-asset consent action in the UI, and
each is verified against a pinned SHA-256 before use — a download is never started implicitly.

| Component | Pinned version | License | Consent action | Source |
|---|---|---|---|---|
| ffmpeg (win64, shared build) | 9.0.2 | LGPL-3.0-or-later | Audio screen → "Download ffmpeg" | [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds) |
| sherpa-onnx-spleeter-2stems (ONNX model weights) | `source-separation-models` release | See the disclosure below | Show Import screen → "Download separation model" | [k2-fsa/sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) |

### Disclosure: sherpa-onnx-spleeter-2stems weight licensing is unresolved

This model is used by the whole-song generation pipeline (`XlightsAI.Studio.Pipeline`) to isolate a
vocal/lead stem for melody analysis. The sherpa-onnx conversion tooling and code are permissively
licensed (MIT / Apache-2.0), but **the redistribution license of the underlying Spleeter pretrained
weights is genuinely contested and unresolved** — see `SherpaOnnxSpleeterModelAsset`'s own doc comment
(`src/XlightsAI.Studio.Stems/SherpaOnnxSpleeterModelAsset.cs`) and `docs/phase-specs/PHASE-06A.md` §3/§8
for the full, previously-recorded analysis.

This project does **not** redistribute those weights: nothing is bundled in the installer, and the file
is fetched directly from the upstream project's own release by the user's own machine, only on an
explicit click. That posture is what keeps the open question off this project's own distribution — it is
not a claim that the upstream licensing question has been answered. Anyone planning to bundle, mirror,
or otherwise redistribute these weights should resolve that question first.
