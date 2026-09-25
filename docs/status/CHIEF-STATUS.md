# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `a5f7426` (**M32f — the environment arrives as ambient: measured;
the glint fallback built and not yet rendering**). M32c OPEN, no done marker._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS volume files1). Unity 6000.3.23f1, URP._
_Order of 2026-09-25: **M32c + M32d + M32e + M32f** — then STOP; the scarecrow and the lantern wait on Todd's
models. Mark: `/tmp/corn-crew-done.20260925-m32c` ABSENT (M32c is not closed). **`/tmp/corn-crew-blocked` IS
WRITTEN** — now naming two agent-settleable defects, not a taste call. Unity slot free, no app running._

## 1. The order, and where it stands

| # | Item | State |
|---|---|---|
| — | M32 matte ground · M31b lane edge · M32b puddles placed | GREEN `a2e1119` · `992da42` · `9085719` |
| 1 | **M32c — the puddle reads as WATER and the lane stops glowing** | lane half **MET** (dark lane, lit corn, not a pale ribbon). Water half: **not met** — see §2 |
| 1b | **M32d — prove the environment capture** | **DONE `1a58e10`** — it was a defect, two of them |
| 1c | **M32e — shoot the player's own view** | **DONE `61ee0e5`** — pose 1.66 m / 8.09 m; lane beat, water not |
| 1d | **M32f — the ambient diagnosis, read back and measured; build the glint fallback** | **DONE `a5f7426`** — every switch correct, the gradient still flat, the fallback built but dark: two defects, both agent-settleable |
| — | M33 lanterns · M34 scarecrow | **PARKED** — built on Todd's models; nothing started, nothing waiting on us |

## 2. M32f — the water material is right and the environment still is not a reflection

**Read back at runtime, not what I set:** `_EnvironmentReflections=1.00`, no `_ENVIRONMENTREFLECTIONS_OFF`, no
`_SPECULARHIGHLIGHTS_OFF`, `_Smoothness=1.00`, `glossMap=T_Ground_Puddle_M`, `_METALLICSPECGLOSSMAP=True`,
`_SmoothnessTextureChannel=0`. So the smoothness rides in the **`_M` map's alpha** (0.876 inside the water; the
albedo carries the *coverage*, a different map — the albedo's alpha could not be read at runtime, that texture is
not Read/Write). Every switch Ernie suspected is **correct**, and it still does not reflect.

**The decisive measurement — six buckets across the puddle, 6.0 m to 10.3 m past the eye, on the water's own px:**

    shipped frame:                     25.3 -> 23.5 -> 24.9 -> 25.8 -> 22.1 -> 25.2   of 255   (FLAT)
    the environment's OWN contribution
    (frame ON minus frame OFF):        -1.4 -> -13.7 -> -8.3 -> -8.1 -> -28.0 -> -14.8  of 255

A dielectric's reflection must **rise** toward the horizon. It is flat, and the environment's own contribution is
**negative and deepens with distance** — so the environment reaches the water as **ambient** (view-independent by
definition) and makes it slightly *darker*. **Remaining cause, named rather than asserted:** the puddle is an
**alpha-blend (transparent)** surface, and URP does not hand a transparent surface the probe's specular cube the way
it does an opaque one. **The one-line test that settles it: force the puddle opaque for a single frame and re-read
this same gradient.**

**The glint fallback is built and does not light up.** Four additive quads on the puddle prefab (the puddle's own
rut mesh, a procedural moon streak, `_SrcBlend=SrcAlpha _DstBlend=One`, `_EmissionMap` + `_EMISSION`), one switch:
`PuddleDecals.GlintEnabled`. Same camera: water 24.60 against the shipped 24.57 (**+0.03 of 255**), pixels above
140: **0 with it ON, 0 without**. Absent, not subtle — a defect in the variant. Two candidates: the emissive term
not reaching the shader variant, or the additive quad drawn under the decal by transparent sorting. **No frame was
handed to Todd as a choice, because the two frames do not differ.**

## 3. M32e — the frame the player actually sees (`61ee0e5`)

`artifacts/review/world/m32e-gameplay-view.png`: eye **1.66 m** (the player's own), puddle **8.09 m** up its own
lane — 79 deg off the water's normal, where a dielectric reflects **35 %**, against the 8 % my earlier flattened
poses measured. **LANE MET:** the frame is a dark lane with lit corn, not a pale ribbon. **WATER NOT MET:** the
puddle is a mottled damp patch, and the Fresnel slope is flat.

## 4. M32d — the capture was one image on all six faces (`1a58e10`)

`Camera.RenderToCubemap` returns a single render copied to every face in this player build — proved by writing the
six faces to PNG and hashing them: **byte-identical**. The six views are now rendered explicitly (each face its own
rotation, fog off for those frames, into a temporary render texture read straight back — a camera with a target
texture never draws to the screen). **Read back:** `+X 18.1 [0..189] · -X 10.5 · +Y 0.5 · -Y 26.5 · +Z 12.2 ·
-Z 7.7 · overall 12.57`, and in the moon's own direction within 8 deg: **mean 22.3, max 108**. `m32d-cube-X.png`
shows the moon, its halo, stars and the lit field. The lane control was **VOID** (`PathMudWetness` rewrites the
lane's `_Smoothness` every frame); with the writer detached it measures **+2.37**. `MatteSmoothCeiling 0.20`
reported: writes 0.200 now and 0.200 at full mud — **rain cannot walk the lane past the matte rule.**

## 5. Open — reported, not hidden

- **THE NEXT PASS IS ONE TEST:** force the puddle opaque for a frame and re-read the gradient (§2). If it rises, the
  transparent pass is the mechanism and the fix is a real decision (opaque water loses the coverage blend; the glint
  is the alternative). If it does not rise, the probe's cube is not reaching an opaque surface either — a different
  fix.
- **The glint variant does not render** — two named candidates, one instrumentation pass each.
- **The lane's ratio depends on the crop** — 1.07x near the puddle (in the damp halo) to 1.56x on the near lane.
- **`Camera.RenderToCubemap` is not to be trusted in this build** — one image, six faces. Use the explicit render.
- **Per-frame writers silently undid earlier harness ablations:** `DuskSky.cs:380` (moon light) and
  `PathMudWetness.Apply` (lane colour + smoothness — twice). All filed VOID. **A runtime state change is not
  evidence until the thing that owns that state has been stopped.** Harness frame 3 is the glint pair now; the
  moon-light ablation it used to hold proved nothing.
- **Passes 1–3's paired numbers are VOID or superseded; pass 7's lane control is VOID too** — all labelled.
- **The albedo's alpha cannot be read at runtime** (not Read/Write enabled) — its coverage number rests on the
  builder that wrote it; a design-time read of the PNG would settle it.
- **`AmbientMeanTarget` (49.8) is empirical** — two measured points, linear model; the harness prints both.
- Carried: `CornMaze/StarUnlit` does not resolve in the player build; locked docs still say "Crumb Beast" and FSD
  §17 still describes a noise ground and a sphere Husk (**Ernie applies**); the 60 fps floor is a phone target and
  unmeasured; §25.6's device listen pass needs the phone.

## 6. CAPTURES FOR TODD

    artifacts/review/world/m32e-gameplay-view.png     THE frame — the player's own view, eye 1.66 m, puddle 8.09 m
    artifacts/review/world/m32f-glint-on.png          the glint variant — and it is indistinguishable from the above
    artifacts/review/world/m32c-water-probe-off.png   the same camera, environment off
    artifacts/review/world/m32c-water-mask.png        the puddles painted, showing the water's footprint
    artifacts/review/world/m32c-lane-mask.png         the lane painted, for the lane/field numbers
    artifacts/review/world/m32d-cube-X.png            THE CAPTURED SKY — the moon, its halo, stars, the lit field
    artifacts/m32c-puddle-report.txt                  every number, the void ones labelled, the capture and the reason
    artifacts/reference/ground-preview-dry.png        the dry ground variant, unchanged, not switched in

## 7. For Todd — the park, and one recommendation

1. **PARKED (needs your models, nothing else blocks it): M33 lanterns and M34 the scarecrow.** A model needs a
   recorded licence line before it ships, and if its terms do not permit distribution inside an App Store build
   that has to be known *before* it is wired. *Recommend: send them whenever; the placeholders stand.*
2. **M32c's water half — my recommendation is: let me have one more pass before you pick anything.** Your instinct
   was right twice (the capture was empty; the environment was arriving as ambient) and both are now measured
   rather than argued. What is left is a named mechanism test — opaque water, one frame — plus a glint that is
   built but dark. **I am not putting a choice to you until the two frames actually differ**, because picking
   between a frame and a copy of it is not a decision. If you would rather overrule that and take the wet patch as
   it stands, say so and M32c closes with the marker.
3. **The dry ground variant** (`14d7ed6`, preview `artifacts/reference/ground-preview-dry.png`): withered-grass
   field, gravel-road lane, 2K. *Recommend: keep the current sets.* Taste, so the frame is there, not switched in.

## 8. Standing rules in force

`scripts/shoot.sh` is the only launcher: one launch per run, window shrunk into a corner and minimised, app killed
the moment a **fresh** report lands. No window left on Todd's screen — seven frames per run over eight runs, and the
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

## 9. Last commits

`a5f7426` M32f (read-back + six-bucket gradient + the glint fallback) · `61ee0e5` M32e (the player's own view) ·
`1a58e10` M32d (one image on six faces) · `dd02504` M32c pass 7 · `29eb9ae` pass 6 · `0172956` pass 5 · `6238881`
pass 4 · `3594fad` · `3aa060a` · `4bb5e37` passes 1–3 · `9085719` M32b · `992da42` M31b · `a2e1119` M32 ·
`ea81ad8` M31 · `34dbf49` M28 · `e460272` M27 · `f46b9e0` M29.
