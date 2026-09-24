# Assets/Corn — the corn field building block

The field is tiled from a **2.0 × 2.0 m block** holding 9 corn plants. Six distinct blocks are shipped;
the game picks a block and a 90° rotation per slot, so the maze reads as one continuous field while
costing six meshes.

**Read `docs/CORN-BRIEF.md` before touching anything here** — it carries the locked facts, the import
traps and the reason the corn is deliberately sparse.

## What's here

| Path | What it is |
|---|---|
| `Blocks/CornBlock_2m_01.fbx` … `_06.fbx` | the six blocks, full detail, 8 955 tris each |
| `Blocks/CornBlock_2m_XX_LOD1.fbx` | 50 % — 4 476 tris |
| `Blocks/CornBlock_2m_XX_LOD2.fbx` | 12.5 % — 1 118 tris |
| `Blocks/blocks-manifest.json` | per block: seed, stands, bbox, height, tri counts, layout signature |
| `Textures/T_Corn_01_D.png` | albedo, corn-old plants |
| `Textures/T_Corn_01_NRM.png` | normal, corn-old plants |
| `Textures/corn_texture.png` | albedo **with alpha** for corn-corn-corn plants |

Each block holds 7 corn-old and 2 corn-corn-corn plants, so it uses **two materials**. The FBX's slots
are named `corn` and `Maize` after the source files — the names are misleading, match on the texture.

## Unity import settings

The FBX cannot carry these, so they are set once per material asset:

1. **Cutout.** The leaf cards need **Alpha Clipping on, threshold 0.5**, or leaves render as solid slabs.
   On the corn-corn-corn material the alpha comes from `corn_texture.png`.
2. **Base map** ← the albedo above; **Normal map** ← `T_Corn_01_NRM.png` (mark it Normal in its import
   settings, and give it no sRGB).
3. **Enable GPU instancing** on both materials — 1 144 block instances per maze.
4. Model import: leave scale at 1, import materials as authored, and turn **Generate Colliders off** —
   the field's collision is the maze walls, not the leaves.

## Numbers

- 2.25 plants/m² · block bbox 2.22–2.44 m (leaves overhang the 2 m square by design)
- 1 144 blocks per maze (286 wall cells × 4) · 10 296 plants · 10.2 M tris full, 5.1 M at LOD1
- Plants 2.896–3.200 m tall (10 ft ± 5 %), yaw uniform, tilt 1–3°

## Regenerating

```
python3 scripts/corn_block_build.py     # design-time only — never in the build
```

Blocks are code-generated from seeds (`1661 + 1013 × k`); hand-editing the FBX would be lost on the
next regeneration. Any change to density or leaf spread changes every block — regenerate the LODs and
the manifest in the same commit.

Source models and licence status: `docs/ASSETS-INVENTED.md`.
