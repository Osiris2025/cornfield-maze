# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `992da42` (M31b). One page, not a log._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS volume files1). Unity 6000.3.23f1, URP._
_Order of 2026-09-25 (cap 10 passes): **M31, M32, M33, M34** — with M31b and M32b attached. Unity slot free._

## 1. The order, by number

| # | Milestone | State |
|---|---|---|
| 1 | M31 — the lane blends by **transparency** | GREEN `ea81ad8`; **M31b now also GREEN `992da42`** |
| 2 | M32 — the ground and the moonlight | GREEN `a2e1119` — matte by read-back; sheen is the puddles' job |
| 3 | M33 — scary lanterns, placeholder + swap-in point | **next pass** |
| 4 | M34 — the scarecrow is THE antagonist | not started; silhouette test **OPEN, not passing** |

M32b (puddles, §3c) sits inside the M32 block and is **not started** — it is the only open sub-item left before
M33. Previous order, all green: M29 `f46b9e0`, M27 `e460272`, M28 `34dbf49`. Earlier queue untouched: M20
`7a6a93f`, M21 `ca83b66`, M22 `7518c78`, M23 `318b543`, M24 `4d2b253`, M25 `874d90f`, M25b `dc3b53a`, M26 `aa0f23a`.

## 2. M31b — the blend was a haze; it is ground eating into ground now

Ernie read M31's own numbers back and was right: 0.867 alpha at the centre line, a full-strength band of only
1.20 m of a 2.08 m lane. The M31 bake resampled the strip's profile straight into the lane's alpha, so the lane
inherited a wide ramp. That is haze, not a join.

M31b keeps the strip and stops obeying it — **measured off the result, not asserted**:

| item | M31 | M31b |
|---|---|---|
| interior | 0.867 at the centre line | **1.000**, 75.5 % of the lane fully opaque |
| transition | faded over 0.84 m from the centre | **0.167 m** (0.98 → 0.02 band; crease 0.122 m) |
| edge | smooth ramp | **displaced ±0.040 m** by the strip's own per-column grain (±0.071 m raw, clamped) |
| half-width | 0.84 m | **constant at 1.000 m** — irregular edge, no pulsing lane |
| mesh edge | alpha 0.000 | alpha 0.000, so the mesh edge is still not the cut |

The strip's grain does the work: its own 0.5-crossing wobbles along the lane, and that wobble is what pushes the
boundary in and out — fingers of field into lane and lane into field at texture scale. 477 pieces, all still
four-vertex quads, every UV check passing, coverage exact (0 doubled, 0 holes over 2151 probes), blend cost
**0.01 ms/frame** against forced-opaque on the Mac. Baked at design time, stock URP/Lit, and neither source
texture is Read/Write in the player.

## 3. Open — reported, not hidden

- **The `_M` maps are Read/Write** so the M32 read-back can sample them: 2 x 4 MB of CPU copy at 1024 a phone
  should not keep. One-line removal once green (`importer.isReadable = kind == SmoothnessMap`); the values do not
  change. Temporary and named, not shipped silently.
- **Flagged, unexplained:** binding the M32 maps lifts the ground's luminance overall (whole-frame mean 33.68 →
  49.45 of 255) while leaving the peak unchanged. Broad, not localised — not the sheen Todd rejected — but
  measured and in the report rather than explained away.
- **M31b's displacement is clamped to ±4 cm** where the strip's own wobble is ±7.1 cm. A deliberate cap: a lane
  whose edge travels 7 cm starts to read as a lane that changes width.
- **The plan frame is mostly corn canopy** — a coverage aid, not an acceptance frame. The acceptance pair is
  `m31-ground-edge.png` against M31's own edge frame, plus `m31-ground-lane.png` at eye level.
- **M31's corner keeps one hard edge** — a linear strip cannot fade two adjacent sides; `m31-ground-corner.png`.
- **M28's silhouette test is OPEN, not passing** — the placeholder reads as a blocky figure, not a scarecrow.
  Todd's model is the answer. M34 owns it.
- Carried: `CornMaze/StarUnlit` does not resolve in the player build; locked docs still say "Crumb Beast" and
  FSD §17 still describes a noise ground and a sphere Husk (**Ernie applies** — Harrow may not edit locked docs);
  the 60 fps floor is a phone target and unmeasured; §25.6's device listen pass still needs the phone.

## 4. CAPTURES FOR TODD

    artifacts/review/world/m31-ground-lane.png           NEW: down the lane at eye level — solid, not a ghost
    artifacts/review/world/m31-ground-edge.png           NEW: where the lane eats into the field
    artifacts/reference/m31b-lane-alpha-preview.png      NEW: the baked alpha itself — flat interior, eaten edge
    artifacts/review/world/m29-ground-edge.png           the M29 plate with the faceted border
    artifacts/m31b-lane-edge-report.txt                  the four items, measured + what I am not claiming
    artifacts/review/world/m32-reflection-on.png         M32: the matte ground as it ships
    artifacts/m32-reflection-report.txt                  M32: both read-backs and the frame diff
    artifacts/review/world/m28-scarecrow-silhouette.png  M28's silhouette — OPEN pending Todd's model

## 5. Questions for Todd (each with my recommendation)

1. **The dry ground variant** (`14d7ed6`, preview `artifacts/reference/ground-preview-dry.png`): withered-grass
   field, gravel-road lane, 2K. *Recommend: keep the current sets.* Taste, so the frame is there and I have not
   switched it in.
2. **M32's brightness lift** (§3) — if the field reads too live for you, say so and I pull the smoothness numbers
   down further. *Recommend: ship it — broad and dim, and the peak is gone.*
3. **M34 needs your scarecrow model to close its silhouette test.** *Recommend: build the factory hook and the
   escalation multiplier now, leave the test marked open until the model lands, and record its licence line
   before it ships.*

## 6. Standing rules in force

`scripts/shoot.sh` is the only launcher: one launch per run, window shrunk into a corner and minimised, app
killed the moment a **fresh** report lands. No window is left on Todd's screen.

**Markers:** `/tmp/corn-crew-done` ABSENT (correct — M32b, M33, M34 remain). `/tmp/corn-crew-blocked` ABSENT —
no human-only blocker.

**Working tree left alone as ordered:** the two pre-existing M0 items; Ernie's ground art edits (`_R` maps,
previews, the new `T_Ground_Puddle*` art) uncommitted as not mine. Unity re-serialised `ProjectSettings/*`,
`PC_RPAsset.asset` and `UniversalRenderPipelineGlobalSettings.asset` during builds.

## 7. Last commits

`992da42` M31b solid lane, broken edge · `70b0783` status after M32 · `a2e1119` M32 matte ground + the
alphaSource fix · `7062531` M32 v1 · `ea81ad8` M31 lane blends by transparency · `09a59c3` matte ground + puddle
art (Ernie) · `f655ffd` the mirror floor is an import bug (Ernie) · `850a8ac` order M31b · `34dbf49` M28 the
scarecrow · `e460272` M27 first person · `f46b9e0` M29 the ground.
