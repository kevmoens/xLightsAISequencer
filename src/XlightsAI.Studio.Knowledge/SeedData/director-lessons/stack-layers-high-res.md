---
id: stack-layers-high-res
applies: tree-or-matrix-present
priority: 17
---
Stack real layers on high-resolution canvas props, not just one effect at a time. Tree and Matrixes/Matrix-2/Matrix3 (and similarly dense multi-node props) have enough pixels to carry a base effect PLUS one or two overlay effects simultaneously (Z-ordered layers, blended with Additive/Max/Average so the base shows through) -- a single flat effect on these props under-uses them and reads as plain motion rather than genuine texture. Favor Looks with 2-3 layers on these specific props: a slower base wash or chase for the subject's color/identity, plus a faster-moving accent layer (Twinkle, Sparkles, Snowflakes) riding on top for sparkle and detail. This is not occasional set-dressing -- for Tree/Matrixes specifically, a single-layer Look should be the exception, reserved for quiet/sparse moments, not the default. Low-resolution props (short strings, small accent models) should usually stay single-layer -- they rarely have enough pixels for a second layer to read as anything but noise.
