# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `7062531` (M32). This file is the current page, not a log._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS, files1). Unity 6000.3.23f1._
_Order of 2026-09-25 (cap 10 passes): **M31, M32, M33, M34**. One writer at a time; Unity slot free at STATE-A._

## 1. The order, by number

| # | Milestone | State |
|---|---|---|
| 1 | M31 — the lane blends by **transparency**, not geometry | **GREEN** — `ea81ad8` |
| 2 | M32 — the ground catches the moonlight | **GREEN** — `7062531` |
| 3 | M33 — scary lanterns, placeholder + swap-in point | not started |
| 4 | M34 — the scarecrow is THE antagonist | not started; silhouette test **open, not passing** |

Previous order, all green: M29 `f46b9e0`, M27 `e460272`, M28 `34dbf49`. Earlier queue untouched:
M20 `7a6a93f`, M21 `ca83b66`, M22 `7518c78`, M23 `318b543`, M24 `4d2b253`, M25 `874d90f`,
M25b `dc3b53a`, M26 `aa0f23a`. The done marker gets written only when all four are closed.

## 2. M32 — the ground catches the moonlight

Todd's question was "can you use the PBR files for reflections off the moonlight?" — and until this pass the
answer was no: one constant `_Smoothness` per ground material, roughness maps never reaching the shader, so
the whole floor was uniformly matte. M29's report had that written down as a deferral.

The sets ship roughness as a separate `_R` map and URP/Lit cannot take a standalone roughness texture, so the
materials now wear URP's own **metallic/smoothness** map: `T_Ground_Field_M` / `T_Ground_Lane_M`, RGB
metallic 0, **A = smoothness**, wired to `_MetallicGlossMap` with `_SmoothnessTextureChannel = 0` and
`_Smoothness = 1`. The keyword is `_METALLICSPECGLOSSMAP` — taken from URP's own material upgrader, because a
metallic map bound under a different keyword renders as if it were not there. Normal maps stay bound: they
are what breaks the highlight into grain instead of a mirror blob.

**Measured** (`artifacts/m32-reflection-report.txt`), same view, one variable: band mean luminance
**49.70 → 64.67 of 255 (+30 %)** and band peak **158 → 255** with the maps bound versus the exact pre-M32
constants. Attribution: the field's map accounts for almost all of the mean, the lane's map for the peak.
The peak clips, so it is a floor, not a value.

Getting an honest measurement cost three runs, and the reasons are in the report: writing the camera's
transform drifts 76 m (the rig owns it); correcting the look state by the measured residual oscillates
between both pitch clamps, because **the rig's pitch sign is inverted against the accessor that reports it** —
so the aim is now a sign-free hill-climb, and it lands the mirror point with an 11 px residual. The Husk is
disabled during the measurement, after one run died at "Caught by the Husk" and measured corn leaves.

## 3. Open — reported, not hidden

- **The band peak clips at 255.** The true highlight is at least that bright. If it wants dialling back the
  lever is the roughness numbers in the `_M` maps (lane mean 0.28, field 0.17), not the light.
- **The field's sheen is broad** — in the ON frame the field around the lane is noticeably live, not just the
  lane. It reads as a damp field at night to me; it is the first thing I would soften if Todd disagrees.
- **Nothing here says what a phone does** with a second full-size map pair per ground material.
- **M31's corner keeps one hard edge** (a linear strip cannot fade two adjacent sides); `m31-ground-corner.png`.
- **M28's silhouette test is OPEN, not passing** — the old page claimed otherwise and Ernie's order overrides
  it. Todd's model is the answer; the placeholder is the bar, not a claim. M34 owns this.
- Carried: `CornMaze/StarUnlit` does not resolve in the player build; locked docs still say "Crumb Beast" and
  FSD §17 still describes a noise ground and a sphere Husk (Ernie applies); the 60 fps floor is a phone target
  and unmeasured; §25.6's device listen pass still needs the phone.

## 4. Questions for Todd (each with my recommendation)

1. **Is the reflection too strong, right, or too weak?** Look at `m32-reflection-on.png` against
   `m32-reflection-off.png`. *Recommend: ship it — the highlight breaks into grain across the stones rather
   than reading as glass, and the field reads as damp rather than lacquered. If you want it softer, we change
   the maps' roughness and nothing else.*
2. **Next pass is M33 — scary lanterns.** *Recommend: yes, exactly as the order frames it: a procedural
   placeholder in the §17 register, placed at junctions and dead ends, with the swap-in hook named for the
   models you are sourcing. I will not wait for the models and will not import a third-party asset.*
3. **M34 (the scarecrow as THE antagonist) needs your model to close its silhouette test.** *Recommend: I
   build the factory hook and the escalation multiplier now, and leave the silhouette test marked open until
   the model lands — his licence line gets recorded before it ships.*

## 5. Blockers

None. `/tmp/corn-crew-blocked` not written; nothing in this list needed a human.
Working-tree items left alone as ordered: deleted `Assets/GingerbreadMan.meta` and the four
`Assets/Resources/PerformanceTestRun*.{json,meta}`; Ernie's ground-variant edits (`_R` maps, previews, the
new `T_Ground_Puddle*` art) left uncommitted as not mine. Unity re-serialised `ProjectSettings/*`,
`PC_RPAsset.asset` and `UniversalRenderPipelineGlobalSettings.asset` during builds.

## 6. CAPTURES FOR TODD

    artifacts/review/world/m32-reflection-on.png    AFTER  — wet gravel under the moon, highlight in grain
    artifacts/review/world/m32-reflection-off.png   BEFORE — the same lane flat and even. Compare these two.
    artifacts/review/world/m32-reflection-fieldonly.png    the field's map alone, lane unbound
    artifacts/review/world/m31-ground-edge.png      gravel dissolving into the field (M31)
    artifacts/review/world/m29-ground-edge.png      the plate with the faceted border it replaced
    artifacts/m32-reflection-report.txt             all M32 numbers + what I am not claiming
    artifacts/m31-lane-blend-report.txt             all M31 numbers
    artifacts/review/world/m28-scarecrow-silhouette.png   M28's silhouette — OPEN pending Todd's model

## 7. Last commits

`7062531` M32 the ground catches the moonlight · `8b2c368` status after M31 · `ea81ad8` M31 lane blends by
transparency · `4131fa7` order M31-M34 · `a890aff` roughness maps + ground variants (Ernie) · `cfe164e` lane
alpha strip (Ernie) · `34dbf49` M28 the scarecrow · `e460272` M27 first person · `f46b9e0` M29 the ground.
