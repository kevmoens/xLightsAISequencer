using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 28a: bundles the one <see cref="SectionMusicContext"/> plus the four optional
/// per-section signal lists <see cref="SectionTimingMarks.Resolve"/> needs, so a caller threading real
/// anchor data through <c>XlightsAI.Studio.Director.SectionScriptGenerator</c>/<c>SectionScriptBatchGenerator</c>
/// (one call per real section) passes a single bundle per section instead of four parallel dictionaries.
/// </summary>
public sealed record SectionTimelineAnchorContext(
    SectionMusicContext Context,
    IReadOnlyList<PhraseCandidate>? Phrases = null,
    IReadOnlyList<TransientHit>? TransientHits = null,
    IReadOnlyList<NoteEvent>? VocalNotes = null,
    IReadOnlyList<LeadInstrumentWindow>? LeadInstrumentWindows = null);
