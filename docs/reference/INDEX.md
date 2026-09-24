# Corn Field Maze — Reference Index

Where to look for what. Maintained by **Dough** (@corn-art). One line per question, pointing at the **narrowest** file that actually answers it. Every path below was checked on disk on 2026-09-17; the FSD rows and the corn rows were added 2026-09-24 by Ernie.

| Question | File that answers it |
|---|---|
| **What is the game, as designed — progression, levels, coins, upgrades, enemies, bosses, leaderboard?** | **`docs/reference/CORN-FIELD-MAZE-FSD.md`** (design of record, 2026-09-24) |
| **Which levels exist, and their exact numbers (size, seed, storm onset, enemies, economy)?** | `docs/reference/CORN-FIELD-MAZE-FSD.md` §6 (the level table) |
| **How does the cookie take damage / what is the health model?** | `docs/reference/CORN-FIELD-MAZE-FSD.md` §5 (dough integrity) |
| Why does this crew exist / what is the rejected gingerbread man? | `docs/DECISIONS.md` (§M0) |
| Which tasks are there, by number, and what are their verify criteria? | `docs/plans/2026-09-17-mvp-plan.md` |
| Where did the replacement cookie asset come from and what is its licence status? | `docs/ASSETS-INVENTED.md` |
| What are the measured facts about `gb_man.fbx` (bones, verts, triangles, meshes, clips, bounds)? | `docs/DECISIONS.md` §M0 (locked) + `docs/ASSETS-INVENTED.md` (per-mesh table) |
| What must the cookie read as / how do we integrate it? | `docs/COOKIE-BRIEF.md` |
| What are the Blender preview frames of the asset, and where are they? | `artifacts/review/cookie/ASSET-PREVIEW-front.png` (`-side`, `-back`, `-head` alongside) |
| Where is the FBX that Unity imports? | `Assets/GingerbreadMan/gb_man.fbx` |
| Where are the phone (512) textures Unity imports? | `Assets/GingerbreadMan/textures/gb_man_color_512.png`, `gb_man_normals_512.png` |
| Where are the original 2048 textures (reference only, deliberately NOT imported)? | `docs/reference/gingerbread_man/textures/2048x2048/gb_man_color.png`, `gb_man_normals.png` |
| Where is the pristine copy of the FBX for reference? | `docs/reference/gingerbread_man/gb_man.fbx` |
| Where is the Mac review build and its log? | `Builds/Corn Field Maze.app` (exists); log `Builds/mac-build.log` — **not on disk yet**, produced by `scripts/build-mac.sh` (see `docs/status/CHIEF-STATUS.md`) |
| How do I verify a change really built? | `scripts/verify.sh` — **expected deliverable this pass (Lantern), not on disk yet**; the verify doctrine is in `docs/DECISIONS.md` → Working rules |
| How do I build Mac / iOS / capture review frames? | `scripts/build-mac.sh`, `scripts/build-ios.sh`, `scripts/capture.sh` — **expected deliverables this pass (Lantern), not on disk yet** |
| What is the current crew state and what frames are waiting for Todd? | `docs/status/CHIEF-STATUS.md` |
| What replaces which file in the player code (the primitive cookie)? | `Assets/Scripts/FarmWalkerController.cs` — `GingerbreadMesh.Build()` (line 576) + `struct FarmRig` (line 559) |
| Where is the maze/world built at runtime? | `Assets/Scripts/GameBootstrap.cs` (entry) → `MazeGenerator.cs` → `MazeWorldBuilder.cs` |
| What are the procedural textures / the audio? | `Assets/Scripts/Materials.cs` (textures) / `Assets/Scripts/MazeMoodAudio.cs` (audio) |
| How does the rain dissolve wash the icing first? | `Assets/Scripts/FarmWalkerController.cs` — `ApplyDissolve(float amount)` (line 275) + `List<bool> _icingFlags` (line 51) |
| What is the scale/camera contract for the player? | `Assets/Scripts/FarmWalkerController.cs` — `CameraDistance 5.2f` (12), `CameraHeight 2.1f` (13), capsule `height = 1.80f` (67) |
| **What is the corn field made of, and why is it deliberately sparse?** | **`docs/CORN-BRIEF.md`** + `docs/reference/CORN-FIELD-MAZE-FSD.md` §24 (locked 2026-09-24) |
| Where are the corn block FBXs Unity imports? | `Assets/Corn/Blocks/` — six blocks + `_LOD1`/`_LOD2` |
| Which seed, bbox and tri counts does each block have? | `Assets/Corn/Blocks/blocks-manifest.json` |
| Where are the corn textures? | `Assets/Corn/Textures/` (`T_Corn_01_D.png`, `T_Corn_01_NRM.png`, `corn_texture.png`) |
| Why must the corn materials be Cutout, and what else must be set on import? | `Assets/Corn/README.md` → Unity import settings; brief §5 |
| Where did the corn models come from, and what is their licence? | `docs/ASSETS-INVENTED.md` → **licence UNKNOWN, must be confirmed before App Store** |
| How do I regenerate the corn blocks? | `scripts/corn_block_build.py` (design-time only, never in the build) |
| How was the corn density decided, and how is it proven? | `scripts/corn_block_verify.py` (ray test + renders) + FSD §24.3 |
| **Is there a title / help / introduction screen before the game starts?** | **No — `GameBootstrap.Start()` drops the player straight into the maze.** Requirement: FSD §25.1 (M21) |
| What is the movement target — "normal 3D / FPS" on a phone? | FSD §25.2's feel-contract table. **First-person vs third-person is open with Todd.** |
| Can objects be picked up and thrown at threats? | **No — nothing in the tree carries or throws.** Requirement + the cob's ballistics: FSD §25.3 (M23) |
| What is the thing chasing Gingy called? | `Assets/Scripts/CrumbBeast.cs` today; **rename open with Todd** — FSD §25.4 |
| What keeps a threat from just parking in a corridor? | **The flow law** (no threat may hold the player still >1.5 s) + §8's fairness law — FSD §25.4 |
| Where is the dusk / moonrise / Halloween sky required? | FSD §25.5 (M25), built on `Assets/Scripts/NightSky.cs` |
| Where are the cornstalk rustle and the eerie music? | `Assets/Scripts/MazeMoodAudio.cs` — **already built** (`CornRustle`, `AnxiousDrama`). The gap is the threat-distance term + a device listen pass: FSD §25.6 (M26) |
