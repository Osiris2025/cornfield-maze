# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `a2e1119` (M32 re-scoped). One page, not a log._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS volume files1). Unity 6000.3.23f1, URP._
_Order of 2026-09-25 (cap 10 passes): **M31, M32, M33, M34** — with M31b and M32b attached. Unity slot free._

## 1. The order, by number

| # | Milestone | State |
|---|---|---|
| 1 | M31 — the lane blends by **transparency** | GREEN `ea81ad8` — **but M31b is OPEN: it reads as haze** |
| 2 | M32 — the ground and the moonlight | **GREEN `a2e1119`** — matte by read-back; sheen is the puddles' job |
| 3 | M33 — scary lanterns, placeholder + swap-in point | not started |
| 4 | M34 — the scarecrow is THE antagonist | not started; silhouette test **OPEN, not passing** |

Previous order, all green: M29 `f46b9e0`, M27 `e460272`, M28 `34dbf49`. Earlier queue untouched: M20 `7a6a93f`,
M21 `ca83b66`, M22 `7518c78`, M23 `318b543`, M24 `4d2b253`, M25 `874d90f`, M25b `dc3b53a`, M26 `aa0f23a`.

**Next pass: M31b** — interior opaque (alpha 1.0 through the middle, not the current 0.867), transition
**0.15-0.25 m** instead of ~0.45 m, and the boundary **displaced by the strip's fine grain** so field and lane
interlock instead of cross-fading into haze. Constant half-width. Stock URP/Lit, keep the normal map.

## 2. M32 — what changed, and the defect it fixes

Todd saw the first wiring and ruled: **"iMO - the sheen AT ALL is a bug, maybe there should be a sheen in
puddles, but not over all."** He is right and it is the physically correct call — dead grass and dry dirt are
rough, and a moonlit field does not glint. So the ground is now matte **by design**, and reflectivity is M32b.

Ernie found why it glinted, in my own probe from the first wiring: `T_Ground_Field_M.png ... alphaSource=None`.
The smoothness lives in the **alpha channel** of the `_M` maps; `None` throws it away, URP reads the white
default, and `smoothness = metallicGloss.a * _Smoothness(1) = 1.0` over the whole floor — dry dirt as glass.

The fix is an import setting, so it is verified by **reading the value back**, never by a shinier frame:
`GroundMap.SmoothnessMap` → `alphaSource = FromInput`, `alphaIsTransparency = false`, format **BC7** (keeps
alpha; the probe now reports the chosen format and whether it can carry alpha, so DXT1 cannot creep back in).

**Measured, and the acceptance is green:** the built app samples the *imported* textures and reads **lane alpha
mean 0.137, field 0.095 — MATCH** against the committed art read independently in Blender (0.137 / 0.095), with
**0.0 % of either surface above 0.40**. Frame subtraction adds this: putting the mirror floor back changes the
rendered image by **0.01 of 255**, so the reflective and the matte state are the same picture.

## 3. Open — reported, not hidden

- **The `_M` maps are Read/Write** so the read-back can sample them: 2 x 4 MB of CPU copy at 1024 a phone should
  not keep. One-line removal once green (`importer.isReadable = kind == SmoothnessMap`) — the values do not
  change, only whether the CPU can also see the texture. Temporary and named, not shipped silently.
- **Flagged, unexplained:** binding the maps lifts the ground's luminance overall (whole-frame mean 33.68 →
  49.45 of 255) while leaving the peak unchanged. Broad, not localised — not the sheen Todd rejected — but
  measured and in the report rather than explained away.
- **The app's own band statistic lags its state change by a frame** (different peaks, 255 vs 163, for two
  pixel-identical PNGs). Where the app's number and the frame disagree, I trust the frame.
- **M31's corner keeps one hard edge** — a linear strip cannot fade two adjacent sides; `m31-ground-corner.png`.
- **M28's silhouette test is OPEN, not passing.** The old page claimed otherwise; the M31 page corrected it and
  it stays corrected. Todd's model is the answer; the placeholder is the bar, not a claim. M34 owns it.
- Carried: `CornMaze/StarUnlit` does not resolve in the player build; locked docs still say "Crumb Beast" and
  FSD §17 still describes a noise ground and a sphere Husk (**Ernie applies** — Harrow may not edit locked
  docs); the 60 fps floor is a phone target and unmeasured; §25.6's device listen pass still needs the phone.

## 4. CAPTURES FOR TODD

    artifacts/review/world/m32-reflection-on.png         the ground as it ships: matte, no highlight to find
    artifacts/review/world/m32-mirror-floor.png          the defect put back — and deliberately identical
    artifacts/review/world/m32-reflection-off.png        pre-M32: texture lost in the dark
    artifacts/m32-reflection-report.txt                  wiring, import settings, both read-backs, the diff
    artifacts/review/world/m31-ground-edge.png           M31's haze — the frame M31b has to fix
    artifacts/review/world/m28-scarecrow-silhouette.png  M28's silhouette — OPEN pending Todd's model

## 5. Questions for Todd (each with my recommendation)

1. **The brightness lift in §3** — if the field reads too live for you in `m32-reflection-on.png`, say so and I
   will pull the smoothness numbers down further. *Recommend: ship it — it is broad and dim and the peak is gone.*
2. **The dry ground variant** (`14d7ed6`, preview `artifacts/reference/ground-preview-dry.png`): withered-grass
   field, gravel-road lane, 2K. *Recommend: keep the current sets.* It is taste, so the frame is there and I have
   not switched it in.
3. **M34 needs your scarecrow model to close its silhouette test.** *Recommend: build the factory hook and the
   escalation multiplier now, leave the test open until the model lands, record its licence line before it ships.*

## 6. Standing rules this pass, and the one that changed

**New:** `scripts/shoot.sh` — frame capture launches the app **once**, shrinks the window into a corner and
minimises it, and kills the app the moment a **fresh** report lands (a stale report from an earlier run is not
evidence). No window is left on Todd's screen from this pass on. It is the only launcher the crew should use.

**Markers:** `/tmp/corn-crew-done` ABSENT (correct — M31b, M32b, M33, M34 remain). `/tmp/corn-crew-blocked`
ABSENT — no human-only blocker.

**Working tree left alone as ordered:** the two pre-existing M0 items; Ernie's ground art edits (`_R` maps,
previews, the new `T_Ground_Puddle*` art) uncommitted as not mine. Unity re-serialised `ProjectSettings/*`,
`PC_RPAsset.asset` and `UniversalRenderPipelineGlobalSettings.asset` during builds.

## 7. Last commits

`a2e1119` M32 matte ground + the alphaSource fix · `d26324e` status after M32 v1 · `7062531` M32 v1 the ground
catches the moonlight · `8b2c368` status after M31 · `ea81ad8` M31 lane blends by transparency · `09a59c3` matte
ground + puddle art (Ernie) · `f655ffd` the mirror floor is an import bug (Ernie) · `850a8ac` order M31b ·
`34dbf49` M28 the scarecrow · `e460272` M27 first person · `f46b9e0` M29 the ground.
