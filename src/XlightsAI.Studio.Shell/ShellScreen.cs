namespace XlightsAI.Studio.Shell;

/// <summary>
/// The screens reachable from basic shell navigation in this step. Later phases add more
/// screens; this step establishes the navigation mechanism, not the full screen set.
/// </summary>
public enum ShellScreen
{
    Welcome,
    Projects,
    Settings,
    Diagnostics,

    /// <summary>PHASE-02 Step 009: SCREEN 04 SHOW IMPORT (read-only initial discovery only).</summary>
    ShowImport,

    /// <summary>
    /// Build-plan Phase 14: a "last stop before you generate" tab for the user's own free-text
    /// creative direction (colors, effects, layers, intensity, pacing, prop focus), placed
    /// immediately before <see cref="GenerateExport"/> per that plan's §2b.1 -- not a blocking gate,
    /// since an empty custom prompt is a fully valid, unchanged default.
    /// </summary>
    CustomPrompt,

    /// <summary>
    /// Choreography generation + xLights delivery: the whole-song plan, the XSQ export and the
    /// "open in xLights" hand-off. Split out of <see cref="ShowImport"/> (which had grown to host
    /// five unrelated jobs on one scroll) so the import screen answers only "what props does this
    /// show have," and this one answers only "turn the imported show + probed song into a file
    /// xLights can open." Both bind the same <see cref="ShowImportViewModel"/> — this is a view
    /// split, not a second copy of that state.
    /// </summary>
    GenerateExport,

    /// <summary>PHASE-03 Step 016: SCREEN 13 TIMELINE (time ruler, horizontal scroll and zoom).</summary>
    Timeline,

    /// <summary>PHASE-04 Step 024: SCREEN 06 AUDIO (import/probe a local audio file; no waveform/playback yet).</summary>
    Audio,

    /// <summary>PHASE-07 Step 049: SCREEN 10 PIANO ROLL (editable note view with low-confidence markers).</summary>
    PianoRoll,

    /// <summary>PHASE-08 Step 058: SCREEN 11 STRUCTURE (phrase relationship/propagation inspector).</summary>
    StructureInspector,

    /// <summary>PHASE-09 Step 061: SCREEN 05 DISPLAY PATHS (graphical layout/path editor with position correction).</summary>
    DisplayPathEditor,

    /// <summary>
    /// PHASE-15 Step 095: first-run onboarding wizard (choose a workspace folder, probe runtime
    /// capability, surface any startup health diagnostics). Shown automatically on a fresh install
    /// (no persisted <see cref="AppSettings.HasCompletedFirstRun"/> yet) before any other screen —
    /// "User completes first run without command line," this step's own required acceptance text.
    /// </summary>
    FirstRun,

    /// <summary>
    /// Build-plan Phase 17 §2e: browse every mined exemplar (song, bar range, section kind, tempo,
    /// energy percentile, real description) and enable/disable individual entries or a whole source
    /// song's worth of material, persisted separately from <c>exemplar-index.json</c> so a re-mine never
    /// resets a user's curation choices -- see <see cref="ExemplarLibraryViewModel"/>.
    /// </summary>
    ExemplarLibrary,
}
