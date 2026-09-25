# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `e460272` (M27). This file is the current page, not a log._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS, files1). Unity 6000.3.23f1._
_Unity slot free at STATE-A; one writer at a time, `build-mac.sh` guard intact._

## 1. The order, by number

| # | Milestone | State |
|---|---|---|
| 1 | M29 — the ground | **GREEN** — `f46b9e0` |
| 2 | M27 — first person, third person as a choice (§25.8) | **GREEN this pass** — `e460272` |
| 3 | M28 — the chaser becomes a scarecrow (§25.8) | not started — next pass, owner Dough |

Earlier queue still green: M20 `7a6a93f`, M21 `ca83b66`, M22 `7518c78`, M23 `318b543`, M24 `4d2b253`,
M25 `874d90f`, M25b `dc3b53a`, M26 `aa0f23a`.

**The done marker is NOT written** — M28 is still open.

## 2. M27 — what the camera does now

- **First person is the default.** The camera sits at the cookie's eyes; the look drag turns the BODY and
  pitches the head; no boom and no LookAt between the two. The eye height is **measured** off the cookie's
  own renderer bounds — he is 1.799 m tall, the eye sits at 1.655 m (0.92 of it) — not copied from a human.
- **The cookie is not drawn in front of his own camera.** His renderers drop to ShadowsOnly in first person
  (4 of 4), so his shadow stays (§25.2's table) and his body is still §5's health model. He returns in full
  for the death shot, which stays third-person; the eat sequence itself is untouched.
- **Third person is a toggle, and it is exercised.** V on the Mac, VIEW on the phone, both into the same
  `ToggleFirstPerson()`. Measured: True → False, switches 0 → 1, and the rig comes back 3.09 m behind and
  2.24 m up (5.2 m nominal, pulled in by M20's corn guard in a lane), near clip 0.12 m.
- **The HUD grew §5's dough meter.** It was specified at §19 — "top-left, 220 pt wide" — and had never been
  built. In first person it is the *only* health read the player has, so it exists now: a 214 pt bar, colour
  by state, the state word beside it, reading `DoughIntegrity` and nothing else. Still one health model.

Measured on the built Mac app — `artifacts/m27-fp-report.txt`:

```
default mode      firstPerson=True at startup
first person      eye 1.655 m up, 1.655 m from the player = no boom; cookie 4/4 renderers ShadowsOnly
corner frame      nearest corn 1.792 m from the eye against a 0.060 m near clip — the crop is outside
                  the near plane, so it renders rather than being cut through
toggle            True -> False, switches 0 -> 1; third person 3.09 m behind / 2.24 m up, near clip 0.120
phone control     VIEW 1ST at -104,-87, 96x96
dough meter       fill 123 of 214 pt, "DOUGH 57%  softening"
frame time        avg 3.49 ms, p95 4.58 ms over 861 frames; worst 17.90 ms — ONE hitch, not a plateau
feel contract     dead zone 0.12 -> full at 0.30, look on the whole right side above x=0.46 of the
                  screen, clamp -87 .. +48 deg — §25.2, unchanged
```

### Three defects the first M27 run found — all by reading the report, not the code

1. **The cookie was drawn across his own camera.** `SetFirstPerson` only fires on a *change*, and first
   person is the initial state, so nothing ever hid him. The report said `0 of 4 renderers ShadowsOnly`
   where 4 was wanted. Fixed by applying the visibility at spawn.
2. **The dough meter read 100 % while the cookie was visibly half dissolved.** `ApplyDissolve` moves the
   model and the materials but does *not* set the dissolve number, and `UpdateDissolveFromRain` pulls that
   number back toward the rain clock every frame — so the test hook moved the picture and not the value the
   meter reads. Both are set now, and the meter moved (118 of 214 pt at 0.45).
3. **The corner frame photographed a face full of leaves.** Aiming straight into the inside corner is
   technically "corn very close" and useless as evidence — you cannot tell it is a corner. It now aims down
   the turn: the jutting block close on the left, the lane continuing on the right.

Worth recording: in first person the **nearest corn in the maze is 1.79 m away**, so the near-clip question
in §25.8 answers itself — with the walls where this maze puts them, the crop can never reach the lens.

## 3. In flight / carried over

- **M28 scarecrow** — next pass. The sphere blob is untouched at `Husk.cs:129-175`.
- **NEW, and it is Todd's call: a second ground variant arrived mid-pass** (`14d7ed6`, Ernie) — a *dry*
  register from six more Poly Haven CC0 sets at 2K: withered grass as the field, gravel road as the lane.
  Previews: `artifacts/reference/ground-preview-dry.png` (and `-dry-raw.png`). The maze still wears the
  first set; switching is re-running `scripts/ground_build.py` for that variant, not a code change.
- **Phone texture budget (M29)** — the 4 MB readable CPU copy of the lane-edge mask. Recommend the
  design-time jitter bake rather than downscaling the art.
- **Carried, unchanged:** the §25.6 device listen pass (needs the phone); `CornMaze/StarUnlit` does not
  resolve in the player build so the star twinkle is inert (Always Included Shaders is the cheap fix);
  locked docs still say "Crumb Beast" (`docs/DECISIONS.md`, the FSD, `…/INDEX.md` — Ernie applies); the
  60 fps floor is a phone target and unmeasured.
- FSD §17 still has no text for the photographic ground; the §25.8 section itself is current.

## 4. Questions for Todd (each with my recommendation)

1. **First person — is this the read you wanted?** `m27-fp-lane.png` is standing in a lane;
   `m27-fp-corner.png` is the turn. *Recommend: accept — the look turns the body, the crop is 1.8 m out and
   nothing clips, and the corner that was unworkable in third person is legible.*
2. **Is the dough meter doing its job?** Top-left in every frame: bar plus "DOUGH 57%  softening".
   *Recommend: accept — in first person it is the only health you have, and it reads mid-run.*
3. **Which ground ships — leaf litter (now) or the dry straw register?** `m29-ground-*.png` against
   `artifacts/reference/ground-preview-dry.png`. *Recommend: leaf litter for a night hunt; the dry straw
   register will read almost white under the moon and fight the dough meter and the gold. Your call.*
4. **The moon, now that first person is in.** First person sees far more sky than the boom camera did, and
   §25.8 says the moon was built for a camera that no longer exists. No frame of it this pass (the capture
   ran under storm light). *Recommend: capture one first-person look-up frame next pass and judge it there
   — do not re-aim the moon until it has been seen from the new camera.*
5. **The one 17.9 ms frame.** A single hitch in 861 frames, not a plateau (p95 is 4.58 ms). *Recommend:
   leave it; if it repeats on the phone it is a GC spike, and that is a device job.*

## 5. Blockers

None for the build or the code. Nothing in §4 stops the work.
Working-tree items left alone as ordered: deleted `Assets/GingerbreadMan.meta` and the four
`Assets/Resources/PerformanceTestRun*.{json,meta}`. Unity re-serialised `ProjectSettings/*`,
`PC_RPAsset.asset` and `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` during builds.

## 6. CAPTURES FOR TODD

    artifacts/review/world/m27-fp-lane.png     first person, standing in a lane
    artifacts/review/world/m27-fp-corner.png   first person at the turn — the frame that matters
    artifacts/review/world/m27-tp-toggle.png   third person after the toggle, §25.2's rig back
    artifacts/review/world/m29-ground-lane.png night, eye height, down a lane (M29)
    artifacts/review/world/m29-ground-edge.png the lane margin, ragged (M29)
    artifacts/m27-fp-report.txt                every number above, with its source

## 7. Last commits

`e460272` M27 first person · `14d7ed6` the dry ground variant · `f30ea16` M29 status · `f46b9e0` M29 the
ground · `551c437` M25b status · `dc3b53a` M25b the moon · `aa0f23a` M26 threat audio · `318b543` M23 cob
throw · `4d2b253` M24 the Husk · `874d90f` M25 dusk/moonrise · `7a6a93f` M20 corn field.
