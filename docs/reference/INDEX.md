# Corn Field Maze — Reference Index

Where to look for what. Maintained by **Dough** (@corn-art). One line per question, pointing at the **narrowest** file that actually answers it. Every path below was checked on disk on 2026-09-17.

| Question | File that answers it |
|---|---|
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
