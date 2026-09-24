# DESIGN-TIME ONLY - not compiled into the Unity build, never ships on device.
# Corn block kit: the two source models -> stalks -> the 2 x 2 m block set.
# Rebuild with scripts/corn_block_build.py; design facts in docs/CORN-BRIEF.md.
"""Assemble the shipped maze out of a handful of distinct 2 x 2 m corn blocks
and render it textured from three-quarters overhead.

Blocks are deterministic: block k is built from seed 1661 + 1013*k, so a block
is a fixed asset. The maze then just picks a block index and a 90 degree
rotation per 2 x 2 m slot from the cell coordinate - no unique geometry per
slot, which is what makes this GPU-instanceable in Unity.

Layout comes from the shipped generator (25 x 21, seed 1661, bias 0.58,
CellSize 4 m) via the validated Python mirror, so the corn sits exactly where
the game puts walls. Design-time only - never in the build.
"""
import hashlib
import json
import math
import os
import random
import sys

import bpy
import mathutils

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, "artifacts", "corn-blocks")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corn_block_lib as M                                      # noqa: E402
import corn_maze_carver as C                                    # noqa: E402
import corn_block_convert                                       # noqa: E402

K_BLOCKS = 6
BLOCK_M = 2.0
CELL = 4.0
MAZE_W, MAZE_H, MAZE_BIAS = 25, 21, 0.58   # the shipped layout (MazeGenerator.cs)
SPARSE = os.environ.get("CORN_DENSITY", "dense").lower() == "sparse"
# dense: 36 stands in a 2 x 2 m block (9/m2) -> a solid wall
# sparse: 9 stands (2.25/m2) -> measured 5.6 % of a shape visible through the
#         wall, and a quarter of the triangles
STANDS_PER_M2 = 2.25 if SPARSE else 9
GRID = 3 if SPARSE else 6
MIX_SPEC = [("A", 7), ("B", 2)] if SPARSE else [("A", 32), ("B", 4)]
TAG = "-sparse" if SPARSE else ""
TARGET_SPREAD = 0.90           # the tight variant Todd signed off on
FULL_MAZE = True


def engine(sc):
    for e in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "BLENDER_WORKBENCH"):
        try:
            sc.render.engine = e
            break
        except TypeError:
            continue
    if sc.render.engine != "BLENDER_WORKBENCH":
        sc.eevee.taa_render_samples = 24
    return sc


def signature(obj):
    """Proof that the blocks really differ: hash of the stand layout."""
    h = hashlib.sha256()
    for v in obj.data.vertices:
        h.update(f"{v.co.x:.4f},{v.co.y:.4f},{v.co.z:.4f};".encode())
    return h.hexdigest()[:12]


def build_blocks(lib):
    M.COLS = M.ROWS = GRID
    M.PLOT = BLOCK_M
    M.JITTER = 0.10
    M.INSET = 0.15
    M.MIX = MIX_SPEC
    blocks = []
    for k in range(K_BLOCKS):
        rng = random.Random(1661 + 1013 * k)
        blk, stats = M.build_plot(lib, rng, name=f"cornblock_{k}")
        blk.data.calc_loop_triangles()
        tris = len(blk.data.loop_triangles)
        print(f"  block {k}: seed {1661 + 1013*k}, {sum(stats['used'].values())} stands, "
              f"{tris} tris, bbox {stats['width']:.2f} x {stats['depth']:.2f} m, "
              f"layout hash {signature(blk)}")
        blocks.append(blk)
    return blocks


def decimate_copy(objs, ratio):
    """Linked LOD copies: one shared mesh per block, not per placement."""
    out = []
    for b in objs:
        o = b.copy()
        o.data = b.data.copy()
        bpy.context.collection.objects.link(o)
        mod = o.modifiers.new("decimate", "DECIMATE")
        mod.ratio = ratio
        for x in bpy.data.objects:
            x.select_set(False)
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=mod.name)
        o.hide_render = True
        o.data.calc_loop_triangles()
        out.append(o)
    return out


def place_maze(blocks, level_seed=1661):
    wall, _start = C.build(MAZE_W, MAZE_H, level_seed, MAZE_BIAS)
    grid = dict(w=MAZE_W, h=MAZE_H, seed=level_seed, cell=CELL,
                walls=[(x, y) for x in range(MAZE_W) for y in range(MAZE_H) if wall[x][y]])
    placed = 0
    counts = {}
    for (cx, cy) in grid["walls"]:
        for qx in range(2):
            for qy in range(2):
                k = (cx * 7 + cy * 13 + qx * 3 + qy * 5) % len(blocks)
                rot = ((cx * 3 + cy * 5 + qx * 11 + qy * 7) % 4) * 90
                o = bpy.data.objects.new(f"blk_{cx}_{cy}_{qx}{qy}", blocks[k].data)
                o.matrix_world = (mathutils.Matrix.Translation(
                                      ((cx + qx * 0.5) * CELL, (cy + qy * 0.5) * CELL, 0))
                                  @ mathutils.Matrix.Rotation(math.radians(rot), 4, "Z"))
                bpy.context.collection.objects.link(o)
                counts[k] = counts.get(k, 0) + 1
                placed += 1
    print(f"  placed {placed} blocks over {len(grid['walls'])} wall cells; "
          f"block usage {dict(sorted(counts.items()))}")
    return grid, placed


def main():
    os.makedirs(OUT, exist_ok=True)
    M.fresh()
    if not os.path.exists(os.path.join(M.SRC, "corn-corn-corn.obj")):
        print("converting the supplied source models first")
        corn_block_convert.convert()
    print(f"loading stalks (leaf spread target {TARGET_SPREAD} m at 10 ft)")
    lib = M.load_stalks(TARGET_SPREAD)
    print(f"\nbuilding {K_BLOCKS} distinct {BLOCK_M:.0f} x {BLOCK_M:.0f} m blocks "
          f"({GRID*GRID} stands each, {STANDS_PER_M2}/m2)")
    blocks = build_blocks(lib)

    # every block in the set is an asset, not just block 0: the maze tiles six
    blocks_dir = os.path.join(OUT, "blocks")
    os.makedirs(blocks_dir, exist_ok=True)
    manifest = []
    for k, blk in enumerate(blocks):
        name = f"CornBlock_2m_{k + 1:02d}"
        M.deselect()
        blk.select_set(True)
        bpy.context.view_layer.objects.active = blk
        blk.data.calc_loop_triangles()
        w, d, h = M.span(blk)
        row = dict(name=name, seed=1661 + 1013 * k, stands=GRID * GRID,
                   plants_per_m2=round(GRID * GRID / BLOCK_M ** 2, 2),
                   tris_full=len(blk.data.loop_triangles), width=round(w, 3),
                   depth=round(d, 3), height=round(h, 3),
                   signature=signature(blk), files={})
        for suffix, ratio in (("", None), ("_LOD1", 0.5), ("_LOD2", 0.25)):
            if ratio:
                mod = blk.modifiers.new("decimate", "DECIMATE")
                mod.ratio = ratio
                bpy.ops.object.modifier_apply(modifier=mod.name)
            blk.data.calc_loop_triangles()
            t = len(blk.data.loop_triangles)
            fn = f"{name}{suffix}.fbx"
            bpy.ops.export_scene.fbx(filepath=os.path.join(blocks_dir, fn),
                                     use_selection=True, object_types={"MESH"},
                                     path_mode="COPY", embed_textures=False)
            row["files"][suffix or "full"] = dict(file=fn, tris=t)
            print(f"  exported {fn}  ({t} tris)")
        manifest.append(row)
    with open(os.path.join(blocks_dir, "blocks-manifest.json"), "w") as f:
        json.dump(dict(block_m=BLOCK_M, stands_per_block=GRID * GRID,
                       plants_per_m2=round(GRID * GRID / BLOCK_M ** 2, 2),
                       target_spread_m=TARGET_SPREAD, blocks=manifest), f, indent=2)
    print(f"  wrote blocks-manifest.json ({len(manifest)} blocks)")

    lod = decimate_copy(blocks, 0.25)
    print(f"\nassembling the maze (LOD2 for the overview: "
          f"{len(lod[0].data.loop_triangles)} tris/block)")
    for o in bpy.data.objects:                  # hide the sources
        if o.name.startswith(("cornblock_", "stalk_")):
            o.hide_render = True
    grid, placed = place_maze(lod)

    sc = engine(bpy.context.scene)
    bpy.ops.mesh.primitive_plane_add(size=400, location=(50, 42, -0.02))
    g = bpy.context.view_layer.objects.active
    m = bpy.data.materials.new("ground")
    m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.10, 0.11, 0.09, 1)
    g.data.materials.append(m)
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 4.2
    sun.rotation_euler = (math.radians(52), 0, math.radians(40))
    bpy.context.collection.objects.link(sun)
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.06, 0.09, 1)
    sc.world = w
    cd = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cd)
    ctr = mathutils.Vector((grid["w"] * CELL / 2, grid["h"] * CELL / 2, 0))
    cam.location = ctr + mathutils.Vector((-14, -78, 66))
    cam.rotation_euler = (cam.location - ctr).to_track_quat("Z", "Y").to_euler()
    cd.lens = 40
    bpy.context.collection.objects.link(cam)
    sc.camera = cam
    sc.render.resolution_x, sc.render.resolution_y = 1600, 1100
    sc.render.filepath = os.path.join(OUT, f"maze-3q-overhead{TAG}.png")
    bpy.ops.render.render(write_still=True)
    print(f"  wrote {sc.render.filepath}")

    # close-up: full detail blocks over a 5 x 4 cell patch
    for o in list(bpy.data.objects):
        if o.name.startswith("blk_"):
            bpy.data.objects.remove(o, do_unlink=True)
    for o in bpy.data.objects:
        if o.name.startswith("cornblock_"):
            o.hide_render = False
    placed = 0
    for (cx, cy) in grid["walls"]:
        if not (2 <= cx <= 6 and 6 <= cy <= 9):
            continue
        for qx in range(2):
            for qy in range(2):
                k = (cx * 7 + cy * 13 + qx * 3 + qy * 5) % len(blocks)
                rot = ((cx * 3 + cy * 5 + qx * 11 + qy * 7) % 4) * 90
                o = bpy.data.objects.new(f"blk_{cx}_{cy}_{qx}{qy}", blocks[k].data)
                o.matrix_world = (mathutils.Matrix.Translation(
                                      ((cx + qx * 0.5) * CELL, (cy + qy * 0.5) * CELL, 0))
                                  @ mathutils.Matrix.Rotation(math.radians(rot), 4, "Z"))
                bpy.context.collection.objects.link(o)
                placed += 1
    print(f"  close-up: {placed} full-detail blocks")
    ctr2 = mathutils.Vector((4.5 * CELL, 8 * CELL, 0))
    cam.location = ctr2 + mathutils.Vector((-6, -22, 14))
    cam.rotation_euler = (cam.location - ctr2).to_track_quat("Z", "Y").to_euler()
    cd.lens = 45
    sc.render.resolution_x, sc.render.resolution_y = 1500, 950
    sc.render.filepath = os.path.join(OUT, f"maze-3q-closeup{TAG}.png")
    bpy.ops.render.render(write_still=True)
    print(f"  wrote {sc.render.filepath}")


if __name__ == "__main__":
    main()
