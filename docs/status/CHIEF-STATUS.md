# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `29eb9ae` (M32c pass 6, OPEN). One page, not a log._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS volume files1). Unity 6000.3.23f1, URP._
_Order of 2026-09-25: **M32c only** — then STOP; the scarecrow and the lantern wait on Todd's models, which are
coming. Mark: `/tmp/corn-crew-done.20260925-m32c` (ABSENT — M32c is not closed). Unity slot free, no app running._

## 1. The order, and where it stands

| # | Item | State |
|---|---|---|
| — | M32 matte ground · M31b lane edge · M32b puddles placed | GREEN `a2e1119` · `992da42` · `9085719` |
| 1 | **M32c — the puddle reads as WATER and the lane stops glowing** | **OPEN `29eb9ae`** — 6 passes. **Lane half MET: 1.12x the field, inside the ceiling, matte by construction, and 400x less reflective than the water.** Water half: the environment map now exists, is read back and calibrated — the water reflects it at **3.93/255** against the lane's 0.01, Fresnel direction right — but **the puddle still does not read as water in the frame**. No done marker |
| — | M33 lanterns · M34 scarecrow | **PARKED** — built on Todd's models; nothing started, nothing waiting on us |

## 2. M32c pass 6 — the environment map, built and read back

Pass 5 left one named fault: the probe produced no capture. Pass 6 stops asking a realtime capture to happen —
the probe cleared to the skybox, and this scene's sky is a 57 m **mesh dome**, not a skybox material — and
**builds** the environment instead: the night sky as a 64 px cube generated in code (horizon band under the
storm, dark overhead, the moon disc placed at `DuskSky.MoonDirection`, a wide halo, sparse stars). Procedural, no
third-party asset, and the order allows it ("baked once — the sky barely moves").

It was wrong on the first build, and the read-back caught it in one run:

    array +Y mean G -560.08,  g range -2.203 .. -0.769,  post-upload 0.00

`Mathf.SmoothStep(from, to, t)` clamps `t` and returns a value **between `from` and `to`** — it is not "smooth a
0..1 ramp". Handing it `(0, 3.6, ang)` returns up to 3.6, so `1 - that` ran to −2.6 and RGBA32 clamped the
negative radiance to black. A black probe cube does not merely fail to light the water — it takes the ground's
ambient with it (field 22.32 → 6.45, water 46.56 → 5.90). Normalised, and the map reads back real:

    ENVIRONMENT MAP (face mean G of 255): +X 64.6 [177 bright] · -X 39.3 · +Y 27.8 · -Y 87.0 · +Z 42.1 · -Z 39.6
    overall 50.06, brightest 255 on +X — the moon at 28 deg, where DuskSky puts it.

**Calibrated, not guessed.** A probe's cube feeds the ambient as well as the reflections, so a dark sky dims the
ground. Two measured points (cube 12.23 → field 10.23; cube 26.88 → field 14.95) give ambient ≈ 0.322 of the
cube's mean plus a 6.29 light-only floor, so matching the 22.32 baseline needs a cube mean of ~50 = gain 4.1.
Result: field **20.92** against 22.32, water **44.90** against 46.56 — the ground Todd approved is preserved.

## 3. The acceptance, line by line (pass 6)

- **Shallow night frame, environment on and off, same camera:** provided, one frozen pose, pose-stamped. The
  water changes by **−1.66 of 255, mean |change| 3.93**; **the same switch on the lane (lobe and env restored)
  is +0.01** — so the water does carry the sky and the lane does not, by ~400:1. But 3.93/255 is not a glint.
  At a ~16 deg view the Fresnel reflectance of a dielectric is a few percent of a night sky, and a few percent of
  a night sky is four levels of 255.
- **Water's bright count and mean against the lane's, from the frame:** water 54,405 px, mean G **44.90**,
  bright(>140) **0 (0.0 %)**; lane 9,089 px, mean **23.39** → **water/lane 1.92x**.
- **Lane vs field: 23.39 against 20.92 = 1.12x** — inside the order's 15 % ceiling. **MET.**
- **Fresnel direction is right:** water near half 38.54, grazing far half 51.26 — brighter toward the horizon.
- **One honest sentence — does the puddle read as water in that frame? No.** In `m32c-water-on.png` the lane
  reads as a wet grey path with a lighter mottled patch where the water is: wet lane, not standing water with a
  sheen. **Correction to pass 5:** that page said "Yes". Looking at the frame with the environment actually in it,
  the earlier brightness was the ambient, not the water. It reads as water only if you want it to.
  What it would need: a much shallower view down the lane than this shot has, or a mechanism other than
  environment reflection — which the order forbids faking with an emissive patch. That choice is Todd's.

**Why no done marker:** the reflected-sky line is present but not visible. I will not certify a frame that does
not convince.

## 4. Open — reported, not hidden

- **The moon's disc does not land in the puddle at this camera angle** — the reflection is diffuse (a Fresnel
  lift, brighter at the grazing end) with no glint. Options if Todd wants glass: shoot the acceptance at a much
  shallower angle, or move to a different mechanism (his call — an emissive patch is off the table).
- **The environment map is a stand-in for the sky dome mesh**: it is a code-built night sky, not a capture of the
  game's own sky. If the storm's cloud band should be in the water, the map's horizon band is where it would go.
- **Per-frame writers silently undid earlier harness ablations:** `DuskSky.cs:380` (moon light) and
  `PathMudWetness.Apply` (lane colour + smoothness). Both were reported VOID, not as physics. **A runtime state
  change is not evidence until the thing that owns that state has been stopped.**
- **Passes 1–3's paired numbers are VOID or superseded** (two different cameras; a geometry classifier that
  ignored the water's own material; a false highlight) — all labelled, none dropped.
- **The `EnvGain` calibration is empirical** (two measured points, linear model). If the ground's lighting
  changes, the gain needs re-measuring — the harness prints the two numbers needed.
- **The water mask still includes the damp halo** (the marker nulls the normal map too); the core is 7.6 % of the
  quad. The `_M` maps are Read/Write for the read-backs — CPU copies a phone should not keep, one line to remove.
- Carried: `CornMaze/StarUnlit` does not resolve in the player build; locked docs still say "Crumb Beast" and
  FSD §17 still describes a noise ground and a sphere Husk (**Ernie applies** — Harrow may not edit locked docs);
  the 60 fps floor is a phone target and unmeasured; §25.6's device listen pass needs the phone.

## 5. CAPTURES FOR TODD

    artifacts/review/world/m32c-water-on.png        THE acceptance frame — wet grey lane, water a lighter patch, no sheen
    artifacts/review/world/m32c-water-off.png       the same frozen pose, environment off — the pair differs by 3.93/255
    artifacts/review/world/m32c-lane-control.png    the control: the lane's lobe and env reflections restored (+0.01)
    artifacts/review/world/m32c-water-mask.png      the diagnostic: the puddles painted, showing the decal's footprint
    artifacts/review/world/m32c-lane-mask.png       the lane painted — its true footprint, for lane/field
    artifacts/review/world/m32c-water-black.png     the ablation: water with a BLACK albedo and no gloss
    artifacts/m32c-puddle-report.txt                every number, the void ones labelled void, the map and the reason
    artifacts/reference/ground-preview-dry.png      the dry ground variant, unchanged, not switched in

## 6. For Todd — parked, and two questions

1. **PARKED (needs your models, nothing else blocks it): M33 lanterns and M34 the scarecrow.** A model needs a
   recorded licence line before it ships, and if its terms do not permit distribution inside an App Store build
   that has to be known *before* it is wired. *Recommend: send them whenever; the placeholders stand.*
2. **M32c: the lane half is done (1.12x, inside the ceiling). The water half is where I need you.** The
   environment reflection now exists and is measured — the water carries it 400x more than the lane — but at the
   ~16 deg view of the acceptance shot it is 4 levels of 255, invisible. *Recommend: look at
   `m32c-water-on.png`. If "wet lane with a lighter patch" is enough, M32c closes and I write the marker. If you
   want a visible sheen, tell me which way: (a) acceptance shot from a shallower angle down the lane, or (b) a
   different mechanism for the water — that is a design call, not tuning.*
3. **The dry ground variant** (`14d7ed6`, preview `artifacts/reference/ground-preview-dry.png`): withered-grass
   field, gravel-road lane, 2K. *Recommend: keep the current sets.* Taste, so the frame is there, not switched in.

## 7. Standing rules in force

`scripts/shoot.sh` is the only launcher: one launch per run, window shrunk into a corner and minimised, app
killed the moment a **fresh** report lands. No window left on Todd's screen — this pass shot seven frames per
run, in five runs, and took no window. The harness freezes the pose (`Time.timeScale = 0`, the stand re-asserted)
and stamps every frame with its camera pose, so a non-comparable pair cannot be built by accident.

**Markers:** `/tmp/corn-crew-done.20260925-m32c` ABSENT (correct — M32c is not closed). `/tmp/corn-crew-blocked`
ABSENT — nothing needs a human to continue; the question is in §6.

**Working tree left alone as ordered:** the two pre-existing M0 items; Ernie's ground art uncommitted as not
mine. Unity re-serialised `ProjectSettings/*` and the RP assets during builds.

**Flagged, not staged:** unstaged deletions of three tracked `.meta` files (`Assets/GingerbreadMan.meta`,
`Assets/Resources/PerformanceTestRun{Info,Settings}.json.meta`). Not mine, not touched, in no commit of ours.
But `Assets/Resources/PerformanceTestRun*.json` **are rewritten inside `Assets/` every time the built app runs**,
which is why those paths keep changing shape between passes. A build should not write into `Assets/`. **No
`git add -A` has been run and none should be** — it would commit these deletions.

## 8. Last commits

`29eb9ae` M32c pass 6 (the environment map, built/calibrated/read back; water reflects 400x the lane; not visible)
· `0172956` M32c pass 5 (the water's env read-back, the probe's empty capture; lane 1.06x) · `6238881` M32c pass 4
(mud-smoothness clamp + matte by construction) · `3594fad` M32c pass 3 · `3aa060a` M32c pass 2 · `4bb5e37` M32c
pass 1 · `0c16809`/`c3e6850`/`207554b`/`a5b3fcf`/`154d229` status pages · `ce0eae8` M32b pass 4 · `b1c36ca` ·
`0e3bc05` · `9085719` M32b puddles · `992da42` M31b · `a2e1119` M32 · `ea81ad8` M31 · `09a59c3` (Ernie) ·
`34dbf49` M28 · `e460272` M27 · `f46b9e0` M29.