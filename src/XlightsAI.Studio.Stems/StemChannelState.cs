namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Step 042 ("Add waveform tracks, solo/mute and synchronized stem playback"): one stem's
/// current mixer state. <see cref="StemId"/> matches a <see cref="StemAsset.Id"/> within the
/// <see cref="StemManifest"/> this channel belongs to; a channel for a stem that is not present in
/// that manifest is simply ignored by <see cref="StemMixRules"/>/<see cref="StemMixdownRenderer"/>
/// rather than treated as an error, so a stale channel list never blocks a remix.
/// </summary>
/// <param name="StemId">The <see cref="StemAsset.Id"/> this channel controls.</param>
/// <param name="Muted">When <see langword="true"/>, this stem is always silent in the mix, regardless of <see cref="Soloed"/> (mute always wins for its own channel; see <see cref="StemMixRules"/>).</param>
/// <param name="Soloed">When <see langword="true"/> and at least one other channel is also soloed or this is the only soloed channel, every non-soloed, non-muted channel is silenced.</param>
public sealed record StemChannelState(Guid StemId, bool Muted, bool Soloed);
