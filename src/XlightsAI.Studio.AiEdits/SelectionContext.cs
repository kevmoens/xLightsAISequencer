using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 072 ("Create persisted selection context: interval, props, instruments, phrases,
/// locks"): the exact, persisted snapshot of what a user selected before issuing an edit instruction --
/// implementation guidance, verbatim: "Snapshot selected notes/stem/phrase/path/effects with original
/// revision and allowed models/time, not entire show by default."
///
/// <para>
/// Maps the step heading's five named concepts directly: "interval" is <see cref="Scope"/>'s own
/// <see cref="EditScope.Time"/>, "props" is <see cref="Scope"/>'s own <see cref="EditScope.Models"/>,
/// "instruments" is <see cref="InstrumentTrackIds"/>, "phrases" is <see cref="PhraseIds"/>, and "locks"
/// is <see cref="Scope"/>'s own <see cref="EditScope.RespectLocks"/> (already-accepted, PHASE-03). The
/// implementation guidance's own additional "notes"/"path"/"effects" map to <see cref="NoteIds"/>,
/// <see cref="SpatialPathIds"/> and <see cref="Scope"/>'s own <see cref="EditScope.EffectIds"/>
/// respectively -- this type reuses the already-accepted <see cref="EditScope"/> wholesale rather than
/// re-declaring a parallel interval/props/locks shape.
/// </para>
///
/// <para>
/// Deliberately stores caller-defined <see cref="Guid"/>/<see cref="string"/> identities for
/// notes/phrases/paths/instruments rather than referencing <c>XlightsAI.Studio.Melody.NoteEvent</c>,
/// <c>XlightsAI.Studio.Structure.SongStructure</c> or <c>XlightsAI.Studio.Display.SpatialPath</c>
/// directly -- those aggregates' own validity is each aggregate's own already-accepted responsibility
/// (e.g. <c>NoteEditScope</c> already owns note-id validity against a <c>MelodyTrack</c>); this project
/// only owns the <see cref="EditScope"/>/<c>SequenceDocument</c>-facing invariants a
/// <see cref="StructuredEditOperation"/> actually needs (revision, model scope, locks), per PHASE-11
/// Section 2's own "redacted, least-privilege context" architectural note. A caller free to reference
/// those other aggregates' own real ids here without this project ever depending on them.
/// </para>
/// </summary>
/// <param name="Id">Stable identity for this one captured selection, independent of its contents.</param>
/// <param name="BaseDocumentRevision">
/// The <c>SequenceDocument.Revision</c> this selection was captured against -- Section 2's own "treat
/// model output as untrusted... reject... stale revision" rule starts here: any later proposal built
/// from this context must be revalidated against the document's current revision before being applied.
/// </param>
/// <param name="Scope">The exact interval/props/locks boundary this selection is permitted to reach -- see type remarks.</param>
/// <param name="InstrumentTrackIds">Every selected stem/instrument track id ("instruments," step heading, verbatim). Never <see langword="null"/>; empty when no instrument was explicitly selected.</param>
/// <param name="PhraseIds">Every selected phrase/section id ("phrases," step heading, verbatim). Never <see langword="null"/>; empty when no phrase was explicitly selected.</param>
/// <param name="SpatialPathIds">Every selected display-path id ("path," implementation guidance). Never <see langword="null"/>; empty when no path was explicitly selected.</param>
/// <param name="NoteIds">Every selected note id ("notes," implementation guidance). Never <see langword="null"/>; empty when no note was explicitly selected.</param>
/// <param name="CapturedAtUtc">When this selection was captured -- provenance only, never part of equality-sensitive validation.</param>
public sealed record SelectionContext(
    Guid Id,
    DocumentRevision BaseDocumentRevision,
    EditScope Scope,
    IReadOnlyList<string> InstrumentTrackIds,
    IReadOnlyList<Guid> PhraseIds,
    IReadOnlyList<Guid> SpatialPathIds,
    IReadOnlyList<Guid> NoteIds,
    DateTimeOffset CapturedAtUtc);
