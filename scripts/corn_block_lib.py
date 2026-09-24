# DESIGN-TIME ONLY - not compiled into the Unity build, never ships on device.
# Corn block kit: the two source models -> stalks -> the 2 x 2 m block set.
# Rebuild with scripts/corn_block_build.py; design facts in docs/CORN-BRIEF.md.
"""CornPlot v3: 9 stands per square metre (3x3 grid, jittered), and the leaves
narrowed in X/Y only - the stalk geometry is left exactly as authored, because
both sources' stalks measure realistic already (corn-old's is 46 mm across).

The narrowing is a per-source factor chosen so a 10 ft plant's foliage spans a
realistic width (a real 3 m corn plant is about 2.5:1 height:width). Two
variants are built so the tightness can be chosen by eye:
  CornPlot.fbx        target spread 1.20 m at 10 ft  (~2.5:1, realistic)
  CornPlot-tight.fbx  target spread 0.90 m at 10 ft  (~3.4:1, tighter wall)

  corn-old: leaves are 20 separate meshes, so only they are scaled - the stalk
            object is untouched.
  corn-corn-corn: one welded mesh, so vertices within 50 mm of the stem axis
            are left alone and the factor ramps in by 150 mm - the stem column
            and every leaf base keep their exact position.
Design-time only - never in the build.
"""
import math
import os
import random
import shutil

import bpy
import mathutils

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, "artifacts", "corn-blocks")
SRC = os.path.join(OUT, "src")          # converted source models
DL = "/Users/toddadams/Downloads"       # the supplied source models live here
SEED = 1661
TARGET_H = 3.048
HEIGHT_JITTER = 0.05
TILT = (1.0, 3.0)
COLS = ROWS = 3                   # 9 stands per m2
PLOT = 1.0
INSET = 0.11
JITTER = 0.070                    # "a little jitter" on a 333 mm grid
CCC_SCALE = 0.0838
MIX = [("A", 8), ("B", 1)]
GAPS = 0                          # stands deliberately left out of a block
STEM_SAFE = 0.050                 # untouched radius around the stem axis
STEM_RAMP = 0.150                 # full narrowing from here out

STALKS = {
    "A": dict(path=os.path.join(DL, "corn-old", "source", "SM_Corn_01.fbx"),
              kind="fbx",
              tex=[os.path.join(DL, "corn-old", "textures", "T_Corn_01_D.png"),
                   os.path.join(DL, "corn-old", "textures", "T_Corn_01_NRM.png")],
              label="corn-old SM_Corn_01"),
    "B": dict(path=os.path.join(SRC, "corn-corn-corn.obj"),
              kind="obj", scale=CCC_SCALE, rot_x=-90.0,
              tex=[os.path.join(DL, "corn-corn-corn", "textures", "corn_texture.png")],
              label="corn-corn-corn corn.fbx"),
}


def fresh():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    return bpy.context.scene


def bake(obj):
    obj.data.transform(obj.matrix_world)
    obj.matrix_world = mathutils.Matrix.Identity(4)


def deselect():
    for o in bpy.data.objects:
        o.select_set(False)


def join(objs, name):
    deselect()
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = o.data.name = name
    return o


def span(o):
    xs = [v.co.x for v in o.data.vertices]
    ys = [v.co.y for v in o.data.vertices]
    zs = [v.co.z for v in o.data.vertices]
    return max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)


def part_span(o):
    xs = [v.co.x for v in o.data.vertices]
    ys = [v.co.y for v in o.data.vertices]
    zs = [v.co.z for v in o.data.vertices]
    return max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)


def joined_span(objs):
    xs, ys, zs = [], [], []
    for o in objs:
        for v in o.data.vertices:
            xs.append(v.co.x); ys.append(v.co.y); zs.append(v.co.z)
    return max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)


def stem_axis(o):
    xs = [v.co.x for v in o.data.vertices]
    ys = [v.co.y for v in o.data.vertices]
    return mathutils.Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, 0.0))


def squeeze_xy(obj, f):
    """Scale X and Y about the stem axis only, leaving Z alone."""
    obj.data.transform(mathutils.Matrix.Diagonal((f, f, 1.0, 1.0)))


def squeeze_radial(obj, f):
    """Same, but vertices inside STEM_SAFE of the axis are untouched and the
    factor ramps in to STEM_RAMP, so the stem column and leaf bases stay put."""
    me = obj.data
    for v in me.vertices:
        r = math.hypot(v.co.x, v.co.y)
        if r <= STEM_SAFE:
            continue
        t = min(1.0, (r - STEM_SAFE) / (STEM_RAMP - STEM_SAFE))
        k = 1.0 - t * (1.0 - f)
        v.co.x *= k
        v.co.y *= k


def ground_centre(obj):
    me = obj.data
    xs = [v.co.x for v in me.vertices]
    ys = [v.co.y for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    off = mathutils.Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, min(zs)))
    for v in me.vertices:
        v.co -= off
    return max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)


def wire_textures(obj, paths):
    base = next((p for p in paths if "_NRM" not in p and "normal" not in p.lower()), None)
    nrm = next((p for p in paths if p != base), None)
    for slot in obj.material_slots:
        m = slot.material
        if not m:
            continue
        m.use_nodes = True
        nt = m.node_tree
        bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if not bsdf:
            continue
        if base and os.path.exists(base):
            img = bpy.data.images.load(base, check_existing=True)
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.image = img
            tex.location = (-500, 250)
            nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
            if "Alpha" in bsdf.inputs:
                nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
            if img.channels == 4 or "alpha" in m.name.lower():
                try:
                    m.blend_method = "CLIP"
                    m.alpha_threshold = 0.5
                    m.use_backface_culling = False
                except (AttributeError, TypeError):
                    pass
        if nrm and os.path.exists(nrm):
            img = bpy.data.images.load(nrm, check_existing=True)
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.image = img
            try:
                img.colorspace_settings.name = "Non-Color"
            except (AttributeError, TypeError):
                pass
            tex.location = (-500, -50)
            nm = nt.nodes.new("ShaderNodeNormalMap")
            nm.location = (-250, -50)
            nt.links.new(tex.outputs["Color"], nm.inputs["Color"])
            nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])


def load_stalks(target_spread):
    """Returns {key: {obj, height, spread}} with the leaves already narrowed."""
    lib = {}
    fresh()                       # once: a per-source reset would invalidate the
                                  # stalk loaded before it
    for key, cfg in STALKS.items():
        before = set(bpy.data.objects)
        if cfg["kind"] == "fbx":
            bpy.ops.import_scene.fbx(filepath=cfg["path"])
        else:
            bpy.ops.wm.obj_import(filepath=cfg["path"])
        objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
        if not objs:
            print(f"  {key}: NO MESHES")
            continue
        for o in objs:
            bake(o)
        m = mathutils.Matrix.Identity(4)
        if "rot_x" in cfg:
            m = mathutils.Matrix.Rotation(math.radians(cfg["rot_x"]), 4, "X") @ m
        if "scale" in cfg:
            s = cfg["scale"]
            m = mathutils.Matrix.Diagonal((s, s, s, 1.0)) @ m
        for o in objs:
            o.data.transform(m)
        # native spread and the factor that brings the foliage to `target_spread`
        # once the plant is 10 ft tall
        pre = joined_span(objs)
        native_spread, native_h = max(pre[0], pre[1]), pre[2]   # widest span, height
        spread_at_10ft = native_spread * (TARGET_H / native_h)
        f = max(0.35, min(1.0, target_spread / spread_at_10ft))
        if len(objs) > 1:
            # corn-old: the stem is its own mesh, so the leaves can be scaled
            # alone and the stem column never moves
            stem = max(objs, key=lambda o: part_span(o)[2])
            sx = part_span(stem)
            ax = stem_axis(stem)
            for o in objs:
                if o is stem:
                    continue
                o.data.transform(mathutils.Matrix.Translation(ax)
                                 @ mathutils.Matrix.Diagonal((f, f, 1.0, 1.0))
                                 @ mathutils.Matrix.Translation(-ax))
            print(f"  {key} ({cfg['label']}): {len(objs)-1} leaf meshes narrowed x{f:.2f} "
                  f"about the stem axis; stem mesh left at "
                  f"{sx[0]*1000:.0f} x {sx[1]*1000:.0f} mm across, {sx[2]:.3f} m tall")
        else:
            obj0 = objs[0]
            before_co = [tuple(v.co) for v in obj0.data.vertices]
            squeeze_radial(obj0, f)
            moved = 0
            worst_inside = 0.0
            for old, v in zip(before_co, obj0.data.vertices):
                d = math.dist(old, tuple(v.co))
                if d > 1e-9:
                    moved += 1
                if math.hypot(old[0], old[1]) <= STEM_SAFE:
                    worst_inside = max(worst_inside, d)
            print(f"  {key} ({cfg['label']}): welded mesh, radial narrowing x{f:.2f}; "
                  f"{moved} of {len(before_co)} verts moved, "
                  f"worst displacement inside the {STEM_SAFE*1000:.0f} mm stem radius "
                  f"= {worst_inside*1e6:.3f} micrometres (stem column untouched)")
        obj = join(objs, f"stalk_{key}")
        w, d, h = ground_centre(obj)
        wire_textures(obj, cfg.get("tex", []))     # v3 dropped this call: the
                                                   # plot rendered untextured
        obj.data.calc_loop_triangles()
        before_tris = len(obj.data.loop_triangles)
        w, d, h = ground_centre(obj)
        obj.data.calc_loop_triangles()
        lib[key] = dict(obj=obj, height=h, spread=max(w, d), tris=len(obj.data.loop_triangles),
                        native=native_spread, factor=f, tris_before=before_tris)
        print(f"      {native_spread:.2f} m spread -> {max(w, d):.2f} m at native height, "
              f"{max(w, d)*(TARGET_H/h):.2f} m at 10 ft, {lib[key]['tris']} tris")
    return lib


def build_plot(lib, rng, name):
    picks = []
    for key, n in MIX:
        picks += [key] * n
    while len(picks) < COLS * ROWS:
        picks.append(MIX[0][0])
    picks = picks[:COLS * ROWS]
    if GAPS:
        picks = picks[:COLS * ROWS - GAPS] + ["_"] * GAPS
    rng.shuffle(picks)
    step = PLOT / COLS
    slots = [(-PLOT / 2 + step * (c + 0.5) + rng.uniform(-JITTER, JITTER),
              -PLOT / 2 + step * (r + 0.5) + rng.uniform(-JITTER, JITTER))
             for c in range(COLS) for r in range(ROWS)]
    rng.shuffle(slots)
    copies, heights, used = [], [], {}
    for i, key in enumerate(picks):
        if key not in lib:                    # a deliberate gap
            continue
        src = lib[key]
        x, y = slots[i]
        x = max(-PLOT / 2 + INSET, min(PLOT / 2 - INSET, x))
        y = max(-PLOT / 2 + INSET, min(PLOT / 2 - INSET, y))
        target = TARGET_H * (1.0 + rng.uniform(-HEIGHT_JITTER, HEIGHT_JITTER))
        s = target / src["height"]
        o = src["obj"].copy()
        o.data = src["obj"].data.copy()
        bpy.context.collection.objects.link(o)
        yaw = rng.uniform(0, 2 * math.pi)
        ax = rng.uniform(0, 2 * math.pi)
        tilt = math.radians(rng.uniform(*TILT))
        o.matrix_world = (mathutils.Matrix.Translation((x, y, 0.0))
                          @ mathutils.Matrix.Rotation(yaw, 4, "Z")
                          @ mathutils.Matrix.Rotation(tilt, 4, (math.cos(ax), math.sin(ax), 0.0))
                          @ mathutils.Matrix.Diagonal((s, s, s, 1.0)))
        copies.append(o)
        heights.append(target)
        used[key] = used.get(key, 0) + 1
    plot = join(copies, name)
    bake(plot)
    w, d, h = ground_centre(plot)
    return plot, dict(width=w, depth=d, height=h, used=used,
                      hmin=min(heights), hmax=max(heights))


def export(plot, label, path_fbx, scale=None):
    deselect()
    plot.select_set(True)
    bpy.context.view_layer.objects.active = plot
    if scale:
        mod = plot.modifiers.new("decimate", "DECIMATE")
        mod.ratio = scale
        bpy.ops.object.modifier_apply(modifier=mod.name)
    plot.data.calc_loop_triangles()
    tris = len(plot.data.loop_triangles)
    bpy.ops.export_scene.fbx(filepath=path_fbx, use_selection=True,
                             object_types={"MESH"}, path_mode="COPY",
                             embed_textures=False)
    print(f"    {label}: {tris} tris -> {os.path.basename(path_fbx)}")
    return tris
