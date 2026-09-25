# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `ea81ad8` (M31). This file is the current page, not a log._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS, files1). Unity 6000.3.23f1._
_Order of 2026-09-25 (cap 10 passes): **M31, M32, M33, M34**. One writer at a time; Unity slot free at STATE-A._

## 1. The new order, by number

| # | Milestone | State |
|---|---|---|
| 1 | M31 — the lane blends by **transparency**, not geometry | **GREEN** — `ea81ad8` |
| 2 | M32 — the ground catches the moonlight | not started; `_M` maps are in the repo, unwired |
| 3 | M33 — scary lanterns, placeholder + swap-in point | not started |
| 4 | M34 — the scarecrow is THE antagonist | not started; silhouette test **open, not passing** |

Previous order, all green: M29 `f46b9e0`, M27 `e460272`, M28 `34dbf49`. Earlier queue untouched:
M20 `7a6a93f`, M21 `ca83b66`, M22 `7518c78`, M23 `318b543`, M24 `4d2b253`, M25 `874d90f`,
M25b `dc3b53a`, M26 `aa0f23a`. **The done marker still stands from the previous order; it is stale for
this one and gets rewritten only when M31–M34 are all closed.**

## 2. M31 — what the lane is now

M29 made the lane's margin ragged in **geometry**: boundary vertices pulled inwards on a derived mask.
Todd's verdict was "bubbly and janky", and M29's own report named the cause — *"Blend method: geometry."*
A margin that wanders 0.42 m is still a cut, just an irregular one.

The lane is now a plain rectangle (a four-vertex quad, down from a 625-vertex grid) laid over the field,
with lane-local UVs — **U along the lane, V across it**. The whole boundary is the alpha strip:
`scripts/m31_lane_alpha_bake.py` bakes `T_Ground_LaneAlpha` into the lane albedo's alpha at design time,
so the material stays stock URP/Lit set to Alpha Blend and the lane keeps the moon, its normal map and
the `PathMudWetness` path. Measured off the texture: **half-width 0.84 m, plateau 1.20 m of the 2.08 m
lane, alpha 0.000 exactly at the mesh's edge** — which is why there is no line where the layer stops.

Two more fixes fell out of the frames: lane pieces now **abut** instead of overlapping (M29 ran
connectors centre to centre, so every connector sat on two core pieces — two alpha layers read as a
brighter plate with a straight edge, which my own night frame caught), and a cell's core piece now fades
the axis whose edge **actually faces corn** instead of whichever axis happened to be longer. Coverage
counted over 2151 probe points: **max 1 piece per point, 0 doubled, 0 holes.** Baking also deletes the
CPU-readable mask copy M29 carried — the phone cost that milestone asked to remove.

## 3. Open — reported, not hidden

- **A corner keeps one hard edge.** A linear strip cannot fade two adjacent sides, and the alternative
  (two overlapping pieces) measured as the exact defect M31 removes. `m31-ground-corner.png` shows it.
- **Blend cost is unmeasured on the phone.** On the Mac, switching the live material between Alpha Blend
  and Opaque reads as nothing measurable — honest for a machine that is not fill-bound at 3 ms/frame.
- **A magenta test tint did not reach the drawn pixels** (0 of 1,066,000, checked numerically): the
  lane's runtime colour comes from somewhere other than that material's `_BaseColor`. Reported; the
  coverage count does not depend on it.
- **The bright quadrilateral in the night lane frame is pre-existing** — present and unchanged in M29's
  committed frame of the same view. Not a regression, not the lane's boundary, unidentified.
- **M28's silhouette test is OPEN, not passing.** The old page said it passed; Ernie's order overrides
  that and I am not re-litigating it. M28's own frame collapses to a blocky body with two stubs. Todd's
  model is the answer; the placeholder is the bar, not a claim. M34 owns this.
- Carried: `CornMaze/StarUnlit` does not resolve in the player build (star twinkle inert); locked docs
  still say "Crumb Beast" and FSD §17 still describes a noise ground and a sphere Husk — Ernie applies.
  The 60 fps floor is a phone target and stays unmeasured. §25.6 device listen pass still needs the phone.

## 4. Questions for Todd (each with my recommendation)

1. **The fade is wide — 0.84 m half-width of a 2.08 m lane.** Does the lane still read as *worn underfoot*
   or as *suggested*? *Recommend: ship it and look at `m31-ground-edge.png`; if it wants more gravel the
   lever is the strip's plateau, not the geometry.*
2. **The lane is 87 % opaque at its centre** — the field reads through it. *Recommend: keep; that is what
   killed the plate look.*
3. **Next pass is M32.** *Recommend: yes — the `_M` maps are already committed and the lane is the most
   reflective surface in the game; cheapest visible win left.*

## 5. Blockers

None. `/tmp/corn-crew-blocked` not written; nothing in this list needed a human.
Working-tree items left alone as ordered: deleted `Assets/GingerbreadMan.meta` and the four
`Assets/Resources/PerformanceTestRun*.{json,meta}`. Unity re-serialised `ProjectSettings/*`,
`PC_RPAsset.asset` and `UniversalRenderPipelineGlobalSettings.asset` during builds.

## 6. CAPTURES FOR TODD

    artifacts/review/world/m31-ground-edge.png      AFTER  — gravel dissolving into the field. Look here.
    artifacts/review/world/m29-ground-edge.png      BEFORE — the plate with the faceted border (M29 commit)
    artifacts/review/world/m31-ground-corner.png    where the fade has to give up something
    artifacts/review/world/m29-ground-lane.png      the lane at night, re-shot (M29's view)
    artifacts/review/world/m29-ground-field.png     the field floor, re-shot
    artifacts/m31-lane-blend-report.txt             all M31 numbers + what I am not claiming
    artifacts/review/world/m28-scarecrow-silhouette.png   M28's silhouette — OPEN pending Todd's model
    artifacts/m28-scarecrow-report.txt              every M28 number, the M23 regression, the verdict

## 7. Last commits

`ea81ad8` M31 lane blends by transparency · `4131fa7` order M31-M34 · `a890aff` roughness maps +
ground variants (Ernie) · `cfe164e` lane alpha strip (Ernie) · `34dbf49` M28 the scarecrow ·
`e460272` M27 first person · `f46b9e0` M29 the ground · `aa0f23a` M26 threat audio.
