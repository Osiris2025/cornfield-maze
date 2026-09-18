# CORN FIELD MAZE — Project Decisions (LOCKED)

> Maintained by **Ernie** (default profile). These are settled. Bots do **not** re-litigate them.
> To change one, ask @corn-chief, who escalates to Ernie → Todd. Todd's call is final.

## Product

- **Corn Field Maze** — third-person 3D maze game. You play a **gingerbread cookie**. Long dead ends force backtracking; reach the **pot of gold** to win. Stay too long in the rain and the cookie softens and dissolves. Wrong turns risk the **Crumb Beast**, a hungry cookie-chasing chase beast (original — not Sesame Street IP).
- **Target: iPhone, landscape-only, arm64.** The Mac standalone build is the review surface Todd plays.
- Reference: `README.md` (player-facing description of the intended game), `IDEA.md`.

## ⚠ M0 — THE GINGERBREAD MAN IS THE REJECTED ASSET (the reason this crew exists)

Todd, 2026-09-17: *"we are very unhappy with the gingerbread man."*

- The player **was** built by `GingerbreadMesh.Build()` in `Assets/Scripts/FarmWalkerController.cs` out of **~40 Unity primitives** — `CreatePrimitive(Cube)` pelvis, torso, upper arms, forearms, thighs, shins, feet, icing strips; `Sphere` head, hands, gumdrop buttons. A **blocky robot wearing brown**.
- **Todd has since supplied a real replacement** (2026-09-17): a rigged, textured low-poly gingerbread man in `Assets/GingerbreadMan/gb_man.fbx`. See `docs/ASSETS-INVENTED.md`. **This is what Dough integrates; do not hand-build a cookie mesh.**
- **Verified independently in Blender** (not yet in Unity — the editor holds the slot). Structure, read from the file by two separate tools:
  - **25 bones**, named `spine01`, `spine02`, `shoulder.L/R`, `upper_arm.L/R`, `forearm.L/R`, `hand.L/R`, `neck`, `head`, `waist.L/R`, `hip.L/R`, `chin.L/R`, `foot.L/R` + five `_end` bones.
  - **4 meshes totalling 1,796 verts / 3,238 triangles**, and they are **split by role**:
    `gb_man_body` (2016 tris), **`gb_man_decoration` (742 tris — the icing)**, `gb_man_eyes` (288), `gb_man_mouth` (192).
  - **1 material**, `gb_man_texture`, colour + normal map.
  - **ZERO animation** — no `AnimationStack`/`AnimationLayer`/`AnimCurveNode`. **Motion is authored in code**, as this project already does.
  - **Bounds: 5.376 units tall, 4.75 wide across the arms.** The current player capsule is `height 1.80`, so the model needs a **scale of ≈0.335** (or the capsule re-measured). Do not ship a 5-metre cookie.
  - Rendered previews (Blender, independent of Unity): `artifacts/review/cookie/ASSET-PREVIEW-{front,side,back,head}.png`.
- ⚠️ **The icing is its own mesh: `gb_man_decoration`.** So "the icing washes off first" is directly implementable — **the dissolve targets the decoration mesh/its material separately.** This is locked: do not merge the meshes and do not re-derive the icing separation.
- **This is a HARD GATE.** No other milestone closes until Todd has looked at a frame of the new cookie taken from the running Mac build and said so. A green verify does not close it.
- **What "a cookie, not a robot" means** — the shipped silhouette is now the FBX's, so the work is (a) it must **load and render correctly in Unity** (still unproven — Blender is not Unity), (b) it must be **animated**, (c) it must **read at third-person distance** at the right scale, and (d) the **rain dissolve must still wash the icing first** via the decoration mesh.
- **The gate artefact is a frame from a PLAIN LAUNCH** of the Mac build at **iPhone aspect (2556x1179, 2.168:1)**. A capture whose pose/scene was set by a dev harness shows what the harness can construct, not what the game does — invalid as a gate.
- **Art direction beyond this is settled by the following, which is LOCKED.**

## Tone (LOCKED 2026-09-17) — this is a SCARY game

Todd, verbatim, on the supplied cookie's hollow ring eyes and big red ring mouth:

> *"that is by design. this will be a scary gamer"*

Read as *scary game*. Consequences, and they are requirements:

- **The wide-eyed, hollow, ring-eyes-and-red-ring-mouth face is DELIBERATE. Do not "fix" it.** Any pass that rounds the rings into friendly dot-eyes, shrinks the mouth into a smile, or adds cartoon charm has misread the product and is a defect. The stunned, staring face is the point.
- **The register is horror, not cosy.** The existing shipped material already agrees with this and is now the reference standard: the README's *"looping anxious underscore, chase-string stabs, hollow wind"*, the **horror bed** that ducks on a win, a **Crumb Beast** that eats you, the cookie **dissolving in the rain with its icing washing off first**, the sky going almost night-dark, and lightning before thunder. Every art, audio and feel decision is judged against that register.
- **The cookie is the victim, not a mascot.** Design him as something you are uneasy about watching get eaten.
- **Cute-and-scary is allowed; cute-and-safe is not.** Warm baked dough and icing can stay — a gingerbread man is a soft thing in a bad place. Do not sand off the menace to make him likeable.
- **This is a taste call owned by Todd.** If a bot thinks a change makes the game less scary, it escalates to Harrow with options laid out rather than deciding.

## What "a cookie, not a robot" means (still true)

- It must **load and render correctly in Unity** (still unproven — Blender is not Unity).
- It must be **animated** — a static cookie sliding around is not a character.
- It must **read at third-person distance** at the right scale.
- The **rain dissolve must still wash the icing first** (the `gb_man_decoration` mesh).

## Environment (verified 2026-09-17)

- Unity **6000.3.23f1** at `/Applications/Unity/Hub/Editor/6000.3.23f1` (6000.3.24f1 is also installed — **this project is pinned to 23f1**; do not open it with another editor).
- **Xcode 27.0** at `/Applications/Xcode.app` — present. `appleDeveloperTeamID` in `ProjectSettings.asset` is **empty** and `appleEnableAutomaticSigning: 0`, so a device build needs the team pinned first (see @corn-qa's SOUL).
- Project root: **`/Volumes/files2/CornFieldMaze`** — external volume, no spaces in the path. **It is `exfat` and 94% full (~206 GiB free at last count is WRONG — measured 2026-09-17: 3.44 TiB used of 3.64 TiB).** Not an iCloud File Provider domain.
- ⚠️ **THE PROJECT VOLUME CANNOT HOST A MAC BUILD. Build from an APFS working copy.** This is a property of `exfat`, not of the code, and it cost a full session to find. Both failures are reproduced below so nobody re-litigates them:
  - **No hard links.** `ln` on this volume returns `Operation not supported`. Unity's macOS build *links* resources into the `.app` bundle rather than copying them, so it creates the destination at **zero bytes** and then dies: `Copying .../unity_builtin_extra failed: Operation not permitted` → `*** Tundra build failed`. Verified with a same-volume hardlink probe (a cross-device probe proves nothing — it only reports `Cross-device link`).
  - **AppleDouble `._*` junk is read as code.** macOS writes a `._foo` sibling next to every file Unity creates on such a volume. Unity's managed linker then tries to load `._Assembly-CSharp.dll` as a .NET assembly: `System.BadImageFormatException: Format of the executable (.exe) or library (.dll) is invalid` → `Mono.Cecil.AssemblyResolutionException: Failed to resolve assembly: '._Assembly-CSharp'` → `Burst compiler failed`. **Cleaning before a build cannot win — the build recreates the junk as it writes.** `dot_clean` does nothing on exFAT (returns in 0.012s).
  - **What to do:** build from `/Users/toddadams/CornMazeWork/CornFieldMaze` (**the verified fix** — an APFS working copy). ⚠️ **`CORN_BUILD_OUT` alone is NOT sufficient**, and do not let it talk you out of the working copy: the junk that breaks the linker is written into the **PROJECT's own `Library/Bee/artifacts/.../ManagedStripped/`** (which stays on exFAT), not into the output directory — redirecting only the `.app` leaves that tree on the bad volume, so `._Assembly-CSharp.dll` is still there for the linker to choke on. `build-mac.sh` now tests for hardlink support and **refuses in 0.26s** with this explanation instead of burning a Unity run. APFS uses native extended attributes, so neither failure mode exists there.
- **One Unity writer at a time.** Two batchmode editors on one project corrupt `Library/`. Check with `pgrep -fl "Unity.*CornFieldMaze"` before you open one.
- Renders: URP, `Mobile_RPAsset` / `PC_RPAsset` under `Assets/Settings`.

## Current state of the code (measured 2026-09-17)

- **18 C# files, 5,213 lines**, no assemblies (`.asmdef`), **no tests**, **no `scripts/verify.sh`**, **no git history** (baseline commit is the project's first).
- The world is **built at runtime from code** — `MazeGenerator.Build()` → `MazeWorldBuilder.Build()` → `FarmWalkerController.Spawn()` → `PotOfGold`, `GameHud`, `MazeMoodAudio`, `StormWeather`, `PathMudWetness`, `NightSky`, `CrumbBeast`, all from `GameBootstrap.Start()`. `Assets/Scenes/CornMaze.unity` is a near-empty host scene; `Editor/CornMazeSetup.cs` (`CornMaze.Run`) creates it.
- Textures are **procedurally generated in code** (`Materials.cs` — fbm/value noise for gravel, field grass, path grass, plaid). Audio is **procedurally synthesized in code** (`MazeMoodAudio.cs`, 1,028 lines).
- Known defects carried in from the original build: HUD canvas is `referenceResolution 1920x1080` (a desktop size, on a phone game); `README.md` still references `/Users/arl480/Unity_Projects/CornFieldMaze` (another machine) and Unity 6000.3.23f1 instructions that no longer match this host.

## Architecture (LOCKED)

- **Plain Unity C# MonoBehaviour project — no assembly definitions, no DI, no third-party packages.** Adding `com.unity.*` packages beyond what is installed needs Todd's approval.
- **The world is built at runtime from code.** Prefer a generator script over a hand-authored prefab or scene: it is diffs-able, reviewable and testable. Keep the runtime builder as the single source of truth for the maze.
- **`GingerbreadMesh` is the ONE exception to "generate it simply"** — it is the asset Todd rejected, and it is allowed to be as deliberate as a real model.
- **Simplicity rule.** An abstraction must delete more code than it adds. No plugin systems, no configurable-everything, no interface introduced on the first implementation.
- **Data over code where it is content**: maze size, seed, dead-end depth, dissolve rate, beast speed, storm timing are constants/fields — name them, keep them in one place per system, do not scatter magic numbers.

## Asset policy

- **Procedural or code-generated by default** — meshes, textures and audio are all generated in-code on this project. That is house style and it needs no external asset pipeline.
- Imported assets are allowed only if **logged in `docs/ASSETS-INVENTED.md`** with what they replace and why, and their licence.
- **Never ship a placeholder as final.**
- **Every visual commit names the frame it was judged from** — the capture path and what it shows. A visual commit that names no frame is unfinished work.

## Review gates

- **A gate artefact is a frame from a PLAIN LAUNCH** of the built Mac app — reach the screen by the same input a player uses, capture what is on screen. Harness-posed frames are not evidence. State read from inside the process proves state, not sight.
- **Judge frames at iPhone aspect (2556x1179, 2.168:1)**, or beside an actual-phone reference. A frame that only looks acceptable at 34 inches is not acceptable.
- **A gate is held open until Todd has looked.** A verifier's green does not close it.
- Gates: **G1 the cookie (HARD)** → G2 the world/atmosphere pass → G3 the iPhone device build.

## Scope boundaries

- Do **not** add: multiplayer, achievements, ads, analytics, in-app purchases, leaderboards, a second playable character, procedural level generation, or a shop/upgrade system.
- Faithfulness plus polish. If a change is neither fixing something broken nor making something Todd pointed at better, it needs a reason.

## Milestones

| # | Milestone | Owner |
|---|---|---|
| **M0** | **The gingerbread cookie** — replace the primitive robot with a cookie; frame from the running build | @corn-art → @corn-chief |
| **M1** | Verify + baseline — `scripts/verify.sh`, Mac build script, first green, committed tree | @corn-qa |
| **M2** | Gameplay truth — path-locked movement, beast fairness, gold, lane widths re-measured | @corn-gameplay |
| **M3** | World and storm — corn field, night sky, rain ramp, mud, star arrow readable | @corn-world |
| **M4** | Audio — mood bed, chase stabs, thunder, win jingle, iPhone mute-switch path | @corn-audio |
| **M5** | Phone truth — HUD at device aspect, touch controls complete, perf budget | @corn-gameplay → @corn-qa |
| **M6** | iPhone device build on Xcode 27.0, signing pinned, played on the device | @corn-qa |

M0 is the critical path. Everything else is behind it.

## Reporting chain

`specialist → Harrow (chief) → Ernie (default profile) → Todd`

### Naming convention
The crew's **names** are **Harrow** (chief), **Dough** (art), **Furrow** (gameplay), **Squall** (world), **Rattle** (audio), **Lantern** (QA). Use these names in every report, status file and commit message.

Profile handles (`corn-chief`, `corn-art`, `corn-gameplay`, `corn-world`, `corn-audio`, `corn-qa`) are **wiring only** — they exist for `@`mentions in the room and for CLI dispatch. **Never use a handle as a name in prose.** Write "Dough landed the new cookie", not "corn-art landed the new cookie".

- Harrow maintains **`docs/status/CHIEF-STATUS.md`** — current milestone, per-bot state, blockers, last commits, **CAPTURES FOR TODD**. **Overwrite it, keep it under one page.**
- Ernie reads that file when Todd asks for status. Keep it accurate; a stale status file is a failed task.

## Working rules (all bots)

- Every task ends with: **headless verify green → git commit.** No commit, no credit.
- Verify means an **artefact**, never an exit code: the build log's own result marker (`Built standalone player:` / `Built iOS Xcode project:`), a file that must exist, a hash that must match, a measured number. Unity batchmode exits 0 on failure and on test runs that never started.
- ⚠️ **Never pass `-quit` to a `-runTests` invocation** — the editor quits at the first asset refresh before the runner starts: exit 0, no result XML. Verify commands are written by @corn-qa in `scripts/verify.sh`; use that script.
- Never commit a red project. If you break the build, fix it before your next task.
- **Commit only the files you own — never `git add -A` across the tree.** Another worker's half-finished edits are mid-flight, not abandoned.
- **One Unity writer at a time.**
- Report to your chain, not to Todd directly — except taste calls, which escalate with options laid out.
- ⚠️ **Never edit a file with a `python - <<'PY'` heredoc, or with `python -c "..."`.** The security scan blocks both inside a worker session — the command returns `BLOCKED`, the edit silently does **not** happen, and the pass burns its budget improvising. Write `scripts/<name>.py` and run that, or use the file-edit tools. **Treat a `BLOCKED` line in your own output as a failed edit, not as noise.**
- ⚠️ **A command that trips the scanner must state its OWN reason**, because the approval prompt reproduces the command verbatim and nothing else — the intent is not in the payload. End it with `<command> # why: <one line, plain English>`. A contextless prompt is a defect you caused.
- ⚠️ **Never `rm` the supervisor marker files** (`/tmp/corn-crew-done`, `/tmp/corn-crew-blocked`, `/tmp/corn-supervisor-stop`), and never batch-`rm` any `/tmp` paths. `rm -f` naming three files that do not exist still counts as three delete events and forwards a "mass deletion" prompt to Todd's phone. **Test for them** (`[ -f /tmp/corn-crew-done ] && ...`).
- Never `rm -rf` anything. If a directory must go, rename it in place with a timestamp suffix.
- Keep replies short. Todd hates walls of text.
