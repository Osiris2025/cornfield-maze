# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `9085719` (M32b, OPEN). One page, not a log._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS volume files1). Unity 6000.3.23f1, URP._
_Order of 2026-09-25 (cap 10 passes): **M31, M32, M33, M34** — with M31b and M32b attached. Unity slot free._

## 1. The order, by number

| # | Milestone | State |
|---|---|---|
| 1 | M31 — the lane blends by **transparency** | GREEN `ea81ad8`; M31b also GREEN `992da42` |
| 2 | M32 — the ground and the moonlight | GREEN `a2e1119` — matte by read-back |
| 2c | M32b — puddles, the only reflective surface | **OPEN `9085719`** — built, read-back green, acceptance FRAME not obtained |
| 3 | M33 — scary lanterns, placeholder + swap-in point | not started |
| 4 | M34 — the scarecrow is THE antagonist | not started; silhouette test **OPEN, not passing** |

Previous order, all green: M29 `f46b9e0`, M27 `e460272`, M28 `34dbf49`. Earlier queue untouched: M20 `7a6a93f`,
M21 `ca83b66`, M22 `7518c78`, M23 `318b543`, M24 `4d2b253`, M25 `874d90f`, M25b `dc3b53a`, M26 `aa0f23a`.

## 2. M32b — what is built, and exactly what is not

Todd's ruling put water in charge of the sheen, so the ground is matte (M32) and all reflectivity lives in six
puddle decals.

**Built and green:** `scripts/m32b_puddle_maps.py` derives the two maps the art could not supply —
`T_Ground_PuddleA` (RGB earth, **A = coverage**, because Alpha Blend reads the base map's alpha as opacity) and
`T_Ground_Puddle_M` (RGB metallic 0, **A = smoothness**: 0.88 water, 0.10 halo). `PuddleDecals.cs` places **6
decals on straight-run cells** out of 180 candidates, stride 30, 2.6 × 1.5 m, rotated into the lane's axis, the
short side inside the 2.08 m lane so a puddle cannot reach the corn, all under **one parent named "Puddles"**.
Sampled out of the imported textures in the built app: **puddle smoothness max 0.902, mean 0.876 in the water
(7.6 % of the quad), 92.1 % below 0.2 → WATER REFLECTS; lane 0.137 → MATTE.** Both halves of the sampled
acceptance pass.

**Not green — the frame.** The acceptance is "a night frame with the moon caught in a puddle while the lane
round it stays matte". The shot stands 2.58 m up-lane from the measured puddle and aims at the solved mirror
point, which lands inside the decal, but the water reads as a slightly darker patch on an already dark lane and
I could not point at the moon in it. **The band's 255 peak is not evidence** and is not being called one.
Next pass, in order: raise the wet-earth/dry-lane contrast (or widen the reflective area); verify the aim by
projecting the **puddle's bounds** into the frame, not just the mirror point; then re-shoot.

## 3. Open — reported, not hidden

- **M32b's normal map reads as "none" in the built app** while the albedo and gloss map resolve. It does not
  affect the smoothness result, but a flat water plane without its normal is not what was designed.
- **A copy is not a rename.** The M32b harness was copied from M32's; I renamed the class but not the component
  its installer created, so `-puddletest` ran the M32 harness and quietly wrote the M32 report — two runs lost,
  presenting as "the harness never writes a report". Worth remembering for every future harness copy.
- **The `_M` maps are Read/Write** (ground + puddle) so the read-backs can sample them: CPU copies a phone should
  not keep. One-line removal once green; the values do not change.
- **Flagged, unexplained:** binding the M32 maps lifts the ground's luminance overall (33.68 → 49.45 of 255)
  while leaving the peak unchanged. Broad, not localised — measured and in the report, not explained away.
- **M31b's edge displacement is clamped to ±4 cm** where the strip's own wobble is ±7.1 cm — deliberate, so the
  lane does not read as one that changes width.
- **M28's silhouette test is OPEN, not passing.** The placeholder reads as a blocky figure, not a scarecrow.
  Todd's model is the answer. M34 owns it.
- Carried: `CornMaze/StarUnlit` does not resolve in the player build; locked docs still say "Crumb Beast" and
  FSD §17 still describes a noise ground and a sphere Husk (**Ernie applies**); the 60 fps floor is a phone
  target and unmeasured; §25.6's device listen pass still needs the phone.

## 4. CAPTURES FOR TODD

    artifacts/review/world/m32b-puddle-moon.png          M32b: the attempt — the puddle is NOT legible in it
    artifacts/review/world/m31-ground-lane.png           M31b: down the lane at eye level — solid, not a ghost
    artifacts/reference/m31b-lane-alpha-preview.png      M31b: the baked alpha — flat interior, eaten edge
    artifacts/review/world/m32-reflection-on.png         M32: the matte ground as it ships
    artifacts/m32b-puddle-report.txt                     M32b: what is built, what is proven, what is not
    artifacts/m31b-lane-edge-report.txt                  M31b: the four items, measured
    artifacts/m28-scarecrow-silhouette.png               M28's silhouette — OPEN pending Todd's model

## 5. Questions for Todd (each with my recommendation)

1. **Puddles: the water is nearly invisible at night.** The wet earth (mean 0.114) sits close to the lane's
   value once the moon is the only light. *Recommend: I raise the contrast between wet earth and dry lane next
   pass and re-shoot — the read-back already proves the water is reflective, so this is a look problem, not a
   physics one.*
2. **The dry ground variant** (`14d7ed6`, preview `artifacts/reference/ground-preview-dry.png`): withered-grass
   field, gravel-road lane, 2K. *Recommend: keep the current sets.* Taste, so the frame is there and I have not
   switched it in.
3. **M34 needs your scarecrow model to close its silhouette test.** *Recommend: build the factory hook and the
   escalation multiplier now, leave the test marked open until the model lands, and record its licence line
   before it ships.*

## 6. Standing rules in force

`scripts/shoot.sh` is the only launcher: one launch per run, window shrunk into a corner and minimised, app
killed the moment a **fresh** report lands. No window is left on Todd's screen.

**Markers:** `/tmp/corn-crew-done` ABSENT (correct — M32b, M33, M34 remain). `/tmp/corn-crew-blocked` ABSENT —
no human-only blocker.

**Working tree left alone as ordered:** the two pre-existing M0 items; Ernie's ground art edits (`_R` maps,
previews) uncommitted as not mine. Unity re-serialised `ProjectSettings/*`, `PC_RPAsset.asset` and
`UniversalRenderPipelineGlobalSettings.asset` during builds.

## 7. Last commits

`9085719` M32b puddles built, frame open · `94c2fc1` status after M31b · `992da42` M31b solid lane, broken edge ·
`a2e1119` M32 matte ground + the alphaSource fix · `ea81ad8` M31 lane blends by transparency · `09a59c3` matte
ground + puddle art (Ernie) · `f655ffd` the mirror floor is an import bug (Ernie) · `34dbf49` M28 the scarecrow ·
`e460272` M27 first person · `f46b9e0` M29 the ground.
