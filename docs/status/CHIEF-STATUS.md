# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `3594fad` (M32c pass 3, OPEN). One page, not a log._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS volume files1). Unity 6000.3.23f1, URP._
_Order of 2026-09-25 (cap 4 passes): **M32c only** — then STOP; the scarecrow and the lantern wait on Todd's
models, which are coming. Mark: `/tmp/corn-crew-done.20260925-m32c` (ABSENT — M32c is not finished). Unity slot free._

## 1. The order, and where it stands

| # | Item | State |
|---|---|---|
| — | M32 matte ground · M31b lane edge · M32b puddles placed | GREEN `a2e1119` · `992da42` · `9085719` |
| 1 | **M32c — the puddle reads as WATER and the lane stops glowing** | **OPEN `3594fad`** — 3 of 4 passes spent. The measurement is finally sound; the probe is worth −0.01/255 on the water; the remaining defect is the near-lane grazing brightness |
| — | M33 lanterns · M34 scarecrow | **PARKED** — built on Todd's models; nothing started, nothing waiting on us |

## 2. M32c pass 3 — measured on the water's own pixels, and the defect moved

**Six captures in ONE frozen pose, and the masks come from the renderer.** After five passes of geometry
classifiers (corners, then camera rays — both proven wrong), the water's pixels and the lane's pixels are now
found by painting the surface, shooting again and keeping the pixels that changed. Water mask 54,407 px
(5.10 % of the frame — the four puddles combined); lane mask 10,907 px (1.02 %).

    WATER, on the water's own pixels: mean G 92.51 of 255, bright(>140) 3,882 (7.1 %)
    LANE,  on the lane's own pixels:  mean G 33.57
    FIELD, ray-reached ground neither mask claims: mean G 22.33
    ->  water/lane 2.76x,  lane/field 1.50x        (the order's ceiling is 1.15x)

**The probe, on the water's own pixels: −0.01 of 255.** probe ON 92.51, OFF 92.52; mean |change| across the
water 0.14 of 255; water px brighter than 140: 3,882 with it, 3,889 without. Pass 1 said this from two
different cameras (void), pass 2 from the wrong pixels (void); this one is valid and agrees. The probe is
bound, enabled, refreshed at night, and the water does not use it. No emissive patch was faked on.

**Ablations — one took, one is void, and the void one is a finding.**

    albedo black and gloss gone:  water 89.90 against 92.51  (-2.60 of 255)
    moon light extinguished:      water +0.00,  lane +0.00    -> VOID

The albedo ablation is real: **the water's own albedo and gloss are 3 % of what is visible in those pixels.**
The moon-light ablation is void for a reason in the code — `DuskSky.cs:380` rewrites `_moonLight.intensity`
every frame, so the harness's zero is overwritten before the capture; the report's "moon light at 0.12" is
DuskSky's value, not the harness's. Reported as void, not as physics.

**What the frame shows, and it moves the defect:** `m32c-water-mask.png` paints the puddles magenta and the
paint lands as ONE LARGE QUAD over the NEAR LANE, not four small puddles — the decal's screen footprint is
mostly its damp halo lying on the lane. Set against the numbers (near ground 92.51, lane beyond the decal
33.57, field 22.33) the near lane is nearly **3× the far lane**. The puddle is not bright because it is water:
the ground under it is bright because of where it is — close to the camera at a shallow angle — and the puddle
inherits that through its own transparency. That is Todd's "lane glows like a pale ribbon", measured.

So one suspect now covers both halves of this milestone: **whatever makes ground brightness climb steeply as
the view flattens.** Ruled out already — albedo (pass 1: a 10 % cut moved the frame 0.5 %), the probe
(0.14 of 255 on the water), the water's gloss (2.60 of 92.51); and the moon-light ablation is void. Named, not
guessed, and testable with the same six-capture harness: the lane's brightness at near/middle/far with its
normal map unbound and its smoothness zeroed.

**One honest sentence: does the puddle read as water? No.** It reads as a translucent pale patch on a pale
near lane; the water's own material is 3 % of what is visible and the sky is not in it.

**Pass-2 history, kept because it explains the void numbers:** every paired measurement before it was two
different cameras (the walker walked between samples while the drift figure compared the camera to the player,
not the player to the stand). `scripts/m32c_frame_delta.py` proves the fix: probe-on vs probe-off went from
27.94 % of pixels differing by >8 to 0.01 %. Any water-vs-lane figure from passes 1–2 is **void**, and is
labelled void rather than quietly dropped.

## 3. Open — reported, not hidden

- **The near-lane grazing brightness is the live suspect for both halves of M32c.** Next pass: lane mean at
  near/middle/far, then with its normal map unbound and its smoothness zeroed, in the same frozen pose.
- **DuskSky must be paused for light ablations to be real** (`DuskSky.cs:380` writes the moon light each frame).
- **The water mask still includes the damp halo** (the watermark: the marker nulls the normal map too). The
  water core is 7.6 % of the quad, so the core's own mean is lower than 92.51; narrowing the mask is worth one
  capture.
- **The blue speckle on the lane** around the puddles reads as glitter noise; named, unmeasured.
- **The M32 map lift:** binding the ground's `_M` maps raises the whole frame 33.68 → 49.45 of 255 with the peak
  unchanged — broad, not localised, never explained. Possibly the same term as the grazing brightness.
- **The `_M` maps are Read/Write** (ground + puddle) for the read-backs: CPU copies a phone should not keep.
  One line to remove once M32c is green; the values do not change.
- **Harness lessons that cost runs:** a build failure presents as "the harness never writes a report" (the app
  that runs is the previous build) — check `error CS` in `Builds/mac-build.log` first; a copied harness that
  keeps its installer's component name runs the wrong harness ("a copy is not a rename"); a paired A/B is
  worthless until the poses are proven identical; and an A/B measured over the whole frame cannot see a puddle
  (1–2 % of the picture) — a real change on the water reads as ~0.3 of 255 and looks like nothing.
- Carried: `CornMaze/StarUnlit` does not resolve in the player build; locked docs still say "Crumb Beast" and
  FSD §17 still describes a noise ground and a sphere Husk (**Ernie applies** — Harrow may not edit locked
  docs); the 60 fps floor is a phone target and unmeasured; §25.6's device listen pass needs the phone.

## 4. CAPTURES FOR TODD

    artifacts/review/world/m32c-water-mask.png     THE diagnostic: the puddles painted magenta — one big quad on the near lane
    artifacts/review/world/m32c-water-on.png       the shipped shallow view — near ground bright, water 2.76x the far lane
    artifacts/review/world/m32c-water-off.png      same frozen pose, probe off — the probe is worth -0.01/255 on the water
    artifacts/review/world/m32c-water-black.png    the ablation that took: water with a BLACK albedo and no gloss
    artifacts/review/world/m32c-lane-mask.png      the lane painted — its true footprint, for the lane/field number
    artifacts/m32b-puddle-moon.png                 the mirror-angle frame; lane 0.68x field here
    artifacts/m32c-puddle-report.txt               every number, the void ones labelled void, the honest sentence
    artifacts/reference/ground-preview-dry.png     the dry ground variant, unchanged, not switched in

## 5. For Todd — parked, and one question

1. **PARKED (needs your models, nothing else blocks it): M33 lanterns and M34 the scarecrow.** A model needs a
   recorded licence line before it ships, and if its terms do not permit distribution inside an App Store
   build that has to be known *before* it is wired. *Recommend: send them whenever; the placeholders stand.*
2. **The puddles, three passes in.** The honest position: passes 1–2 produced numbers from a broken instrument
   and those numbers are void; pass 3 replaced the instrument and measured correctly, and the result is that the
   water's own material is 3 % of what is visible — the near-lane brightness is what the puddle is wearing. One
   pass left in the order. *Recommend: let me spend it on that single term rather than another round of frames.*
3. **The dry ground variant** (`14d7ed6`, preview `artifacts/reference/ground-preview-dry.png`): withered-grass
   field, gravel-road lane, 2K. *Recommend: keep the current sets.* Taste, so the frame is there, not switched in.

## 6. Standing rules in force

`scripts/shoot.sh` is the only launcher: one launch per run, window shrunk into a corner and minimised, app
killed the moment a **fresh** report lands. No window is left on Todd's screen — this pass shot six frames in
one run and took no window.

**Markers:** `/tmp/corn-crew-done.20260925-m32c` ABSENT (correct — M32c is open). `/tmp/corn-crew-blocked`
ABSENT — nothing needs a human to continue.

**Working tree left alone as ordered:** the two pre-existing M0 items; Ernie's ground art uncommitted as not
mine. Unity re-serialised `ProjectSettings/*` and the RP assets during builds.

## 7. Last commits

`3594fad` M32c pass 3 (renderer masks, six captures, one frozen pose) · `3aa060a` M32c pass 2 (camera freeze,
the void-by-geometry ablation, the marker method) · `4bb5e37` M32c pass 1 (probe; later shown to be measured on
two cameras) · `154d229`/`02e1d1d`/`b5df115` status pages · `ce0eae8` M32b pass 4 · `b1c36ca` M32b pass 3 ·
`0e3bc05` M32b pass 2 · `9085719` M32b puddles · `992da42` M31b solid lane · `a2e1119` M32 matte ground ·
`ea81ad8` M31 lane blends by transparency · `09a59c3` matte ground + puddle art (Ernie) · `34dbf49` M28 the
scarecrow · `e460272` M27 first person · `f46b9e0` M29 ground.