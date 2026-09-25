# CHIEF-STATUS — Corn Field Maze

_Updated 2026-09-25 by Harrow (@corn-chief). HEAD `f46b9e0` (M29). This file is the current page, not a log._
_Project root `/Volumes/files1/projects/cornmaze/CornFieldMaze` (APFS, files1). Unity 6000.3.23f1._
_Unity slot free at STATE-A; one writer at a time, `build-mac.sh` guard intact._

## 1. The order, by number

| # | Milestone | State |
|---|---|---|
| 1 | **M29 — the ground** (wire the derived field/lane textures) | **GREEN this pass** — `f46b9e0` |
| 2 | M27 — first person, third person kept as a choice (§25.8) | not started — next pass, owner Furrow |
| 3 | M28 — the chaser becomes a scarecrow (§25.8) | not started — owner Dough |

Earlier queue all still green: M20 `7a6a93f`, M21 `ca83b66`, M22 `7518c78`, M23 `318b543`, M24 `4d2b253`,
M25 `874d90f`, M25b `dc3b53a`, M26 `aa0f23a`.

**The done marker is NOT written** — the list is not finished.

## 2. M29 — what is actually in the world now

Todd's six Poly Haven CC0 sets were already derived and committed (`7b85f5f`, provenance in
`source-art/ground/PROVENANCE.md`). This pass wired them in.

- **The noise is deleted, not shadowed.** `Materials.Gravel/FieldGrass/PathGrass` and their generators
  (`MakeGravelTex`, `MakeGrassTex`, `Fbm`, `ValueNoise`, `Hash01`) are gone. Nothing else referenced them —
  grep over `Assets/` is clean. A 128 px tile over a 2 m lane could only read as synthetic.
- **The lane is superimposed as geometry.** 477 lane pieces are meshes whose boundary vertices are pulled
  inward by the derived raggedness mask (`GroundLaneMesh`), sampled at the *world* position so overlapping
  pieces jitter identically and the margin stays continuous. Chosen over a blend shader so URP/Lit survives:
  lighting, normal maps and the `PathMudWetness` path all keep working on the lane.
- **Tiling: 2.00 m per repeat**, carried in the lane UVs (world-metres ÷ 2) and in a property block on the
  field cube. Measured off the geometry: a 4.00 × 2.08 m piece has a UV span of 2.00 × 1.04.

Measured on the built Mac app — `artifacts/m29-ground-report.txt`:

```
ragged margin     a boundary line wanders by up to 0.42 m, mean 0.37 m over 1908 faces
metres/repeat     2.00 m (read off the mesh, not from the constant)
texture weight    gpu ~5.83 MB + cpu ~4.00 MB  (DXT1/DXT5, as the build chose)
frame time        avg 3.84 ms / p95 4.55 / worst 5.51, after a 90-frame warm-up
                  (M20's noise floor: 3.14 / 6.68 / 14.80 — same harness, same machine)
import settings   albedo sRGB+Repeat; normal = NormalMap, non-Color, Repeat, green NOT flipped;
                  _R and LaneEdge non-Color + sRGB off — all read back from the importers
```

Smoothness is a constant per material **on purpose**: driving it from `_R` means packing roughness into
URP's metallic/smoothness slot — a second full-size map in memory for a dry/wet variation the mud system
already drives at runtime. Stated in the report.

### Two defects this pass caught, both in my own instrument

1. **The first ragged-margin measurement was wrong and said so.** It compared boundary vertices against the
   mesh's bounding box — but the box is computed *from* the jittered vertices, so it moves with them and the
   cut can only ever read 0.00 m. It reported "0 of 6287 vertices cut back" about a lane that was already
   ragged. It now measures the **wander of each boundary line** (identified by UV column, which is computed
   before the jitter): 0.42 m, over 1908 of 1908 faces.
2. **The first close-up frames photographed the sky.** `Quaternion.Euler` x positive looks *down* in Unity's
   left-handed frame, while the walker's `_pitch` is negative when it looks up. Two runs produced sky-and-corn
   close-ups before that was noticed; the committed frames are the third run.

Also fixed en route: a frame-time sample that reported **1713 ms/frame** (three frames of shader compilation,
unfocused and uncapped — a stall sold as a frame time), and `Profiler.GetRuntimeMemorySizeLong` returning 0
for every DXT map in a player build (residency is now computed from format × size × mips, with the profiler
column printed beside it for comparison).

## 3. In flight / carried over

- **M27 first person** — next pass. §25.8 lands on `MobileControls` and on the HUD's dough meter, which in
  first person becomes the only health read the player has.
- **M28 scarecrow** — the sphere blob is untouched, still at `Husk.cs:129-175`.
- **Phone texture budget.** The 4 MB CPU copy of `T_Ground_LaneEdge` exists only because the mesh builder
  reads the mask at world-build time. Recommended before submission: bake the jitter offsets at design time
  and drop Read/Write from the mask. Not done — that is a pipeline, not a wiring fix.
- **Carried, unchanged:** the §25.6 device listen pass (needs the phone); `CornMaze/StarUnlit` does not resolve
  in the player build so the star twinkle and its blend properties are inert (flagged last pass, still open —
  adding it to Always Included Shaders is the cheap fix); locked docs still say "Crumb Beast"
  (`docs/DECISIONS.md`, the FSD, `docs/reference/INDEX.md` — Ernie applies); the 60 fps floor is a phone target
  and unmeasured.
- **FSD §17 has no description of the photographic ground.** The text for it is in the M29 report; Ernie applies.

## 4. Questions for Todd (each with my recommendation)

1. **Is the ground scale right?** 2 m per repeat — `m29-ground-field.png` is the frame to judge it on.
   *Recommend: accept. The leaf fragments in that frame are roughly hand-sized, which is what 1K over 2 m gives.*
2. **Does the margin read as superimposed rather than painted on?** `m29-ground-edge.png`. *Recommend: accept —
   it is ragged geometry and it measures 0.42 m of wander.*
3. **Phone weight.** *Recommend: keep 1024² — 5.83 MB of GPU texture is not the problem, the 4 MB readable mask
   copy is; schedule the design-time jitter bake rather than downscaling the art.*
4. **Still open from last pass:** the moon should be judged again once §25.8's first-person camera exists, since
   first person sees far more sky than the boom camera did.

## 5. Blockers

None for the build or the code. Nothing in §4 stops the work.
Working-tree items left alone as ordered: deleted `Assets/GingerbreadMan.meta` and the four
`Assets/Resources/PerformanceTestRun*.{json,meta}`. Unity re-serialised `ProjectSettings/*`, `PC_RPAsset.asset`
and `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` during builds (not committed).

## 6. CAPTURES FOR TODD

    artifacts/review/world/m29-ground-lane.png    night, eye height, looking down a lane
    artifacts/review/world/m29-ground-field.png   the field floor close up (dusk, lit)
    artifacts/review/world/m29-ground-edge.png    the lane margin close up — the blend frame
    artifacts/m29-ground-report.txt               every number above, with its source

## 7. Last commits

`f46b9e0` M29 the ground · `48c8f0f` the stale ORDER comment · `e05f66d` the M29 order ·
`551c437` M25b status · `dc3b53a` M25b the moon · `aa0f23a` M26 threat audio · `318b543` M23 cob throw ·
`4d2b253` M24 the Husk · `874d90f` M25 dusk/moonrise · `7a6a93f` M20 corn field · `7518c78` M22 movement ·
`ca83b66` M21 front end.
