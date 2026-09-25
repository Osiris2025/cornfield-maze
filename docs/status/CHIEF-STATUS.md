# Corn Field Maze — Chief Status

**Updated:** 2026-09-24 21:58 EDT — overnight loop, pass 6 of the night
**Milestone:** **M26 is GREEN this pass. ALL SIX MILESTONES IN THE QUEUE ARE GREEN — the mission is done.**
**Project root:** `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS, volume files1). The old files2 tree is retired.
**Unity slot:** free when this pass began; one writer at a time used it, `build-mac.sh` guard intact.

## 1. What is green right now — the whole queue

| M | What | Owner | Evidence |
|---|---|---|---|
| M21 | front end: title, help, intro, pause | Dough+Furrow | `ca83b66` · `artifacts/front-end-title-m21.png` |
| M22 | movement feel: dead zone, look, sprint, no lane auto-centring | Furrow | `7518c78` · `artifacts/m22-feel-report.txt` |
| M20 | the corn blocks in the maze, CUTOUT leaves, LOD chain, boom fix | Squall+Dough | `7a6a93f` · `artifacts/m20-field-report.txt` |
| M25 | dusk, the Halloween sky, the moonrise, cloud occultation | Squall | `874d90f` · `artifacts/m25-sky-report.txt` |
| M24 | the rename to the Husk, everywhere in code | Furrow+Rattle | `4d2b253` |
| M23 | pick up and throw the cob | Furrow+Dough | `318b543` · `artifacts/m23-throw-report.txt` · `m23-cob-side-1..4.png` |
| M26 | the field tells you where the threat is | Rattle+Squall | this commit · `artifacts/m26-threat-report.txt` · **`artifacts/m26-threat-listen.wav`** · `m26-husk-close.png` |

M26, measured on the built Mac app (`-audiotest`). One normalised threat from `|Husk − player|` in cells
drives both consumers; every line below is read back off the **live AudioSources**, not re-derived:

```
threat       1 at 1.5 cells -> 0 at 8 cells:  0.000 -> 0.308 -> 0.615 -> 0.769 -> 0.923 -> 1.000   PASS
rustle gain  0.16 at 8 cells -> 0.40 at 1.5:  0.160 -> 0.234 -> 0.308 -> 0.345 -> 0.382 -> 0.400   PASS
rustle pitch +0-4 %:                          +0.0% -> +1.2% -> +2.5% -> +3.1% -> +3.7% -> +4.0%   PASS
low partial  only inside 3 cells:             0.000 -> 0.000 -> 0.000 -> 0.000 -> 0.097 -> 0.150    PASS
music bed    same number, cap 0.42:           0.260 -> ... -> 0.338 (never above the cap)          PASS
tension      one source of truth:             0.000 -> ... -> 1.000                                PASS
the field's own mix: far 0.0216 -> near 0.0515 RMS = 2.4x louder as the thing closes               PASS
```

**The listen artifact is the deliverable, and it is a real file:** `artifacts/m26-threat-listen.wav` —
6.06 s, 44.1 kHz stereo, the game's own final mix tapped at the AudioListener, 1 s held at each of
6 / 8 / 4 / 3 / 2 / 1.5 cells. `scripts/m26_verify_wav.py` **decodes the file** (it does not read the
game's report) and measures 1.00× → 1.31× → 1.69× → 1.62× → 1.97× → **2.39×**: two independent
measurements of the same recording, agreeing.

## 2. In flight / next

**Nothing. The queue is closed.** `/tmp/corn-crew-done` is written.

## 3. Questions for Todd (morning) — each with my recommendation

1. **THE LISTEN PASS NEEDS YOUR PHONE.** §25.6 is explicit that the rustle and the bed cannot be judged in
   the editor and wants a **device recording**. I have the Mac mix as a real WAV — the numbers are right and
   the audio rises as it should — but the Mac is not the phone: speaker, mix and iOS audio session all differ.
   *Recommend:* listen to `artifacts/m26-threat-listen.wav` first (it is short, 6 s), then run
   `-audiotest` on the iPhone when a device is attached. This is the one outstanding item of the night.
2. **The gust cycle rides over the threat term.** The raw RMS dips ~4 % at 3 cells because the pre-existing
   wind gust and storm shaping sit on top. §25.6 says the threat term rides "on top of the existing shaping",
   so I left it alone rather than refactor verified M20/M25 audio. *Recommend:* keep; if you want the threat
   to dominate, we can widen the threat term instead.
3. **The scatter rule (M23, my call).** Three cob hits scatter the Husk and it re-forms in 8 s rather than
   dying. *Recommend:* keep — §25.3's cob "is not a weapon that kills" and §8 forbids a trivially removable threat.
4. **The Mac throw key (M23).** The touch controls are mobile-only, so **F** drives pick-up/throw in the Mac
   build. *Recommend:* keep; it is gated on `!ShouldShow` so it can never double-fire on the phone.
5. **Locked docs still say "Crumb Beast"** (carried from M24): `docs/DECISIONS.md`,
   `docs/reference/CORN-FIELD-MAZE-FSD.md`, `docs/reference/INDEX.md`. *Recommend:* Ernie applies the rename.
6. **Licences** — `gb_man.fbx` and both corn source models are third-party with licence **unknown**
   (`docs/ASSETS-INVENTED.md`). *Recommend:* confirm before any App Store submission.
7. **The 60 fps floor on the phone** is unmeasured (no device, `appleDeveloperTeamID` empty).
   *Recommend:* measure on your iPhone when you are up — M22's frame times on the Mac were 3.14 ms avg, and
   that is not this claim.
8. **`capture.sh` cannot resolve its window** while another app named "Corn Field Maze" is on the machine
   (four passes now); frames come from an in-engine `ScreenCapture`. *Recommend:* Lantern reworks it.

## Crew

| Name | Handle | Role | State |
|---|---|---|---|
| Harrow | @corn-chief | chief | closed the queue; verified M26 himself; wrote this file |
| Dough | @corn-art | cookie, silhouette, materials | M20 materials + M23 cob |
| Furrow | @corn-gameplay | controller, maze, HUD, touch | M22, M23, M24 |
| Squall | @corn-world | corn field, sky, dusk, storm | M20, M25 |
| Rattle | @corn-audio | bed, cues, jingle, rustle | **M26 landed**: one threat number, the low partial, the WAV |
| Lantern | @corn-qa | builds, captures, measured evidence | verified every milestone's build; `capture.sh` rework open |

## Decisions made this pass (recorded where the morning reader will find them)

1. **The threat term is multiplicative on the existing shaping and identical to the old expression at
   `threat = 0`.** At 8 cells the field sounds exactly as it did before M26 — the mechanic adds nothing when
   the Husk is far, which is what keeps it an honest signal rather than a mood knob.
2. **The low partial is its own source, not a filter on the rustle.** A looping procedural bed crossfaded at
   the seam (`MazeMoodSynth.StalkLow`), faded in only inside 3 cells, so it cannot click and cannot leak into
   the far-field mix.
3. **`Tension()` keeps its other terms.** The music's tension is now `max(gold proximity, dead-end proximity,
   threat)`. The threat is a fourth way to raise the same term rather than a rival score, so the bed still
   builds near the gold and in dead ends, and the 0.42 cap and the jingle ducking are untouched.
4. **The threat is measured in the raw mix, not asserted.** `RustleThreatGain`, `Tension01` and `StalkLow01`
   are exposed so verification reads the spec's numbers off the live sources; the WAV then proves the result
   is audible, and the python check proves the WAV is what it claims.
5. **The evidence tap is `OnAudioFilterRead` on the AudioListener** — the game's own final mix, buffered on
   the audio thread and written from the main thread. No third-party recorder, no virtual audio device, and
   nothing in the audio path is bypassed.
6. **The sweep holds the Husk at each distance every frame** rather than letting it walk, because a moving
   threat makes the per-distance number a lie about one point. Measured cells matched requested to 0.01.

## Blockers

**Nothing blocking the build or the code.** The one outstanding item is the §25.6 **device listen pass**,
which needs a physical iPhone — parked in the status file, not raised as a 2 am blocker.
The two pre-existing M0 working-tree items are untouched as ordered: deleted `Assets/GingerbreadMan.meta`,
modified `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`. Unity re-serialised
`ProjectSettings/*.asset` and `Assets/Settings/PC_RPAsset.asset` during the builds — not committed. The four
`Assets/Resources/PerformanceTestRun*.json` files the builds removed were restored to HEAD.
Known leftover: three superseded frames `artifacts/review/world/m23-cob-arc-1..3.png` remain **untracked**
(a delete was blocked by the safety scanner; not worth forcing).

## CAPTURES FOR TODD — the night's work, in order

1. `artifacts/front-end-title-m21.png` — the front end over a live corn field.
2. `artifacts/review/world/m20-field-play.png` — **the corn field is really in the maze** (M20).
3. `artifacts/review/world/m22-2026-09-24.png` — movement, plain launch (M22).
4. `artifacts/review/world/sky-t0.png` then `sky-t40.png` — **dusk, then the moonrise** (M25).
5. `artifacts/review/world/m23-cob-held.png`, `m23-cob-side-1..4.png`, `m23-cob-hit.png` — **the cob arc** (M23).
6. `artifacts/review/world/m26-husk-close.png` + **`artifacts/m26-threat-listen.wav`** — **the field
   telling you where the threat is** (M26). Listen to the WAV: it is 6 s, one second per distance, and the
   stalks close in as it plays.
   Numbers: `m26-threat-report.txt`, `m23-throw-report.txt`, `m25-sky-report.txt`, `m20-field-report.txt`,
   `m22-feel-report.txt`.

## Last commits

this commit M26 rustle + music off one threat number · `318b543` M23 pick up and throw · `4d2b253` M24 the
Husk rename · `874d90f` M25 dusk/moonrise · `7a6a93f` M20 corn field · `7518c78` M22 movement · `ca83b66` M21.
