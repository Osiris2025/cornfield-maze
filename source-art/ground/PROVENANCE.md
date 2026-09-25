# Ground textures — provenance and derivation

The maze floor is no longer procedural noise. It is built by `scripts/ground_build.py` from six
photographic PBR sets that Todd supplied in `downloads/ground_textures` (outside this repo), and the
result is blended, graded and baked into `Assets/Resources/Ground/`.

## Sources — Poly Haven, CC0

Every set is a **Poly Haven** asset, identified by its folder name. Verified against the Poly Haven
API on 2026-09-25 (`ground_license_check.py`): all six ids exist and return their canonical names;
authors are **Rob Tuytel** (four sets) and **eye-candy.xyz** (two).

Poly Haven's licence page (`polyhaven.com/license`, retrieved 2026-09-25) says, verbatim:

> Our assets are all licensed as CC0. […] You can use our assets for any purpose, including
> commercial work. You do not need to give credit to the authors. You can redistribute them.

CC0 means **no attribution obligation and no share-alike obligation** — the reason these are usable
where the CC BY-SA moon candidates were rejected. The credit below is recorded because it is honest
practice, not because the licence demands it.

| set (folder) | author | role in the blend |
| --- | --- | --- |
| `forest_floor_1k` | Rob Tuytel | field base — damp soil and fine litter |
| `forest_leaves_04_1k` | Rob Tuytel | field — the leaf litter layer |
| `brown_mud_leaves_01_1k` | Rob Tuytel | field — wet mud patches |
| `gravel_ground_01_1k` | eye-candy.xyz | lane base — worn gravel |
| `stony_dirt_path_1k` | Rob Tuytel | lane — stones and dust overlay |
| `grass_path_2_1k` | eye-candy.xyz | lane — grass creeping in at the edge band |

Each set ships `*_diff_1k.jpg` (sRGB), `*_nor_gl_1k.exr` (OpenGL-convention normal),
`*_rough_1k.jpg|exr`, `*_disp_1k.png` and `*_arm_1k.jpg`, 1024×1024.

Source diffuse checksums (sha256), so a re-derivation can prove it used the same pixels:

| set | sha256 of `*_diff_1k.jpg` |
| --- | --- |
| brown_mud_leaves_01 | `c598c555f13cd532…` |
| forest_floor | `f12e5adea1741f9e…` |
| forest_leaves_04 | `a898e6ef0668a40f…` |
| grass_path_2 | `7f30b163739435c1…` |
| gravel_ground_01 | `c3a12b0e3a939991…` |
| stony_dirt_path | `a662000ff3f7f64e…` |

Full digests: `python3 scripts/ground_build.py` re-reads the same files; the truncated digests here
identify the files, and `/tmp/ground-src-sha.json` held the full set for the derivation run.

## What the blend does

Two composed materials, each albedo + normal + roughness:

- **Field** (`T_Ground_Field*`) — `forest_floor` as the base, then leaf litter and mud patches laid in
  by *tileable* value-noise fbm masks, so leaf density varies in clumps instead of evenly.
- **Lane** (`T_Ground_Lane*`) — `gravel_ground_01` base, `stony_dirt_path` stones/dust over it, and
  `grass_path_2` allowed in only inside an edge band, which is what makes the lane margin look like
  ground that is being reclaimed rather than a cut stripe.
- **Lane edge** (`T_Ground_LaneEdge`) — a 1K tileable fbm. The runtime uses it to break the boundary
  between lane and field: the lane is *superimposed* on the field rather than being a separate strip,
  so the join is ragged instead of a straight line.

Tileability is measured, not asserted. The builder reports the wrap discontinuity as a multiple of a
normal neighbouring-column step (1.0 = seamless): field **0.94× / 1.07×**, lane **1.10× / 1.07×**. All
blend masks are periodic by construction, and all six sources are already seamless.

## The grade

The photographs are sunny autumn. Faithful-to-source ground under a Halloween dusk sky is wrong twice
over — too saturated and too bright — so `grade(arr, saturation, gain, tint)` pulls the field to 0.72
saturation / 0.84 gain and the lane to 0.45 / 1.10. The lane is deliberately pulled *greyer and
lighter*: graded dark like the field, it vanished into the litter and stopped reading as a path.

Both previews are kept so the call is reviewable and reversible:

- `artifacts/reference/ground-preview.png` — graded (what ships)
- `artifacts/reference/ground-preview-raw.png` — raw blend, ungraded

## Known synthetic tells

Honest limits, for the record:

1. The lane boundary is a soft lerp. It has no stones or leaves *straddling* the join; scattering a few
   leaf cards across the margin at runtime would sell it further.
2. The preview is a compositing schematic, not a render. It shows the blend logic, not the lit result.
3. At 1024² × six maps the set is heavier than a phone wants as-is; a mobile pass (512² or ASTC) is
   worth doing before shipping.
