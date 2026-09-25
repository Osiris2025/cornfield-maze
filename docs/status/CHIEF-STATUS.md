# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `7078010` **M32c CLOSE-OUT — Todd accepted the puddles as dark
wet patches. M32c is CLOSED.** Milestone 1 of the order is DONE; the crew stops here._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS volume files1). Unity 6000.3.23f1, URP._
_Marks: **`/tmp/corn-crew-done.20260925-m32c` WRITTEN** (M32c closed). `/tmp/corn-crew-blocked` CLEARED by
overwriting it with "CLEARED … not blocked" — marker files are never deleted by this crew. Unity slot free, no app
running, no window on Todd's screen._

## 1. The order, and where it stands

| # | Item | State |
|---|---|---|
| — | M32 matte ground · M31b lane edge · M32b puddles placed | GREEN `a2e1119` · `992da42` · `9085719` |
| 1 | **M32c — the puddle reads as WATER and the lane stops glowing** | **CLOSED 2026-09-25** — lane half **MET**; water half **accepted as a dark wet patch by Todd's call** (option 1) |
| 1b–1e | M32d capture · M32e player's view · M32f ambient read-back · M32g opaque + glint | **DONE** `1a58e10` · `61ee0e5` · `a5f7426` · `e735ea0` |
| 1f | **close-out** | **DONE `7078010`** — water variant kept, markers set, the two pipeline bugs recorded below |
| — | M33 lanterns · M34 scarecrow | **PARKED** — built on Todd's models; nothing started, nothing waiting on us |

## 2. WHAT SHIPS, AND HOW TO UNDO IT

The frame Todd judged (`artifacts/review/world/m32g-water-opaque.png`) was shot with the water **opaque and
alpha-tested**, so that is the water that ships — the look he saw is the look in the build. Proved, not asserted:
the shipped puddle material reads back as `renderQueue=2000 renderType=Opaque _Surface=0 _SrcBlend=1 _DstBlend=0
_ZWrite=1 _SURFACE_TYPE_TRANSPARENT=False` with the sheen's data still bound (`glossMap=T_Ground_Puddle_M`,
`_Smoothness=1.00`, `_EnvironmentReflections=1.00`), and the shipped frame matches the harness-forced one bucket for
bucket — 25.3/20.4/25.9/26.2/21.5/26.5 against 25.3/20.4/25.9/26.1/21.4/26.5, five buckets at **0.0**, one at
**−0.1**.

- **Revert the water: one line** — `Materials.WaterOpaque = false` (restores the alpha blend, which keeps the damp
  halo; the accepted variant clips it at 0.45 so the coverage mask does the shaping). Frame of the shipped water:
  `artifacts/review/world/m32c-water-shipped-accepted.png`.
- **The puddle set is one deletion** — four quads under a single parent named `Puddles` (`PuddleDecals.Build`),
  4.4 x 1.8 m, elongated along the lane, inside the 2.08 m lane width, never tiled, never on the field.
- **The glint is not built.** `PuddleDecals.GlintEnabled` is false and the shipping path never spawns one — only the
  harness ever asked. `Materials.Glint()` and the spawn code stay as the one-line route if he changes his mind.
- **Lane unchanged and MET** — dark lane, lit corn, never the brightest thing in the maze.

## 3. THE TWO PIPELINE BUGS — WORTH MORE THAN THE PUDDLE

Both were latent, both made a physically sensible surface behave impossibly, and both cost passes to find. Recorded
in the words that make them reusable:

1. **`alphaSource = None` throws away the alpha that carries smoothness.** The ground importer read the derived maps
   without their alpha, so a map whose alpha *is* the smoothness arrived matte — and the floor shipped looking
   "reflective" while every number said otherwise. **Rule: anything that carries data in an alpha channel has to be
   read back, not trusted** — at import time as well as at runtime.
2. **`RenderSettings.customReflectionTexture` set without `RenderSettings.defaultReflectionMode = Custom` leaves
   `unity_SpecCube0` on the boot-time probe.** With no skybox in this project that probe is *nothing*, so the
   environment arrived as indirect diffuse only — and because the probe's ambient was darker than the boot probe's,
   the water's environment term came out **negative and deepening** (−1.4 .. −28.0 of 255) instead of adding light.
   Setting the mode flipped it to **+14 .. +19** immediately. **Rule: a runtime-assigned environment map needs the
   default-reflection mode set too, or the shader keeps sampling whatever the pipeline built at startup.**
3. **`Camera.RenderToCubemap` writes ONE image to all six faces in this player build** — proved by hashing the six
   face PNGs: byte-identical. Render the six views explicitly into a temporary render texture instead; a camera with
   a target texture never draws to the screen, which also keeps windows off Todd's desk.
4. **A per-frame writer silently voided two ablations** (`DuskSky.cs:380` moon light; `PathMudWetness.Apply` lane
   colour + smoothness, twice). **A runtime state change is not evidence until the thing that owns that state has
   been stopped.**

**NOTE AGAINST M33 — the lantern glass will want exactly this path.** Lantern glass is the next cheap reflective
surface in this game and it will meet all four of these the moment anyone wires a cubemap to it at runtime. Start
from `ReflectionProbes.Refresh` (which now sets the mode) and read the material back before believing any number.

## 4. Open — reported, not hidden

- **The water's sheen is present but not visible, and that is now the accepted state:** a night sky this dark
  (capture mean **12.6 of 255**, **22** in the moon's own direction) reflected at the angles this camera sees is not
  worth many pixels. The physics is understood; the pixels are not there. Todd chose to spend the effort elsewhere.
- **`Camera.RenderToCubemap` is not to be trusted in this build** — one image, six faces. Use the explicit render.
- **VOID or superseded, all labelled:** M32c passes 1–3 paired numbers, pass 7's lane control, and the M32g
  "opaque MINUS blended" line (both frames are now the same state — which is itself the close-out proof).
- **The lane's ratio depends on the crop** — 1.07x near the puddle, 1.56x on the near lane, both inside the rule.
- **The albedo's alpha cannot be read at runtime** (not Read/Write enabled) — its coverage number rests on the
  builder that wrote it; a design-time read of the PNG would settle it.
- **`AmbientMeanTarget` (49.8) is empirical** — two measured points, linear model; the harness prints both.
- Carried, not ours: `CornMaze/StarUnlit` does not resolve in the player build; locked docs still say "Crumb Beast"
  and FSD §17 still describes a noise ground and a sphere Husk (**Ernie applies**); the 60 fps floor is a phone
  target and unmeasured; §25.6's device listen pass needs the phone; the dry ground variant (`14d7ed6`) is a taste
  call — preview `artifacts/reference/ground-preview-dry.png`, not switched in.

## 5. CAPTURES FOR TODD

    artifacts/review/world/m32c-water-shipped-accepted.png  THE SHIPPED WATER — the accepted variant, read back
    artifacts/review/world/m32g-water-opaque.png            the frame he judged (and accepted)
    artifacts/review/world/m32e-gameplay-view.png           the player's own view, eye 1.66 m, puddle 8.09 m
    artifacts/review/world/m32d-cube-X.png                  the captured sky — the moon, its halo, stars, the field
    artifacts/review/world/m32c-water-mask.png              the puddles painted, showing the water's footprint
    artifacts/review/world/m32c-lane-mask.png               the lane painted, for the lane/field numbers
    artifacts/m32c-puddle-report.txt                        every number, the void ones labelled, the close-out
    artifacts/reference/ground-preview-dry.png              the dry ground variant, unchanged, not switched in

## 6. Standing rules in force

`scripts/shoot.sh` is the only launcher: one launch per run, window shrunk into a corner and minimised, app killed
the moment a **fresh** report lands. No window left on Todd's screen — seven frames per run across eleven runs, and
the sky capture renders to a render texture so it never touches the screen either. The harness freezes the pose and
stamps every frame with its camera pose, so a non-comparable pair cannot be built by accident.

**Marker files are never deleted by this crew** — `/tmp/corn-crew-blocked` was cleared by overwriting it, not by
deleting it.

**Working tree left alone as ordered:** the two pre-existing M0 items. Unity re-serialised `ProjectSettings/*` and
the RP assets during builds. **Flagged, not staged:** three tracked `.meta` deletions (`Assets/GingerbreadMan.meta`,
`Assets/Resources/PerformanceTestRun{Info,Settings}.json.meta`), and `Assets/Resources/PerformanceTestRun*.json`
**are rewritten inside `Assets/` every time the built app runs** — a build should not write into `Assets/`. **No
`git add -A` has been run and none should be.**

## 7. Last commits

`7078010` M32c CLOSE-OUT (the accepted water variant + the read-back proof) · `9c25de4` CHIEF-STATUS M32g page ·
`e735ea0` M32g (opaque water, `defaultReflectionMode`, the glint on Particles/Unlit) · `a5f7426` M32f · `61ee0e5`
M32e · `1a58e10` M32d · `dd02504` M32c pass 7 · `29eb9ae` pass 6 · `0172956` pass 5 · `6238881` pass 4 ·
`3594fad` · `3aa060a` · `4bb5e37` passes 1–3 · `9085719` M32b · `992da42` M31b · `a2e1119` M32 · `ea81ad8` M31 ·
`34dbf49` M28 · `e460272` M27 · `f46b9e0` M29.
