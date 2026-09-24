# Corn Field Maze — Chief Status

**Updated:** 2026-09-24 22:25 EDT — overnight loop, pass 1 of the night
**Milestone:** M22 (movement — the FPS/touch feel contract) — **DONE this pass**; next pass takes M20.
**Project root:** `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS). The old files2 tree is retired.

## Correction to the previous status file (it would have misled every worker)

The 2026-09-17 file claims **the Unity slot is held by an open editor. That is false now** — no editor
holds this project, and build → launch → capture has been proven end to end. Everything else in it is
superseded by the M0 / M21 work committed since (`ca83b66` front end, `dd4f529` corn blocks, `8671f1e` FSD §25).

## Crew

| Name | Handle | Role | State |
|---|---|---|---|
| Harrow | @corn-chief | chief | running M22; verifies each claim himself |
| Dough | @corn-art | cookie, silhouette, materials | M0/M21 landed; queued M20 blocks / M23 cob |
| Furrow | @corn-gameplay | controller, maze, HUD, touch, throw | **M22 this pass** |
| Squall | @corn-world | corn field, sky, dusk, storm | queued: M20, then M25 |
| Rattle | @corn-audio | bed, cues, jingle, rustle | queued: M26 (+ M24 cue names) |
| Lantern | @corn-qa | builds, captures, measured evidence | builds + captures this pass |

## Queue (FSD §25) — one milestone per pass, in order

| M | What | Owner | State |
|---|---|---|---|
| M21 | front end: title, help, intro, pause | Dough+Furrow | **DONE** `ca83b66` · frame `artifacts/front-end-title-m21.png` |
| M22 | movement feel: dead zone, look, sprint, no lane auto-centring | Furrow | **in flight — see the pass result below** |
| M20 | the real corn blocks in the maze, CUTOUT leaves, LOD chain | Squall+Dough | not started |
| M25 | dusk, Halloween sky, moonrise | Squall | not started |
| M24 | the rename to the Husk, everywhere | Furrow+Rattle | not started |
| M23 | pick up and throw (the cob) | Furrow+Dough | not started |
| M26 | rustle + music off one threat-distance term | Rattle+Squall | not started |

## M22 — what was MEASURED in the code before touching it

- `MobileControls.ReadTouches` had **no dead zone at all** — `ClampMagnitude(raw / radius, 1f)` fed
  straight into `Move`; §25.2 requires 0.12 ramped to full by 0.30.
- `FarmWalkerController.CorridorMove` returns **only a cardinal unit vector**. A deliberate sideways
  push scores ≈0 against every corridor direction and `CanStep` refuses the wall, so the move is
  `Vector3.zero` — **pushing sideways moved the player nowhere at all.**
- `ConstrainToPath` then **hard-snapped to the lane centreline** (`pos.z = center.z` / `pos.x =
  center.x`) and only afterwards clamped to ±0.42 m. That is the auto-centring §25.2 lands.
- Wall cells **do** carry colliders (`MazeWorldBuilder.AddCornBlock`: `BoxCollider` 3.68 m × 2.7 m at
  1.35 m), so the lane can be held by the world instead of by the controller — which is why the
  centring snap can go without the player escaping into the corn.
- Look-drag is already "anywhere on the right half" (`pos.x >= Screen.width * 0.46f` — no fixed
  look-pad), and sprint already exists (hold RUN → 7.4). Those two are a check, not a build.

## Blockers

Nothing blocking tonight. Standing, non-blocking:
1. `gb_man.fbx` licence/provenance still unknown (App Store only) — `docs/ASSETS-INVENTED.md`.
2. `appleDeveloperTeamID` empty / automatic signing off — real only at the iPhone device build.
3. Two pre-existing working-tree items on the M0 path are left untouched as ordered: deleted
   `Assets/GingerbreadMan.meta`, modified `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`.

## CAPTURES FOR TODD

- `artifacts/front-end-title-m21.png` — the title screen with the corn field alive behind it (M21).
- M22's frame + measured numbers: see the pass result at the foot of this file.

## Last commits

`b02bd55` overnight loop cap · `ca83b66` M21 front end · `8671f1e` FSD §25 · `dd4f529` corn block set.

## Pass result — M22 (movement feel) — DONE this pass

**Verified on the BUILT Mac app, not in the editor.** The numbers come from `-m22selftest`, which drives
the real `FarmWalkerController` inside the real maze — full report in `artifacts/m22-feel-report.txt`.

| §25.2 check | Measured | Verdict |
|---|---|---|
| Stick dead zone | 0.05 → 0.000 and 0.12 → 0.000 move; 0.21 → 0.500; 0.30 → 1.000 | **PASS** (0.12 ramped to full by 0.30) |
| A deliberate sideways push | moved **0.420 m** — it moved **0.000 m** before this pass | **PASS** (the push used to be refused outright) |
| Lane bound | held at 0.420 m = `LaneHalf` | **PASS** — the player never leaves the lane |
| Release, no yank | stayed at 0.420 m (change 0.000 m) | **PASS** — the old code snapped to the centreline |
| Walk / sprint | **4.38 / 7.40 m/s** against constants 4.40 / 7.40 | **PASS** |
| Look | 0.14°/pt (exposed), invert-Y toggle, drag anywhere right of x ≥ 598 px of 1300 | exposed |

Frame: `artifacts/review/world/m22-2026-09-24.png` — **2556x1179 plain launch** of the built app: title
screen with the corn field alive behind it, no harness, no other window on top.

**Defects found and fixed while verifying:**
1. `MobileControls` had **no dead zone at all** — raw displacement went straight into `Move`.
2. `CorridorMove` refused any input no corridor answered: a sideways push moved the player 0.000 m.
3. `ConstrainToPath` **hard-snapped to the lane centreline** — the auto-centring §25.2 lands.
4. `GameFrontEnd.Instance` was **declared and read but never assigned** (permanently null, so nothing
   outside `Create` could reach the front end). Fixed in `Create`.

`M22FeelSelfTest.cs` ships in the build but is dormant unless launched with `-m22selftest`.

**Deliberately NOT done this pass:**
- **The camera boom fix.** §25.2 ties it to M20 — a boom tuned against the placeholder corn is tuned
  against the wrong world. Hook for the M20 pass: the boom in `FarmWalkerController` (`boom`/`desired`).
- **The 60 fps floor is not claimed.** It is a *phone* target, no device is attached, and Mac frame time
  is not evidence for it. It is measured when M20's field and a device exist.
