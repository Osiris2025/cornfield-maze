# Corn Field Maze — Chief Status

**Updated:** 2026-09-24 20:32 EDT — overnight loop, pass 3 of the night
**Milestone:** **M25 is GREEN this pass**; the next pass takes M24 (the Husk rename).
**Project root:** `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS, volume files1). The old files2 tree is retired.
**Unity slot:** free when this pass began; one writer at a time used it, `build-mac.sh` guard intact.

## 1. What is green right now

| M | What | Owner | Evidence |
|---|---|---|---|
| M21 | front end: title, help, intro, pause | Dough+Furrow | `ca83b66` · `artifacts/front-end-title-m21.png` |
| M22 | movement feel: dead zone, look, sprint, no lane auto-centring | Furrow | `7518c78` · `artifacts/m22-feel-report.txt` |
| M20 | the real corn blocks in the maze, CUTOUT leaves, LOD chain, boom fix | Squall+Dough | `7a6a93f` · `artifacts/m20-field-report.txt` · `artifacts/review/world/m20-field-play.png` |
| M25 | dusk, the Halloween sky, the moonrise, cloud occultation | Squall | this commit · `artifacts/m25-sky-report.txt` · `artifacts/review/world/sky-t0.png`, `sky-t40.png` |

M25 measured on the **built Mac app** (`-skycapture`), frames straight from the engine:

```
t = 0.0 s  DUSK   night01=0.00  sun (1.00,0.62,0.30) intensity 0.55  sun ELEVATION -2.0 deg  starGate 0.00
t = 40.0 s NIGHT  night01=1.00  sun (0.34,0.40,0.58) intensity 0.05  sun ELEVATION -13.0 deg starGate 1.00
moon: elevation 1.0 -> 28.0 deg, angular size 6.6 -> 6.8 deg   (real moon: 0.5 deg — oversized on purpose)
clouds: 1 occultation beat, 13.7 s of the rise with the moon more than half covered, peak occlusion 0.72  PASS
night arrived at t = 40.0 s   (rule: 40 s OR 2 cells in, whichever comes first)
```

Both frames read by eye: t=0 is warm amber light with the corn lit and shadowed; t=40 is cool moonlight,
the oversized moon up with cloud across it, the field lit by the moon and not by the sun.

**The milestone's whole point is the sun elevation line.** The first run measured `sunElevation=38.0 deg` at
BOTH ends — the sun never set — because `DuskSky` had grabbed the storm's *lightning* light as "the sun"
(`FindFirstObjectByType<Light>()` is the wrong question once other lights exist). Fixed, and now measured.

## 2. In flight / next

Nothing is mid-edit. Next pass starts **M24 (the Husk rename)**, then M23 (pick up and throw), then M26
(rustle + music off one threat-distance term).

**What M25 did NOT do, stated rather than implied:** the two frames come from a run of the built app with a
dormant capture shim (`-skycapture`), not from `capture.sh` — that script cannot resolve its window while
another app of the same name is on the machine (see Decisions). The moon's *cloud* art is procedural
torn-noise in an atlas, not hand-painted; it reads as cloud at play distance but it is a first pass.
§23.5's later rule ("the moon is a usable bearing early and a liar later") is untouched — that is a
later-milestone behaviour, not M25's.

## 3. Questions for Todd (morning) — each with my recommendation

1. **Licences.** `gb_man.fbx` and both corn source models are third-party with licence **unknown**
   (`docs/ASSETS-INVENTED.md`). *Recommend:* confirm before any App Store submission. Nothing else blocks.
2. **The 60 fps floor on the phone.** No device attached, so it is unmeasured. *Recommend:* measure it on
   your iPhone when you are up — `appleDeveloperTeamID` is still empty, so a device build needs that first.
3. **The sky in a locked doc.** `docs/reference/CORN-FIELD-MAZE-FSD.md` §25.0 says `NightSky.cs` "already
   carries the moon, star drift and the cloud grammar of §23.5". **It did not** — there was no moon and no
   cloud code anywhere in `Assets/Scripts` (0 matches). M25 built them. *Recommend:* Ernie corrects §25.0's
   row so the next reader is not misled; I did not touch the locked doc.
4. **Boom pull-in under a canopy.** Unchanged from last pass: the boom shortens to as little as 1.6 m when
   the camera would sit in the crop. *Recommend:* keep; reversible in one constant.
5. **The Husk caught a motionless player inside 40 s.** Observed while capturing the sky. Not a defect, but
   it is a data point for how fast the threat closes. *Recommend:* leave for M24/M26.

## Crew

| Name | Handle | Role | State |
|---|---|---|---|
| Harrow | @corn-chief | chief | verified M25 himself; wrote this file |
| Dough | @corn-art | cookie, silhouette, materials | corn materials + prefabs landed (M20) |
| Furrow | @corn-gameplay | controller, maze, HUD, touch | M22 landed; boom canopy guard landed (M20) |
| Squall | @corn-world | corn field, sky, dusk, storm | **M25 dusk + moonrise landed this pass** |
| Rattle | @corn-audio | bed, cues, jingle, rustle | idle; M24 cue names, then M26 |
| Lantern | @corn-qa | builds, captures, measured evidence | built + measured M25; frames via `ScreenCapture` |

## Decisions made this pass (recorded where the morning reader will find them)

1. **One owner per property.** `DuskSky` publishes the calm palette (sun colour/intensity, fog, ambient,
   star gate); `StormWeather` *applies* it, blending calm → storm — and now does so in `LateUpdate` so it
   always runs after `DuskSky.Update`. Two components writing the same light from `Update` is a race, and
   the loser is whichever Unity happens to run second. `DuskSky` owns the sun's **direction** only.
2. **The sun is found by name.** `DuskSky` looks for the directional light named `Sun`, because by install
   time the storm has already added its lightning light.
3. **The dusk clock starts at handover**, not at scene load — otherwise the whole rise plays behind the
   title screen. `Night01 = max(40 s ramp, 2 cells travelled)`, so night arrives at whichever comes first.
4. **Occlusion is geometry, not hope.** A strip is tested with its elevation extent (`Rad x Squash`), not as
   a circle: the first run's circle test peaked at 0.32 while the cloud was visibly covering the moon.
   Three strips are laid across the moon's actual path.
5. Frames come from `ScreenCapture` inside the game. `capture.sh` (System Events, window 1) could not resolve
   its window this pass — two apps named "Corn Field Maze" are on the machine. This is the second pass it
   has failed; **Lantern should rework `capture.sh` to prefer an in-engine capture** rather than depend on
   window resolution.

## Blockers

Nothing blocking. Standing, non-blocking: the licence question and the empty `appleDeveloperTeamID` above.
The two pre-existing M0 working-tree items are untouched as ordered: deleted `Assets/GingerbreadMan.meta`,
modified `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`. Unity re-serialised
`ProjectSettings/*.asset` and `Assets/Settings/PC_RPAsset.asset` during the builds — not committed, not this
pass's work. The four `Assets/Resources/PerformanceTestRun*.json` files the builds removed were restored to HEAD.

## CAPTURES FOR TODD

- `artifacts/review/world/sky-t0.png` — **dusk**, warm amber on the corn, sun already below the horizon.
- `artifacts/review/world/sky-t40.png` — **night**, the oversized moon up with cloud across it, corn lit by
  moonlight. The two frames are the §25.5 beat list, in order.
- `artifacts/review/world/m20-field-play.png` — the corn field in the maze (M20).
- `artifacts/review/world/m22-2026-09-24.png` — movement pass, plain launch.
- `artifacts/front-end-title-m21.png` — M21 front end.
- Numbers: `artifacts/m25-sky-report.txt`, `artifacts/m20-field-report.txt`, `artifacts/m22-feel-report.txt`.

## Last commits

this commit M25 dusk/moonrise · `7a6a93f` M20 corn field · `7518c78` M22 movement · `ca83b66` M21 front end ·
`8671f1e` FSD §25 · `dd4f529` corn block set.
