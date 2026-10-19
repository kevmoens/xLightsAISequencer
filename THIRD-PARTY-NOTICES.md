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
