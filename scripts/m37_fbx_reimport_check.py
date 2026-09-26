# DESIGN-TIME ONLY - not compiled into the Unity build, never ships on device.
#
# M37 — re-import the FBX we EXPORTED and look at it.
#
# The source GLB renders upright and detailed (artifacts/review/world/m35-model-rest.png). The built app
# renders the same creature hunched over into a blob. That leaves exactly two suspects and this file
# separates them:
#
#   * if this re-import renders HUNCHED, the Blender FBX export broke the rig (rest pose vs bind pose,
#     or the exporter dropped a bone), and the fix is upstream in m35_scarecrow_import.py's export call;
#   * if it renders UPRIGHT, the FBX is good and Unity's import/animation path is what deforms it
#     (Husk.cs plays a raw AnimationClipPlayable onto an Animator with no Avatar, no controller).
#
# It also measures the bind pose directly: the head bone's world Z at rest, which must be near the top
# of the mesh box. A rest pose that disagrees with the mesh is exactly what a hunch is.
#
# Run: Blender -b -noaudio --python scripts/m37_fbx_reimport_check.py
import math
import os

import bpy
from mathutils import Vector

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FBX = os.path.join(REPO, "Assets", "Resources", "Scarecrow", "scarecrow.fbx")
SHOTS = os.path.join(REPO, "artifacts", "review", "world")
BLIND = os.path.join(REPO, "artifacts")
OUT = os.path.join(BLIND, "m37-fbx-reimport-report.txt")

lines = []


def say(s):
    print(s)
    lines.append(s)


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)

meshes = [o for o in bpy.data.objects if o.type == "MESH"]
arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]

say("=" * 70)
say("M37 — the EXPORTED FBX re-imported (does the export carry the pose intact?)")
say("=" * 70)
say(f"  source       {os.path.relpath(FBX, REPO)}  {os.path.getsize(FBX)} bytes")
say(f"  objects      {len(bpy.data.objects)}  meshes {len(meshes)}  armatures {len(arms)}")


def obj_dims(objs):
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            for i in range(3):
                lo[i] = min(lo[i], w[i])
                hi[i] = max(hi[i], w[i])
    return lo, hi


for o in meshes:
    o.data.calc_loop_triangles()
    say(f"  mesh '{o.name}': {len(o.data.loop_triangles)} tris, {len(o.data.vertices)} verts,"
        f" uv {[l.name for l in o.data.uv_layers]},"
        f" vgroups {len(o.vertex_groups)}, mods {[(m.type, m.name) for m in o.modifiers]}")
    say(f"      materials: {[m.name if m else None for m in o.data.materials]}")
    used = set()
    pairs = 0
    for v in o.data.vertices:
        for g in v.groups:
            if g.weight > 1e-6:
                used.add(g.group)
                pairs += 1
    empty = [g.name for g in o.vertex_groups if g.index not in used]
    say(f"      skin weights: {pairs} (vertex,group) pairs; groups carrying weight "
        f"{len(used)}/{len(o.vertex_groups)}; groups with NO weighted vertex: {empty}")

for a in arms:
    say(f"  armature '{a.name}': {len(a.data.bones)} bones")
    say(f"      scale {tuple(round(v, 4) for v in a.scale)}"
        f"  rot {tuple(round(math.degrees(v), 2) for v in a.rotation_euler)}")
    # the bones that decide whether this reads as standing or hunched
    for name in ("Hips", "Spine", "Spine01", "Spine02", "Neck", "Head", "LeftFoot", "RightFoot"):
        b = a.data.bones.get(name)
        if b is None:
            continue
        w = a.matrix_world @ b.head_local
        say(f"      bone {name:<11} head_local z={b.head_local.z:8.4f}"
            f"   world xyz=({w.x:7.4f},{w.y:7.4f},{w.z:7.4f})")

say(f"  actions      {[x.name for x in bpy.data.actions]}")


def action_curves(a):
    """Blender 5 moved fcurves behind action layers/slots/channelbags, so an Action no longer
    reliably exposes .fcurves. Walk the new containers when the old attribute comes back empty
    (matching scripts/probe_glb.py) — otherwise a fully animated clip misreports as 0 curves."""
    curves = list(getattr(a, "fcurves", None) or [])
    if curves:
        return curves
    for layer in getattr(a, "layers", None) or []:
        for strip in getattr(layer, "strips", None) or []:
            for cb in getattr(strip, "channelbags", None) or []:
                curves.extend(cb.fcurves)
    return curves


def bone_of(path):
    # 'pose.bones["Head"].rotation_quaternion' -> 'Head'
    if 'pose.bones["' not in path:
        return path
    return path.split('pose.bones["', 1)[1].split('"', 1)[0]


for x in bpy.data.actions:
    curves = action_curves(x)
    bones = sorted({bone_of(c.data_path) for c in curves})
    keyed = sum(len(c.keyframe_points) for c in curves)
    say(f"      '{x.name}' frames {x.frame_range[0]:.0f}-{x.frame_range[1]:.0f}"
        f" animated curves {len(curves)}  keyframes {keyed}  bone paths {len(bones)}")
    say(f"        bones: {bones}")

lo, hi = obj_dims(meshes)
size = hi - lo
say(f"  bounds       {size.x:.3f} x {size.y:.3f} x {size.z:.3f}  -> {size.z:.3f} tall")
say(f"  REST pose: is the mesh upright at the action's first frame? (render below decides)")

# ---- does an action even arrive bound? -------------------------------------------------
anim = None
if arms:
    anim = arms[0].animation_data
say(f"  action bound to armature: {anim.action.name if anim and anim.action else 'NONE'}")

# ---- render: rest, then three beats the same way m35 did --------------------------------
os.makedirs(SHOTS, exist_ok=True)
scn = bpy.context.scene
scn.render.engine = "BLENDER_EEVEE"
scn.render.resolution_x, scn.render.resolution_y = 620, 900
try:
    scn.view_settings.view_transform = "AgX"
    scn.view_settings.look = "AgX - Medium High Contrast"
except Exception as exc:
    say(f"  view transform: {exc}")
scn.view_settings.exposure = 0.35

world = bpy.data.worlds.new("dusk")
scn.world = world
world.use_nodes = True
bg = world.node_tree.nodes["Background"]
bg.inputs[0].default_value = (0.052, 0.058, 0.075, 1.0)
bg.inputs[1].default_value = 1.35

for name, loc, energy, colour, size_l in [
        ("key", (2.6, -3.4, 3.2), 420.0, (0.80, 0.84, 1.00), 2.4),
        ("rim", (-3.0, 2.6, 2.6), 260.0, (0.62, 0.72, 1.00), 2.0)]:
    ld = bpy.data.lights.new(name, "AREA")
    ld.energy, ld.color, ld.size = energy, colour, size_l
    o = bpy.data.objects.new(name, ld)
    o.location = loc
    d = Vector((0, 0, 1.0)) - Vector(loc)
    o.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    bpy.context.collection.objects.link(o)

me = bpy.data.meshes.new("ground")
me.from_pydata([(-8, -8, 0), (8, -8, 0), (8, 8, 0), (-8, 8, 0)], [], [(0, 1, 2, 3)])
me.update()
g = bpy.data.objects.new("ground", me)
bpy.context.collection.objects.link(g)
gm = bpy.data.materials.new("M_ground")
gm.use_nodes = True
gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.05, 0.045, 0.035, 1)
g.data.materials.append(gm)

cam_d = bpy.data.cameras.new("cam")
cam_d.lens = 62.0
cam = bpy.data.objects.new("cam", cam_d)
bpy.context.collection.objects.link(cam)
scn.camera = cam


def shoot(name, azim, dist_mult, target_z):
    mid = (lo.z + hi.z) * 0.5
    centre = Vector(((lo.x + hi.x) * 0.5, (lo.y + hi.y) * 0.5, 0.0))
    span = max(size.x, size.z)
    a = math.radians(azim)
    d = span * dist_mult
    cam.location = (centre.x + d * math.sin(a), centre.y - d * math.cos(a), mid + span * 0.16)
    look = Vector((centre.x, centre.y, mid * target_z))
    cam.rotation_euler = (look - Vector(cam.location)).to_track_quat("-Z", "Y").to_euler()
    scn.render.filepath = os.path.join(SHOTS, name)
    bpy.ops.render.render(write_still=True)
    say(f"  frame: {name}")


shoot("m37-fbx-rest.png", 18.0, 1.9, 1.0)

acts = list(bpy.data.actions)
if acts and anim is not None and anim.action is not None:
    start = int(acts[0].frame_range[0])
    for i, f in enumerate([1, 24, 48]):
        scn.frame_set(start + f)
        shoot(f"m37-fbx-walk-{i + 1}.png", 18.0 + i * 26.0, 1.9, 1.0)
    scn.frame_set(start)
else:
    say("  WARN: no action bound on re-import — nothing to render walking")

with open(OUT, "w") as fh:
    fh.write("\n".join(lines) + "\n")
say(f"  report -> {os.path.relpath(OUT, REPO)}")
print("M37_REIMPORT_DONE")