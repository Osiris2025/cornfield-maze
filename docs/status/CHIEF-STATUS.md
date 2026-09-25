# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `0172956` (M32c pass 5, OPEN). One page, not a log._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS volume files1). Unity 6000.3.23f1, URP._
_Order of 2026-09-25: **M32c only** — then STOP; the scarecrow and the lantern wait on Todd's models, which are
coming. Mark: `/tmp/corn-crew-done.20260925-m32c` (ABSENT — M32c is not closed). Unity slot free, no app running._

## 1. The order, and where it stands

| # | Item | State |
|---|---|---|
| — | M32 matte ground · M31b lane edge · M32b puddles placed | GREEN `a2e1119` · `992da42` · `9085719` |
| 1 | **M32c — the puddle reads as WATER and the lane stops glowing** | **OPEN `0172956`** — 5 passes. **Lane half MET: 1.06x the field, inside the 15 % ceiling, matte by construction.** Water half: reads as water in the frame (46.57, 1.98x the lane, grazing end brighter 52.37 vs 40.77) but carries **no reflected sky** — every material switch verified correct, the probe's capture is the one missing piece. No done marker |
| — | M33 lanterns · M34 scarecrow | **PARKED** — built on Todd's models; nothing started, nothing waiting on us |

## 2. M32c pass 5 — the one switch nobody had read

Five passes argued about whether the reflection probe reaches the water. None had read the switch on the water
that decides it. Read back at runtime from the shipped material:

    WATER ENV READ-BACK: _EnvironmentReflections=1.00, keyword _ENVIRONMENTREFLECTIONS_OFF=False,
    _SpecularHighlights=1.00, keyword _SPECULARHIGHLIGHTS_OFF=False, _Smoothness=1.00,
    glossMap=T_Ground_Puddle_M, _METALLICSPECGLOSSMAP=True

**Every switch is correct** — the water is set to reflect — and the probe still moves it by **+0.00 of 255**
(mean |change| 0.07), while the same probe fails to help the lane (+0.01) even with the lane's lobe and
environment reflections explicitly restored. And `PROBE READ-BACK: the probe has no render texture to read`: a
realtime probe whose `texture` is null never handed anything to the renderer. *(Caveat: Unity does not document
exposing a realtime probe's cubemap through `texture`, so this is strong evidence, not proof — which is why the
material read-backs above matter, and they all pass.)* The reading: **the probe produced no capture.** Five
passes of material work could never have fixed that.

    WATER, on the water's own pixels: 54,404 px, mean G 46.57, bright(>140) 0 (0.0 %)
    LANE,  on the lane's own pixels:  7,279 px, mean G 23.56      FIELD: 5,222 px, mean G 22.32
    ->  water/lane 1.98x,  lane/field 1.06x

**The lane is 1.06x the field — inside the ceiling, no longer the brightest thing on the ground.** (Its mask moved
1,204 → 7,279 px between runs because `PathMudWetness` keeps writing the lane's colour and smoothness as the
storm comes and goes — the surface is not static while it is measured. Both figures are inside the ceiling; this
one rests on the larger sample.)

**Next attempt — one change, not a pass of tuning.** Stop trying to make a realtime probe work: bake the probe
to a cubemap asset (or assign `RenderSettings.customReflectionTexture`) and re-run this same harness. The
`WATER ENV READ-BACK` line and the probe on/off pair over the water's own pixels will say in one run whether the
moon lands in the puddle. If it still does not, the honest answer is that a flat alpha-blended decal at this
angle cannot show a moon disc and the mechanism needs to be a different one — a call for Todd, not another pass.

**Carried from pass 4 (the fix that stopped the glow).** `PathMudWetness` was rewriting the lane's `_BaseColor`
and its `_Smoothness` on every frame, lerping smoothness toward 0.42 as the storm ran — the lane could walk past
the matte rule while the M32 read-back still showed the map's 0.137 (two numbers for one surface, and no
read-back of the map could have caught it). Capped by `PathMudWetness.MatteSmoothCeiling` (0.20). Plus
`Materials.MakeMatte`: lane and field get no specular lobe and no environment reflection at all, structurally.
That took the lane from 1.50x the field to 1.15x and the water region from 92.51 to 46.56.

## 3. The acceptance, line by line (pass 5)

- **Shallow night frame, probe on and off, same camera, water carrying a reflected sky and the lane not:** frame
  provided (`m32c-water-on/off.png`), one frozen pose, pose-stamped — **and the reflected sky is NOT there: the
  probe is worth +0.00 of 255 on the water.** Not claimed, not faked with an emissive patch. This is the one
  unmet line.
- **Water's bright count and mean against the lane's, from the frame:** water 54,404 px, mean G **46.57**,
  bright(>140) **0 (0.0 %)**; lane 7,279 px, mean **23.56** → **water/lane 1.98x**.
- **Lane vs field, same distance: 23.56 against 22.32 = 1.06x** — inside the order's 15 % ceiling, or darker.
- **Fresnel direction is right:** on the water's own pixels the near half means 40.77 and the grazing far half
  52.37 — brighter toward the horizon, falling off as the view steepens, which is what a dielectric does.
- **One honest sentence: does the puddle read as water in that frame? Yes** — a dark wet gravel lane with pale
  cool patches in the rut, brighter at their far end, unmistakably standing water rather than painted spots.
  **Not mirror-like: no glint, no sky in them.** The sheen is a diffuse cool lift, not a specular sparkle. If
  Todd wants glass, that is a different mechanism from the one this order proposed.

**Why no done marker:** the order's fallback clause covers a probe that ships sky-only, not one that ships
nothing. The reflected-sky line cannot be shown, so I will not certify it. The call is Ernie's.

## 4. Open — reported, not hidden

- **The probe produced no capture** (see §2). Everything else on the reflection path is verified on paper: the
  water's switches, the lane's control, the RP asset's probes, the ground's env reflections.
- **Two per-frame writers silently undid harness state changes:** `DuskSky.cs:380` rewrites the moon light's
  intensity every frame; `PathMudWetness.Apply` rewrites the lane's colour and smoothness every frame. Ablations
  on either were VOID and were reported void, not as physics. **A runtime state change is not evidence until the
  thing that owns that state has been stopped** — the same defect as `alphaSource=None`, the copied harness and
  the two-camera A/B, in a fourth form.
- **Passes 1–3's paired numbers are VOID or superseded:** pass 1–2's came from two different cameras and then
  from a geometry classifier that ignored the water's own material; pass 3's 92.51 / 1.50x were real measurements
  of the lane's false highlight, not of water. All labelled, none dropped.
- **The water mask still includes the damp halo** (the marker nulls the normal map too); the core is 7.6 % of the
  quad. The `_M` maps are Read/Write for the read-backs — CPU copies a phone should not keep, one line to remove.
- **The M32 map lift** (binding the `_M` maps raises the frame 33.68 → 49.45, peak unchanged) is still
  unexplained, and may be the same term as the grazing brightness now fixed.
- Carried: `CornMaze/StarUnlit` does not resolve in the player build; locked docs still say "Crumb Beast" and
  FSD §17 still describes a noise ground and a sphere Husk (**Ernie applies** — Harrow may not edit locked docs);
  the 60 fps floor is a phone target and unmeasured; §25.6's device listen pass needs the phone.

## 5. CAPTURES FOR TODD

    artifacts/review/world/m32c-water-on.png        THE acceptance frame — dark wet lane, pale puddles in the rut
    artifacts/review/world/m32c-water-off.png       the same frozen pose with the probe off — worth +0.00/255
    artifacts/review/world/m32c-lane-control.png    the control: lobe and env reflections restored on the lane (+0.01)
    artifacts/review/world/m32c-water-mask.png      the diagnostic: the puddles painted, showing the decal's footprint
    artifacts/review/world/m32c-lane-mask.png       the lane painted — its true footprint, for lane/field
    artifacts/review/world/m32c-water-black.png     the ablation: water with a BLACK albedo and no gloss (3 % of the light)
    artifacts/m32c-puddle-report.txt                every number, the void ones labelled void, the fix and the reason
    artifacts/reference/ground-preview-dry.png      the dry ground variant, unchanged, not switched in

## 6. For Todd — parked, and two questions

1. **PARKED (needs your models, nothing else blocks it): M33 lanterns and M34 the scarecrow.** A model needs a
   recorded licence line before it ships, and if its terms do not permit distribution inside an App Store build
   that has to be known *before* it is wired. *Recommend: send them whenever; the placeholders stand.*
2. **Is M32c done?** The lane no longer glows — 1.06x the field, inside the ceiling you asked for — and the
   puddles read as water in `m32c-water-on.png`: those are the two things you asked for, both measured. What is
   missing is the *mechanism* the order named: the reflection probe produced no capture, so the water's sheen is
   diffuse rather than glassy. *Recommend: look at the frame first. If the puddles read as water to you, the open
   line is a mechanism note, not a defect; if you want glass, say so and I will price the real reflection
   separately — one change, not another pass of tuning.*
3. **The dry ground variant** (`14d7ed6`, preview `artifacts/reference/ground-preview-dry.png`): withered-grass
   field, gravel-road lane, 2K. *Recommend: keep the current sets.* Taste, so the frame is there, not switched in.

## 7. Standing rules in force

`scripts/shoot.sh` is the only launcher: one launch per run, window shrunk into a corner and minimised, app
killed the moment a **fresh** report lands. No window is left on Todd's screen — this pass shot seven frames per
run, in three runs, and took no window. The harness freezes the pose (`Time.timeScale = 0`, the stand
re-asserted) and stamps every frame with its camera pose, so a non-comparable pair cannot be built by accident.

**Markers:** `/tmp/corn-crew-done.20260925-m32c` ABSENT (correct — M32c is not closed). `/tmp/corn-crew-blocked`
ABSENT — nothing needs a human to continue; the next step is named in §2.

**Working tree left alone as ordered:** the two pre-existing M0 items; Ernie's ground art uncommitted as not
mine. Unity re-serialised `ProjectSettings/*` and the RP assets during builds.

**Flagged, not staged:** the working tree carries unstaged **deletions of three tracked `.meta` files** —
`Assets/GingerbreadMan.meta` and `Assets/Resources/PerformanceTestRun{Info,Settings}.json.meta`. Not mine and not
touched; they are not in any commit from this crew. But `Assets/Resources/PerformanceTestRun*.json` themselves
are **rewritten inside `Assets/` every time the built app runs** (mtime tracks each `shoot.sh` run), which is why
those two paths keep changing shape between passes. A build should not write into `Assets/`; worth a line in the
clean-up list. **No `git add -A` has been run and none should be** — it would commit these deletions.

## 8. Last commits

`0172956` M32c pass 5 (the water's env read-back, the probe's empty capture; lane 1.06x) · `6238881` M32c pass 4
(the mud-smoothness clamp + matte by construction; lane 1.15x) · `3594fad` M32c pass 3 (renderer masks, one
frozen pose) · `3aa060a` M32c pass 2 (camera freeze, the void-by-geometry ablation) · `4bb5e37` M32c pass 1 (the
probe) · `207554b`/`a5b3fcf`/`154d229`/`02e1d1d`/`b5df115` status pages · `ce0eae8` M32b pass 4 · `b1c36ca` ·
`0e3bc05` · `9085719` M32b puddles · `992da42` M31b solid lane · `a2e1119` M32 matte ground · `ea81ad8` M31
lane blends by transparency · `09a59c3` matte ground + puddle art (Ernie) · `34dbf49` M28 the scarecrow ·
`e460272` M27 first person · `f46b9e0` M29 ground.