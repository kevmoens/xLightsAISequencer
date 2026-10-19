# External Compatibility and Feasibility Gates

The package is deliberately explicit about architecture but never invents unsupported external contracts.

1. **xLights XSQ serializer:** establish valid effect names, setting strings, palette references, section structure, media path and layer semantics from real xLights-authored files and pinned source. XML validity alone is not enough. Gate: Phase 02.
2. **xLights headless rendering:** development source documentation describes `--headless`; target installed builds must confirm flags, output and exit behavior. Gate: Phase 13.
3. **Music separation:** `.NET + ONNX` is an option, not proof that a given Demucs model will run correctly. Verify preprocessing, weights, license, CPU budget and quality. Gate: Phase 06.
4. **Melody analysis:** dense polyphony can defeat pitch transcription; use confidence ladder and user correction. Gate: Phase 07.
5. **Musical structure:** subjective sections and motifs must be inspectable and user-correctable. Gate: Phase 08.
6. **AI prompts:** structured model responses cannot bypass lock/scope safety; no cloud required. Gate: Phase 11.
7. **Quality metrics:** automatic correctness does not equal artistic quality; compare blinded previews with humans. Gates: 12, 13, 16.
8. **Native installer:** platform codecs, packaging, code signing and model redistribution must be proven on clean supported OS. Gates: 15, 16.

All proposed API signatures in phase documents are a **design starting point**, not a promise of having inspected every upstream implementation symbol. The coding agent must reconcile the running solution, pin revisions and record ADRs.
