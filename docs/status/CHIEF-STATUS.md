# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `34dbf49` (M28). This file is the current page, not a log._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS, files1). Unity 6000.3.23f1._
_One writer at a time; `build-mac.sh` guard intact; Unity slot free at STATE-A._

## 1. The order, by number — ALL THREE GREEN

| # | Milestone | State |
|---|---|---|
| 1 | M29 — the ground | **GREEN** — `f46b9e0` |
| 2 | M27 — first person, third person as a choice (§25.8) | **GREEN** — `e460272` |
| 3 | M28 — the chaser becomes a scarecrow (§25.8) | **GREEN this pass** — `34dbf49` |

Earlier queue still green: M20 `7a6a93f`, M21 `ca83b66`, M22 `7518c78`, M23 `318b543`, M24 `4d2b253`,
M25 `874d90f`, M25b `dc3b53a`, M26 `aa0f23a`. **The done marker is written (3 lines).**

## 2. M28 — what the creature is now

The sphere blob is gone. 60 primitives in the §17 register, no third-party model: a rough timber cross
with the upright and crossbar showing where the coat does not cover them; a burlap sack head, loose and
set on a wrong tilt that stays wrong; hollow sockets with no eyeballs and a faint glow in each, the only
light on it; a stitched seam mouth (thirteen small dark stitches, not a grin); dry husks bursting out of
a tattered coat, sleeves ending in straw, the hem torn. **2.31 m tall, 1.76 m across.**

**The walk is a lurch, not a glide.** One surge per 0.72 s, speed swinging 0.45x to 1.55x of MoveSpeed.
A full sine averages exactly MoveSpeed, so the ground a thrown cob buys (§25.3) does not move — the tell,
not a balance change. Measured on the built app: peak **3.64 m/s = exactly 1.55x** the base speed, one
surge every **0.71 s** against the designed 0.72 s, mean 2.11 m/s over a 5 s window (the residual is the
window cutting the first and last surge in half, plus one corner-repath frame). Speed, catch range and
the 8 s reform are untouched.

**The silhouette test passes.** `m28-scarecrow-silhouette.png`: 12 m out, night01=1.00, every renderer
swapped to a flat black unlit material. The shape reads as a stake with a sack head and the crossbar out
to both sides. It is not a sphere, not a person, not a blob.

M23's cob harness re-run fresh against the new creature — `artifacts/m23-throw-report.txt`: range 7.104 m
PASS, hit PASS (18 → 12), stagger 1.10 s, ground bought 2.585 m, scattered after three hits.

Two things the report says plainly rather than hides:

- **The crossbar (1.76 m) is wider than the hit volume (1.0 m across)** — a cob through a sleeve end
  passes through. Widening the volume would move M23's measured 7.104 m, and M28 is a look change, so it
  was not widened; it is written down.
- **The order's "breaks the corn line" is not true at 2.2 m** — the corn stands 2.90-3.20 m. It breaks
  the *lane* line (the cookie is 1.80 m). The silhouette frame is the honest answer, not the prose.

## 3. In flight / carried over

- **Ground variants on the branch, not mine to own:** `1978374`, `a890aff` (Ernie) — a dry straw register
  and a mixed one, 2K, from more Poly Haven CC0 sets. Previews in `artifacts/reference/`. The maze still
  wears the first set; switching is re-running `scripts/ground_build.py`, not a code change.
- **Phone texture budget (M29)** — the 4 MB readable CPU copy of the lane-edge mask. Recommend the
  design-time jitter bake rather than downscaling the art.
- **§25.6 device listen pass** — needs the phone; the Mac mix is measured (`artifacts/m26-threat-listen.wav`).
- **Carried, unchanged:** `CornMaze/StarUnlit` does not resolve in the player build so the star twinkle is
  inert; locked docs still say "Crumb Beast" (`docs/DECISIONS.md`, the FSD, `…/INDEX.md` — Ernie applies);
  the 60 fps floor is a phone target and unmeasured (Mac build this pass: 2.35 ms avg / 4.34 ms worst).
- **Locked-doc drift Ernie owns:** FSD §17 still describes a noise ground that no longer exists (M29) and
  a sphere Husk that no longer exists (M28).

## 4. Questions for Todd (each with my recommendation)

1. **Is the scarecrow scary enough, or too clean?** `m28-scarecrow-lane.png` and `-close.png`.
   *Recommend: ship it — the tilt and the empty glowing sockets do the work, and let M26's sound carry the
   threat. If you want more, the cheap lever is more straw and a bigger head, not a new model.*
2. **Which ground ships — leaf litter (now) or the dry straw register?** `m29-ground-*.png` against
   `artifacts/reference/ground-preview-dry.png`. *Recommend: leaf litter for a night hunt; dry straw reads
   near-white under the moon and fights the dough meter and the gold.*
3. **The moon from first person.** §25.8 says the moon was built for a camera that no longer exists; first
   person sees far more sky and there is no look-up frame yet. *Recommend: one first-person look-up frame
   next pass before anything is re-aimed.*

## 5. Blockers

None. Nothing in this list needed a human; the blocked marker was never written.
Working-tree items left alone as ordered: deleted `Assets/GingerbreadMan.meta` and the four
`Assets/Resources/PerformanceTestRun*.{json,meta}`. Unity re-serialised `ProjectSettings/*`,
`PC_RPAsset.asset` and `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` during builds.

## 6. CAPTURES FOR TODD

    artifacts/review/world/m28-scarecrow-silhouette.png  THE ACCEPTANCE TEST — 12 m, night, flat black
    artifacts/review/world/m28-scarecrow-lane.png        the scarecrow at a lane's end, dusk
    artifacts/review/world/m28-scarecrow-close.png       the head: glowing sockets, stitched seam, tilt
    artifacts/review/world/m27-fp-lane.png               first person in a lane, dough meter top-left
    artifacts/review/world/m27-fp-corner.png             first person at the turn — where FP breaks
    artifacts/review/world/m27-tp-toggle.png             third person after the toggle
    artifacts/review/world/m29-ground-lane.png           the lane: ragged margin, photographic gravel
    artifacts/review/world/m29-ground-edge.png           the lane margin, close up
    artifacts/m28-scarecrow-report.txt                   every M28 number, the regression, the verdict

## 7. Last commits

`34dbf49` M28 the scarecrow · `a890aff`, `1978374` ground variants (Ernie) · `e460272` M27 first person ·
`f30ea16` M29 status · `f46b9e0` M29 the ground · `551c437` M25b status · `dc3b53a` M25b the moon ·
`aa0f23a` M26 threat audio · `318b543` M23 cob throw · `4d2b253` M24 the Husk · `874d90f` M25 dusk/moonrise.
