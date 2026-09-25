# Corn Field Maze — Chief Status

**Updated:** 2026-09-24 20:41 EDT — overnight loop, pass 4 of the night
**Milestone:** **M24 is GREEN this pass**; the next pass takes M23 (pick up and throw).
**Project root:** `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS, volume files1). The old files2 tree is retired.
**Unity slot:** free when this pass began; one writer at a time used it, `build-mac.sh` guard intact.

## 1. What is green right now

| M | What | Owner | Evidence |
|---|---|---|---|
| M21 | front end: title, help, intro, pause | Dough+Furrow | `ca83b66` · `artifacts/front-end-title-m21.png` |
| M22 | movement feel: dead zone, look, sprint, no lane auto-centring | Furrow | `7518c78` · `artifacts/m22-feel-report.txt` |
| M20 | the real corn blocks in the maze, CUTOUT leaves, LOD chain, boom fix | Squall+Dough | `7a6a93f` · `artifacts/m20-field-report.txt` · `artifacts/review/world/m20-field-play.png` |
| M25 | dusk, the Halloween sky, the moonrise, cloud occultation | Squall | `874d90f` · `artifacts/m25-sky-report.txt` · `artifacts/review/world/sky-t0.png`, `sky-t40.png` |
| M24 | the rename to the Husk, everywhere in code | Furrow+Rattle | this commit |

M24 verified, and behaviour unchanged:

```
git grep "CrumbBeast|Crumb Beast" over Assets/     -> 0 hits           (no aliases left behind)
shipped Assembly-CSharp.dll: "CrumbBeast" = 0, "HuskChase" = 1         (the cue rename is IN the build)
runtime, built Mac app: "husk instances observed alive during the rise = 1"
                                                                        (the renamed class spawns, by type)
M25 numbers re-run after the rename: night at 40.0 s, occlusion PASS    (no behaviour drift)
```

Renamed: file + class `CrumbBeast.cs` -> `Husk.cs` (with `git mv`, so the `.meta` guid survives), the
`DisplayName` const -> `"the Husk"`, the `Spawn` call in `GameBootstrap`, both diagnostic harnesses, and
`MazeMoodAudio`'s chase cue `ChaseStrings` -> `HuskChase` (source name, synth method, clip name). HUD and
death copy already said "the Husk" — checked, not assumed. The `"Crumb"` parts on its snout are cookie-crumb
freckles and keep their names: that word is crumbs, not the threat.

## 2. In flight / next

Nothing is mid-edit. Next pass starts **M23 (pick up and throw)**, then **M26 (rustle + music off one
threat-distance term)** — the last item in the queue.

## 3. Questions for Todd (morning) — each with my recommendation

1. **Locked docs still name the old threat.** `docs/DECISIONS.md` (lines 8, 42, 69),
   `docs/reference/CORN-FIELD-MAZE-FSD.md` (§8 tables, §9/§14, §23.5, §25.4's own text) and
   `docs/reference/INDEX.md` row 40 still say **Crumb Beast**. They are **locked**, so I did not touch them.
   *Recommend:* Ernie applies the rename there — the code is done, so those docs are now the only place the
   old name survives. Deliberately left alone: `docs/status/ORDERS-20260924.md` (a copy of the mission
   order — a record of what was asked) and `docs/plans/2026-09-17-mvp-plan.md` (a dated plan).
2. **Licences.** `gb_man.fbx` and both corn source models are third-party with licence **unknown**
   (`docs/ASSETS-INVENTED.md`). *Recommend:* confirm before any App Store submission. Nothing else blocks.
3. **The 60 fps floor on the phone.** No device attached, so it is unmeasured. *Recommend:* measure it on
   your iPhone when you are up — `appleDeveloperTeamID` is still empty, so a device build needs that first.
4. **The sky in a locked doc.** Carried over: FSD §25.0 still claims `NightSky.cs` "already carries the
   moon, star drift and the cloud grammar". It did not; M25 built them. *Recommend:* Ernie corrects that row.
5. **`capture.sh` cannot resolve its window** while another app named "Corn Field Maze" is on the machine
   (two passes now); frames currently come from an in-engine `ScreenCapture` shim.
   *Recommend:* Lantern reworks `capture.sh` to prefer the in-engine path.

## Crew

| Name | Handle | Role | State |
|---|---|---|---|
| Harrow | @corn-chief | chief | verified M24 himself; wrote this file |
| Dough | @corn-art | cookie, silhouette, materials | corn materials + prefabs landed (M20); M23 cob next |
| Furrow | @corn-gameplay | controller, maze, HUD, touch | **M24 rename landed**; M23 (throw) next |
| Squall | @corn-world | corn field, sky, dusk, storm | M20 + M25 landed |
| Rattle | @corn-audio | bed, cues, jingle, rustle | **M24 cue rename landed**; M26 next |
| Lantern | @corn-qa | builds, captures, measured evidence | built + verified M24; `capture.sh` rework listed above |

## Decisions made this pass (recorded where the morning reader will find them)

1. **`git mv` for the file**, `.cs` and `.meta` both, so the asset guid survives — a delete+add mints a new
   guid and silently breaks any reference that later points at it.
2. **The class doc's historical note was removed.** "No aliases left behind" is checked by grep, and a
   comment naming the old type would make that check meaningless. The history belongs in this file and in
   the commit message.
3. **`DisplayName` is currently unused** (the HUD hardcodes "the Husk"). Renamed in place because §25.4 names
   it in the sweep; not worth a refactor mid-queue.
4. **No new harness for the rename.** The proof is the runtime husk count the M25 sky harness already
   prints, plus a zero-hit grep and a read of the shipped assembly — cheaper, and less to carry.
5. **One M22-harness gotcha found:** `M22FeelSelfTest` has no `Application.Quit` and does not set
   `runInBackground`, so launched unfocused it never finishes and looks like a hang. My later harnesses do
   both. *Recommend:* fold that in when M22 is next touched, rather than churning it now.

## Blockers

Nothing blocking. Standing, non-blocking: the licence question and the empty `appleDeveloperTeamID` above.
The two pre-existing M0 working-tree items are untouched as ordered: deleted `Assets/GingerbreadMan.meta`,
modified `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`. Unity re-serialised
`ProjectSettings/*.asset` and `Assets/Settings/PC_RPAsset.asset` during the builds — not committed, not this
pass's work. The four `Assets/Resources/PerformanceTestRun*.json` files the builds removed were restored to HEAD.

## CAPTURES FOR TODD

- `artifacts/review/world/sky-t0.png` — **dusk**, warm amber on the corn, sun already below the horizon.
- `artifacts/review/world/sky-t40.png` — **night**, the oversized moon up with cloud across it.
- `artifacts/review/world/m20-field-play.png` — the corn field in the maze.
- `artifacts/review/world/m22-2026-09-24.png` — movement pass, plain launch.
- `artifacts/front-end-title-m21.png` — M21 front end.
- Numbers: `artifacts/m25-sky-report.txt` (now carries the M24 husk-spawn line), `artifacts/m20-field-report.txt`,
  `artifacts/m22-feel-report.txt`.

## Last commits

this commit M24 the Husk rename · `874d90f` M25 dusk/moonrise · `7a6a93f` M20 corn field ·
`7518c78` M22 movement · `ca83b66` M21 front end · `8671f1e` FSD §25.
