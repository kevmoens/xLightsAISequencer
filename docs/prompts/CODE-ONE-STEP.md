# Reusable Prompt — Implement Exactly One Vertical Slice Step

```text
Act as the senior .NET 10/Avalonia architect and coding agent. Read the complete `phase-specs/PHASE-NN.md` for the assigned phase, then implement ONLY Step XXX from that document. Treat all preceding completed step contracts as immutable unless the current step has an accepted migration plan. Read relevant source/fixtures and run baseline tests first. Turn the step's acceptance text into executable tests and add at least one failure-path assertion.

Build the minimum complete UI -> application -> domain -> data/adapter slice; run `dotnet build` / applicable `dotnet test` commands and xLights or native inference manual tests when this step requires them. If an external environment is unavailable, label the real integration test BLOCKED instead of pretending a mock is equivalent. Never overwrite original show files; preserve effect locks and unchanged timelines.

Create `implementation-evidence/PHASE-NN/STEP-XXX.md` recording actual commands, outputs, changed paths, fixtures/source versions, UI screenshots/XSQ artifacts, risks, and PASS/BLOCKED. Show me any architecture deviations and do not execute the next step until this step is proven.
```

Replace `NN` and `XXX` with the desired numbers. Phase MD files contain the exact acceptance, C# interfaces, class/flow diagrams and failure cases.
