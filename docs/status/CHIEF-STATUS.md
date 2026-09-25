# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `b1c36ca` (M32b pass 3, still OPEN). One page, not a log._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS volume files1). Unity 6000.3.23f1, URP._
_Order of 2026-09-25 (cap 10 passes): **M31, M32, M33, M34** — with M31b and M32b attached. Unity slot free._

## 1. The order, by number

| # | Milestone | State |
|---|---|---|
| 1 | M31 — the lane blends by **transparency** | GREEN `ea81ad8`; M31b also GREEN `992da42` |
| 2 | M32 — the ground and the moonlight | GREEN `a2e1119` — matte by read-back |
| 2c | M32b — puddles, the only reflective surface | **OPEN `b1c36ca`** — read-back green, camera now on the lane, moon not yet caught in the water |
| 3 | M33 — scary lanterns, placeholder + swap-in point | not started |
| 4 | M34 — the scarecrow is THE antagonist | not started; silhouette test **OPEN, not passing** |

Previous order, all green: M29 `f46b9e0`, M27 `e460272`, M28 `34dbf49`. Earlier queue untouched: M20 `7a6a93f`,
M21 `ca83b66`, M22 `7518c78`, M23 `318b543`, M24 `4d2b253`, M25 `874d90f`, M25b `dc3b53a`, M26 `aa0f23a`.

## 2. M32b — pass 2: a real bug fixed, the frame still not obtained

**Fixed this pass — a defect pass 1 shipped:** `PuddleDecals` rotated each decal "east-west lane → 90°", but
the mesh puts its LONG side on local X — so every east-west puddle lay **across** the lane: 3.4 m of water
across a 2.08 m lane, running into the corn on both sides. Now east-west gets 0 and north-south 90.

**Also corrected:** pass 1's "missing normal map" was my read-back asking for `_NormalMap` when URP/Lit's
property is `_BumpMap`. The map was never missing. The built app reads `baseMap=T_Ground_PuddleA
normalMap=T_Ground_Puddle_N metallicGlossMap=T_Ground_Puddle_M smoothnessChannel=0 _Smoothness=1.00
keyword _METALLICSPECGLOSSMAP=True` — nothing is unbound.

**Also changed:** the mirror distance is derived from the live moon (3.12 m at 28° and an eye height of
1.655 m) instead of a guessed 2.5 m; every frame now projects the decal's own quad and counts bright pixels
inside the waterline and outside it.

**Still verified by read-back (unchanged):** water smoothness mean **0.875** (max 0.902) in the water,
**0.104** in the damp halo; lane **0.137**; water 7.6 % of the quad, halo 21.1 %; **6 decals under one parent
named "Puddles"**, 3.4 × 1.7 m, inside the 2.08 m lane.

**Still open — the frame.** Pass 3 fixed the blocking defect: the harness no longer guesses where the lane
is, it asks the maze. It starts at the puddle's cell, steps while the next cell is a path cell, and takes the
lane cell closest to the mirror distance — so the stand is on the lane **by construction**. The built app
reports `stand: (36.00, 0.08, 44.00) — on maze cell (9,11), 1 step from the puddle's cell (9,10)`, and the
frame finally looks down a gravel lane with corn either side instead of from inside a corn block. One cell is
4.00 m, so the nearest lane cell to the 3.12 m mirror distance is 4.00 m; the reflection lands 0.88 m short
of the puddle's centre, still inside a 3.4 m decal. **The moon is still not caught in the water** and the
puddle is not distinguishable in the frame, so the milestone stays open. **Next, in order:** sample the
puddle's own pixels against the lane's in the same frame at the same distance — if the water is not
measurably different from the lane beside it, the decal is not reading and the fault is the decal, not the
light; and rewrite the projected-rect counter (it still reports "3 corners rejected" and a degenerate rect
for a decal 4 m away, so its inside/outside split is not quoted as evidence).

## 3. Open — reported, not hidden

- **M32b's band numbers, for the record:** with the reflective maps bound the band's mean G is 40.74 of 255,
  peak 255; unbound it is 22.57, peak 120. That 1.8× is the **ground's** own highlight, not the puddle's —
  the lane is what Todd called a bug — and it is why the puddle's contribution must be isolated before
  anything is claimed. **The puddle is not visible in the current frame and I am not claiming it is.**
- **Two Unity build failures this pass, both mine and both cheap to avoid:** a local named `step` collided
  with an enclosing scope's `step` (CS0136) and `chosen` was used outside the block that declared it. Both
  presented as "the harness never writes a report", because the app that ran was the previous build. **Check
  for `error CS` in `Builds/mac-build.log` before blaming the harness** — the log says exactly what broke.
- **A copy is not a rename.** The M32b harness was copied from M32's; I renamed the class but not the
  component its installer created, so `-puddletest` ran the M32 harness and quietly wrote the M32 report —
  two runs lost, presenting as "the harness never writes a report". Worth remembering for every harness copy.
- **Unity crashed once mid-build** this pass (a Burst child-domain crash, no compile errors); the retry built
  clean. Recorded because a crash is not a failure of the code and a silent retry would hide it.
- **The `_M` maps are Read/Write** (ground + puddle) so the read-backs can sample them: CPU copies a phone
  should not keep. One-line removal once green; the values do not change.
- **Flagged, unexplained:** binding the M32 maps lifts the ground's luminance overall (33.68 → 49.45 of 255)
  while leaving the peak unchanged. Broad, not localised — measured and in the report, not explained away.
- **M31b's edge displacement is clamped to ±4 cm** where the strip's own wobble is ±7.1 cm — deliberate, so
  the lane does not read as one that changes width.
- **M28's silhouette test is OPEN, not passing.** The placeholder reads as a blocky figure, not a scarecrow.
  Todd's model is the answer. M34 owns it.
- Carried: `CornMaze/StarUnlit` does not resolve in the player build; locked docs still say "Crumb Beast" and
  FSD §17 still describes a noise ground and a sphere Husk (**Ernie applies**); the 60 fps floor is a phone
  target and unmeasured; §25.6's device listen pass still needs the phone.

## 4. CAPTURES FOR TODD

    artifacts/review/world/m32b-puddle-moon.png          M32b: pass 3 — down the lane now; the puddle is not in it
    artifacts/review/world/m31-ground-lane.png           M31b: down the lane at eye level — solid, not a ghost
    artifacts/reference/m31b-lane-alpha-preview.png      M31b: the baked alpha — flat interior, eaten edge
    artifacts/review/world/m32-reflection-on.png         M32: the matte ground as it ships
    artifacts/m32b-puddle-report-3.txt                   M32b pass 3: the stand fixed, the glint not there
    artifacts/m32b-puddle-report-2.txt                   M32b pass 2: the lane-crossing puddles
    artifacts/m32b-puddle-report.txt                     M32b pass 1: the read-back that does stand
    artifacts/m28-scarecrow-silhouette.png               M28's silhouette — OPEN pending Todd's model

## 5. Questions for Todd (each with my recommendation)

1. **Puddles: the water is nearly invisible at night.** The read-back proves the water is reflective (0.875),
   so this is a look problem, not a physics one. *Recommend: I finish the frame first — the harness is
   standing in the wrong place, and until it stands on the lane I cannot tell you whether the water reads from
   a normal view. If it still does not after that, I raise the wet-earth/dry-lane contrast.*
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

**Working tree left alone as ordered:** the two pre-existing M0 items; Ernie's ground art edits uncommitted as
not mine. Unity re-serialised `ProjectSettings/*`, `PC_RPAsset.asset` and
`UniversalRenderPipelineGlobalSettings.asset` during builds.

## 7. Last commits

`b1c36ca` M32b pass 3 · `6dc6c9a` status after M32b pass 2 · `0e3bc05` M32b pass 2 · `ea0d85c` status after
M32b pass 1 · `9085719` M32b puddles built, frame open · `94c2fc1` status after M31b · `992da42` M31b solid
lane, broken edge · `a2e1119` M32 matte ground + the alphaSource fix · `ea81ad8` M31 lane blends by
transparency · `09a59c3` matte ground + puddle art (Ernie) · `34dbf49` M28 the scarecrow · `e460272` M27 first
person · `f46b9e0` M29 the ground.
