# Corn Field Maze — Chief Status

**Updated:** 2026-09-25 07:40 EDT — **M25b (the moon) is GREEN this pass.**
**Project root:** `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS, volume files1). Unity 6000.3.23f1.
**Unity slot:** free at STATE-A; one writer at a time, `build-mac.sh` guard intact.

## 1. What is green now

| M | What | Owner | Evidence |
|---|---|---|---|
| M21 | front end: title, help, intro, pause | Dough+Furrow | `ca83b66` |
| M22 | movement feel: dead zone, look, sprint, no auto-centring | Furrow | `7518c78` · `artifacts/m22-feel-report.txt` |
| M20 | the corn blocks in the maze, CUTOUT leaves, LOD chain | Squall+Dough | `7a6a93f` · `artifacts/m20-field-report.txt` |
| M25 | dusk, the Halloween sky, the moonrise, cloud occultation | Squall | `874d90f` · `artifacts/m25-sky-report.txt` |
| M24 | the rename to the Husk | Furrow+Rattle | `4d2b253` |
| M23 | pick up and throw the cob | Furrow+Dough | `318b543` · `artifacts/m23-throw-report.txt` |
| M26 | one threat number drives rustle + music | Rattle+Squall | `aa0f23a` · **re-run this pass, unchanged** |
| **M25b** | **the moon is the photograph, not the blob** | **Squall** | this commit · **`artifacts/m25b-moon-report.txt`** · frames below |

**The moon is the NASA photograph now, and it measures as a moon.** From `artifacts/m25b-moon-report.txt`,
measured on the built Mac app, with the disc measured out of the PNG pixels by
`scripts/m25b_measure_moon.py` (stdlib PNG decode — no image library on this box):

```
drawn with       texture=T_Moon_Full 1024x1024, its own material, submeshes=2, queue 3209  (read off the live renderer)
angular size     6.96 deg designed (disc); quad 11.22 deg = 1.613x  ->  the 1/0.62 wiring is in
t=40s frame      disc 80 px measured  vs  83.0 px camera projection  = 96.4%   PASS
                 angular 6.96 deg designed -> 6.71 deg as measured on the frame
                 disc fraction achieved 0.60 vs 0.62 built                        PASS
                 height/width 1.01 (oblate squash released at 28 deg elevation)    PASS
                 threshold bracket 0.45/0.50/0.55 -> 81/80/79 px: not a threshold artefact
occultation      beats=1, 13.6s occluded, max=0.72   — unchanged from M25
§25.5 beats      dusk -2.0 deg, night -13.0 deg, moon 1 -> 28 deg, starGate 0->1, night at t=40s  — all unchanged
M26              -audiotest re-run: threat 0.160->0.400, RMS 2.40x, every line PASS — no regression
```

## 2. The frames — what each one actually is

1. **`artifacts/review/world/m25b-moon-t40.png`** — the deliverable: night, the moon high over the corn with
   maria, a round limb and a warm halo.
2. `artifacts/review/world/m25b-moon-t0.png` — t=0 dusk. The moon is dead centre in the frame but **behind
   the crop**: at 1 deg of elevation it sits below the 2.9-3.2 m corn from any lane-level camera. The
   measured sliver is 50 px = 60 % of the disc, i.e. occluded. Honest, and worth knowing.
3. `artifacts/review/world/m25b-moon-clear.png` — t=22 s, the moon at 15 deg clearing the corn, with a torn
   cloud against it (§25.5's occultation working). Not measurable: the cloud merges with the disc, and the
   report says so rather than measuring around it.
4. `artifacts/review/world/m25b-sky-t0.png` / **`m25b-sky-t40.png`** — the plain launch, exactly as ordered.
   **They do not contain the moon**, and that is a fact about the camera, not the sky: from the spawn the
   camera looks 106 deg off the moon's azimuth and 27 deg down the lane. The player sees the moon by looking
   up (measured pitch range 87 deg up to 53 deg down; the HUD already says "look straight up") — but the Mac
   build's look control is a mouse drag, and synthetic CoreGraphics drags do **not** reach Unity's Mouse X/Y
   axes (measured: a 2000 px drag left the view pixel-identical). Frames 1-3 therefore come from the same
   build driven by `-skycapture`, which aims through the game's own look state. Provenance per frame is in
   the report.

## 3. Three real defects found and fixed this pass

1. **The moon was never drawn.** `_mesh.triangles = …` is shorthand for submesh 0 and **resets
   `subMeshCount` to 1**, so the following `SetTriangles(…, 1)` failed with "Submesh index is out of bounds"
   and the moon quad was silently absent. Caught in `Player.log`, fixed by filling both submeshes with
   `SetTriangles`. Without it this pass would have been a false green.
2. **The aim's pitch assumption was wrong.** Setting the controller's pitch to the moon's elevation moved
   the camera the *wrong way* (it hangs behind a rig that also moves the boom) and left the moon 81 deg out
   of frame. Replaced with a bisection against the camera's own measured forward elevation: residual now
   0.0 deg yaw / 0.2 deg pitch.
3. **The sky is not drawn by our shader.** `Shader.Find("CornMaze/StarUnlit")` does not resolve in the player
   build (nothing else references it), so every sky material falls back to `Sprites/Default`. The report now
   prints the shader actually in use. Consequence: the star twinkle and the custom shader's blend properties
   are inert in the build. Pre-existing, not M25b's to fix — flagged for Todd.

## 4. Questions for Todd (morning) — each with my recommendation

1. **Is the moon big enough?** Deliberately oversized (§25.5: ~7 deg, ~14x the real moon) and it now reads
   as a photograph. *Recommend:* keep — it is the Halloween read the references ask for.
2. **Should the moon rise where the player is looking?** It rises 106 deg off the spawn view, so a player who
   never turns never sees it. *Recommend:* keep the sky, but consider turning the spawn 20-30 deg so the moon
   is glimpsable at the edge of frame — that is a maze-spawn decision, not a sky one.
3. **`CornMaze/StarUnlit` is not in the build** (finding 3). *Recommend:* add it to Always Included Shaders so
   the twinkle works, or delete the shader and keep Sprites/Default honestly. Adding it is the cheap fix.
4. **The blob is gone for good:** the perlin disc generator was deleted, not bypassed, so it cannot return.
5. Carried from last night: the §25.6 **device listen pass** (needs the phone); locked docs still say "Crumb
   Beast" (`docs/DECISIONS.md`, the FSD, `docs/reference/INDEX.md` — Ernie applies); the 60 fps phone floor is
   unmeasured; `capture.sh` needs one instance only (see below).

## 5. Decisions made this pass

1. **A separate material for the moon, not an atlas region.** Region 0 of a 128 px atlas would resample a
   1024 px photograph to 128 while the disc covers ~200 px of a 2556 px frame — throwing away exactly the
   detail this pass exists for. Cost: one extra draw call. The atlas also dropped 3 columns to 2 (band,
   cloud) and its moon generator was deleted.
2. **The quad is sized A/0.62 and the reader divides it back out.** `DuskSky.DiscFraction` is documented as
   moving with `scripts/moon_build.py`'s `DISC_FRACTION`; `MoonAngularDegrees()` reports the DISC, so §25.5's
   ~7 deg reading stays comparable with M25 instead of appearing to jump to 11 deg.
3. **Measure the limb at half the disc's own peak.** A photograph's maria are darker than its highlands, so a
   high threshold measures only the bright core (0.70 gave 59 px against a true ~80 px). The full sensitivity
   table is in the report so the number can be audited, not just believed.
4. **The evidence frames are aimed by the harness, and each frame's provenance is stated.** A frame without
   the moon in it is not evidence about the moon; pretending a plain launch produced one would be worse than
   saying plainly why it cannot.
5. **`capture.sh`'s real failure was a stale instance**, not permissions: a second process named "Corn Field
   Maze" makes the System Events window lookup fail (Invalid index -1719). The new plain-launch script kills
   stale instances first, waits past the Unity splash, and presses SPACE through the title and 4 intro cards
   (the first two attempts photographed intro cards 0 and 2 instead of the sky).

## 6. Blockers

None for the build or the code. The three items needing a human are in §4 and none stops the work.
Working-tree items left alone as ordered: deleted `Assets/GingerbreadMan.meta`, modified
`Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`. Unity re-serialised `ProjectSettings/*` and
`PC_RPAsset.asset` during builds (not committed); the four `PerformanceTestRun*.json` files were restored to
HEAD. Untracked scratch: `scripts/_probe_png_tools.py` (12-line availability probe, harmless).

## 7. CAPTURES FOR TODD

1. **`artifacts/review/world/m25b-moon-t40.png`** — start here. A real moon over the corn at night.
2. `artifacts/review/world/m25b-moon-clear.png` — the moon clearing the crop at 15 deg, cloud against it.
3. `artifacts/review/world/m25b-moon-t0.png` — dusk, moon dead centre but behind the corn (why t=0 shows none).
4. `artifacts/review/world/m25b-sky-t0.png`, `m25b-sky-t40.png` — the plain launch as ordered; §2 explains
   exactly why the moon is not in them.
5. Numbers: `artifacts/m25b-moon-report.txt`.

## 8. Crew

| Name | Handle | Role | State |
|---|---|---|---|
| Harrow | @corn-chief | chief | M25b wired, verified and measured; wrote this file |
| Dough | @corn-art | cookie, silhouette, materials | M20, M23 |
| Furrow | @corn-gameplay | controller, maze, HUD, touch | M22-M24; added the test-only aim hook |
| Squall | @corn-world | corn field, sky, dusk, storm | **M25b landed**: the photograph moon, the atlas, the material split |
| Rattle | @corn-audio | bed, cues, jingle, rustle | M26 (re-verified unchanged) |
| Lantern | @corn-qa | builds, captures, measured evidence | frames measured out of the pixels this pass; `capture.sh` note in §5.5 |

## 9. Last commits

this commit M25b the moon · `aa0f23a` M26 threat audio · `318b543` M23 cob throw · `4d2b253` M24 the Husk ·
`874d90f` M25 dusk/moonrise · `7a6a93f` M20 corn field · `7518c78` M22 movement · `ca83b66` M21 front end.
