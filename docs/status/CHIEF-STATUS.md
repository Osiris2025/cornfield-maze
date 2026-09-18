# Corn Field Maze — Chief Status

**Updated:** 2026-09-17 20:45 EDT — pass 1 (docs + scripts only; **no Unity touched**)
**Milestone:** M0 — the gingerbread cookie. **Nothing else closes until the gate.**

## Crew

| Name | Handle | Role | State |
|---|---|---|---|
| Harrow | @corn-chief | chief | pass 1 run; verified every artefact below by hand |
| Dough | @corn-art | cookie, animation, materials | **T4a done** — `docs/reference/INDEX.md`, `docs/COOKIE-BRIEF.md` |
| Furrow | @corn-gameplay | maze, walker, beast, gold, HUD, touch | idle (M2, behind M0) |
| Squall | @corn-world | corn, sky, storm, mud | idle (M3, behind M0) |
| Rattle | @corn-audio | mood bed, cues, jingle, iOS audio | idle (M4, behind M0) |
| Lantern | @corn-qa | verify, Mac/iPhone builds, perf, evidence | **T2/T3 written — UNRUN** (see Blockers 1) |

## This pass — written but NOT green

- **T4a** `docs/reference/INDEX.md` (question → narrowest file, 20 rows) · `docs/COOKIE-BRIEF.md` (M0 brief, incl. LOCKED tone).
- **T2** `scripts/build-mac.sh` (batchmode `BuildStandaloneMac` → `Builds/mac-build.log`, one-Unity-writer guard) · `scripts/verify.sh` (asserts on the **artefact**: log marker `Built standalone player:` + non-empty `.app` executable + no `error CS`; prints one PASS/FAIL line).
- **T3** `scripts/capture.sh` (fresh `{position,size}` of the game window read **every run**, `-ApplePersistenceIgnoreState YES`, final frame at 2556x1179 into `artifacts/review/<category>/`) · `scripts/build-ios.sh` (`BuildIosPlayer` → `Builds/iOS`).
- **None of these was executed.** Only `bash -n` (syntax) is clean. **There is no green verify for this pass and I am not claiming one.**
- Two defects found by Harrow's verification and fixed before commit: `verify.sh` would have **false-FAILed every run** (its `failed:` grep matched Unity's real `[W] opendir() failed:` warning — proven against `Builds/ios-build.log`); `build-ios.sh` was mislabelled T2.

## Blockers

1. **Unity 6000.3.23f1 has the slot (pid 77444, this project).** No batchmode build, no import probe, no capture. `verify.sh` cannot earn its green until Todd closes the editor.
2. **Unity import of the FBX landed mid-pass** (`Assets/GingerbreadMan/gb_man.fbx.meta`, guid `d92b875a…`, 20:39) — but the importer's own state (**25 bones, 4 submeshes, material shader**) has **not** been read back. Blender is not Unity. T4's verify is still open.
3. **`.meta` files are untracked** (`Assets/GingerbreadMan*`) — owner Dough, commit with T5.
4. **Licence/provenance of `gb_man.fbx` is UNKNOWN** — fine for us, **blocks App Store shipping**. Logged in `docs/ASSETS-INVENTED.md`.
5. `appleDeveloperTeamID` empty, automatic signing off → T21/M6.
6. `scripts/crew-supervisor.sh` is modified in the tree by another writer — left alone, not committed.

## Last commits

`fbbd5bb` tri-count corrected to 3,238 (Ernie) · `b87d666` **tone LOCKED: scary game** (Ernie) · `db855e8` M0 structure · `cc98d3e` the supplied asset · `ddd9006` baseline. **This pass: one commit — "pass 1: T4a + T2 + T3 …"** (message names the task numbers; sha is in `git log`).

## CAPTURES FOR TODD

- **Asset previews — `artifacts/review/cookie/`** (Blender, not Unity): `ASSET-PREVIEW-front.png`, `-side.png`, `-back.png`, `-head.png`. ⚠️ **Not the gate.**
- **The gate frame does not exist and could not be made this pass.** It must come from a **plain launch of the Mac build** at iPhone aspect 2556x1179. Path once the slot frees: Lantern builds → `scripts/capture.sh cookie`.

## Need from Todd

1. **Close the Unity editor on this project** so the crew can build — nothing can go green until then.
2. **Source/licence of `gb_man.fbx`** (files dated Dec 2020, no author string) before any App Store upload.
3. Nothing else. Tone (scary, victim not mascot) is read and written into `docs/COOKIE-BRIEF.md` §8.
