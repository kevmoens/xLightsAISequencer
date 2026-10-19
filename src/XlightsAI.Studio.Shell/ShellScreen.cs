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
}
