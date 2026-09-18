# Corn Field Maze — Chief Status

**Updated:** 2026-09-17 (scaffold — crew NOT yet dispatched; see Blockers)
**Milestone:** M0 — the gingerbread cookie. **Nothing else is in flight.**

## Crew

| Name | Handle | Role | State |
|---|---|---|---|
| Harrow | @corn-chief | Chief | armed, not dispatched |
| Dough | @corn-art | the gingerbread cookie, animation, materials | idle — M0 is theirs |
| Furrow | @corn-gameplay | maze, walker, path rules, beast, gold, HUD, touch | idle |
| Squall | @corn-world | corn, sky, storm, mud | idle |
| Rattle | @corn-audio | mood bed, cues, jingle, iOS audio | idle |
| Lantern | @corn-qa | verify.sh, Mac + iPhone builds, perf, evidence | idle |

## Why this crew exists

Todd: *"we are very unhappy with the gingerbread man."* The player was ~40 Unity primitives — a blocky robot wearing brown.

**Todd supplied the replacement on 2026-09-17**: `Assets/GingerbreadMan/gb_man.fbx` — rigged, textured, low-poly (25 bones, 1,796 verts / 3,238 tris, 1 material, **no animation clips**). Verified by parsing the FBX binary and independently by importing it in Blender. The icing is its **own mesh** (`gb_man_decoration`), so the "icing washes off first" dissolve is directly implementable. Full notes: `docs/ASSETS-INVENTED.md` and `docs/DECISIONS.md` §M0.

Replacing the player is **M0 and a HARD GATE**: no other milestone closes until Todd has seen a frame of it from the running Mac build.

## Last commits

- `cc98d3e` — M0: take Todd's rigged gingerbread man as the player asset (asset + docs).
- `ddd9006` — baseline: the whole project as found.

## CAPTURES FOR TODD

**The asset, rendered in Blender (independent of Unity)** — `artifacts/review/cookie/`:
- `ASSET-PREVIEW-front.png` — full body: rounded cookie, baked dough, icing ring eyes, red ring mouth, red bow, three icing buttons, zigzag cuffs, belt and ankles.
- `ASSET-PREVIEW-side.png` — the slab profile: it is a real cookie with thickness, icing standing proud.
- `ASSET-PREVIEW-head.png` — face close-up.
- `ASSET-PREVIEW-back.png` — reverse.

⚠️ **These are NOT the gate.** They prove the asset loads and is textured; they are not the game. The gate frame must come from a plain launch of the Mac build, in Unity.

## Blockers

1. **The crew cannot run — the six profiles have no API key.** The dotfile-write approval timed out, so nothing was written (verified: 0 keys in all six `.env` files).
2. **Unity `6000.3.23f1` is already open on this project** (pid, launched from Hub). It holds the Unity slot: any batchmode build or import probe by a worker would be a second writer on `Library/`. It must be closed before the crew builds, **or** the import must be confirmed by focusing the editor.
3. **The FBX is still unimported by Unity** (no `Assets/GingerbreadMan/gb_man.fbx.meta`) — the open editor defers its asset refresh while unfocused. Blender is not Unity: the Unity import is unproven until this happens.
4. Standing, becomes real at T21: `appleDeveloperTeamID` is empty and iOS automatic signing is off.

## Open question for Todd

The gingerbread man's **licence and origin are unknown** (files dated Dec 2020, no readme, no author string). Fine for us to use; **App Store shipping needs the source confirmed.** Logged in `docs/ASSETS-INVENTED.md` so it cannot quietly ship.

## In flight

- nothing. Scaffold only.
