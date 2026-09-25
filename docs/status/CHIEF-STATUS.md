# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `dd02504` (M32c pass 7, OPEN — BLOCKED on a taste call)._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS volume files1). Unity 6000.3.23f1, URP._
_Order of 2026-09-25: **M32c only** — then STOP; the scarecrow and the lantern wait on Todd's models, which are
coming. Mark: `/tmp/corn-crew-done.20260925-m32c` ABSENT (M32c is not closed). **`/tmp/corn-crew-blocked` IS
WRITTEN** — the water half is a look decision. Unity slot free, no app running._

## 1. The order, and where it stands

| # | Item | State |
|---|---|---|
| — | M32 matte ground · M31b lane edge · M32b puddles placed | GREEN `a2e1119` · `992da42` · `9085719` |
| 1 | **M32c — the puddle reads as WATER and the lane stops glowing** | **OPEN `dd02504`**, BLOCKED. **Lane half MET: 1.12x the field, inside the ceiling, matte by construction, 5.59/255 reflective against the lane's 0.00.** Water half: the environment map is built, solved, read back and calibrated, the water reflects it at **5.59/255** with the Fresnel direction right — and the sheen is **still not visible**. Making it visible needs a stylisation Todd must pick |
| — | M33 lanterns · M34 scarecrow | **PARKED** — built on Todd's models; nothing started, nothing waiting on us |

## 2. M32c pass 7 — what the map has to carry, and why it still does not show

Pass 6 built a night-sky cube and measured the water reflecting 3.93 of 255 — real, and invisible. The limit is
**not** Fresnel: at the acceptance view (eye 1.15 m, water ~4 m down the lane) the view is ~74 deg off the
water's normal, where Schlick gives a dielectric **0.23**, not the 0.04 that "grazing" is usually taken to mean.
The water reflects 23 % of the sky; what limits the sheen is that **a night sky is dark**. So the map was rebuilt
around what a night puddle actually shows:

- the **storm's lit cloud band at the horizon** — the part a shallow view reflects, and why the reflection
  belongs at the horizon end of the puddle and falls off as the view steepens (horizon/zenith now 0.34/0.030,
  was 0.10/0.015);
- the **moon disc at `DuskSky.MoonDirection`**, unscaled, keeping its 255 peak;
- a **halo whose width was chosen on measurement**: at 20 deg it spreads the moon's light over the sphere and
  what the water actually reflects DROPS (5.68 → 2.99 of 255), because the water's ripple normal scatters a
  broad source instead of catching it. Narrow and brighter wins;
- everything except the disc scaled by a **solved** factor that pins the cube's mean at the 49.8 of 255 the two
  measured points say holds the ground on its 22.32 baseline — and the solve counts the halo's own mean, so sky
  brightness cannot sneak into the ground.

## 3. The acceptance, line by line (pass 7)

    water, on the water's own pixels: 54,405 px, mean G 44.12, bright(>140) 0 (0.0 %)
    lane,  on the lane's own pixels:   8,928 px, mean G 22.27      field: 5,222 px, mean G 19.88
    ->  water/lane 1.98x,  lane/field 1.12x        (the order wants 1.15x or darker — MET)
    ENVIRONMENT ON vs OFF, on the water: 44.12 vs 46.57, mean |change| 5.59 of 255
    THE SAME SWITCH ON THE LANE, lobe and env reflections restored: +0.00 of 255
    THE ENVIRONMENT'S OWN CUBE, read back: +X 54.5 [18 bright] · -X 39.3 · +Y 18.2 · -Y 104.5 · +Z 41.0 · -Z 39.5
      overall 49.51 of 255, peak 255 on +X — the moon at 28 deg, where DuskSky puts it
    FRESNEL: near half 37.43, grazing far half 50.81 — brighter toward the horizon, falling off as the view steepens

- **Water carries the sky, the lane does not: 5.59 of 255 against 0.00**, and the water's grazing end is 2.3x the
  field. The water still has **0 pixels above 140**: the reflection is a diffuse lift at the horizon end, not a
  glint — at 26 deg off the mirror direction there is no moon to catch, you are reflecting the cloud band.
- **One honest sentence — does the puddle read as water in that frame? No.** It reads as a wet lane with a
  lighter, slightly mottled patch where the water is. (Pass 5 said "Yes"; that page is corrected in the report —
  the brightness it saw was the ambient, not the water.)

**Why no done marker:** the reflected-sky line is structurally met and visually absent. I will not certify a
frame that does not convince.

## 4. Open — reported, not hidden

- **THE BLOCKER (see §6.2):** every remaining way to make the sheen visible is a look decision — a brighter
  atmospheric halo in the sky, a shallower acceptance view, or water that is not a plain dielectric. No further
  agent pass can produce one.
- **The lane's ratio straddles the ceiling across runs** — measured 1.06 / 1.07 / 1.12 / 1.12 / 1.13 / 1.20 over
  six runs, because `PathMudWetness` keeps writing the lane as the storm comes and goes during a capture. It sits
  *at* the ceiling, not comfortably under it, and the lane's mask size moves with it (1,204 – 9,108 px). If the
  ceiling must be met with margin, the mud system's target tint is the thing to change.
- **The environment map is a stand-in for the sky dome mesh**: a code-built night sky, not a capture of the
  game's own. If the storm's cloud band should be in the water from the *rendered* sky, that is a different job.
- **Per-frame writers silently undid earlier harness ablations:** `DuskSky.cs:380` (moon light) and
  `PathMudWetness.Apply` (lane colour + smoothness). Both reported VOID, not as physics. **A runtime state change
  is not evidence until the thing that owns that state has been stopped.**
- **Passes 1–3's paired numbers are VOID or superseded** (two different cameras; a geometry classifier that
  ignored the water's own material; a false highlight) — all labelled, none dropped.
- **The map's `AmbientMeanTarget` (49.8) is empirical** — two measured points, linear model. If the ground's
  lighting changes it needs re-measuring; the harness prints the two numbers needed.
- **The water mask still includes the damp halo** (the marker nulls the normal map too); the core is 7.6 % of the
  quad. The `_M` maps are Read/Write for the read-backs — CPU copies a phone should not keep, one line to remove.
- Carried: `CornMaze/StarUnlit` does not resolve in the player build; locked docs still say "Crumb Beast" and
  FSD §17 still describes a noise ground and a sphere Husk (**Ernie applies** — Harrow may not edit locked docs);
  the 60 fps floor is a phone target and unmeasured; §25.6's device listen pass needs the phone.

## 5. CAPTURES FOR TODD

    artifacts/review/world/m32c-water-on.png        THE acceptance frame — wet grey lane, water a lighter patch, no sheen
    artifacts/review/world/m32c-water-off.png       the same frozen pose, environment off — the pair differs by 5.59/255
    artifacts/review/world/m32c-lane-control.png    the control: the lane's lobe and env reflections restored (+0.00)
    artifacts/review/world/m32c-water-mask.png      the diagnostic: the puddles painted, showing the decal's footprint
    artifacts/review/world/m32c-lane-mask.png       the lane painted — its true footprint, for lane/field
    artifacts/review/world/m32c-water-black.png     the ablation: water with a BLACK albedo and no gloss
    artifacts/m32c-puddle-report.txt                every number, the void ones labelled void, the map and the reason
    artifacts/reference/ground-preview-dry.png      the dry ground variant, unchanged, not switched in

## 6. For Todd — the blocker, the park, and one question

1. **PARKED (needs your models, nothing else blocks it): M33 lanterns and M34 the scarecrow.** A model needs a
   recorded licence line before it ships, and if its terms do not permit distribution inside an App Store build
   that has to be known *before* it is wired. *Recommend: send them whenever; the placeholders stand.*
2. **THE BLOCKER — M32c's water half is your call.** The lane half is done and measured (1.12x the field, inside
   the ceiling). The water reflects the sky and the lane does not — 5.59 of 255 against 0.00 — with the Fresnel
   direction right, and the map behind it holds a 255-peak moon in the right direction, read back face by face.
   What is missing is that you can *see* it: at this view a physically correct dielectric reflecting a night sky
   gives a few levels of 255, so there are no pixels above 140 in the water. *Recommend: look at
   `m32c-water-on.png` and pick one — (a) accept it: the puddles read as damp patches on a dark wet lane, M32c
   closes and I write the marker; (b) a brighter atmospheric halo in the sky (a stylised moon bloom, still the
   sky, not an emissive patch on the water); (c) a shallower acceptance view down the lane; or (d) water that is
   not a plain dielectric. (b)–(d) are look decisions and I will build whichever you name.*
3. **The dry ground variant** (`14d7ed6`, preview `artifacts/reference/ground-preview-dry.png`): withered-grass
   field, gravel-road lane, 2K. *Recommend: keep the current sets.* Taste, so the frame is there, not switched in.

## 7. Standing rules in force

`scripts/shoot.sh` is the only launcher: one launch per run, window shrunk into a corner and minimised, app
killed the moment a **fresh** report lands. No window left on Todd's screen — this pass shot seven frames per
run, in six runs, and took no window. The harness freezes the pose (`Time.timeScale = 0`, the stand re-asserted)
and stamps every frame with its camera pose, so a non-comparable pair cannot be built by accident.

**Markers:** `/tmp/corn-crew-done.20260925-m32c` ABSENT (correct — M32c is not closed).
**`/tmp/corn-crew-blocked` written** — one line: the water sheen needs Todd's stylisation choice.

**Working tree left alone as ordered:** the two pre-existing M0 items; Ernie's ground art uncommitted as not
mine. Unity re-serialised `ProjectSettings/*` and the RP assets during builds.

**Flagged, not staged:** unstaged deletions of three tracked `.meta` files (`Assets/GingerbreadMan.meta`,
`Assets/Resources/PerformanceTestRun{Info,Settings}.json.meta`). Not mine, not touched, in no commit of ours.
But `Assets/Resources/PerformanceTestRun*.json` **are rewritten inside `Assets/` every time the built app runs**,
which is why those paths keep changing shape between passes. A build should not write into `Assets/`. **No
`git add -A` has been run and none should be** — it would commit these deletions.

## 8. Last commits

`dd02504` M32c pass 7 (the map's horizon band + measured halo width + solved scale; water 5.59 vs lane 0.00; lane
1.12x; sheen still not visible) · `29eb9ae` M32c pass 6 (the environment map, built/calibrated/read back) ·
`0172956` M32c pass 5 (the water's env read-back, the probe's empty capture) · `6238881` M32c pass 4
(mud-smoothness clamp + matte by construction) · `3594fad` M32c pass 3 · `3aa060a` M32c pass 2 · `4bb5e37` M32c
pass 1 · `e93b33f`/`0c16809`/`c3e6850`/`207554b`/`a5b3fcf`/`154d229` status pages · `ce0eae8` M32b pass 4 ·
`b1c36ca` · `0e3bc05` · `9085719` M32b puddles · `992da42` M31b · `a2e1119` M32 · `ea81ad8` M31 · `09a59c3`
(Ernie) · `34dbf49` M28 · `e460272` M27 · `f46b9e0` M29.