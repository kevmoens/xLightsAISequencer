# Phase 4 real-track metric comparison (declared/IR-level, not rendered)

Compiled against a 90s clip of `C:\code\xLightsAISequencer\tests\XlightsAI.Studio.Choreography.Tests\bin\Debug\net10.0\Fixtures\dubstep-90s-clip.mp3` + `C:\xLights\xlights_rgbeffects.xml`, compared to the
hand-sequenced reference at `C:\xLights\Dubstep Christmas Light Show.xsq`.

| Metric | Compiled (this engine) | Hand-sequenced reference | Placeholder-era (architecture §1) |
|---|---:|---:|---:|
| Effect count | 2251 | 36453 | 207 |
| Effect type diversity | 7 | 6 | 1 |
| Timing tracks | 5 | 0 | 0 |
| Color palettes | 9 | 12 | 1 (single fixed white) |
| Legality violations (architecture §7) | 6 | n/a | n/a |
| Sections compiled | 9 | n/a | n/a |
| Largest gap across every targeted prop | within 1 bar (asserted) | n/a | 17s (documented original defect) |

Export warnings: none