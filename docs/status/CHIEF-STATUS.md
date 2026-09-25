# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `3aa060a` (M32c pass 2, OPEN). One page, not a log._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS volume files1). Unity 6000.3.23f1, URP._
_Order of 2026-09-25 (cap 4 passes): **M32c only** — then STOP; the scarecrow and the lantern wait on Todd's
models, which are coming. Mark: `/tmp/corn-crew-done.20260925-m32c` (ABSENT — M32c is not finished). Unity slot free._

## 1. The order, and where it stands

| # | Item | State |
|---|---|---|
| — | M32 matte ground · M31b lane edge · M32b puddles placed | GREEN `a2e1119` · `992da42` · `9085719` |
| 1 | **M32c — the puddle reads as WATER and the lane stops glowing** | **OPEN `3aa060a`** — 2 of 4 passes spent; the measurement was lying and is now fixed, the probe route is dead |
| — | M33 lanterns · M34 scarecrow | **PARKED** — built on Todd's models; nothing started, nothing waiting on us |

## 2. M32c pass 2 — the instrument was lying, and the probe route is dead

**Every paired number this milestone produced, including pass 1's headline, was two different cameras.**
`Sample()` re-aimed the player but never re-established its POSITION, and the walker walks between samples, so
"same camera, one variable" was metres apart — while the printed drift said 0.00 m, because it compares the
camera to the player and never the player to the stand. Proof (`scripts/m32c_frame_delta.py`, new):

    probe-on vs probe-off BEFORE:  mean abs diff 9.658 of 255, 27.94 % of pixels differing by >8
    probe-on vs probe-off AFTER:   mean abs diff 0.276 of 255,  0.01 % of pixels differing by >8

Fixed: `Time.timeScale = 0` per sample (the walker integrates on deltaTime), the stand re-asserted twice
around the re-aim, and **every frame line now carries its camera POSE**, so non-comparable frames cannot be
quoted as a comparison again.

**The probe route is dead** — a valid negative from a frozen camera: probe ON vs OFF differ by **0.276 of 255
mean and 0.01 % of pixels** (band mean 95.59 vs 95.60, peak 202 vs 202). The sky reflection probe is in the
scene, enabled, bound and refreshed at night and it moves the picture by a quarter of one level. The water
does not carry the sky that way, no emissive patch was faked to hide it, and M32c is not green.

**The ablation voids four passes of water-vs-lane numbers, including mine.** Four states, one variable each,
same frozen camera:

    state                                   water region mean G   lane mean G
    shipped (probe on)                            92.01               30.69
    water _SpecularHighlights OFF                 92.44               36.40
    water gloss map unbound, _Smoothness 0        89.83               36.40
    water _BaseColor BLACK                        92.42               36.40
    MoonLight intensity 0                         92.44               36.40

The "water" region sits at ~92 of 255 with the water's albedo **black**, its gloss and specular **removed** and
the moon light **extinguished**. No lit surface behaves that way — those pixels are not the puddle's. The
geometry classifier (corners in pass 4, camera rays in pass 1) was never measuring the water, which is why five
passes of numbers would not reconcile. Earlier water-vs-lane figures in this milestone are **void**, left void
rather than quietly dropped.

**The measurement now asks the renderer:** `WaterFootprint()` paints the decal with a marker (keeping its
coverage alpha), re-shoots the frozen camera and takes the pixels that changed — that set IS the decal.

    water 36,208 sampled px, mean G 74.50 of 255, bright(>140) 1,466 (4.0 %)
    lane 42.43 over 18,450 px, field 22.11 over 5,176 px  ->  water/lane 1.76x, lane/field 1.92x

Caveat stated, not hidden: the marker also flattens the normal map, so this set includes the damp halo, not
only the water core (7.6 % of the quad by the baked maps). Narrowing the marker to an albedo-only change is
pass 3's first job; the core's own mean will be lower than 74.50.

**Lane vs field, both frames frozen:** shallow 42.43 / 22.11 = **1.92x** (ceiling 1.15x, field sample thin at
5,176 px); mirror-angle 50.67 / 74.22 = **0.68x**. The lane is not the 2-3x pale ribbon it was, but it is still
the brightest thing on the ground in the shallow view. Not called fixed. The 0.90 lane tint stays (read back in
the material as `_BaseColor RGBA(0.846, 0.828, 0.792, 1.000)`); on the fixed instrument it is worth 0.5 %, which
is the honest reason not to expect more from tinting.

**Scene changes:** four puddles per level instead of six, 4.4 × 1.8 m instead of 3.4 × 1.7, long side along the
lane. And a real bug fixed in the old alignment test — it summed a straight-run cell's two opposite neighbours,
which cancel exactly, so it reported |dot| 0.00 for every puddle while claiming to measure alignment.

**One honest sentence: does the puddle read as water? Partly, and better than last pass.** In
`m32c-water-probe-on.png` the puddles are pale blue-grey patches lying in the lane's rut with a wet, glossy
look — unmistakably water, not painted spots — at 1.76× the lane. They carry **no** reflection of the sky or
moon: the moon is high in frame and there is no disc and no sky in the water. The frame also shows a fine blue
speckle across the lane around the puddles that reads as glitter noise, not water.

## 3. Open — reported, not hidden

- **What makes the water bright is still unknown.** The ablation that would have named it is void (wrong pixel
  set), so pass 3 re-runs it against the marker-derived set. Not a guess, a measurement.
- **The blue speckle on the lane** around the puddles reads as noise; unmeasured, named so it is not lost.
- **The M32 map lift is still unexplained:** binding the ground's `_M` maps raises the whole frame 33.68 →
  49.45 of 255 with the peak unchanged — broad, not localised, never explained.
- **The `_M` maps are Read/Write** (ground + puddle) for the read-backs: CPU copies a phone should not keep.
  One line to remove once M32c is green; the values do not change.
- **Harness lessons that cost runs:** a build failure presents as "the harness never writes a report" (the app
  that runs is the previous build) — check `error CS` in `Builds/mac-build.log` first; a copied harness that
  keeps its installer's component name runs the wrong harness ("a copy is not a rename"); and a paired A/B is
  worthless until the camera's pose is proven identical between the two frames.
- Carried: `CornMaze/StarUnlit` does not resolve in the player build; locked docs still say "Crumb Beast" and
  FSD §17 still describes a noise ground and a sphere Husk (**Ernie applies** — Harrow may not edit locked
  docs); the 60 fps floor is a phone target and unmeasured; §25.6's device listen pass needs the phone.

## 4. CAPTURES FOR TODD

    artifacts/review/world/m32c-water-probe-on.png      the acceptance frame — the puddles as water, water/lane 1.76x
    artifacts/review/world/m32c-water-probe-off.png     same frozen camera, probe off — 0.28/255 apart
    artifacts/review/world/m32c-water-blackalbedo.png   the ablation: water is still ~92 with a BLACK albedo
    artifacts/review/world/m32b-puddle-moon.png         the mirror-angle frame; lane 0.68x field here
    artifacts/m32c-puddle-report.txt                    every number, the void numbers labelled void, the honest sentence
    artifacts/reference/ground-preview-dry.png          the dry ground variant, unchanged, not switched in

## 5. For Todd — parked, and one question

1. **PARKED (needs your models, nothing else blocks it): M33 lanterns and M34 the scarecrow.** A model needs a
   recorded licence line before it ships, and if its terms do not permit distribution inside an App Store
   build that has to be known *before* it is wired. *Recommend: send them whenever; the placeholders stand.*
2. **The puddles as water.** Two passes in, the honest position: pass 1's evidence was not sound (different
   cameras) and pass 2 spent itself finding that out and replacing the measurement, which is now trustworthy.
   Pass 3 uses it to find what actually makes the water bright. *Recommend: let me spend pass 3 on that rather
   than bring you another round of numbers from an instrument I no longer trust.*
3. **The dry ground variant** (`14d7ed6`, preview `artifacts/reference/ground-preview-dry.png`): withered-grass
   field, gravel-road lane, 2K. *Recommend: keep the current sets.* Taste, so the frame is there, not switched in.

## 6. Standing rules in force

`scripts/shoot.sh` is the only launcher: one launch per run, window shrunk into a corner and minimised, app
killed the moment a **fresh** report lands. No window is left on Todd's screen — this pass shot seven frames
that way, in two runs, and took no window.

**Markers:** `/tmp/corn-crew-done.20260925-m32c` ABSENT (correct — M32c is open). `/tmp/corn-crew-blocked`
ABSENT — nothing needs a human to continue.

**Working tree left alone as ordered:** the two pre-existing M0 items; Ernie's ground art uncommitted as not
mine. Unity re-serialised `ProjectSettings/*` and the RP assets during builds.

## 7. Last commits

`3aa060a` M32c pass 2 (instrument + ablation + marker method) · `4bb5e37` M32c pass 1 (probe, later shown to be
measured on two cameras) · `02e1d1d`/`b5df115` status after M32b pass 4 · `ce0eae8` M32b pass 4 · `b1c36ca`
M32b pass 3 · `0e3bc05` M32b pass 2 · `9085719` M32b puddles · `992da42` M31b solid lane · `a2e1119` M32 matte
ground · `ea81ad8` M31 lane blends by transparency · `09a59c3` matte ground + puddle art (Ernie) · `34dbf49`
M28 the scarecrow · `e460272` M27 first person · `f46b9e0` M29 ground.