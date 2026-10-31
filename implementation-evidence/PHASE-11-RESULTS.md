# Phase 11 results: AI provider request correctness and observability

Build plan: `docs/AI-CHOREOGRAPHY-BUILD-PLAN-PHASE-11-PROVIDER-RELIABILITY.md`.

## §2a — Responses API endpoint recognition and client

`ProviderEndpointUri.IsResponsesApiEndpoint`/`BuildResponses` recognize the `.../openai/responses` shape
(with its `api-version` query string) distinctly from the classic `v1/chat/completions`/`v1/messages`
suffixes. `ChatCompletionClientFactory` routes an `OpenAiCompatible` config naming that shape to a new
`OpenAiResponsesChatCompletionClient`, which speaks the real `input`/`output` wire format instead of
`messages`/`choices`.

`ProviderEndpointUriTests.Build_RealIncidentEndpoint_ReproducesTheBug_SilentlyDropsPathAndApiVersion`
pins the original bug against `ProviderEndpointUri.Build` (unchanged — the real configured incident
endpoint still resolves to `.../openai/v1/chat/completions` through that method, confirming the bug was
never "fixed" by patching `Build` itself); the companion `IsResponsesApiEndpoint`/`BuildResponses` tests
prove the real fix resolves the same string correctly. `OpenAiResponsesChatCompletionClientTests` cover the
wire shape, factory routing, and response parsing end to end against a fake transport.

## §2b — Reasoning effort / thinking budget

`RoleProviderConfig.ReasoningEffort` (optional, defaults to `null`) is forwarded as `reasoning.effort` on
every Responses API request; `OpenAiResponsesChatCompletionClient.DefaultReasoningEffort = "low"` is sent
when unset, so an unconfigured role never silently inherits the model's own unbounded reasoning budget.
Surfaced in the Settings screen (`AiProviderSettingsView.axaml`) with help text naming the latency/richness
tradeoff.

## §2c — Request logging and endpoint sanity check

`RequestDiagnostics` (`RequestUri`, bounded `RequestBody`, optional `EndpointSanityWarning`) is attached to
every `ChatCompletionResult`/`AiCallRejectedException` and threaded into `AiCallLogEntry.Diagnostics` for
every Show Bible/SectionScript call, success or rejection. `EndpointSanityChecker.CheckDivergence` compares
the configured endpoint's own path against the actually-constructed request path and is covered by a test
reproducing the exact real incident shape (`OpenAiCompatibleChatCompletionClientTests.CompleteAsync_ConfiguredEndpointPathDivergesFromActualRequestPath_SurfacesSanityWarning`).

## §2d — Real-run re-verification (2026-10-30)

Re-ran the exact same real song/show `tools/GenerateOnce` used for the original incident
(`docs/AI-CHOREOGRAPHY-REAL-RUN-COMPARISON-2026-10-30.md`), against the same real, unmodified Azure
OpenAI Responses API endpoint and credential, with no change other than this phase's fix already built.

| | Before (original incident) | After (this phase's fix) |
|---|---|---|
| Total wall-clock generation time | 1h 14m (single Director call ~57 min) | **7m 20s** |
| Actual request URI (per the new call-log `Diagnostics`) | `.../openai/v1/chat/completions` (wrong endpoint, silently substituted) | `.../openai/responses?api-version=2025-04-01-preview` (exactly as configured) |
| Wire shape actually sent | classic `messages`/`max_tokens` (unsupported by the configured endpoint's own real API) | real Responses API `input`/`max_output_tokens`/`reasoning.effort` |
| `reasoning.effort` sent | never (field didn't exist in the old client) | `"low"` (the new conservative default — role was not explicitly configured) |
| Generation outcome | completed (via Azure's tolerant `v1` compatibility surface) | completed: 754 `EffectInstance`s, 9 distinct effect names, correct `<mediaFile>`, real `.xsq` exported |
| Endpoint sanity warning in call log | n/a (didn't exist) | none (request path matches configured path exactly) |

Confirmed directly from the real `llm/` call log for this run (not inferred): every entry's
`Diagnostics.RequestUri` is the exact configured Responses API URL with its `api-version` query string
intact, `Diagnostics.RequestBody` shows the real `input`/`reasoning.effort` wire shape, and
`Diagnostics.EndpointSanityWarning` is `null` throughout. A representative entry also shows real prompt
caching in effect against this endpoint (`CacheReadInputTokens: 2504` of `InputTokens: 3227` on one
Choreographer call) — the fix does not disturb caching behavior.

**This is strong, though not conclusive, evidence that the endpoint-construction bug (wrong wire format +
no bounded reasoning effort) was the dominant cause of the ~57-minute single-call latency** this phase's
incident traced — an 8-10x wall-clock reduction on an unchanged song/show/credential, with no other variable
changed. It does not, by itself, settle Phase 12/13's own findings (change-density flatness, shallow
layering, narrow effect vocabulary) — this run still used the same Director/Choreographer prompt logic
those phases target, and the `.xsq`'s effect-name diversity (9 distinct names) has not been re-compared
against the original reference numbers in `AI-CHOREOGRAPHY-REAL-RUN-COMPARISON-2026-10-30.md`. That
comparison is still worth re-running explicitly before treating Phase 12/13's motivating data as fully
settled, per this phase's own §3.
