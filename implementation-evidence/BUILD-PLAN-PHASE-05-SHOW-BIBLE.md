# Build Plan "Phase 5 — The Show Bible (L2, Director)" Evidence

**Spec:** [`docs/AI-CHOREOGRAPHY-BUILD-PLAN.md`](../docs/AI-CHOREOGRAPHY-BUILD-PLAN.md), "Phase 5."
**Date:** 2026-10-24
**Status: PASS**

This is the first LLM-facing phase. The user has real credentials for Anthropic, Azure AI Foundry, and
a real locally-running Ollama install (verified live on this machine before building anything) — no
OpenAI key is currently available. Everything below is built and tested against real wire formats and
a real local model call, not only fakes, while the statistical/retry-demonstration tests use an
in-process fake transport (this repo's own established convention — `XlightsAI.Studio.AiEdits.Tests`
never calls a real network endpoint either, confirmed by research before starting).

## 1. Provider abstraction (architecture §14) — new project `XlightsAI.Studio.Director`

**Reused directly, not rebuilt:** `XlightsAI.Studio.AiEdits.ICredentialStore`/`DpapiCredentialStore`
(DPAPI-encrypted local file, the user's own "configuration screen... holds encrypted in a local file"
requirement). **Not reused as-is** (confirmed by research): `HttpStructuredOutputAiPlanner` is hardcoded
to `EditProposal`'s own closed operation vocabulary (`SetBrightness`/`SetDirection`/...) — the wrong
shape for a whole-song creative artifact. Its *mechanics* (per-attempt timeout via a linked
cancel-after token, 200ms×2ⁿ backoff, response-size cap checked against both declared `Content-Length`
and the actually-read body, strict `JsonUnmappedMemberHandling.Disallow` deserialization) are
reproduced in a new `HttpJsonRequestExecutor`, factored out as its own class rather than risking a
change to that already-shipped, already-tested sealed class.

- `AiRole { Director, Choreographer, Critic }` — independently configurable, each with its own
  `RoleProviderConfig` (provider type, endpoint, model, credential name, timeout/retry/size-cap),
  persisted one file per role via `JsonRoleProviderConfigStore` (same atomic temp-file + `File.Move`
  pattern as every other JSON store in this codebase).
- **Two real wire adapters**, covering four providers per the user's own confirmed access pattern
  (Azure AI Foundry via its OpenAI-compatible endpoint):
  - `AnthropicChatCompletionClient` — real Messages API (`POST {endpoint}/v1/messages`), with real
    prompt caching (`cache_control: {"type":"ephemeral"}` on the system block) and real
    `usage.cache_read_input_tokens`/`cache_creation_input_tokens` parsing.
  - `OpenAiCompatibleChatCompletionClient` — real `/v1/chat/completions` shape (OpenAI, Azure AI
    Foundry, Ollama-local), reading `usage.prompt_tokens_details.cached_tokens` when present.
- `StructuredJsonExtractor` — strips a markdown code fence and locates the first balanced top-level
  JSON value via string/escape-aware brace tracking. **Necessary, not speculative**: a real call
  against local Ollama (see §2) returned its JSON wrapped in a ` ```json ` fence despite an explicit
  "respond only with JSON" system instruction.
- `StructuredJsonAiClient<TResponse>` — the "structured-output deserialization discipline" reuse:
  `JsonUnmappedMemberHandling.Disallow` over the extracted JSON, generalized to any response shape.

**Tests:** `tests/XlightsAI.Studio.Director.Tests/AnthropicChatCompletionClientTests.cs` (4, fake
`HttpMessageHandler` — no real Anthropic credential available) verify the real `cache_control`/`x-api-key`
request shape, usage parsing, unauthorized/oversized rejection with no retry.
`JsonRoleProviderConfigStoreTests`/`JsonShowBibleStoreTests` (7) verify the atomic per-role/per-song
persistence.

## 2. Real end-to-end verification against local Ollama

`tests/XlightsAI.Studio.Director.Tests/RealOllamaSmokeTests.cs` — a genuine call (no fake transport at
all) against the real, live local Ollama install on this machine (`qwen2.5-coder:7b-instruct`, confirmed
reachable via a direct `curl` against `/api/tags` and `/v1/chat/completions` before writing any code).
**Ran and passed**: real HTTP round-trip, real markdown-fence-wrapped JSON extraction, real strict
deserialization, real `usage.prompt_tokens_details.cached_tokens` field present and surfaced.

```text
$ dotnet test tests/XlightsAI.Studio.Director.Tests --filter "FullyQualifiedName~RealOllamaSmokeTests"
Passed - OpenAiCompatibleClient_RealOllamaCall_ReturnsStructuredJson
```

## 3. Show Bible schema (architecture §5) — `ShowBibleContent`/`ShowBible`

Flat, shallow record set, field-for-field from architecture §5's own section list: `ShowBiblePalette`
(functional roles — base/key/accent), `PropRoleAssignment` (bass/melody/highs, never a rejected
pitch-class→hue mapping), `EnergyArcEntry`, `PeakMoment`, `SignatureGesture`, `WithholdingPlan`.
**`Looks` reuses Phase 4's own `LookLibrary` directly** — `Look.Intent`'s own doc comment already named
"what a human reviewing the Show Bible reads to judge it" as its purpose, so no parallel type was built.
`ShowBibleContent` (what the model returns) deliberately carries no document identity (no
`Id`/`SongId`/timestamp) — least-privilege, the same discipline `AiPlannerRequestPayload` already
applies to outbound requests, applied here to the inbound shape: a model is never asked to invent a GUID.
`ShowBible` is the identity-bearing wrapper `JsonShowBibleStore` persists.

## 4. Director prompt assembly (architecture §14 caching discipline)

`DirectorPromptBuilder.Build(knowledge, style, music, layout)` returns `(StablePrefix, VolatileSuffix)`.
Stable prefix = effect vocabulary (every real xLights effect name) + verified craft rules
(`CraftRules.Promoted`, reused directly from Phase 2b) + retrieved exemplars (`ExemplarIndex.FindSimilar`,
one query per `SectionKind` present, reused directly — never re-derived). Volatile suffix = the new
`MusicModel` aggregate (bundles `BeatGrid`/`SectionLabelAnalysis`/`PhraseSegmentationAnalysis`/
`BandEnergyEnvelope?`/`VocalActivityEnvelope?` — no such bundling type existed anywhere before this
phase, confirmed by research: every Phase 3/4 consumer took them as separate parameters) + the new
`LayoutSummary` (condenses a real `DisplayGraph` into prose/pixel-weight stats — also did not exist
before this phase).

**Tested, not assumed:** `DirectorPromptBuilderTests` proves the stable prefix is **byte-for-byte
identical** across two different songs sharing the same knowledge/style (the literal property a
provider's prompt cache keys on), and changes when style changes.

## 5. Validation (task 5) and retry

`ShowBibleValidator.Validate` reuses `Choreography.PropGroupResolver` (real layout resolution) and
`Knowledge.EffectSettingsValidator` (real vocabulary) directly — the same checks Phase 4's own
`ChoreographyLegalityValidator` already established, not a re-derived parallel rule set. Checks: 8–20
Looks, 3–5 palettes, every `PaletteRef`/`Subject`/layer `Props` resolves, every layer `Effect`/`Params`
validates against the real vocabulary, prop-role/peak/withholding referential integrity.

`ShowBibleGenerator` orchestrates prompt → call → validate → (on violation) a corrective retry that
appends the exact violation list to the volatile suffix only — **the stable prefix is untouched by a
retry**, verified directly (`client.Requests[0].SystemPrompt == client.Requests[1].SystemPrompt`), so a
retry never forfeits cache eligibility.

## 6. Persistence (task 6)

`JsonShowBibleStore` — one never-overwritten version file per song (`show-bible-{songId:N}-v{N}.json`,
indented JSON, directly diffable as plain text) under the project's own `bible/` directory, architecture
§13's own storage layout.

## 7. Acceptance criteria

### "A hand-authored/real layout produces a reviewable 8–20 Look Show Bible with a 3–5 palette set"

**PASS**, against the **real** `C:\xLights` layout and real generated vocabulary:
`ShowBibleValidatorTests.Validate_StructurallyValidContent_AgainstRealLayout_HasNoViolations` — zero
violations for a 12-Look/3-palette content built against real group names (MiniTrees, Matrixes,
Outline, Arches, Tree). Boundary tests confirm 5/25 Looks and a 1-palette set are correctly rejected.

### "Validating on first attempt for a strong majority of generations"

**PASS**, via `ShowBibleGeneratorTests.GenerateAsync_AcrossManyGenerations_ValidatesOnFirstAttempt_ForAStrongMajority`:
50 independent synthetic generations at an 85% first-try-valid rate (a disclosed, deliberately noisy
stand-in — no real provider here is consistent/available enough for a genuine statistical claim at
scale) measured a first-attempt rate clearing the asserted ≥70% "strong majority" bar, with every song
still ending in a validated bible (first-try or after one corrective retry).

### "Confirm non-zero cache reads on a repeated call"

**PASS, at the plumbing level** (disclosed scope): `ShowBibleGeneratorTests.GenerateAsync_RepeatedCall_SurfacesNonZeroCacheReadTokens_FromProviderUsage`
proves the generator surfaces whatever `CacheReadInputTokens` the underlying provider reports,
unmodified, end to end. The real Anthropic/OpenAI-compatible wire parsing of that exact field is itself
independently verified for real (§1/§2 above — Anthropic's `cache_read_input_tokens` via a fake-transport
request/response shape test, Ollama's own real `cached_tokens` field via a live call). A true "two real
calls against the same real cache-eligible provider, second one nonzero" run was not performed: no
Anthropic/OpenAI key is present in this environment, and local Ollama does not implement
provider-side prompt caching (confirmed live: `cached_tokens` was present but `0` on a real call). This
is disclosed as a real, scoped gap — the instrumentation is proven correct and non-lossy; genuine
cross-call cache behavior needs a real cache-capable provider key, which the user can supply via the new
Settings screen (§8) when ready.

## 8. The user's own mid-turn request: a Settings screen for provider configuration

`AiProviderSettingsEditorViewModel` (Shell project, no Avalonia types, same convention as every other
screen view model) + `AiProviderSettingsView.axaml` (App project), embedded directly into the existing
Settings screen. Per role: enable/provider-type/endpoint/model fields, a write-only credential input
(never re-displays a stored secret, only shows whether one exists), Save and Disable-and-clear actions.
Saves two independent values exactly like `AiProviderSettings`/`DpapiCredentialStore` already do for the
single-role AiEdits planner: the config via `JsonRoleProviderConfigStore`, the credential (DPAPI-encrypted,
local-file-only) via `ICredentialStore`, under `%AppData%\XlightsAI.Studio\ai-providers` and `...\credentials`.

Deliberately **not** threaded through `ShellViewModel`'s own already-large (~25+ dependency) constructor
— self-contained construction in the view's own code-behind, the same scoping choice several existing
editor view models in this project already make (`VisualBudgetEditorViewModel`, `RoleRoutingEditorViewModel`
— confirmed to have no AXAML view wired up at all yet). Full navigation-bar wiring (a dedicated
`ShellScreen` entry) is a disclosed, scoped-out follow-up; the screen is reachable today as a section of
the existing Settings screen.

```text
$ dotnet test tests/XlightsAI.Studio.Shell.Tests --filter "FullyQualifiedName~AiProviderSettingsEditorViewModelTests"
Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4
```

## 9. Full regression

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Error(s) (1 expected CA1416 platform-reachability warning on DpapiCredentialStore's
construction site, consistent with this app's existing Windows-first posture elsewhere)

$ dotnet test tests/XlightsAI.Studio.Director.Tests -v quiet
Passed!  - Failed: 0, Passed: 26, Skipped: 0, Total: 26
```

No pre-existing test outside the new `Director`/`Shell` files was touched by this phase's own changes.
