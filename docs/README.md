# AI xLights Choreography Studio — Architect Edition

**16 senior-architect-level self-contained phase specifications, 100 testable implementation steps, dedicated coding prompts, full system architecture, 17 PNG/SVG screen wireframes, and xLights format verification references.**

## Which file do I give a coding agent?

**Start with [`phase-specs/PHASE-01.md`](phase-specs/PHASE-01.md).** Each phase file can be handed to a coding agent alone **as a specification**: it repeats common assumptions and includes C# signatures, a Mermaid class diagram, a core process flowchart, per-step implementation notes, negative tests, evidence requirements and a complete coding prompt. The agent must still read the actual codebase and complete previous phase interfaces; this is not 16 independent apps.

- [Master architecture](MASTER-SYSTEM-ARCHITECTURE.md) — dependencies/source-of-truth/phase integration.
- [Phase index](PHASE-INDEX.md) — order, inputs and gates.
- [Full interface catalog](CONTRACTS-AND-CLASS-DIAGRAM-INDEX.md) — cross-phase consistency reference.
- [Existing UI screen blueprints](UI-UX-SCREEN-BLUEPRINTS.md) — descriptions and wireframes of 17 screens.
- [xLights engineering guide](XLIGHTS-SEQUENCE-ENGINEERING-GUIDE.md) and [verification-first checklist](XLIGHTS-VERIFICATION-FIRST-REFERENCE.md) — exact-format evidence process.
- [Quality and acceptance](TEST-DATA-AND-ACCEPTANCE.md) — fixture and gate conventions.
- [Start prompt](START-HERE-COPY-PASTE-PROMPT.md) and [phase-specific prompts](prompts/).

## Agent instruction files

`AGENTS.md` provides repository-level rules to compatible coding agents. For tight incremental work use [`prompts/CODE-ONE-STEP.md`](prompts/CODE-ONE-STEP.md); to implement one entire phase with strict gates use its dedicated phase prompt.

## The implementation rule

Build **one numbered step** at a time, verify it, collect evidence, then continue. Phase is complete only when its gate can be observed. Every user-approved manual effect must survive all later automatic processing; xLights show files remain read-only unless a user explicitly instructs a safe write.

## Honest status

This ZIP is an **implementation specification**, not the finished application. C# signatures are intentional proposed API contracts and must be reconciled with working source code; a few external details (effect recipe XML, model inference quality, exact headless renderer support) cannot be honestly asserted without pinned upstream source and real installed-version tests. Each phase includes validation work to resolve those unknowns.

## File counts and edition

Generated 2026-10-10. The archive includes all previous screen/assets/documents and replaces the older short per-phase checklists with full architecture specifications. No licensed song or copyrighted third-party code is bundled.
