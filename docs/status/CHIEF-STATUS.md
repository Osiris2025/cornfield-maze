# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `e735ea0` (**M32g — two tests, both negative; one real defect
fixed on the way**). M32c OPEN, no done marker._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS volume files1). Unity 6000.3.23f1, URP._
_Order of 2026-09-25: **M32c + M32d + M32e + M32f + M32g** — then STOP; the scarecrow and the lantern wait on Todd's
models. Mark: `/tmp/corn-crew-done.20260925-m32c` ABSENT (M32c is not closed). **`/tmp/corn-crew-blocked` IS
WRITTEN** — two agent-settleable defects, no taste call. Unity slot free, no app running._

## 1. The order, and where it stands

| # | Item | State |
|---|---|---|
| — | M32 matte ground · M31b lane edge · M32b puddles placed | GREEN `a2e1119` · `992da42` · `9085719` |
| 1 | **M32c — the puddle reads as WATER and the lane stops glowing** | lane half **MET** (dark lane, lit corn, not a pale ribbon). Water half: **not met** — see §2 |
| 1b | **M32d — prove the environment capture** | **DONE `1a58e10`** — it was a defect, two of them |
| 1c | **M32e — shoot the player's own view** | **DONE `61ee0e5`** — pose 1.66 m / 8.09 m; lane beat, water not |
| 1d | **M32f — the ambient diagnosis, read back; build the glint fallback** | **DONE `a5f7426`** — every switch correct, gradient flat, glint built but dark |
| 1e | **M32g — opaque water, one frame; a glint that renders** | **DONE `e735ea0`** — opaque water is flat too (killed my own hypothesis); the glint renders at ~4 % of its level; `defaultReflectionMode` was a real defect and is fixed |
| — | M33 lanterns · M34 scarecrow | **PARKED** — built on Todd's models; nothing started, nothing waiting on us |

## 2. M32g — both tests negative, and one real defect found on the way

**The bucket table — six buckets across the puddle, 6.0 m to 10.3 m past the eye, on the water's own px, of 255:**

    shipped water (alpha-blend):   25.3 -> 23.5 -> 25.0 -> 25.8 -> 22.1 -> 25.2
    OPAQUE water, same camera:     25.3 -> 20.4 -> 25.8 -> 26.0 -> 21.5 -> 26.5     <- TEST 1
    the glint variant:             25.3 -> 23.5 -> 25.4 -> 26.4 -> 22.2 -> 25.3     <- TEST 2
    environment's own share:      +17.9 -> +15.3 -> +16.8 -> +19.0 -> +13.9 -> +17.7
    opaque MINUS blended:          +0.0 -> -3.1 -> +0.9 -> +0.3 -> -0.7 -> +1.3
    glint MINUS shipped:           +0.0 -> +0.0 -> +0.5 -> +0.6 -> +0.1 -> +0.0

**TEST 1 — opaque water: FLAT, and that kills the transparent-pass hypothesis (mine, and the order's).** Alpha 1,
opaque queue, alpha-test 0.45 so the coverage mask still shapes the pool. It does not rise toward the horizon and it
does not differ from the blended water in any way with a trend. **A real defect did fall out of it:** the environment
was assigned with `RenderSettings.customReflectionTexture` but **without `RenderSettings.defaultReflectionMode =
Custom`**, so `unity_SpecCube0` stayed on whatever the pipeline built at boot — with no skybox in this project, that
is nothing. The environment's contribution to the water went from **-1.4 .. -28.0** to **+14 .. +19 of 255**. Fixed,
and it is why the term was *negative*: the surfaces were being lit by a darker environment than the probe's.
What it did not fix: that contribution is large and still **flat** (+17.9 near edge, +17.7 far edge) on both passes.
Large and view-independent is **ambient**. Not a reflection.

**TEST 2 — the glint renders, at ~4 % of the level its colour calls for.** Rebuilt on `Universal Render Pipeline/
Particles/Unlit` — the shader the rain already uses, so it is provably not stripped (that was the `CornMaze/
StarUnlit` trap) — additive, unlit, queue 3100 so transparent sorting cannot hide it under the water, and oriented
from the meshes' own local bounds instead of an assumed +Y. It moved the water **+0.15 of 255** (from +0.03) with
its own bump in the middle buckets (+0.5, +0.6): it is drawing, in the right place. One switch:
`PuddleDecals.GlintEnabled`, **OFF in the build**.

**ONE HONEST SENTENCE: no frame reads as water.** `m32g-water-opaque.png` shows a blue-grey mottled patch in the
lane — no sheen, no bright horizon band, no moon in it. The glint variant is indistinguishable from the shipped
frame at **+0.15 of 255**. I am not putting two frames side by side and calling it a choice.

## 3. M32f — the water material is right and the environment is not a reflection (`a5f7426`)

Read back at runtime, not what I set: `_EnvironmentReflections=1.00`, no `_ENVIRONMENTREFLECTIONS_OFF`, no
`_SPECULARHIGHLIGHTS_OFF`, `_Smoothness=1.00`, `glossMap=T_Ground_Puddle_M`, `_METALLICSPECGLOSSMAP=True`,
`_SmoothnessTextureChannel=0` — the smoothness rides the **`_M` map's alpha** (0.876 in the water), the albedo
carries the *coverage*. Every switch Ernie suspected is correct. The gradient was flat then too (-1.4..-28.0 for the
environment's own share) and it made the water *darker*.

## 4. M32e — the frame the player actually sees (`61ee0e5`)

`artifacts/review/world/m32e-gameplay-view.png`: eye **1.66 m** (the player's own), puddle **8.09 m** up its own
lane — 79 deg off the water's normal, where a dielectric reflects **35 %** against the 8 % of the earlier flattened
poses. **LANE MET:** a dark lane with lit corn, not a pale ribbon. **WATER NOT MET.**

## 5. M32d — the capture was one image on all six faces (`1a58e10`)

`Camera.RenderToCubemap` returns a single render copied to every face in this player build — proved by writing the
six faces to PNG and hashing them: **byte-identical**. The six views are now rendered explicitly (each face its own
rotation, fog off, into a temporary render texture read straight back — a camera with a target texture never draws to
the screen). **Read back:** `+X 18.1 [0..189] · -X 10.5 · +Y 0.5 · -Y 26.5 · +Z 12.2 · -Z 7.7 · overall 12.57`,
and in the moon's own direction within 8 deg: **mean 22.3, max 108**. The lane control was **VOID**
(`PathMudWetness` rewrites the lane's `_Smoothness` every frame); writer detached, it measures **+2.37**.
`MatteSmoothCeiling 0.20` reported: writes 0.200 now and 0.200 at full mud — **rain cannot walk the lane past the
matte rule.**

## 6. Open — reported, not hidden

- **THE NEXT PASS IS ONE INSTRUMENTED TEST EACH, and both are agent-settleable:**
  1. **Separate the ambient from the reflection.** Now that the environment's contribution is +17 and flat, the
     question is whether the *specular* term exists at all: ablate `_EnvironmentReflections` alone (keeping the
     probe's indirect-diffuse) against the full-probe frame. If the water loses nothing when the environment
     reflections go off, the specular term is zero and the cube's content is irrelevant — and that is a fix, not a
     tuning argument.
  2. **The glint's own material, read back at runtime:** the shader that actually resolved, `_BaseColor` as it
     holds, the sampled alpha of its map at the centre, and the pixels it covers. +0.15 where the arithmetic calls
     for +30 is a factor of ~200, not a subtlety.
- **The lane's ratio depends on the crop** — 1.07x near the puddle (in the damp halo) to 1.56x on the near lane.
- **`Camera.RenderToCubemap` is not to be trusted in this build** — one image, six faces. Use the explicit render.
- **Per-frame writers silently undid earlier harness ablations:** `DuskSky.cs:380` (moon light) and
  `PathMudWetness.Apply` (lane colour + smoothness — twice). All filed VOID. **A runtime state change is not
  evidence until the thing that owns that state has been stopped.**
- **Passes 1–3's paired numbers are VOID or superseded; pass 7's lane control is VOID too** — all labelled.
- **The albedo's alpha cannot be read at runtime** (not Read/Write enabled) — its coverage number rests on the
  builder that wrote it; a design-time read of the PNG would settle it.
- **`AmbientMeanTarget` (49.8) is empirical** — two measured points, linear model; the harness prints both.
- Carried: `CornMaze/StarUnlit` does not resolve in the player build; locked docs still say "Crumb Beast" and FSD
  §17 still describes a noise ground and a sphere Husk (**Ernie applies**); the 60 fps floor is a phone target and
  unmeasured; §25.6's device listen pass needs the phone.

## 7. CAPTURES FOR TODD

    artifacts/review/world/m32e-gameplay-view.png     THE frame — the player's own view, eye 1.66 m, puddle 8.09 m
    artifacts/review/world/m32g-water-opaque.png      TEST 1 — the same view, water forced opaque: a blue-grey mottle
    artifacts/review/world/m32f-glint-on.png          TEST 2 — the glint variant, +0.15 of 255 over the shipped frame
    artifacts/review/world/m32c-water-on.png          the shipped water, same camera
    artifacts/review/world/m32c-water-probe-off.png   the same camera, environment off
    artifacts/review/world/m32c-water-mask.png        the puddles painted, showing the water's footprint
    artifacts/review/world/m32c-lane-mask.png         the lane painted, for the lane/field numbers
    artifacts/review/world/m32d-cube-X.png            THE CAPTURED SKY — the moon, its halo, stars, the lit field
    artifacts/m32c-puddle-report.txt                  every number, the void ones labelled, the capture and the reason
    artifacts/reference/ground-preview-dry.png        the dry ground variant, unchanged, not switched in

## 8. For Todd — the park, and one recommendation

1. **PARKED (needs your models, nothing else blocks it): M33 lanterns and M34 the scarecrow.** A model needs a
   recorded licence line before it ships, and if its terms do not permit distribution inside an App Store build
   that has to be known *before* it is wired. *Recommend: send them whenever; the placeholders stand.*
2. **M32c's water half — my recommendation: one more pass, then it is yours.** The order's two tests ran and both
   came back negative, which narrowed it instead of closing it: the transparent pass is not the fault, and the
   environment's contribution is now +17 but still view-independent. The next pass separates the ambient from the
   reflection and reads the glint's material back — both are named mechanism tests, not opinions. **I am not putting
   a choice to you while the two frames differ by 0.15 of 255**, because picking between a frame and a copy of it is
   not a decision. If you would rather overrule that and take the wet patch as it stands, say so and M32c closes with
   the marker.
3. **The dry ground variant** (`14d7ed6`, preview `artifacts/reference/ground-preview-dry.png`): withered-grass
   field, gravel-road lane, 2K. *Recommend: keep the current sets.* Taste, so the frame is there, not switched in.

## 9. Standing rules in force

`scripts/shoot.sh` is the only launcher: one launch per run, window shrunk into a corner and minimised, app killed
the moment a **fresh** report lands. No window left on Todd's screen — seven frames per run over ten runs, and the
sky capture renders to a render texture so it never touches the screen either. The harness freezes the pose and
stamps every frame with its camera pose, so a non-comparable pair cannot be built by accident.

**Markers:** `/tmp/corn-crew-done.20260925-m32c` ABSENT (correct — M32c is not closed).
**`/tmp/corn-crew-blocked` written** — two agent-settleable defects named, no taste call.

**Working tree left alone as ordered:** the two pre-existing M0 items. Unity re-serialised `ProjectSettings/*` and
the RP assets during builds.

**Flagged, not staged:** unstaged deletions of three tracked `.meta` files (`Assets/GingerbreadMan.meta`,
`Assets/Resources/PerformanceTestRun{Info,Settings}.json.meta`). Not mine, not touched, in no commit of ours. But
`Assets/Resources/PerformanceTestRun*.json` **are rewritten inside `Assets/` every time the built app runs**. A
build should not write into `Assets/`. **No `git add -A` has been run and none should be.**

## 10. Last commits

`e735ea0` M32g (opaque water + `defaultReflectionMode` + the glint on Particles/Unlit) · `a5f7426` M32f (read-back +
six-bucket gradient + the glint fallback) · `61ee0e5` M32e (the player's own view) · `1a58e10` M32d (one image on
six faces) · `dd02504` M32c pass 7 · `29eb9ae` pass 6 · `0172956` pass 5 · `6238881` pass 4 · `3594fad` · `3aa060a`
· `4bb5e37` passes 1–3 · `9085719` M32b · `992da42` M31b · `a2e1119` M32 · `ea81ad8` M31 · `34dbf49` M28 ·
`e460272` M27 · `f46b9e0` M29.
