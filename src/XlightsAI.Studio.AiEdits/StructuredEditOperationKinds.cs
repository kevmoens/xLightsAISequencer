namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 073 ("Implement deterministic command AST for brightness/motion/direction/speed/
/// replace"): the closed, locally-recognized vocabulary of <see cref="StructuredEditOperation.Kind"/>
/// values <see cref="IStructuredEditInterpreter.ParseKnownCommand"/> can ever produce. Declared as
/// named string constants (not a C# <see langword="enum"/>) because PHASE-11.md Section 3's own
/// proposed <c>StructuredEditOperation</c> signature declares <c>Kind</c> as a bare <see cref="string"/>
/// — Section 2's own "Treat model output as untrusted: strict JSON schema; reject unknown properties"
/// rule means a later step's validator (Step 076) must be able to reject an unrecognized <c>Kind</c>
/// string arriving from an external JSON envelope exactly like this one, which only a string-keyed
/// vocabulary (not a CLR enum that JSON deserialization could coerce unpredictably) makes a uniform
/// check against both this interpreter's own output and a future AI-proposed value.
///
/// <para>
/// Reconciles the two slightly different five-word lists PHASE-11.md itself uses for this same step:
/// the step heading ("brightness/motion/direction/speed/replace") and the implementation guidance
/// ("brightness, direction, snap, hold, and role-change actions") are not a literal one-to-one checklist
/// — both describe the same bounded "offline quick actions" surface from two angles. This interpreter
/// reconciles them as five concrete, disjoint, testable kinds: <see cref="SetBrightness"/> (both lists'
/// own "brightness"), <see cref="SetDirection"/> (both lists' own "direction"), <see cref="SetSpeed"/>
/// (the heading's own "speed"), <see cref="SnapToGrid"/> (the guidance's own "snap", and the heading's
/// own "motion" — a selection's effects snapping onto the beat grid is itself a motion/timing action),
/// and <see cref="ReplaceRole"/> (the heading's own "replace" and the guidance's own "role-change
/// actions" — changing which musical source an effect follows, not swapping an effect's xLights recipe,
/// which Section 2's own "do not invent xLights effect parameters" rule would make unverifiable at this
/// layer; the guidance's own "hold" is deliberately not given its own kind — PHASE-03's own already-
/// accepted <c>ResizeEffect</c> command already owns "how long an effect lasts," so a "hold" quick
/// action is UI sugar over that existing command, not a new AiEdits-owned operation kind).
/// </para>
/// </summary>
public static class StructuredEditOperationKinds
{
    public const string SetBrightness = "SetBrightness";
    public const string SetDirection = "SetDirection";
    public const string SetSpeed = "SetSpeed";
    public const string SnapToGrid = "SnapToGrid";
    public const string ReplaceRole = "ReplaceRole";
}
