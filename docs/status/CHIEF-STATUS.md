# Corn Field Maze — Chief Status

**Updated:** 2026-09-24 20:25 EDT — overnight loop, pass 2 of the night
**Milestone:** **M20 is GREEN this pass**; the next pass takes M25 (dusk sky).
**Project root:** `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS, volume files1). The old files2 tree is retired.
**Unity slot:** free when this pass began; one writer at a time used it, `build-mac.sh` guard intact.

## 1. What is green right now

| M | What | Owner | Evidence |
|---|---|---|---|
| M21 | front end: title, help, intro, pause | Dough+Furrow | `ca83b66` · `artifacts/front-end-title-m21.png` |
| M22 | movement feel: dead zone, look, sprint, no lane auto-centring | Furrow | `7518c78` · `artifacts/m22-feel-report.txt` · `artifacts/review/world/m22-2026-09-24.png` |
| M20 | the real corn blocks in the maze, CUTOUT leaves, LOD chain, boom fix | Squall+Dough | this commit · `artifacts/m20-field-report.txt` · `artifacts/review/world/m20-field-play.png` |

M20 measured on the **built Mac app** (`-m20selftest`), not in the editor:

```
corn block instances placed = 1144    (expected 4 x 286 wall cells = 1144)      PASS
other LOD groups            = 0       (the primitive scatter is fully replaced) PASS
corn LOD0 triangles         = 10,244,520 = 1144 x 8955  (§24.2 predicted 10.2 M) PASS
frame time (MAC, context)   = avg 3.14 ms, p95 6.68 ms, worst 14.80 ms
camera inside the crop      = 0 of 954 samples                                  PASS
```

The camera line is the **§25.2 5.2 m boom defect**: with the leaves in, the boom parked the camera inside
the crop. The frames were read by eye too: leafy corn with cut-out leaves and cobs, **no magenta, no white
quads, no solid slabs**, corridor clear, the corn walls read as a maze. Import and factory state was read
back rather than assumed: normal map now `NormalMap` + non-sRGB; both materials `URP/Lit` with
`_ALPHATEST_ON`, cutoff 0.5, instancing on; every prefab `LOD0 8955 / LOD1 4476 / LOD2 1118`.

## 2. In flight / next

Nothing is mid-edit. Next pass starts **M25 (dusk, the Halloween moonrise)**, then M24 (the Husk rename),
M23 (pick up and throw), M26 (rustle + music off one threat-distance term).

**What M20 did NOT do, stated rather than implied:** the 60 fps floor is still unmeasured — it is a **phone**
target, no device is attached, and the Mac frame time above is context, not that claim. The LOD transition
heights (0.50 / 0.16 / 0.02) are a first pass and want an eye. The corn is soft at arm's length because the
source textures are small (§24.4 — accepted; a crisper atlas is a separate decision).

## 3. Questions for Todd (morning) — each with my recommendation

1. **Licences.** `gb_man.fbx` and both corn source models are third-party with licence **unknown**
   (`docs/ASSETS-INVENTED.md`). *Recommend:* confirm before any App Store submission. Nothing else blocks.
2. **The 60 fps floor on the phone.** No device attached, so it is unmeasured. *Recommend:* measure it on
   your iPhone when you are up — `appleDeveloperTeamID` is still empty, so a device build needs that first.
3. **Corn crispness.** Source textures are 256×1024 / 512×512, so corn is soft close up. *Recommend:* ship
   as is; treat a crisper atlas as its own milestone (it changes all six blocks and their LODs).
4. **Boom pull-in under a canopy.** The boom now shortens to as little as 1.6 m when the camera would sit in
   the crop. *Recommend:* keep — the standard third-person answer, and reversible in one constant.

## Crew

| Name | Handle | Role | State |
|---|---|---|---|
| Harrow | @corn-chief | chief | verified M20 himself; wrote this file |
| Dough | @corn-art | cookie, silhouette, materials | corn materials + prefabs landed (M20) |
| Furrow | @corn-gameplay | controller, maze, HUD, touch | M22 landed; boom canopy guard landed (M20) |
| Squall | @corn-world | corn field, sky, dusk, storm | `MazeWorldBuilder` corn swap landed (M20); M25 next |
| Rattle | @corn-audio | bed, cues, jingle, rustle | idle this pass; M24 cue names, then M26 |
| Lantern | @corn-qa | builds, captures, measured evidence | built + measured M20; frames via `ScreenCapture` |

## Decisions made this pass (recorded where the morning reader will find them)

1. Corn FBX import `materialImportMode` → **None**; the two materials became real assets in
   `Assets/Corn/Materials/`. The FBX stays byte-identical to what the generator wrote, so regenerating the
   blocks cannot lose work.
2. Prefabs live in `Assets/Resources/Corn/` and carry the LOD chain. The blocks stay in
   `Assets/Corn/Blocks/` because the generator writes there — moving them would break
   `scripts/corn_block_build.py`.
3. Slot order differs per block (01/04/05/06 = `[Maize, corn]`, 02/03 = `[corn, Maize]`), so materials are
   assigned by the **texture identity** of the FBX's own material, never by slot index.
4. The boom guard reads the **maze**, not physics: the leaf cards carry no colliders, so a cast cannot see
   the canopy.
5. Frames are taken with `ScreenCapture` inside the game. The OS window capture failed this pass because two
   apps named "Corn Field Maze" were on the machine and System Events could not resolve the window.

## Blockers

Nothing blocking. Standing, non-blocking: the licence question and the empty `appleDeveloperTeamID` above.
The two pre-existing M0 working-tree items are untouched as ordered: deleted `Assets/GingerbreadMan.meta`,
modified `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`. Unity also re-serialised
`ProjectSettings/*.asset` and `Assets/Settings/PC_RPAsset.asset` during this pass's builds — not committed,
not M20's work. The four `Assets/Resources/PerformanceTestRun*.json` files the builds removed were restored
to HEAD.

## CAPTURES FOR TODD

- `artifacts/review/world/m20-field-play.png` — **the corn field in the maze**, third-person, cookie in the
  corridor. The one to look at: "does it look like a corn field" answered.
- `artifacts/review/world/m20-field-title.png` — the same field alive behind the title screen.
- `artifacts/review/world/m22-2026-09-24.png` — movement pass, plain launch.
- `artifacts/front-end-title-m21.png` — M21 front end.
- Numbers: `artifacts/m20-field-report.txt`, `artifacts/m22-feel-report.txt`.

## Last commits

`7518c78` M22 movement feel · `4c8b2d6` order update · `b02bd55` loop cap ·
`ca83b66` M21 front end · `8671f1e` FSD §25 · `dd4f529` corn block set.
