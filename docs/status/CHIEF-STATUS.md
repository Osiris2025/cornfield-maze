# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `1a58e10` (**M32d — the environment capture: found, proven,
fixed**). M32c OPEN, no done marker._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS volume files1). Unity 6000.3.23f1, URP._
_Order of 2026-09-25: **M32c + M32d** — then STOP; the scarecrow and the lantern wait on Todd's models, which are
coming. Mark: `/tmp/corn-crew-done.20260925-m32c` ABSENT (M32c is not closed). **`/tmp/corn-crew-blocked` IS
WRITTEN** (one line) — M32d is done, and the water's sheen is now a measured magnitude, not a mystery. Unity slot
free, no app running._

## 1. The order, and where it stands

| # | Item | State |
|---|---|---|
| — | M32 matte ground · M31b lane edge · M32b puddles placed | GREEN `a2e1119` · `992da42` · `9085719` |
| 1 | **M32c — the puddle reads as WATER and the lane stops glowing** | lane half **MET** (1.07x the field, inside the ceiling, matte by construction). Water half: the reflection is real, present and now **proven** — worth 4.32/255 — and still not *visible*. Open on a stylisation choice |
| 1b | **M32d — prove the environment capture** | **DONE `1a58e10`** — it was a defect, and two of them (§2) |
| — | M33 lanterns · M34 scarecrow | **PARKED** — built on Todd's models; nothing started, nothing waiting on us |

## 2. M32d — the capture was a defect, exactly as you said. Two, in fact.

**1. `Camera.RenderToCubemap` returns ONE image on all six faces in this player build.** Proved the only way that
settles it: the six faces were written to PNG and hashed — **all byte-identical**. Everything concluded earlier
from "the probe's capture holds no sky" was statistics of a capture that was never six views.
**FIX:** the six views are rendered explicitly — each face its own camera rotation (up vectors checked against
Unity's cube convention), fog off for those six frames (the storm's fog otherwise fills every direction), into a
temporary render texture read straight back. A camera with a target texture never draws to the screen, so nothing
lands on Todd's display.
**READ BACK:** `+X 18.1 [0..189] · -X 10.5 [0..162] · +Y 0.5 [0..146] · -Y 26.5 [1..169] · +Z 12.2 [0..161] ·
-Z 7.7 [0..181] · overall 12.57 of 255 · in the moon's own direction (within 8 deg) mean 22.3, max 108` ·
brightest pixel anywhere 189 on `+X`. Dark zenith, lit ground, the moon where `DuskSky` puts it. **The faces now
differ** (three distinct MD5s) and `m32d-cube-X.png` shows the moon, its halo, stars and the lit field. Scaled
x3.95 so the cube mean lands on the 49.8 of 255 that holds the ground on its approved level — content from the
scene, level a lighting value.

**2. The lane control was VOID and pass 7 used it as evidence.** `PathMudWetness.Apply` rewrites the lane's
`_Smoothness` every frame (capped at 0.20), so the harness's "mirror lane" was overwritten before the capture —
the very trap pass 6 documented and pass 7 relied on anyway. **FIX:** the writer is detached for that one frame
and re-registered straight after. Re-measured, the mirror lane **gains +2.37**: the environment **does** reach
the surfaces, weakly. An unproven claim is retired.

## 3. The acceptance, line by line

    water, on the water's own pixels: 54,407 px, mean G 45.12, bright(>140) 0 (0.0 %)
    lane,  on the lane's own pixels:   7,093 px, mean G 20.55      field: 5,220 px, mean G 19.20
    ->  water/lane 2.20x,  lane/field 1.07x        (the order wants 1.15x or darker — MET)
    ENVIRONMENT ON vs OFF, on the water: 45.12 vs 46.56, mean |change| 4.32 of 255
    THE SAME SWITCH ON THE LANE at mirror smoothness, writer detached: +2.37 of 255
    FRESNEL: near half 39.13, grazing far half 51.11 — brighter toward the horizon, falling off as it steepens
    PathMudWetness: MatteSmoothCeiling 0.20, base _Smoothness 1.000, mud 0.024 (max 0.72)
      -> writes 0.200 now, 0.200 at full mud (the ceiling holds: rain cannot walk the lane past the matte rule)

- **One honest sentence: does the puddle read as water in that frame? No** — it reads as a wet lane with a lighter
  patch. Said now with a proven capture, a valid control and the numbers above, so what is left is a look choice.

## 4. Open — reported, not hidden

- **THE BLOCKER (§6.2):** the sheen is a **magnitude**, not a missing mechanism — a dielectric's reflection of a
  dark night sky is 4.32 of 255 at this view. Every remaining way to make it visible is a look decision.
- **The lane's ratio straddles the ceiling** — 1.06/1.07/1.12/1.12/1.13/1.20 over six runs, because
  `PathMudWetness` writes the lane as the storm comes and goes. If the ceiling needs margin, that target tint is
  the lever.
- **The map is now a real capture of the game's own sky** (no longer a stand-in) — scaled for the ambient level.
- **Per-frame writers silently undid earlier harness ablations:** `DuskSky.cs:380` (moon light) and
  `PathMudWetness.Apply` (lane colour + smoothness). Both filed VOID, not physics. **A runtime state change is not
  evidence until the thing that owns that state has been stopped.** (Count now: two ablations, one control.)
- **Passes 1–3's paired numbers are VOID or superseded** — all labelled, none dropped. **Pass 7's "+0.00 on the
  lane" is void too** and is corrected in the report.
- **`Camera.RenderToCubemap` is not to be trusted in this build** — one image, six faces. Use the explicit render.
- **`AmbientMeanTarget` (49.8) is empirical** — two measured points, linear model; the harness prints both.
- **The water mask includes the damp halo** (the marker nulls the normal map too); the core is 7.6 % of the quad.
  The `_M` maps are Read/Write for the read-backs — CPU copies a phone should not keep, one line to remove.
- Carried: `CornMaze/StarUnlit` does not resolve in the player build; locked docs still say "Crumb Beast" and FSD
  §17 still describes a noise ground and a sphere Husk (**Ernie applies**); the 60 fps floor is a phone target and
  unmeasured; §25.6's device listen pass needs the phone.

## 5. CAPTURES FOR TODD

    artifacts/review/world/m32c-water-on.png        THE acceptance frame — wet grey lane, water a lighter patch, no sheen
    artifacts/review/world/m32c-water-off.png       the same frozen pose, environment off — the pair differs by 4.32/255
    artifacts/review/world/m32c-lane-control.png    the control, now VALID — the lane at mirror smoothness (+2.37)
    artifacts/review/world/m32d-cube-X.png          THE CAPTURED SKY, +X face — the moon, its halo, stars, the lit field
    artifacts/review/world/m32d-cube-{Y,Z}.png      the same capture: the dark zenith (Y) and a second horizon (Z)
    artifacts/review/world/m32c-water-mask.png      the diagnostic: the puddles painted, the decal's footprint
    artifacts/review/world/m32c-lane-mask.png       the lane painted — its true footprint, for lane/field
    artifacts/m32c-puddle-report.txt                every number, the void ones labelled, the capture and the reason
    artifacts/reference/ground-preview-dry.png      the dry ground variant, unchanged, not switched in

## 6. For Todd — the blocker, the park, and one question

1. **PARKED (needs your models, nothing else blocks it): M33 lanterns and M34 the scarecrow.** A model needs a
   recorded licence line before it ships, and if its terms do not permit distribution inside an App Store build
   that has to be known *before* it is wired. *Recommend: send them whenever; the placeholders stand.*
2. **THE BLOCKER — M32c's water half is your call, and it is now an honest one.** You were right that the capture
   was a defect; it is fixed and proved (the read-back above). What that leaves is physics: a dielectric reflecting
   a dark night sky is a few levels of 255, so the water carries the sky (4.32) where the lane carries nothing, and
   still never crosses 140. *Recommend: look at `m32c-water-on.png` and pick one — (a) accept it: puddles read as
   damp patches on a dark wet lane, M32c closes and I write the marker; (b) a brighter atmospheric moon bloom in
   the sky (still the sky, not an emissive patch on the water); (c) a shallower acceptance view down the lane; or
   (d) water that is not a plain dielectric.*
3. **The dry ground variant** (`14d7ed6`, preview `artifacts/reference/ground-preview-dry.png`): withered-grass
   field, gravel-road lane, 2K. *Recommend: keep the current sets.* Taste, so the frame is there, not switched in.

## 7. Standing rules in force

`scripts/shoot.sh` is the only launcher: one launch per run, window shrunk into a corner and minimised, app killed
the moment a **fresh** report lands. No window left on Todd's screen — this pass shot seven frames per run over
three runs and took no window; the sky capture renders to a render texture, so it never touches the screen either.
The harness freezes the pose (`Time.timeScale = 0`, the stand re-asserted) and stamps every frame with its camera
pose, so a non-comparable pair cannot be built by accident.

**Markers:** `/tmp/corn-crew-done.20260925-m32c` ABSENT (correct — M32c is not closed).
**`/tmp/corn-crew-blocked` written** — one line: the water sheen is Todd's stylisation call.

**Working tree left alone as ordered:** the two pre-existing M0 items. Unity re-serialised `ProjectSettings/*` and
the RP assets during builds.

**Flagged, not staged:** unstaged deletions of three tracked `.meta` files (`Assets/GingerbreadMan.meta`,
`Assets/Resources/PerformanceTestRun{Info,Settings}.json.meta`). Not mine, not touched, in no commit of ours. But
`Assets/Resources/PerformanceTestRun*.json` **are rewritten inside `Assets/` every time the built app runs**. A
build should not write into `Assets/`. **No `git add -A` has been run and none should be.**

## 8. Last commits

`1a58e10` M32d (the capture was one image on six faces — explicit six-view render, moon read back; the lane control
was void, now valid +2.37) · `dd02504` M32c pass 7 · `29eb9ae` M32c pass 6 · `0172956` M32c pass 5 · `6238881`
M32c pass 4 · `3594fad` · `3aa060a` · `4bb5e37` M32c passes 1–3 · `ce0eae8` · `b1c36ca` · `0e3bc05` · `9085719`
M32b · `992da42` M31b · `a2e1119` M32 · `ea81ad8` M31 · `34dbf49` M28 · `e460272` M27 · `f46b9e0` M29.
