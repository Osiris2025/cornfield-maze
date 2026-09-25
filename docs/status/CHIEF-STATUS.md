# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `4bb5e37` (M32c, OPEN). One page, not a log._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS volume files1). Unity 6000.3.23f1, URP._
_Order of 2026-09-25 (cap 4 passes): **M32c only** — then STOP; the scarecrow and the lantern wait on Todd's
models, which are coming. Mark: `/tmp/corn-crew-done.20260925-m32c` (ABSENT — M32c is not finished). Unity slot free._

## 1. The order, and where it stands

| # | Item | State |
|---|---|---|
| — | M32 — the ground is matte, by read-back | GREEN `a2e1119` |
| — | M31b — the lane edge is not a cut | GREEN `992da42` |
| — | M32b — puddles exist, placed, reflective by read-back | GREEN `9085719` |
| 1 | **M32c — the puddle reads as WATER and the lane stops glowing** | **OPEN `4bb5e37`** — probe built, frames at the right angle, mechanism not yet delivering |
| — | M33 lanterns · M34 scarecrow | **PARKED** — built on Todd's models; nothing started, nothing waiting on us |

## 2. M32c — what was built this pass

**A sky reflection probe, because there was none in the project.** `ReflectionProbes.cs`, created by the world
builder after the ground and the puddles: realtime, refreshed through scripting (the sky barely moves), 128 px,
box-projected, 130 × 114 m over the maze, refreshed by the harness once the sky is at night. Environment
reflections are now set **explicitly** ON for every ground material (`_ENVIRONMENTREFLECTIONS_OFF` cleared,
`_EnvironmentReflections` 1, specular highlights 1) instead of assumed, and read back. One test hook added,
`FarmWalkerController.SetEyeHeightForTest`, so the acceptance pair can be shot with the eye at 1.15 m.

**The acceptance pair, same camera, one variable** — `artifacts/review/world/m32c-water-probe-on.png` and
`m32c-water-probe-off.png`, shot from maze cell (1,1), eye 1.15 m, puddle 3.93 m ahead, aimed 22 m down the
lane at a near-level pitch (the flattest view there is, which is the angle a Fresnel reflection needs):

    frame                     water mean G   lane mean G   water/lane   band mean   peak
    m32c-water-probe-on.png      91.68          29.02         3.16x        95.01      200
    m32c-water-probe-off.png     91.78          28.97         3.17x        95.21      200

**And switching the probe off changes the water by 0.10 of 255.** It is present, enabled, bound and
refreshed — and it contributes nothing measurable (the A/B also set `_ENVIRONMENTREFLECTIONS_OFF` on the
water). So the water does **not** carry a reflection of the sky today and I am not claiming it does; no
emissive patch was faked on the water to hide it. The water's pale blue brightness comes from something else,
and the two measurable candidates for the next pass are the moon light's specular on the near-flat water
normal and the broad lift that binding the M32 maps produces (§3).

**Puddles:** four per level instead of six, **4.4 × 1.8 m** instead of 3.4 × 1.7, long side along the lane; in
the new frame they read as pale blue-grey elongated patches lying in the lane's rut, not near-circular painted
spots. Also fixed a real bug in the last pass's alignment test — it summed a straight-run cell's two opposite
neighbours, which cancel exactly, so it reported |dot| 0.00 for every puddle while claiming to measure
alignment. It now takes one neighbour.

**The lane, measured** (lane and field classified by the maze's own answer per pixel, water excluded):

    m32c-water-probe-on.png    lane 31.49   field 27.14   1.16x     (order's ceiling 1.15x)
    m32b-puddle-moon.png       lane 47.98   field 54.04   0.89x     (lane already darker)

**The lane's brightness is not its albedo.** I scaled the lane's tint by 0.90 and the read-back proves it
reached the shader — `lane _BaseColor RGBA(0.846, 0.828, 0.792, 1.000)` — and the frame moved 48.22 → 47.98,
half a percent for a ten percent albedo cut. That is the finding worth keeping: the pale ribbon cannot be
tuned away by tinting, so the next pass measures what does drive it instead of turning that knob harder. The
0.90 scale stays — verified, harmless, in the right direction.

**One honest sentence: does the puddle read as water? Partly, and not yet.** It reads as a flat pale patch in
the rut with a bluish cast and measures 3.16× the lane, so the water is unmistakably the brightest surface
there — the Fresnel behaviour the order describes. It carries no visible reflection of the sky or moon, and the
probe meant to put one there moves the frame by 0.1 of 255. It needs the true source of its brightness found.

## 3. Open — reported, not hidden

- **The M32 map lift is still unexplained and is now the prime suspect for the water's brightness too:**
  binding the ground's `_M` maps raises the whole frame 33.68 → 49.45 of 255 with the peak unchanged — broad,
  not localised. It is measurable and it has never been explained to the last decimal.
- **The `_M` maps are Read/Write** (ground + puddle) so the read-backs can sample them: CPU copies a phone
  should not keep. One line to remove once M32c is green; the values do not change.
- **Harness lesson worth keeping:** a Unity build failure presents as "the harness never writes a report" —
  the app that runs is the previous build. **Check `error CS` in `Builds/mac-build.log` before blaming the
  harness.** Two of this order's build failures were my own compile errors (`ReflectionProbe.timeSlices` does
  not exist in Unity 6; a local named `step` collided with an enclosing scope).
- **A copy is not a rename:** a copied harness that keeps its installer's component name runs the wrong
  harness and writes the wrong report — it cost two runs. Rename the class, the installer and the flag.
- Carried: `CornMaze/StarUnlit` does not resolve in the player build; locked docs still say "Crumb Beast" and
  FSD §17 still describes a noise ground and a sphere Husk (**Ernie applies** — Harrow may not edit locked
  docs); the 60 fps floor is a phone target and unmeasured; §25.6's device listen pass needs the phone.

## 4. CAPTURES FOR TODD

    artifacts/review/world/m32c-water-probe-on.png      M32c: the acceptance frame — eye 1.15 m, water 3.16x lane
    artifacts/review/world/m32c-water-probe-off.png     M32c: the same camera, probe off — 0.1/255 apart
    artifacts/review/world/m32b-puddle-moon.png         the mirror-angle frame; lane 0.89x field here
    artifacts/review/world/m31-ground-lane.png          M31b: down the lane at eye level — solid, not a ghost
    artifacts/m32c-puddle-report.txt                    M32c: what was built, every number, the honest sentence
    artifacts/reference/ground-preview-dry.png          the dry ground variant, unchanged, not switched in

## 5. For Todd — parked, and one question

1. **PARKED (needs your models, nothing else blocks it): M33 lanterns and M34 the scarecrow.** When a model
   arrives it needs a recorded licence line before it ships, and if its terms do not permit distribution
   inside an App Store build, that has to be known *before* it is wired. *Recommend: send them whenever; the
   build already has the placeholder lanes and the silhouette test is marked open, not passing.*
2. **The puddles as water.** Today they read as pale flat patches in the rut; the probe that should give them
   the sky changes nothing measurable, so the next pass finds the real source of their brightness rather than
   tuning smoothness again. *Recommend: let me spend pass 2 on that — I would rather bring you a frame that
   shows water than another round of numbers.*
3. **The dry ground variant** (`14d7ed6`, preview `artifacts/reference/ground-preview-dry.png`): withered-grass
   field, gravel-road lane, 2K. *Recommend: keep the current sets.* Taste, so the frame is there, not switched in.

## 6. Standing rules in force

`scripts/shoot.sh` is the only launcher: one launch per run, window shrunk into a corner and minimised, app
killed the moment a **fresh** report lands. No window is left on Todd's screen — this pass shot eight frames
that way and took no window.

**Markers:** `/tmp/corn-crew-done.20260925-m32c` ABSENT (correct — M32c is open). `/tmp/corn-crew-blocked`
ABSENT — nothing needs a human to continue.

**Working tree left alone as ordered:** the two pre-existing M0 items; Ernie's ground art edits uncommitted as
not mine. Unity re-serialised `ProjectSettings/*` and the RP assets during builds.

## 7. Last commits

`4bb5e37` M32c reflection probe + the acceptance pair · `02e1d1d`/`b5df115` status after M32b pass 4 ·
`ce0eae8` M32b pass 4 · `b1c36ca` M32b pass 3 · `0e3bc05` M32b pass 2 · `9085719` M32b puddles · `992da42`
M31b solid lane · `a2e1119` M32 matte ground · `ea81ad8` M31 lane blends by transparency · `09a59c3` matte
ground + puddle art (Ernie) · `34dbf49` M28 the scarecrow · `e460272` M27 first person · `f46b9e0` M29 ground.