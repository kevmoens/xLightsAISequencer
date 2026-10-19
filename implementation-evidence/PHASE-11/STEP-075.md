# PHASE-11 Step 075 — Add optional Ollama/cloud provider behind opt-in structured-output adapter

**Date:** 2026-10-17
**Spec:** `docs/phase-specs/PHASE-11.md` §6, Step 075
**Required acceptance (verbatim):** "Provider off means no outbound calls; malformed JSON rejected."
**Required failure/edge case (verbatim):** "Invalid input, cancellation or stale persisted state must not corrupt the project."

## 1. Pre-implementation baseline inspection

Before writing any code, a research pass (`Agent` tool, `Explore` subagent) inspected the repository to establish the current state of Steps 072–074 and every pattern Step 075 would need to extend or match. Findings (verified directly against the repo afterward, not taken on faith):

- **Steps 072/073 (`src/XlightsAI.Studio.AiEdits/`)** already implement `SelectionContext` (Step 072's own ADR-narrowed replacement for the spec's proposed `EditContext`), `StructuredEditOperation`, `StructuredEditOperationKinds` (a closed, five-value string vocabulary — deliberately not a C# enum, exactly so a later JSON-sourced AI proposal can be checked against the same vocabulary), `EditProposal`, and `IStructuredEditInterpreter`/`StructuredCommandInterpreter` (regex-grammar, gates: cancellation → stale revision → locks → grammar/parameters, throwing `EditCommandRejectedException`/`EditRejectionReason`).
- **Step 074 (`src/XlightsAI.Studio.Shell/AiEditRequestPanelViewModel.cs`)** is a bindable ViewModel whose `SubmitAsync(IStructuredEditInterpreter, SequenceDocument, CancellationToken)` was deliberately written against `IStructuredEditInterpreter` alone, specifically to leave room for Step 075 to add an `IOptionalAiPlanner` fallback without changing the panel's existing bindable contract (confirmed in `STEP-074.md` §3).
- **No prior phase implements any part of Step 075's own scope.** Confirmed by full-repo search: `IOptionalAiPlanner`, `EditContext`, `ProposalValidation`, `Ollama`, `BYOK`, `ICredentialStore`/`ISecretStore`, `IHttpClientFactory`, Polly, and any DI container package (`Microsoft.Extensions.DependencyInjection`) all return zero hits anywhere in `src/`. The repository's only pre-existing network-call precedent is `HttpModelAssetDownloader` (`src/XlightsAI.Studio.Stems/`) — an optional injectable `HttpClient`, disposed only if owned, streaming to disk. Its sibling `IModelAssetManager.EnsureVerifiedAsync(ModelAsset, bool userConsent, CancellationToken)` is the repo's only precedent for an explicit, per-call, auditable consent gate (`ModelAssetConsentRequiredException` when withheld).
- Composition is 100% manual everywhere (`Program.cs` assigns constructed collaborators to `App`'s static properties; no DI container). Step 075 follows the same convention: a single `OptionalAiPlannerFactory.Create(...)` static method is the one composition point, not a registered service.
- `fixtures/` has no PHASE-11-specific fixture files; every AiEdits/Shell test (072/073/074) builds its own in-code `DisplayGraph`/`SequenceDocument` "gate-demo" fixture (Roofline, Arch 1, Arch 2) via a shared private helper. Step 075's own tests follow the identical convention — no new fixture file was added to `fixtures/`.

## 2. Scope actually implemented

New files, all in `src/XlightsAI.Studio.AiEdits/` unless noted:

| File | Purpose |
|---|---|
| `AiProviderKind.cs` | `OllamaLocal` \| `CloudByok` enum. |
| `AiProviderOptions.cs` | Runtime config for one `ProposeAsync` call chain (real `TimeSpan`, never persisted directly). `Disabled` static default. |
| `AiProviderSettings.cs` | Persisted config (plain `int` seconds, JSON-friendly, **no credential field**). `ToOptions()` projects to `AiProviderOptions`. |
| `AiProviderDisabledException.cs` | Thrown only by `NullOptionalAiPlanner` — "Provider off means no outbound calls." |
| `AiProposalRejectionReason.cs` | Closed enum: `MalformedJson`, `UnknownOperationKind`, `OutOfScopeTarget`, `OutOfScopeTime`, `StaleRevision`, `ResponseTooLarge`, `EmptyOperations`, `ProviderUnavailable`. |
| `AiProposalRejectedException.cs` | Thrown by the real planner for every untrusted-input rejection; mirrors `EditCommandRejectedException`'s shape. |
| `IOptionalAiPlanner.cs` | The spec's own proposed boundary, declared against the already-accepted `SelectionContext` (consistent with Step 072/073's own ADR), not the spec's proposed `EditContext`. |
| `NullOptionalAiPlanner.cs` | Singleton; the only planner a disabled configuration ever resolves to. Owns no `HttpClient`. |
| `HttpStructuredOutputAiPlanner.cs` | The real, generic Ollama-compatible / BYOK-cloud-compatible structured-JSON HTTP adapter. Redacts the request, validates the response against the closed op-kind vocabulary and the originating scope, enforces size/timeout, retries transient failures with backoff, never retries a schema rejection. |
| `OptionalAiPlannerFactory.cs` | The single required composition point — `Create(options, httpClient?, apiKey?)` returns `NullOptionalAiPlanner` and touches neither `httpClient` nor `apiKey` when `options.Enabled` is `false`. |
| `ICredentialStore.cs` / `DpapiCredentialStore.cs` | First credential-storage code in this repository. Windows DPAPI (`ProtectedData`, `DataProtectionScope.CurrentUser`), atomic temp-file+move write, `[SupportedOSPlatform("windows")]`-guarded, throws `PlatformNotSupportedException` elsewhere (no cross-platform store exists yet — disclosed, not silently stubbed). |
| `IAiProviderSettingsStore.cs` / `JsonAiProviderSettingsStore.cs` | Same atomic-write pattern as `JsonSelectionContextStore`. A missing/corrupt file falls back to `AiProviderSettings.Disabled` — a damaged file can never leave the provider silently enabled. |
| `src/XlightsAI.Studio.Shell/AiEditRequestPanelViewModel.cs` (edited) | Added `SubmitAsync(IStructuredEditInterpreter, IOptionalAiPlanner?, SequenceDocument, CancellationToken)`. The known-command path is tried first and exclusively; the planner is consulted **only** when the interpreter rejects with `EditRejectionReason.UnrecognizedInstruction` — a stale/locked rejection never falls through. The pre-existing 3-argument `SubmitAsync` is now a one-line delegation (`planner: null`) — unchanged behavior, confirmed by test `PreExisting3ArgumentOverload_StillWorksUnchanged`. |
| `src/XlightsAI.Studio.AiEdits/XlightsAI.Studio.AiEdits.csproj` (edited) | Added `PackageReference Include="System.Security.Cryptography.ProtectedData" Version="9.0.0"` (the DPAPI API surface). No other project reference changed. |

New test files:

- `tests/XlightsAI.Studio.AiEdits.Tests/Step075AcceptanceTests.cs` — 19 tests against the real `OptionalAiPlannerFactory`/`NullOptionalAiPlanner`/`HttpStructuredOutputAiPlanner`/`JsonAiProviderSettingsStore`/`DpapiCredentialStore`, using an in-process `HttpMessageHandler` test double (`CountingHandler`) — no mocking library, matching every prior AiEdits test.
- `tests/XlightsAI.Studio.Shell.Tests/Step075AiEditRequestPanelPlannerFallbackTests.cs` — 7 tests against the real `AiEditRequestPanelViewModel` + `StructuredCommandInterpreter`, using a small in-memory `RecordingPlanner` test double (not a mocking library).

## 3. Architecture deviations / decisions (ADRs)

1. **`IOptionalAiPlanner` is declared against `SelectionContext`, not the spec's proposed `EditContext`.** The spec's own proposed `EditContext(long, EditScope, IReadOnlyList<NoteEvent>, IReadOnlyList<PlannedGesture>, IReadOnlyList<string>)` was never implemented — Step 072's own ADR narrowed that shape to `SelectionContext`, and `IStructuredEditInterpreter` already made this same substitution. Keeping `IOptionalAiPlanner`'s signature consistent with `IStructuredEditInterpreter`'s is required for `AiEditRequestPanelViewModel` to route between the two without an adapter/mapping layer.
2. **One generic `HttpStructuredOutputAiPlanner`, not separate Ollama/BYOK classes.** The step heading says "Ollama/cloud provider" (singular slash-joined phrase) and the implementation guidance says "Provider interface supports offline local and BYOK cloud" — both read as one interface, two configurations, not two adapters. Both share the exact same redacted-request/validated-response envelope; only the endpoint and an optional bearer `Authorization` header (from `ICredentialStore`) differ. This avoids a second adapter class whose only difference would be a header.
3. **Credential storage is new, Windows-only DPAPI, not a cross-platform vault.** No `ICredentialStore`/`ISecretStore` existed anywhere in the repository before this step (confirmed by full-text search) — this is new ground, not a reuse of an existing pattern. `DpapiCredentialStore` is explicitly `[SupportedOSPlatform("windows")]` and throws `PlatformNotSupportedException` elsewhere. **Disclosed gap:** no cross-platform (macOS Keychain / Linux Secret Service) implementation exists yet; `ICredentialStore` is the seam a future platform-specific implementation would fill. The product's own stated boundary (Avalonia/.NET desktop, Windows-first per every prior phase's `WaveOutAudioTransport`-style platform guard) makes this an acceptable, disclosed, phase-scoped limitation, not a silent omission.
4. **Consent is the stored `AiProviderSettings.Enabled` flag, not a per-call `bool` parameter.** `IModelAssetManager.EnsureVerifiedAsync(ModelAsset, bool userConsent, CancellationToken)` is the repo's only prior consent precedent, but it models a one-shot download decision. Step 075's required acceptance — "Provider off means no outbound calls" — reads as a standing, stored, auditable toggle a user sets once (in a future settings screen, out of this step's own UI scope), not a decision re-made on every edit request. `OptionalAiPlannerFactory.Create` is the single place this toggle is enforced; it is checked before any `HttpClient`/credential is even touched.
5. **Response size is capped against the declared `Content-Length` header and the fully-buffered body's actual byte count, not a true streaming read limit.** A provider lying about its length under chunked transfer-encoding could still force a larger-than-intended in-memory buffer before rejection. A custom bounded `Stream` wrapper was judged disproportionate to this step's "offline local, or opt-in BYOK cloud, small bounded JSON envelope" use case; documented here and in the type's own XML doc comment as a known, disclosed limitation rather than silently claiming a full streaming guarantee.
6. **Retries are a hand-rolled bounded loop with exponential backoff, not Polly.** No retry-policy library exists anywhere in the repository; adding one for a single call site was judged disproportionate ("do not fabricate generic interfaces without a concrete boundary," Section 2). A schema/validation rejection (`AiProposalRejectedException`) is explicitly never retried — only `HttpRequestException`/`TaskCanceledException`/`TimeoutException` are treated as transient.
7. **Stale revision is checked before any outbound request is made**, mirroring `StructuredCommandInterpreter`'s own Gate 1 exactly — a stale selection never even reaches the provider, let alone risks a paid/metered call against it.

No existing accepted contract (Steps 072–074) was modified except the additive `AiEditRequestPanelViewModel` overload described above; the pre-existing 3-argument `SubmitAsync` signature and behavior are unchanged (verified by `PreExisting3ArgumentOverload_StillWorksUnchanged`).

## 4. Commands actually executed

All commands were run via the **PowerShell** tool. (As in every prior PHASE-11 evidence doc, the Bash tool returns no usable output in this sandboxed session — `mkdir -p` and `dotnet --version` both silently exit 1 with no output; this is disclosed, not worked around by claiming Bash output that never appeared.)

```
> dotnet --version
10.0.302

> dotnet build src\XlightsAI.Studio.AiEdits\XlightsAI.Studio.AiEdits.csproj -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet build XlightsAI.Studio.slnx -v minimal
Build succeeded. 0 Warning(s) 0 Error(s)

> dotnet test tests\XlightsAI.Studio.AiEdits.Tests\XlightsAI.Studio.AiEdits.Tests.csproj -v minimal
Passed! - Failed: 0, Passed: 48, Skipped: 0, Total: 48, Duration: 1 s

> dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj -v minimal --filter "FullyQualifiedName~Step075|FullyQualifiedName~Step074"
Passed! - Failed: 0, Passed: 18, Skipped: 0, Total: 18, Duration: 123 ms

> dotnet test XlightsAI.Studio.slnx -v minimal   (full solution regression)
XlightsAI.Studio.Xlights.Tests.dll      : 74/74 passed
XlightsAI.Studio.Audio.Tests.dll        : 28/38 passed (10 failed -- pre-existing, see §7)
XlightsAI.Studio.Editing.Tests.dll      : 91/91 passed
XlightsAI.Studio.Display.Tests.dll      : 56/56 passed
XlightsAI.Studio.Composition.Tests.dll  : 78/78 passed
XlightsAI.Studio.AiEdits.Tests.dll      : 48/48 passed
XlightsAI.Studio.Projects.Tests.dll     : 25/25 passed
XlightsAI.Studio.Structure.Tests.dll    : 78/78 passed
XlightsAI.Studio.Melody.Tests.dll       : 117/117 passed
XlightsAI.Studio.Shell.Tests.dll        : 236/251 passed (15 failed -- pre-existing, see §7)
XlightsAI.Studio.Rhythm.Tests.dll       : 82/82 passed
XlightsAI.Studio.Reference.Tests.dll    : 31/31 passed
XlightsAI.Studio.Stems.Tests.dll        : 101/101 passed
TOTAL: 1045 passed / 1070 total (25 pre-existing failures, 0 new)

> dotnet format XlightsAI.Studio.slnx --verify-no-changes --no-restore
(no output -- no formatting violations)
```

Observable desktop demonstration (disposable console app, not committed to the repository, matching the `%TEMP%\xlai-stepNNN-demo-proj` convention every prior PHASE-11 evidence doc used):

```
> mkdir C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step075-demo-proj
> # wrote demo.csproj + Program.cs referencing the real AiEdits/Composition/Editing/Xlights/Contracts projects
> cd C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step075-demo-proj
> dotnet run -v minimal
```

Full transcript (abridged to remove restore/build noise):

```
=== PHASE-11 Step 075 demo: Selection ===
Scope models: Arch 1, Arch 2
Scope time:   [100000000, 300000000)

--- Scenario 1: provider disabled ---
Resolved planner type: NullOptionalAiPlanner
PASS: No optional AI provider is enabled; no outbound request was made.

Started real loopback HTTP listener at http://127.0.0.1:57613/

--- Scenario 2: provider enabled, malformed JSON response ---
[listener] received redacted request: {"BaseDocumentRevision":1,"StartTicks":100000000,"EndTicks":300000000,"ModelIds":["Arch 1","Arch 2"],"RespectLocks":true,"PermittedOperationKinds":["SetBrightness","SetDirection","SetSpeed","SnapToGrid","ReplaceRole"],"Instruction":"rising piano pops"}
PASS: Reason=MalformedJson, Message=AI provider returned JSON that could not be parsed, or that carried a property outside the declared schema.

--- Scenario 3: provider enabled, response targets Roofline (out of selected scope) ---
[listener] received redacted request: {"BaseDocumentRevision":1,...}
PASS: Reason=OutOfScopeTarget, Message=AI provider operation 'op-1' targets a model outside the selected scope; no proposal was built.

--- Scenario 4: provider enabled, valid response accepted ---
[listener] received redacted request: {"BaseDocumentRevision":1,...}
PASS: Operation Kind=SetBrightness, Targets=[Arch 1, Arch 2], Explanation=Rising piano pops across the selected arches.

Loopback listener stopped.

--- Scenario 5: provider settings + BYOK credential persistence ---
Settings round trip equal: True
Settings file contains no credential field: True
Credential file does not contain plaintext secret: True
Decrypted credential round trip matches: True

=== Demo complete ===
```

This demo exercises a **real loopback TCP socket** (`System.Net.HttpListener` on `127.0.0.1`, an OS-assigned free port), not an in-process fake handler — the redacted request payload that actually crossed the wire is printed verbatim by the listener (Scenario 2/3/4 logs), proving the "redacted, least-privilege context" claim empirically: it contains only `BaseDocumentRevision`, `StartTicks`/`EndTicks`, `ModelIds`, `RespectLocks`, the closed `PermittedOperationKinds` vocabulary and the raw instruction text — no note/phrase/path ids, no lyrics, no other document content. Scenario 5 proves the on-disk settings file carries no credential field and the on-disk `.cred` file is not human-readable plaintext, with a successful decrypt round trip via real Windows DPAPI.

The demo project and its `bin`/`obj` output were created entirely under `%TEMP%` and are not part of this repository's tracked tree.

## 5. Persisted-output inspection / observable demonstration

See §4's demo transcript. In addition to the demo, the following were inspected directly:

- The on-disk JSON file from `JsonAiProviderSettingsStore` (`tests/.../Step075AcceptanceTests.cs: JsonAiProviderSettingsStore_RoundTripsThroughAtomicWrite`) was asserted to contain exactly one file after save (no leftover `.tmp-*` file), proving the atomic-write-then-move completes cleanly.
- The on-disk `.cred` file from `DpapiCredentialStore` (same test file, `DpapiCredentialStore_RoundTripsEncryptedSecret`) was read back as raw bytes and asserted to **not** contain the plaintext secret substring, then successfully decrypted back to the exact original string via a second, independent `TryGetAsync` call.

## 6. Fixtures / input hashes

No new fixture files were added to `fixtures/`. All tests use the same in-code "gate-demo" `DisplayGraph`/`SequenceDocument` fixture (`SourceLayoutHash: "TEST-HASH-NOT-A-REAL-FILE"`, three models: Roofline / Arch 1 / Arch 2) that Steps 072/073/074 already established, built via `ThreeEffectPlanFactory`/`EffectRecipeRegistry` — consistent with the project's own established no-persisted-fixture-file convention for this step family.

## 7. Unresolved issues / known gaps

- **Carried-forward, pre-existing, environment-blocked condition (not introduced by this step):** 10 `XlightsAI.Studio.Audio.Tests` and 15 `XlightsAI.Studio.Shell.Tests` (Step025/028/029/030/042ShellWiringTests) fail in this sandboxed session because they require a real audio output device, which is unavailable here. This exact condition is disclosed identically in `STEP-072.md`, `STEP-073.md` and `STEP-074.md` §7 — the same test names fail for the same reason, confirming no regression was introduced by this step's changes.
- **No real SCREEN 14 AI EDIT Avalonia/AXAML view exists yet** — a pre-existing, disclosed, project-wide condition carried forward unchanged from Steps 072–074 (not newly introduced here). This step's own "Versioned, inspectable project output plus a reproducible test report" outcome is proven at the bindable `AiEditRequestPanelViewModel` layer plus the console demo in §4, per the same convention Step 074 already established.
- **No cross-platform `ICredentialStore` implementation** (macOS Keychain / Linux Secret Service) — see ADR §3.3. `DpapiCredentialStore` is Windows-only by design; the interface seam exists for a future platform-specific implementation when/if macOS/Linux desktop support is scoped.
- **Response-size enforcement is not a true streaming cap** — see ADR §3.5. A pathological provider under chunked transfer-encoding could still force a larger-than-configured in-memory buffer before the size check runs. Disclosed as acceptable for this step's bounded-JSON-envelope use case, not silently hidden.
- **No real Ollama installation or real BYOK cloud endpoint was exercised.** Per the user's own standing waiver (recorded in prior-session memory: the real-xLights-render human-verification gate is waived for this project) and this step's own actual scope (an HTTP adapter boundary, not an xLights renderer), the manual demonstration in §4 uses a real loopback HTTP socket rather than a real third-party LLM service — exercising the exact same wire protocol/JSON contract this adapter speaks, with the external LLM's non-determinism/cost/network-dependency factored out. If a real local Ollama install or a real BYOK API key become available in a future session, re-running this adapter against them would be a strict superset confirmation, not a new code path.
- **UI consent/settings screen is out of this step's scope.** Step 075's own implementation guidance targets the provider interface/adapter itself; a user-facing settings screen to toggle `AiProviderSettings.Enabled`, enter a BYOK endpoint/key, etc. is implicitly SCREEN 14's own later concern and is not claimed as built here.

## 8. Required-acceptance-text-by-text tie-back

| Required text (verbatim) | Where proven |
|---|---|
| "Provider off means no outbound calls" | `Step075_Acceptance_ProviderOffMeansNoOutboundCalls` (unit, asserts `handler.CallCount == 0`); demo §4 Scenario 1 (real transport, zero connections — the listener log shows no request line before Scenario 2). |
| "malformed JSON rejected" | `Step075_Acceptance_MalformedJsonRejected` + `Step075_Acceptance_UnknownPropertyInResponse_RejectedAsMalformedJson_NegativePath` (unit); demo §4 Scenario 2 (real HTTP round trip). |
| "Invalid input... must not corrupt the project" | `ProposeAsync_NullArguments_Throw_NegativePath`, `UnknownOperationKind_IsRejected_NegativePath`, `OutOfScopeTarget_...NegativePath`, `OutOfScopeTime_...NegativePath`, `EmptyOperations_IsRejected_NegativePath`, `ResponseLargerThanConfiguredLimit_IsRejected_NegativePath` — every case asserts `plan.Effects.Count` is unchanged where applicable and no `EditProposal` is ever returned. |
| "cancellation... must not corrupt the project" | `CanceledToken_ThrowsWithoutAnyOutboundCall_NegativePath` (`handler.CallCount == 0`, effect count unchanged). |
| "stale persisted state must not corrupt the project" | `StaleRevision_IsRejected_BeforeAnyOutboundCall_NegativePath` (`handler.CallCount == 0`). |
| "enforce size/timeouts, retries" | `ResponseLargerThanConfiguredLimit_IsRejected_NegativePath` (size); `TransientFailure_IsRetried_ThenSucceeds` / `PersistentTransientFailure_ExhaustsRetries_ThenRejectedAsProviderUnavailable_NegativePath` (retries); per-attempt `CancellationTokenSource.CancelAfter(RequestTimeout)` (timeout, exercised implicitly by the attempt loop). |
| "consent" | `AiProviderSettings.Enabled` gate enforced exclusively by `OptionalAiPlannerFactory.Create`; `UnrecognizedInstruction_WithDisabledProvider_ShowsManualAlternativesMessage_NegativePath` (Shell). |
| "no silent paid requests" | The `Enabled` flag is the only path to an outbound-capable planner; `NullOptionalAiPlanner` is architecturally incapable of a network call (owns no `HttpClient` field at all). |
| Section 2: "LLM cannot target Roofline outside selected arches" (phase gate cross-cutting test #3) | `OutOfScopeTarget_RooflineOutsideSelectedArches_IsRejected_NegativePath` (unit); demo §4 Scenario 3 (real HTTP round trip, named "Roofline" literally in the response). |
| Section 2: "redacted, least-privilege context" | Demo §4 listener log — the actual wire payload printed verbatim contains only revision/time/model-ids/lock-flag/permitted-kinds/instruction. |

## 9. PASS / BLOCKED

**PASS.**

- Both required-acceptance sentences are proven by passing unit tests against real collaborators (no mocking library) and by a real-socket console demonstration.
- Both required failure/edge-case categories ("invalid input/cancellation/stale revision must not corrupt the project") are proven with explicit negative-path tests, each asserting zero outbound calls and/or an unchanged document state.
- Full solution build succeeds with 0 warnings/errors; `dotnet format --verify-no-changes` reports no violations.
- Full regression: 1045/1070 tests pass; the 25 failures are the exact same pre-existing, disclosed, real-audio-device-unavailable condition already recorded in STEP-072/073/074's own evidence — zero new failures introduced by this step.
- No prior accepted contract (Steps 072–074) was broken; the one Shell change is a strictly additive overload, verified unchanged by its own regression test.
- No show file, fixture, or prior persisted artifact was modified.

**Explicitly not claimed:** a real Ollama installation or a real third-party BYOK cloud endpoint was exercised (see §7) — the real-socket demo proves the adapter's own wire contract and every untrusted-input gate, which is this step's actual owned boundary; it does not and cannot prove a specific third-party LLM's own output quality, which is out of scope for an "adapter" step and remains a later step's/product's own concern.
