# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `61ee0e5` (**M32e — the acceptance frame shot at the player's own
view**). M32c OPEN, no done marker._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS volume files1). Unity 6000.3.23f1, URP._
_Order of 2026-09-25: **M32c + M32d + M32e** — then STOP; the scarecrow and the lantern wait on Todd's models.
Mark: `/tmp/corn-crew-done.20260925-m32c` ABSENT (M32c is not closed). **`/tmp/corn-crew-blocked` IS WRITTEN** and
carries the three options. Unity slot free, no app running._

## 1. The order, and where it stands

| # | Item | State |
|---|---|---|
| — | M32 matte ground · M31b lane edge · M32b puddles placed | GREEN `a2e1119` · `992da42` · `9085719` |
| 1 | **M32c — the puddle reads as WATER and the lane stops glowing** | lane half **MET** (the frame shows a dark lane and lit corn — not a pale ribbon). Water half: **not met** — a mottled damp patch, and the Fresnel gradient across the puddle is flat |
| 1b | **M32d — prove the environment capture** | **DONE `1a58e10`** — it was a defect, two of them (§3) |
| 1c | **M32e — shoot the player's own view** | **DONE `61ee0e5`** — pose 1.66 m / 8.09 m; lane beat, water not; the gap is now nameable (§2) |
| — | M33 lanterns · M34 scarecrow | **PARKED** — built on Todd's models; nothing started, nothing waiting on us |

## 2. M32e — the frame the player actually sees

`artifacts/review/world/m32e-gameplay-view.png`: eye **1.66 m** (the player's own — `FirstPersonEyeHeight` 1.62 m,
rig 1.655 m), puddle **8.09 m** up its own lane. Every earlier acceptance frame was shot from a flattened pose I
invented in pass 1 (eye 1.15 m, 6 m back, aimed 18 m down the lane) or from the mirror point — 62 deg off the
water's normal, where a dielectric reflects **8 %**. This pose is 79 deg off it: **35 %**, four times as much.
(The stand is solved from the puddle's own lane axis; the moon's azimuth snapped to a path cell 4 m off in another
direction and the run came back byte-identical to the pass before it.)

    WATER MASK 14,644 px of the frame (1.37 %) — the puddle is properly in frame for the first time
    water 14,644 px, mean G 24.58 of 255, bright(>140) 0 (0.0 %) | lane 1,589 px 34.20 | field 5,113 px 21.93
    -> water/lane 0.72x (the water is DARKER than the lane), lane/field 1.56x
    ENVIRONMENT ON vs OFF on the water: 24.58 vs 34.38, mean |change| 9.95 — and it is a DARKENING
    FRESNEL: near half 24.49 over 7,322 px; far half 24.67 over 7,322 px — FLAT, not rising toward the horizon

**LANE: MET.** The frame shows a dark lane and lit corn; the lane is not the brightest thing in the maze. Both
crops reported rather than the flattering one: near the puddle the lane is 20.61 (1.07x the field); at the player's
view the near-lane crop is 34.20 against a field of 21.93 (1.56x). The near lane is the brightest part of the lane
(no damp halo on it) and the 1.56x is real — but the corn is far brighter than either.

**WATER: NOT MET. One honest sentence: no — the puddle still reads as a mottled damp patch, not water.** And the
Fresnel line says why: at 79 deg off the normal the reflection must rise toward the horizon across the puddle, and
it is flat. The environment's whole measurable effect on the water is a 9.95-of-255 **darkening** — that is its
**ambient** (the cube feeds the scene's ambient SH), not its reflection. A mirror lane with the environment on
gains +2.37. So the cube reaches the materials as light, and barely at all as a reflection.

**The gap is a mechanism, not taste, and it is nameable:** the capture cube is assigned to a Baked probe's
`bakedTexture` and to `RenderSettings.customReflectionTexture` **at runtime**, and under URP neither may re-upload
to the pipeline's probe atlas after startup — which would leave `unity_SpecCube0` on whatever URP built at boot.
Not proved; what *is* proved is that the cube is correct and that the surfaces are not reflecting it.

## 3. M32d — the capture was one image on all six faces (done, `1a58e10`)

`Camera.RenderToCubemap` returns a single render copied to every face in this player build — proved by writing the
six faces to PNG and hashing them: **byte-identical**. The six views are now rendered explicitly (each face its own
camera rotation, up vectors checked against Unity's cube convention, fog off for those frames, into a temporary
render texture read straight back — a camera with a target texture never draws to the screen). **Read back:**
`+X 18.1 [0..189] · -X 10.5 · +Y 0.5 · -Y 26.5 · +Z 12.2 · -Z 7.7 · overall 12.57 of 255`, and **in the moon's own
direction, within 8 deg: mean 22.3, max 108** — the moon where `DuskSky` puts it. `m32d-cube-X.png` shows the moon,
its halo, stars and the lit field. Scaled x3.95 so the cube mean lands on the 49.8 of 255 that holds the ground at
its approved level.
The lane control was **VOID** and pass 7 had used it as evidence (`PathMudWetness` rewrites the lane's `_Smoothness`
every frame); with the writer detached it measures **+2.37**.
Also reported as asked: `MatteSmoothCeiling 0.20`, base `_Smoothness` 1.000, mud 0.024 (max reachable 0.72) —
writes 0.200 now and 0.200 at full mud. **The ceiling holds: rain cannot walk the lane past the matte rule.**

## 4. Open — reported, not hidden

- **The three options for Todd are in §6.2 and in `/tmp/corn-crew-blocked`; the probe-atlas binding gap (§2) is the
  thing I would chase first** — it is a defect an agent can settle, not a look call.
- **The lane's ratio depends on the crop** — 1.07x near the puddle (in the damp halo) to 1.56x on the near lane. If
  the ceiling must hold at the player's own view with margin, the lane's near-field albedo is the lever.
- **`Camera.RenderToCubemap` is not to be trusted in this build** — one image, six faces. Use the explicit render.
- **Per-frame writers silently undid earlier harness ablations:** `DuskSky.cs:380` (moon light) and
  `PathMudWetness.Apply` (lane colour + smoothness — twice). All filed VOID, not physics. **A runtime state change
  is not evidence until the thing that owns that state has been stopped.**
- **Passes 1–3's paired numbers are VOID or superseded; pass 7's lane control is VOID too** — all labelled, none
  dropped.
- **`AmbientMeanTarget` (49.8) is empirical** — two measured points, linear model; the harness prints both.
- **The water mask includes the damp halo** (the marker nulls the normal map too); the core is 7.6 % of the quad.
  The `_M` maps are Read/Write for the read-backs — CPU copies a phone should not keep, one line to remove.
- Carried: `CornMaze/StarUnlit` does not resolve in the player build; locked docs still say "Crumb Beast" and FSD
  §17 still describes a noise ground and a sphere Husk (**Ernie applies**); the 60 fps floor is a phone target and
  unmeasured; §25.6's device listen pass needs the phone.

## 5. CAPTURES FOR TODD

    artifacts/review/world/m32e-gameplay-view.png     THE frame — the player's own view, eye 1.66 m, puddle 8.09 m
    artifacts/review/world/m32c-water-probe-off.png   the same camera, environment off — the pair differs by 9.95/255
    artifacts/review/world/m32c-water-mask.png        the puddles painted, showing the water's footprint
    artifacts/review/world/m32c-lane-mask.png         the lane painted, for the lane/field numbers
    artifacts/review/world/m32d-cube-X.png            THE CAPTURED SKY — the moon, its halo, stars, the lit field
    artifacts/review/world/m32d-cube-{Y,Z}.png        the same capture: the dark zenith, and a second horizon
    artifacts/m32c-puddle-report.txt                  every number, the void ones labelled, the capture and the reason
    artifacts/reference/ground-preview-dry.png        the dry ground variant, unchanged, not switched in

## 6. For Todd — the park, the choice, and one question

1. **PARKED (needs your models, nothing else blocks it): M33 lanterns and M34 the scarecrow.** A model needs a
   recorded licence line before it ships, and if its terms do not permit distribution inside an App Store build
   that has to be known *before* it is wired. *Recommend: send them whenever; the placeholders stand.*
2. **THE CHOICE — M32c's water half. Look at `m32e-gameplay-view.png` first: that is the view you will actually
   have.** The lane is fixed (dark lane, lit corn). The water is not, and the measurement that decides it is that
   the Fresnel gradient across the puddle is flat where a dielectric's must rise. Three options:
   * **(a) A brighter atmospheric moon halo in the sky.** You like your moons loud; a bloom brightens every
     physical reflection at once, and it stays the sky — not an emissive patch on the water (the order forbids
     that, and so do I).
   * **(b) Non-dielectric water** — a controlled glint that reads at any angle. The standard game cheat, and the
     only option that does not depend on the geometry of the view.
   * **(c) Accept the wet patch.** The puddles read as damp gravel in a downpour, M32c closes, and I write the
     marker. *Recommend: before you pick, let me chase the probe-atlas binding gap in §2 — it is a defect, it is
     mine, and (a) and (b) are both cheaper to judge once the reflection is really reaching the water.*
3. **The dry ground variant** (`14d7ed6`, preview `artifacts/reference/ground-preview-dry.png`): withered-grass
   field, gravel-road lane, 2K. *Recommend: keep the current sets.* Taste, so the frame is there, not switched in.

## 7. Standing rules in force

`scripts/shoot.sh` is the only launcher: one launch per run, window shrunk into a corner and minimised, app killed
the moment a **fresh** report lands. No window left on Todd's screen — this pass shot seven frames per run over
five runs and took no window; the sky capture renders to a render texture, so it never touches the screen either.
The harness freezes the pose (`Time.timeScale = 0`, the stand re-asserted) and stamps every frame with its camera
pose, so a non-comparable pair cannot be built by accident.

**Markers:** `/tmp/corn-crew-done.20260925-m32c` ABSENT (correct — M32c is not closed).
**`/tmp/corn-crew-blocked` written** — the three options and the unproven binding gap, one entry.

**Working tree left alone as ordered:** the two pre-existing M0 items. Unity re-serialised `ProjectSettings/*` and
the RP assets during builds.

**Flagged, not staged:** unstaged deletions of three tracked `.meta` files (`Assets/GingerbreadMan.meta`,
`Assets/Resources/PerformanceTestRun{Info,Settings}.json.meta`). Not mine, not touched, in no commit of ours. But
`Assets/Resources/PerformanceTestRun*.json` **are rewritten inside `Assets/` every time the built app runs**. A
build should not write into `Assets/`. **No `git add -A` has been run and none should be.**

## 8. Last commits

`61ee0e5` M32e (the player's own view: lane beat, water not, Fresnel flat) · `1a58e10` M32d (the capture was one
image on six faces; the lane control was void, now valid +2.37) · `dd02504` M32c pass 7 · `29eb9ae` pass 6 ·
`0172956` pass 5 · `6238881` pass 4 · `3594fad` · `3aa060a` · `4bb5e37` passes 1–3 · `ce0eae8` · `b1c36ca` ·
`0e3bc05` · `9085719` M32b · `992da42` M31b · `a2e1119` M32 · `ea81ad8` M31 · `34dbf49` M28 · `e460272` M27 ·
`f46b9e0` M29.
