# Build Plan: Phase 11 — AI Provider Request Correctness and Observability

**Status:** §2a-§2d implemented and verified 2026-10-30. Real re-run against the original incident's own
Azure endpoint/credential dropped total generation wall-clock time from 1h14m to 7m20s with no other
variable changed. See `implementation-evidence/PHASE-11-RESULTS.md` for the full before/after record,
including the real call-log `Diagnostics` confirming the correct endpoint, wire shape, and reasoning-effort
value were actually sent. Originally written 2026-10-30 in direct response to a real finding from
`docs/AI-CHOREOGRAPHY-REAL-RUN-COMPARISON-2026-10-30.md`: a real `tools/GenerateOnce` run against a
configured Azure OpenAI **Responses API** endpoint took 1h14m total (one single Director call alone ran
~57 minutes), and the request actually being sent was traced to a real endpoint-construction bug, not
expected model latency.

This slots in after Phase 10 in `AI-CHOREOGRAPHY-BUILD-PLAN.md`'s own numbering (`AI-CHOREOGRAPHY-BUILD-PLAN-PHASE-10-SIGNAL-UTILIZATION.md`),
independent of it — nothing here blocks Phase 12/13, and this does not depend on them. It should land
**before** Phase 12/13's own findings are trusted as conclusions about model creative quality, since right
now those findings may be measuring a misconfigured call rather than the intended one.

---

## 1. Why this phase exists

### 1a. Confirmed finding (2026-10-30)

`ProviderEndpointUri.Build` (`src/XlightsAI.Studio.Director/ProviderEndpointUri.cs`) exists to tolerate a
user pasting a provider's full API path into the Settings screen's base `Endpoint` field instead of just
the host — it strips one of two hardcoded known suffixes (`v1/messages`, `v1/chat/completions`) before
appending its own target path. It has no entry for Azure's **Responses API** shape
(`.../openai/responses?api-version=2025-04-01-preview`), which this project's own configured
Director/Choreographer provider actually uses.

Traced directly against the real configured endpoint string:

```
configured:  https://<resource>.openai.azure.com/openai/responses?api-version=2025-04-01-preview
actually sent to: https://<resource>.openai.azure.com/openai/v1/chat/completions
```

The entire `/openai/responses` path **and** the `api-version` query string are silently dropped. This
"worked" (a real response did come back, with real token usage) only because Azure separately exposes a
newer OpenAI-compatible `v1` convenience surface that tolerates the classic chat-completions request/response
shape regardless of what was configured. Two real consequences follow:

- `OpenAiCompatibleChatCompletionClient` has no support for the Responses API's own request/response schema
  (`input`/`output`, not `messages`/`choices`) at all — it always sends the classic shape.
- Any Responses-API-specific setting the user might configure for this model (most importantly a
  reasoning-effort/thinking-budget control) never reaches the model, because the client never speaks that
  API at all.

This is a very plausible root cause of both the extreme single-call latency observed and some of the output
narrowness/flatness `AI-CHOREOGRAPHY-REAL-RUN-COMPARISON-2026-10-30.md` documents (an unbounded/default
reasoning budget on a large prompt can legitimately produce very different, and very slow, output than a
properly bounded one).

### 1b. A related observability gap

Diagnosing the above took manually reconstructing the actual request URL from source and reasoning through
.NET's `Uri` combination rules, then independently confirming it with a throwaway test — the running system
gave no way to see it. `AiCallLogEntry`/`JsonAiCallLogStore` (already written on every call, success or
failure) records usage, cost, and a short note, but never the request URL or body actually sent. Likewise,
nothing validates that the constructed request URL's own path matches the API surface the user configured —
a Responses-API endpoint silently becoming a chat-completions call produces no warning anywhere.

---

## 2. Proposed technical changes

### 2a. Recognize the Responses API endpoint shape (highest priority)

1. Extend `ProviderEndpointUri`'s known-suffix table (or add a dedicated recognizer) to detect an endpoint
   naming `/openai/responses` (with or without a trailing `api-version` query string) distinctly from the
   classic `v1/chat/completions`/`v1/messages` suffixes it already knows.
2. Add a `OpenAiResponsesChatCompletionClient` (or extend `OpenAiCompatibleChatCompletionClient` with a
   second code path) that speaks the Responses API's own real wire shape — `input` instead of `messages`,
   `output`/`output_text` instead of `choices[].message.content`, and forwards the configured `api-version`
   query parameter — selected automatically when `RoleProviderConfig.Endpoint` names that surface, with no
   new user-facing setting required to opt in.
3. Add a unit test asserting the real configured endpoint string from this incident resolves to the correct
   Responses API URL (reproducing the bug, then proving the fix), the same "reproduce, then fix" discipline
   already used throughout this codebase's own test suite.

### 2b. Bound reasoning effort / thinking budget

1. Add an optional reasoning-effort/thinking-budget field to `RoleProviderConfig`, forwarded on the Responses
   API request when present and the provider/model supports it, defaulted to a conservative, documented value
   rather than left unbounded.
2. Document the real tradeoff this exposes in the Settings screen's own help text: a higher budget can
   produce richer per-section reasoning (and real cost already shows this: input/output token counts scale
   with it) at the cost of latency per call — so a user intentionally choosing a slower/richer mode should be
   able to, but the *default* should not silently run for the better part of an hour.

### 2c. Log the real outgoing request

1. Extend `AiCallLogEntry`/`JsonAiCallLogStore` to also persist the actual request URI (and, bounded by the
   same `MaxResponseBytes`-style cap already used for responses, the request body) for every call, success or
   rejection — this is the single change that would have made this incident diagnosable from the call log
   alone, in seconds, instead of requiring source-level reverse-engineering.
2. Add a startup/first-call sanity check comparing the constructed request URL's own path against the
   configured `RoleProviderConfig.Endpoint`'s own path, surfacing a clear warning (not a hard failure — a
   deliberate compatibility redirect may be intentional for some other provider) when they diverge
   unexpectedly.

### 2d. Re-run the real-run comparison once fixed

Re-run the exact same real-song comparison `AI-CHOREOGRAPHY-REAL-RUN-COMPARISON-2026-10-30.md` performed,
against the corrected Responses API call path, to separate "the model's own creative ceiling" from "an
unconfigured/default reasoning budget" as the explanation for that report's Findings 1-4 (change-density
flatness, missing steady base layers, shallow layering, narrow effect vocabulary) before committing further
engineering effort in Phase 12/13 to compensate for what might partly be a configuration defect.

---

## 3. Sequencing and dependencies

Independent of Phase 10's own §3a-§3g; does not block, and is not blocked by, Phase 12 or Phase 13 below —
but should land and be verified (§2d) **before** treating Phase 12/13's own motivating data as settled, since
a corrected call may change the measured baseline those phases are reacting to.

## 4. Acceptance criteria

- A unit test reproduces the real endpoint-mangling bug against the actual configured endpoint string and
  proves the fix resolves it to a correct Responses API URL.
- A real `tools/GenerateOnce` run against the real configured Azure endpoint demonstrably uses the Responses
  API's own request shape (confirmed via the new request-URL/body logging from §2c, not inferred).
- The same call log shows a reasoning-effort/thinking-budget value was actually sent, not silently defaulted
  by omission.
- The real-run comparison from `AI-CHOREOGRAPHY-REAL-RUN-COMPARISON-2026-10-30.md` is re-executed against the
  fix, and its results (including total wall-clock time for a comparable song) are recorded alongside the
  original for contrast.
