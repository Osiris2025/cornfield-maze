# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `6238881` (M32c pass 4, OPEN). One page, not a log._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS volume files1). Unity 6000.3.23f1, URP._
_Order of 2026-09-25 (cap 4 passes): **M32c only** — then STOP; the scarecrow and the lantern wait on Todd's
models, which are coming. Mark: `/tmp/corn-crew-done.20260925-m32c` (ABSENT — M32c is not closed). Unity slot free._

## 1. The order, and where it stands

| # | Item | State |
|---|---|---|
| — | M32 matte ground · M31b lane edge · M32b puddles placed | GREEN `a2e1119` · `992da42` · `9085719` |
| 1 | **M32c — the puddle reads as WATER and the lane stops glowing** | **OPEN `6238881`** — 4 of 4 passes used. Both outcomes met and measured: lane **1.15x** the field (at the ceiling), puddle reads as water in the frame. **One acceptance line not met: the probe ships no sky** — Ernie's/Todd's call, no done marker |
| — | M33 lanterns · M34 scarecrow | **PARKED** — built on Todd's models; nothing started, nothing waiting on us |

## 2. M32c pass 4 — what was fixed, and what found it

**`PathMudWetness` was walking the lane past the matte rule.** `GameBootstrap` installs it and on every frame it
rewrote the lane material's `_BaseColor` **and its `_Smoothness`**, lerping smoothness toward 0.42 as the storm
ran. The standing rule is that the ground is matte and any highlight on the lane is a defect; the M32 read-back
measured 0.137 because it reads the *map's* alpha, not the constant that script overwrites — two different
numbers for one surface, and no read-back of the map could have caught it. Its smoothness is now capped by
`PathMudWetness.MatteSmoothCeiling` (0.20). Wet dirt darkens; it does not grow a highlight.

**`Materials.MakeMatte`** — lane and field get no specular lobe and no environment reflection at all,
structurally, rather than a low smoothness value a later edit could raise. Water does not pass through it.

That clamp is what moved the picture. Same frozen pose, same masks, same harness:

    state                 water region mean G   lane mean   lane/field
    before the clamp            92.51            33.55       1.50x
    after  the clamp            46.56            25.67       1.15x

And the control proves the fix is real, not a change of lighting: with the lobe and the environment reflection
put back on the lane, the same pixels read 32.46 against 25.67 (**+6.79**). The highlight is real and the fix
removes it.

## 3. The acceptance, line by line

- **Shallow night frame, probe on and off, same camera:** provided (`m32c-water-on/off.png`), one frozen pose,
  pose-stamped. **But the probe is worth +0.00 of 255 on the water** (mean |change| 0.07; water px >140: 0 with
  it, 0 without). The control says why: a lane at mirror smoothness with environment reflections on, on top of
  that probe, does not brighten either — **the probe's capture holds no sky.** The frame does *not* show the
  water carrying a reflected sky, that is not claimed, and no emissive patch was faked on.
- **Water's bright count and mean against the lane's, from the frame:** water 54,405 px, mean G **46.56**,
  bright(>140) **0 (0.0 %)**; lane 1,204 px, mean **25.67** → **water/lane 1.81x**. (The lane mask shrank from
  10,901 px to 1,204 px once the lane got darker — fewer pixels pass the paint threshold; smaller sample, said
  out loud.)
- **Lane vs field, same distance: 25.67 against 22.33 = 1.15x** — at the ceiling the order set, not over it.
- **Fresnel direction is right:** on the water's own pixels the near half means 40.76 and the grazing far half
  52.37 — brighter toward the horizon, falling off as the view steepens. Last pass those two were identical to
  the decimal, because what was being measured was the lane's false highlight.
- **One honest sentence: does the puddle read as water in that frame? Yes.** The lane is a dark wet gravel path,
  not a pale ribbon, and the puddles sit in the rut as pale cool patches, brighter at their far end —
  unmistakably standing water, not painted spots. Not mirror-like: no glint, no sky in them. The sheen is a
  diffuse cool lift, not a specular sparkle. If Todd wants glass, that is a different mechanism from the one this
  order proposed.

**Why no done marker:** the order's fallback clause covers a probe that ships sky-only, not one that ships
nothing. I will not certify an acceptance line I cannot show. The call is Ernie's.

## 4. Open — reported, not hidden

- **The probe's capture holds no sky.** Either the sky dome is not in it (a realtime probe renders the scene,
  not the camera's skybox, and the dome is drawn by its own renderer) or the ground's environment path is inert.
  Reading the probe's cubemap back — the trick that caught `alphaSource=None` — is the next single test, cheap.
- **Two per-frame writers silently undid harness state changes:** `DuskSky.cs:380` rewrites the moon light's
  intensity every frame; `PathMudWetness.Apply` rewrites the lane's colour and smoothness every frame. Ablations
  on either were VOID and were reported void, not as physics. **A runtime state change is not evidence until the
  thing that owns that state has been stopped** — the same defect as `alphaSource=None`, the copied harness and
  the two-camera A/B, in a fourth form.
- **Pass 3's 92.51 / 33.57 / 1.50x were real measurements of the scene but of the lane's false highlight**, not of
  water; superseded by §2. Passes 1–2's water-vs-lane figures came from two different cameras and then from a
  geometry classifier that ignored the water's own material — **void**, and labelled void.
- **The water mask still includes the damp halo** (the marker nulls the normal map too); the core is 7.6 % of the
  quad. The `_M` maps are Read/Write for the read-backs — CPU copies a phone should not keep, one line to remove.
- **The M32 map lift** (binding the `_M` maps raises the frame 33.68 → 49.45, peak unchanged) is still
  unexplained, and may be the same term as the grazing brightness now fixed.
- Carried: `CornMaze/StarUnlit` does not resolve in the player build; locked docs still say "Crumb Beast" and
  FSD §17 still describes a noise ground and a sphere Husk (**Ernie applies** — Harrow may not edit locked
  docs); the 60 fps floor is a phone target and unmeasured; §25.6's device listen pass needs the phone.

## 5. CAPTURES FOR TODD

    artifacts/review/world/m32c-water-on.png        THE acceptance frame — dark wet lane, pale puddles in the rut
    artifacts/review/world/m32c-water-off.png       the same frozen pose with the probe off — worth +0.00/255
    artifacts/review/world/m32c-lane-control.png    the control: the lobe and env reflections put back (+6.79)
    artifacts/review/world/m32c-water-mask.png      the diagnostic: the puddles painted, showing the decal's footprint
    artifacts/review/world/m32c-lane-mask.png       the lane painted — its true footprint, for lane/field
    artifacts/review/world/m32c-water-black.png     the ablation: water with a BLACK albedo and no gloss
    artifacts/m32c-puddle-report.txt                every number, the void ones labelled void, the fix and the reason
    artifacts/reference/ground-preview-dry.png      the dry ground variant, unchanged, not switched in

## 6. For Todd — parked, and two questions

1. **PARKED (needs your models, nothing else blocks it): M33 lanterns and M34 the scarecrow.** A model needs a
   recorded licence line before it ships, and if its terms do not permit distribution inside an App Store build
   that has to be known *before* it is wired. *Recommend: send them whenever; the placeholders stand.*
2. **Is M32c done?** The lane no longer glows (1.15x, at the ceiling) and the puddles read as water in the frame
   — the two things you actually asked for, both measured and both in `m32c-water-on.png`. What is missing is the
   *mechanism* the order named: the reflection probe delivers no sky, so the water's sheen is diffuse rather than
   glassy. *Recommend: look at the frame first. If the puddles read as water to you, the one open line is a
   mechanism note, not a defect; if you want glass, say so and I will price the real reflection separately.*
3. **The dry ground variant** (`14d7ed6`, preview `artifacts/reference/ground-preview-dry.png`): withered-grass
   field, gravel-road lane, 2K. *Recommend: keep the current sets.* Taste, so the frame is there, not switched in.

## 7. Standing rules in force

`scripts/shoot.sh` is the only launcher: one launch per run, window shrunk into a corner and minimised, app
killed the moment a **fresh** report lands. No window is left on Todd's screen — this pass shot seven frames per
run, in two runs, and took no window.

**Markers:** `/tmp/corn-crew-done.20260925-m32c` ABSENT (correct — M32c is not closed). `/tmp/corn-crew-blocked`
ABSENT — nothing needs a human to continue.

**Working tree left alone as ordered:** the two pre-existing M0 items; Ernie's ground art uncommitted as not
mine. Unity re-serialised `ProjectSettings/*` and the RP assets during builds.

## 8. Last commits

`6238881` M32c pass 4 (the mud-smoothness clamp + matte by construction; lane 1.15x; puddle reads as water) ·
`3594fad` M32c pass 3 (renderer masks, one frozen pose) · `3aa060a` M32c pass 2 (camera freeze, the
void-by-geometry ablation) · `4bb5e37` M32c pass 1 (the probe) · `a5b3fcf`/`154d229`/`02e1d1d`/`b5df115` status
pages · `ce0eae8` M32b pass 4 · `b1c36ca` M32b pass 3 · `0e3bc05` M32b pass 2 · `9085719` M32b puddles ·
`992da42` M31b solid lane · `a2e1119` M32 matte ground · `ea81ad8` M31 lane blends by transparency · `09a59c3`
matte ground + puddle art (Ernie) · `34dbf49` M28 the scarecrow · `e460272` M27 first person · `f46b9e0` M29 ground.