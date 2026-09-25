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

## Second batch — Poly Haven's own library, at 2K (variant `dry`)

The six sets above were Todd's drop and are 1K leaf litter. Poly Haven publishes **862 texture sets,
several at 8K**, so `scripts/ground_fetch_polyhaven.py` pulls the ones that actually read as a
harvested October cornfield instead of a forest floor — six more sets, all CC0, same publisher, same
licence as above (no new obligation). Downloads land in `downloads/ground_textures_ph/` with a
`manifest.json` holding every file's size and sha256:

| set | role | mean linear luminance |
| --- | --- | --- |
| `withered_grass` | field base — a full carpet of dead straw | 0.59 |
| `dry_mud_field_001` | bare earth showing through the grass | 0.35 |
| `dry_decay_leaves` | sparse fallen leaf litter | 0.33 |
| `gravel_road` | lane base — the compacted crown of a track | 0.37 |
| `rocky_gravel` | loose stones worn up through it | 0.29 |
| `stony_dirt_path` | ruts and dark dirt | 0.22 |

The luminance column is not decoration: it is how the base of each blend was chosen. A blend is mostly
its base, so the base has to be the register you want. `withered_grass` at 0.59 is the pale straw
carpet a cut field has; the first attempt had bare earth as the base and the result read as a dusty
road with a hint of grass, which the preview caught.

Both variants ship the same seven filenames, so switching between them changes no code and no scene
reference — only pixels:

    GROUND_SHIP=dry /Applications/Blender.app/Contents/MacOS/Blender -b -noaudio --python scripts/ground_build.py

`SHIP` in the script names the shipping variant; every other variant writes only previews.

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

## The ground is matte, and that is a ruling, not a default

Todd, 2026-09-25: "iMO - the sheen AT ALL is a bug, maybe there should be a sheen in puddles, but not
over all." Until then the `_M` maps carried a broad specular: lane mean 0.28 smoothness with 7.1% of the
surface above 0.40, the loose stones doing it. Everyone who looked at that frame called it wrong, and the
physics agrees — dead grass and dry dirt are rough, so a moonlit field does not glint.

The shipped ground is now **matte by construction**: lane mean 0.13 smoothness, field 0.11, 0.0% of the
surface above 0.40 (roughness 0.87-0.89). Variation is kept only at the level that stops it reading as
one flat value. Nothing on the field or the lane can throw a highlight after this — if a frame shows one,
it is a defect in the wiring, not the texture.

## Puddles: the one surface allowed to reflect

Built by `scripts/puddle_build.py` (seed 4177, 512², DESIGN-TIME ONLY) — three maps, no third-party
source, nothing licensed, derived in code from the same family of noise as the rest of the ground work:

| file | what it is |
|---|---|
| `T_Ground_PuddleAlpha.png` | coverage: water core 7.3% of the quad, water+damp halo 18.9%, max opacity 0.86 |
| `T_Ground_Puddle.png` | dark wet earth, mean **0.114** against the lane's 0.453 — dark, not black |
| `T_Ground_Puddle_N.png` | a near-flat water plane with a faint silt lip |

They are decals, placed in lane low spots — a handful per level, elongated along the lane, never tiled
and never on the field. A puddle is a place, not a texture that repeats. Three attempts are on record at
`artifacts/reference/puddle-preview.png`: the first read as a painted black hole (round lobes, hard rim,
no damp ring), the second as parallel scratches (over-elongated lobes), and the third is the one on disk.
The damp halo is the cue that sells it from above, and its width had to come from the *distance to the
waterline* — derived from a second noise field it covered 94% of the quad and turned the whole decal into
one damp patch.
