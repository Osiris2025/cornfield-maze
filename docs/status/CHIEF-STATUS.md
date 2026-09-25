# Corn Field Maze — Chief Status

**Updated:** 2026-09-24 21:20 EDT — overnight loop, pass 5 of the night
**Milestone:** **M23 is GREEN this pass**; the next pass takes M26 — the last item in the queue.
**Project root:** `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS, volume files1). The old files2 tree is retired.
**Unity slot:** free when this pass began; one writer at a time used it, `build-mac.sh` guard intact.

## 1. What is green right now

| M | What | Owner | Evidence |
|---|---|---|---|
| M21 | front end: title, help, intro, pause | Dough+Furrow | `ca83b66` · `artifacts/front-end-title-m21.png` |
| M22 | movement feel: dead zone, look, sprint, no lane auto-centring | Furrow | `7518c78` · `artifacts/m22-feel-report.txt` |
| M20 | the real corn blocks in the maze, CUTOUT leaves, LOD chain, boom fix | Squall+Dough | `7a6a93f` · `artifacts/m20-field-report.txt` |
| M25 | dusk, the Halloween sky, the moonrise, cloud occultation | Squall | `874d90f` · `artifacts/m25-sky-report.txt` |
| M24 | the rename to the Husk, everywhere in code | Furrow+Rattle | `4d2b253` |
| M23 | pick up and throw the cob | Furrow+Dough | this commit · `artifacts/m23-throw-report.txt` · `artifacts/review/world/m23-cob-side-1..4.png` |

M23 measured on the built Mac app (`-throwselftest`), against the §25.3 numbers:

```
range            level-ground 7.104 m vs the spec's R = v^2 sin(2θ)/g = 6.999 m   PASS
                 (landing point 9.735 m from a hand released 1.27 m up — both reported)
husk hit         health 18 -> 12, exactly one third, hitsTaken 1                  PASS
stagger          1.10 s of the contract 1.1 s                                     PASS
ground bought    2.585 m = 2.35 m/s x 1.10 s                                      PASS
husk movement during the stagger 0.000 m (a full stop, which is the point)
after 3 hits     scattered, not dead; it re-forms in 8 s
cobs in the lanes 28, audit: 0 off-path, 0 in a dead-end mouth
control          appears only in range (invisible with nothing in reach) and reads PICK UP / THROW
```

**Four real defects this pass, all found by measuring rather than by reading:**
1. Each cob primitive arrived with its own collider (Unity primitives do), so the cob bounced off its own
   tips and clipped the thrower. Stripped to the one root collider.
2. `Physics.IgnoreCollision` reported **true** and the cob still hit the player's `CharacterController`, so
   the throw died 0.42 m from the hand. The cob now arms its collider 0.05 s after release (0.7 m of travel).
3. **The Husk had no collider at all** — its builder strips every primitive's collider, so a thrown cob flew
   straight through it. It now carries a trigger hit-volume: a trigger never pushes the kinematic player.
4. That volume is a **2.6 m column, not the model's 1.15 m**, because the 20° arc peaks at 1.9 m — a
   model-height volume made every throw inside 6 m sail over its head while measuring "correct".

## 2. In flight / next

Nothing mid-edit. **M26 (one threat-distance term driving both the corn rustle and the music) is the last
milestone in the queue.** After it: the DONE marker, per the order.

## 3. Questions for Todd (morning) — each with my recommendation

1. **The scatter rule (new, my call).** Three cob hits take the Husk to 0 health. §25.3 says the cob "is not
   a weapon that kills", and §8's fairness law forbids a trivially removable threat, so a scattered Husk
   **re-forms after 8 s** instead of dying. *Recommend:* keep it; tell me if you would rather it die.
2. **The throw control on the Mac.** The touch controls are mobile-only by design, so a desktop key (**F**)
   drives pick-up/throw in the Mac build — otherwise you cannot try the mechanic on the machine you review on.
   *Recommend:* keep the key; it is gated so it can never double-fire with the phone control.
3. **Locked docs still name the old threat** (carried from M24). `docs/DECISIONS.md`,
   `docs/reference/CORN-FIELD-MAZE-FSD.md`, `docs/reference/INDEX.md` still say **Crumb Beast**.
   *Recommend:* Ernie applies the rename — the code has said "the Husk" since `4d2b253`.
4. **Licences.** `gb_man.fbx` and both corn source models are third-party with licence **unknown**
   (`docs/ASSETS-INVENTED.md`). *Recommend:* confirm before any App Store submission; nothing else blocks.
5. **The 60 fps floor on the phone** is still unmeasured — no device attached, and `appleDeveloperTeamID`
   is empty. *Recommend:* measure it on your iPhone when you are up.
6. **`capture.sh` cannot resolve its window** while another app named "Corn Field Maze" is on the machine
   (three passes now); frames come from an in-engine `ScreenCapture` shim instead.
   *Recommend:* Lantern reworks `capture.sh` to prefer the in-engine path.

## Crew

| Name | Handle | Role | State |
|---|---|---|---|
| Harrow | @corn-chief | chief | verified M23 himself; wrote this file |
| Dough | @corn-art | cookie, silhouette, materials | cob built from primitives, 190 x 45 mm (M23) |
| Furrow | @corn-gameplay | controller, maze, HUD, touch | **M23 landed**: hands, control, ballistics, Husk health |
| Squall | @corn-world | corn field, sky, dusk, storm | M20 + M25 landed |
| Rattle | @corn-audio | bed, cues, jingle, rustle | M24 cue rename landed; **M26 next** |
| Lantern | @corn-qa | builds, captures, measured evidence | built + verified M23; `capture.sh` rework listed above |

## Decisions made this pass (recorded where the morning reader will find them)

1. **The cob integrates its own gravity (18 m/s²) instead of Unity's global gravity.** Velocity-Verlet is
   exact for constant acceleration at any step size, so the measured range does not drift with frame rate —
   which is what makes the 7.0 m claim checkable rather than a claim about one machine. Nothing else in the
   project uses physics gravity, so the global setting was left alone.
2. **The hit-volume column (2.6 m) over the visible body (1.15 m)** — see defect 4 above.
3. **A cob that lands becomes loot again after 0.4 s.** Ammo stays finite and findable; nothing respawns.
4. **Aim is the camera's flat forward.** The look drag aims, there is no aiming stance, and a gold disc shows
   where the cob would come down — §25.3's "aimed by the look drag, thrown on release" with no extra mode.
5. **`MobileControls.ForceShowForTest`** exists so a Mac run can show and audit the real phone controls. It is
   test-only, never set in a shipped run, and it is how the capture shows the cob control at all.
6. **The evidence rig camera is created and destroyed inside the harness.** The player's camera sits behind
   the thrower and a 190 mm cob is a smudge at the frame edge, so the arc frames come from a camera 6 m out
   and 6.5 m up looking over the corn (the crop is ~3 m tall, which swallowed my first attempt).
7. **Correction to the M24 note:** `M22FeelSelfTest` does call `Application.Quit` — what it lacks is
   `runInBackground`, so launched unfocused the run stalls and looks like a hang. My later harnesses set it.

## Blockers

Nothing blocking. Standing, non-blocking: the licence question and the empty `appleDeveloperTeamID` above.
The two pre-existing M0 working-tree items are untouched as ordered: deleted `Assets/GingerbreadMan.meta`,
modified `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`. Unity re-serialised
`ProjectSettings/*.asset` and `Assets/Settings/PC_RPAsset.asset` during the builds — not committed. The four
`Assets/Resources/PerformanceTestRun*.json` files the builds removed were restored to HEAD.
Known leftover: three superseded frames `artifacts/review/world/m23-cob-arc-1..3.png` from an earlier
rig-camera attempt remain **untracked** (a delete was blocked by the safety scanner and they are not worth
forcing); the committed arc evidence is `m23-cob-side-1..4.png`.

## CAPTURES FOR TODD

- `artifacts/review/world/m23-cob-side-1..4.png` — **the throw arc, side on**: the cookie in the lane with
  the cob tracing downrange, plus `m23-cob-held.png` (the gold aim disc and the THROW control) and
  `m23-cob-hit.png` (the Husk staggered by the hit).
- `artifacts/review/world/sky-t0.png` (dusk) · `sky-t40.png` (night, the moon and its cloud).
- `artifacts/review/world/m20-field-play.png` (the corn field) · `m22-2026-09-24.png` (movement) ·
  `artifacts/front-end-title-m21.png` (front end).
- Numbers: `artifacts/m23-throw-report.txt`, `artifacts/m25-sky-report.txt`, `artifacts/m20-field-report.txt`,
  `artifacts/m22-feel-report.txt`.

## Last commits

this commit M23 pick up and throw · `4d2b253` M24 the Husk rename · `874d90f` M25 dusk/moonrise ·
`7a6a93f` M20 corn field · `7518c78` M22 movement · `ca83b66` M21 front end.
