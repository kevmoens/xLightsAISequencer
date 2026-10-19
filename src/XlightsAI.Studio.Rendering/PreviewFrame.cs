namespace XlightsAI.Studio.Rendering;

/// <summary>
/// PHASE-13 Section 3 proposed contract: one sampled instant of <see cref="ILocalPreviewRenderer.PreviewAsync"/>'s
/// fast approximate preview. <see cref="TimeTicks"/> is an absolute song-origin tick (the same 100 ns
/// <c>TimelineTick</c> unit every other contract uses), not a frame index.
///
/// <para>
/// Additive beyond Section 3's bare two-field proposed signature: <see cref="UnsupportedEffectIds"/>. Step 085's
/// own required implementation guidance -- "show an unmistakable 'approximate' indication for unsupported
/// effects" -- has no field to carry that indication in the spec's literal <c>PreviewFrame(TimeTicks,
/// Illuminated)</c> shape, and a consuming view cannot show a visual gesture for an effect this preview could not
/// resolve at all (there is nothing to render in its place). Recording the unresolved <see cref="Xlights.EffectInstance.Id"/>
/// values active at this instant lets a caller (e.g. a future <c>RenderPreviewViewModel</c>) surface a literal
/// "N effects shown as approximate" banner instead of silently under-rendering. Never populated from a guess --
/// only effects whose <c>PreviewGesture</c> parameter (or referenced path/target) this renderer could not resolve
/// land here; see <see cref="LocalGesturePreviewRenderer"/> remarks.
/// </para>
/// </summary>
/// <param name="TimeTicks">Absolute song-origin tick this frame was sampled at.</param>
/// <param name="Illuminated">Every point this renderer could resolve and light at <see cref="TimeTicks"/>.</param>
/// <param name="UnsupportedEffectIds">
/// Ids of every active <see cref="Xlights.EffectInstance"/> at <see cref="TimeTicks"/> whose gesture this fast
/// preview tier does not recognize -- always empty when every active effect resolved.
/// </param>
public sealed record PreviewFrame(long TimeTicks, IReadOnlyList<PixelPoint> Illuminated, IReadOnlyList<Guid> UnsupportedEffectIds);
