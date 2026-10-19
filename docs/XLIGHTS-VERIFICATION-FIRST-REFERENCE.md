# xLights Integration: Verification-First Engineering Guide

**Purpose:** Distinguish confirmed file-format facts from details that the coding agent must inspect on the target xLights build. Do not use this guide to justify silently assuming unverified effect settings.

## Confirmed architecture facts

- Editable sequence files use `.xsq` XML; runtime playback/channel output uses `.fseq` and cannot reconstruct editable effect parameters. Source: xLights manual glossary.
- `xlights_rgbeffects.xml` stores show models/groups and other effect-related settings; `xlights_networks.xml` stores controller setup. Source: xLights manual glossary.
- Public `.xsq` files include elements such as `head`, `EffectDB`, `ColorPalettes`, `DisplayElements`, `ElementEffects`, and per-model `EffectLayer` / `Effect` nodes. Source: linked public XSQ example. Exact attributes are version/recipe specific.
- Model groups can contain overlapping model membership, and group member order affects some render styles. Source: official xLights model-group documentation.
- Upstream xLights development `AGENTS.md` currently describes headless render; installed release support must be capability-tested.

## Required empirical fixture matrix (do not shortcut)

| Fixture | Authored in real xLights | Validate with our compiler |
|---|---|---|
| Empty/minimal sequence | New animation + correct timing type | Can open/round-trip document without changing metadata |
| One On effect on basic prop | One model/one layer | Effect name, setting ref, palette ref, duration + render |
| Multiple layers on one prop | Two simple effects, known blend rule | Layer ordering and duration semantics |
| Timing track | Beat markers in official tool | Correct marks/labels and layer layout |
| Group and submodel target | Group order and submodel selection | Correct effect target identity |
| Model rename | Rename after initial import | Detect mismatch and request mapping |
| Media-linked XSQ | Music file in show folder / external path | Resolve media and avoid path breakage |
| Different xLights versions | One older/one current supported build | Explicit compatibility report and model/effect negotiation |

## XML writer check list

- [ ] XML entities cannot expand unsafely on import.
- [ ] Known required head/datalayer/element sections copied/generated per version fixture.
- [ ] EffectDB and palette integer refs are valid; deterministic ordering and stable IDs.
- [ ] All model names resolve against selected show layout; groups/submodels are correctly targeted.
- [ ] Millisecond timing/frame duration conversion is consistent with selected sequence frame interval.
- [ ] Additional xLights version settings are preserved as appropriate and unsupported features fail closed.
- [ ] Export is atomic, new file by default and manually/authoritatively rendered at phase gate.

## Source pointers and status

- xLights source / headless rendering design (verify installed builds separately): https://github.com/xLightsSequencer/xLights/blob/master/AGENTS.md
- xLights manual glossary (layout/sequence formats): https://manual.xlights.org/xlights/appendicies/glossary
- xLights timing tracks: https://manual.xlights.org/xlights/chapters/chapter-four-sequencer/timing-tracks
- xLights model groups: https://manual.xlights.org/xlights/chapters/chapter-four-tabs/model-groups
- xLights importing and re-rendering: https://manual.xlights.org/xlights/chapters/chapter-five-menus/import
- xLights official source repository: https://github.com/xLightsSequencer/xLights
- Real public XSQ example (for observation; verify against target version): https://github.com/FRC3620/xlights.sign1/blob/main/2026%20Doug.xsq
- xLightsAI upstream raw XSQ generator path: `src/generator/xsq_writer.py` in pinned upstream checkout.

**Unverified until implementation:** actual installed target binary flags, all effect parameter keys, native ML compatibility, every supported OS, all model/group corner cases.
