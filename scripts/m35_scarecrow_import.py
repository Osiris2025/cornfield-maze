# DESIGN-TIME ONLY - not compiled into the Unity build, never ships on device.
#
# M35 — bring Todd's supplied scarecrow into the project.
#
# The GLB he dropped in ~/Downloads holds a rigged humanoid (24 bones, Mixamo naming) with one
# 72-frame clip, "Unsteady_Walk", plus a stray 2 m Icosphere with no material and no bones that
# belongs to nothing. Unity cannot import GLB without a glTF package this project does not carry,
# so the model is converted to FBX here, with the rig and the action intact, and the texture
# written out beside it.
#
# It also renders a look-check of its own: the rest pose, and two frames from the middle of the
# walk, so "the clip actually animates" is a picture and not a claim about a file.
#
# Run: Blender -b -noaudio --python scripts/m35_scarecrow_import.py
import math
import os
import shutil
import sys

import bpy
from mathutils import Vector

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC_DIR = "/Users/toddadams/Downloads/creepy-scarecrow-horror-stylized"
GLB = os.path.join(SRC_DIR, "source", "textured_mesh (1).glb")
TEX_IN = os.path.join(SRC_DIR, "textures", "texture_0_0.png")
DEST = os.path.join(REPO, "Assets", "Resources", "Scarecrow")
SHOTS = os.path.join(REPO, "artifacts", "review", "world")
BLIND = os.path.join(REPO, "artifacts")

TARGET_HEIGHT = 2.30      # the Husk's stated height: it has to break the lane line, not the canopy

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)

# ---- drop the Icosphere ---------------------------------------------------------------
# 80 triangles, no material, no vertex groups, 2 m across — it is not part of the character and
# would import into Unity as a grey ball at his feet.
drop = [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("Icosphere")]
for o in drop:
    print("dropping stray object:", o.name)
    bpy.data.objects.remove(o, do_unlink=True)

meshes = [o for o in bpy.data.objects if o.type == "MESH"]
arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
if not meshes or not arms:
    raise SystemExit("FAIL: expected a mesh and an armature in the GLB")

body = meshes[0]
arm = arms[0]
body.data.calc_loop_triangles()
print("=" * 70)
print("M35 — the supplied scarecrow, measured on import")
print("=" * 70)
print(f"  body mesh      {body.name}: {len(body.data.loop_triangles)} tris, "
      f"{len(body.data.vertices)} verts, {len(body.vertex_groups)} vertex groups")
print(f"  armature       {arm.name}: {len(arm.data.bones)} bones")
print(f"  armature scale {tuple(round(v, 4) for v in arm.scale)}  "
      f"rotation {tuple(round(math.degrees(v), 2) for v in arm.rotation_euler)}")

# ---- what does it actually measure, in world units? -----------------------------------
def world_bounds(objs):
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            for i in range(3):
                lo[i] = min(lo[i], w[i])
                hi[i] = max(hi[i], w[i])
    return lo, hi


lo, hi = world_bounds(meshes)
size = hi - lo
print(f"  bounds         {size.x:.3f} x {size.y:.3f} x {size.z:.3f} (x,y,z) "
      f"-> {size.z:.3f} units tall as authored")
print(f"  scale factor   {TARGET_HEIGHT / size.z:.4f} to stand {TARGET_HEIGHT:.2f} m")

# ---- the walk clip --------------------------------------------------------------------
acts = list(bpy.data.actions)
print(f"  actions        {[a.name for a in acts]}")
anim = arm.animation_data
if anim is None or anim.action is None:
    print("  WARNING: no action bound to the armature — the walk would not export")

# ---- look-check renders ---------------------------------------------------------------
os.makedirs(SHOTS, exist_ok=True)
scn = bpy.context.scene
scn.render.engine = "BLENDER_EEVEE"
scn.render.resolution_x, scn.render.resolution_y = 620, 900
try:
    scn.view_settings.view_transform = "AgX"
    scn.view_settings.look = "AgX - Medium High Contrast"
except Exception as exc:
    print("  view transform:", exc)
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
    print("  frame:", name)


# rest pose first, so the bind pose is on record
shoot("m35-model-rest.png", 18.0, 1.9, 1.0)

# then the clip: three beats of the walk, which is what proves the action travels with the file
frames = [1, 24, 48]
if acts and anim is not None:
    start = int(acts[0].frame_range[0])
    for i, f in enumerate(frames):
        scn.frame_set(start + f)
        shoot(f"m35-model-walk-{i + 1}.png", 18.0 + i * 26.0, 1.9, 1.0)
    scn.frame_set(start)

# ---- export for Unity -----------------------------------------------------------------
os.makedirs(DEST, exist_ok=True)
if os.path.exists(TEX_IN):
    shutil.copy2(TEX_IN, os.path.join(DEST, "scarecrow_albedo.png"))
    print("  texture copied ->", os.path.relpath(os.path.join(DEST, "scarecrow_albedo.png"), REPO))

# Repoint the material at the copy so the FBX exporter carries a file it can see.
for mat in bpy.data.materials:
    if not mat.use_nodes:
        continue
    for n in mat.node_tree.nodes:
        if n.type == "TEX_IMAGE" and n.image is not None:
            n.image.filepath = os.path.join(DEST, "scarecrow_albedo.png")
            n.image.name = "scarecrow_albedo"

fbx = os.path.join(DEST, "scarecrow.fbx")
bpy.ops.object.select_all(action="DESELECT")
for o in list(meshes) + list(arms):
    o.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(
    filepath=fbx,
    use_selection=True,
    object_types={"MESH", "ARMATURE"},
    path_mode="COPY",
    embed_textures=False,
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_force_startend_keying=True,
    add_leaf_bones=False,
    mesh_smooth_type="FACE",
    use_mesh_modifiers=False,
)
print("  fbx ->", os.path.relpath(fbx, REPO), os.path.getsize(fbx), "bytes")

saved = os.path.join(REPO, "artifacts", "m35-scarecrow-source.blend")
bpy.ops.wm.save_as_mainfile(filepath=saved)
print("  blend ->", os.path.relpath(saved, REPO))

with open(os.path.join(BLIND, "m35-import-report.txt"), "w") as fh:
    fh.write("M35 — the supplied scarecrow, imported\n")
    fh.write(f"source {GLB}\n")
    fh.write(f"body {body.name}: {len(body.data.loop_triangles)} tris, "
             f"{len(body.vertex_groups)} vertex groups\n")
    fh.write(f"armature {arm.name}: {len(arm.data.bones)} bones\n")
    fh.write(f"actions {[a.name for a in acts]}\n")
    fh.write(f"authored height {size.z:.3f} units; scale to {TARGET_HEIGHT:.2f} m = "
             f"{TARGET_HEIGHT / size.z:.4f}\n")
    fh.write(f"fbx {os.path.relpath(fbx, REPO)}\n")
    fh.write("dropped: Icosphere (80 tris, no material, no skinning)\n")
print("M35_IMPORT_DONE")
