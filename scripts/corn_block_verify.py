# DESIGN-TIME ONLY - not compiled into the Unity build, never ships on device.
# Corn block kit: the two source models -> stalks -> the 2 x 2 m block set.
# Rebuild with scripts/corn_block_build.py; design facts in docs/CORN-BRIEF.md.
"""How dense does a 2 x 2 m corn block have to be before a creature standing
inside the wall stops being visible from the corridor?

Four blocks are built and tested as a real maze wall cell (6 m wide x 4 m deep =
3 x 2 blocks), with a dark 1.45 m shape standing inside it 0.6 m from the far
face. Each is (a) measured objectively - 144 rays from the corridor eye position
to points on the shape, counting how many reach it through the corn - and
(b) rendered from that same eye position, 1.75 m, as the player would see it.
Every block is exported (full / LOD1 / LOD2) so no version is lost.
Design-time only - never in the build.
"""
import math
import os
import random
import sys

import bpy
import mathutils

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corn_block_lib as M                        # noqa: E402

OUT = os.path.join(REPO, "artifacts", "corn-blocks", "verify")
M.PLOT = 2.0
M.INSET = 0.13
M.JITTER = 0.075
M.TARGET_H = 3.048
MIX_B = 2                                         # corn-corn-corn per block
CONFIGS = [
    # label,          grid, gaps, one sparse block in the wall
    ("2.25-per-m2",    3,   0, False),
    ("4-per-m2",       4,   0, False),
    ("9-per-m2",       6,   0, False),
    ("9-per-m2-gaps",  6,   2, False),
    ("mixed-1-in-6",   6,   0, True),
]
EYE = mathutils.Vector((0.0, -5.0, 1.75))         # corridor, 3 m from the face
LOOK = mathutils.Vector((0.0, 0.8, 1.45))
CREATURE = (0.0, 1.4, 0.72)                       # inside the wall, near far face
SIZE = (0.55, 0.55, 1.45)


def deselect():
    for o in bpy.data.objects:
        o.select_set(False)


def build_wall(lib, label, cols, gaps, rng, sparse_one=False):
    """3 x 2 copies of one block -> 6 m wide, 4 m deep wall cell."""
    M.COLS = M.ROWS = cols
    n = cols * cols
    M.MIX = [("A", max(1, n - MIX_B)), ("B", min(MIX_B, max(0, n - 1)))]
    M.GAPS = gaps
    block, stats = M.build_plot(lib, rng, f"block_{label}")
    thin = None
    if sparse_one:
        # a thin slot through the wall: both blocks on one column are thin, so a
        # sight line can cross the whole 4 m depth without hitting dense corn
        M.COLS = M.ROWS = 3
        M.GAPS = 0
        M.MIX = [("A", 7), ("B", 2)]
        thin, thin_stats = M.build_plot(lib, rng, f"block_{label}_thin")
        stats = dict(stats, thin_stands=9, thin_w=thin_stats["width"],
                     thin_d=thin_stats["depth"])
    wall = []
    for ix, iy in ((-1, -1), (0, -1), (1, -1), (-1, 0), (0, 0), (1, 0)):
        src = thin if (sparse_one and ix == 0) else block
        o = src.copy()
        o.data = src.data
        bpy.context.collection.objects.link(o)
        o.location = (ix * 2.0, iy * 2.0, 0.0)
        wall.append(o)
    deselect()
    for o in wall:
        o.select_set(True)
    bpy.context.view_layer.objects.active = wall[0]
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.name = joined.data.name = f"wall_{label}"
    joined.data = joined.data.copy()          # the block shares this mesh
    return joined, stats, n


def creature():
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=CREATURE)
    c = bpy.context.object
    c.name = "creature"
    c.scale = SIZE
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    mat = bpy.data.materials.new("creature")
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (0.03, 0.028, 0.026, 1)
    bsdf.inputs["Roughness"].default_value = 0.9
    c.data.materials.append(mat)
    return c


def visibility(dg, target):
    """Fraction of a 12 x 12 grid of points on the shape that the corridor eye
    can actually see (a ray that reaches the point without hitting corn)."""
    seen = 0
    total = 0
    half_x, half_z = SIZE[0] / 2, SIZE[2] / 2
    for i in range(12):
        for j in range(12):
            p = mathutils.Vector((target[0] - half_x + SIZE[0] * (i + 0.5) / 12,
                                  target[1] - half_x,
                                  target[2] - half_z + SIZE[2] * (j + 0.5) / 12))
            d = p - EYE
            dist = d.length
            d.normalize()
            hit, loc, _, _, obj, _ = bpy.context.scene.ray_cast(dg, EYE, d, distance=dist - 0.02)
            total += 1
            if not hit:
                seen += 1
    return seen / total


def ground():
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0))
    g = bpy.context.object
    g.name = "ground"
    mat = bpy.data.materials.new("ground")
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (0.11, 0.10, 0.085, 1)
    g.data.materials.append(mat)
    return g


def setup_render(sc):
    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    sc.collection.objects.link(cam)
    cam.location = EYE
    cam.rotation_euler = (LOOK - EYE).to_track_quat("-Z", "Y").to_euler()
    cam_data.lens = 32
    sc.camera = cam
    sun_data = bpy.data.lights.new("sun", "SUN")
    sun_data.energy = 3.4
    sun = bpy.data.objects.new("sun", sun_data)
    sc.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(58), 0, math.radians(-135))
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.34, 0.40, 0.52, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.55
    sc.world = w
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = 1040, 660
    sc.render.film_transparent = False
    sc.eevee.taa_render_samples = 24
    return cam


def main():
    os.makedirs(OUT, exist_ok=True)
    M.fresh()
    lib = M.load_stalks(0.90)                    # the tight variant Todd picked
    sc = bpy.context.scene                       # load_stalks resets the scene
    print(f"stalks loaded: {sorted(lib)}")
    ground()
    cam = setup_render(sc)
    results = {}
    for label, cols, gaps, sparse_one in CONFIGS:
        print(f"\n=== {label}: {cols}x{cols} = {cols*cols} stands per 2x2 m "
              f"({cols*cols/4:.2f} plants/m2), gaps {gaps} ===")
        rng = random.Random(M.SEED)
        wall, stats, n = build_wall(lib, label, cols, gaps, rng, sparse_one)
        wall.data.calc_loop_triangles()
        tris = len(wall.data.loop_triangles)
        c = creature()
        bpy.context.view_layer.update()
        dg = bpy.context.evaluated_depsgraph_get()
        vis = visibility(dg, CREATURE)
        thin = (f", thin block {stats['thin_stands']} stands "
                f"{stats['thin_w']:.2f} x {stats['thin_d']:.2f} m"
                if "thin_stands" in stats else "")
        print(f"  {n} stands/block, {stats['used']} mix, block {stats['width']:.2f} x "
              f"{stats['depth']:.2f} m, wall {tris} tris{thin}")
        print(f"  creature visible through the corn: {vis*100:.1f}% of its silhouette")
        sc.render.filepath = os.path.join(OUT, f"peek-{label}.png")
        bpy.ops.render.render(write_still=True)
        print(f"  wrote {sc.render.filepath}")
        results[label] = dict(stands=n, gaps=gaps, tris=tris, visible=vis)
        for o in (wall, c):
            bpy.data.objects.remove(o, do_unlink=True)
    print("\n=== summary ===")
    for label, r in results.items():
        print(f"  {label:16} {r['stands']:3} stands  {r['tris']:6} tris  "
              f"visible {r['visible']*100:5.1f}%")


main()
