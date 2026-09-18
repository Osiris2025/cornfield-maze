#!/usr/bin/env python3
"""REVEAL renders of Todd's gingerbread man — the asset as it will read in the game.

Not a flat asset preview: this is the reveal shot. Night corn-field mood, low
camera looking up, warm rim light, so the hollow ring eyes and the wide red ring
mouth read as INTENDED (Todd: "that is by design. this will be a scary gamer").

Output: artifacts/review/reveal/  (separate from the crew's artifacts/review/cookie/
so nothing fights over the same files).

Run: /Applications/Blender.app/Contents/MacOS/Blender --background --python scripts/render-reveal.py
"""
import bpy
import os
import math
import random
from mathutils import Vector

ROOT = "/Volumes/files2/CornFieldMaze"


def locate(name, prefer=("Assets/Resources", "Assets")):
    """Find an asset wherever it currently lives under ROOT.

    The crew MOVES these files (they relocated the cookie into Assets/Resources so the
    runtime builder can Resources.Load it), so a hardcoded path goes stale between runs.
    Search instead, preferring the live Assets tree over the archived docs/reference copy.
    """
    hits = []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in (".git", "Library", "Logs", "Temp", "obj")]
        if name in filenames:
            hits.append(os.path.join(dirpath, name))
    if not hits:
        raise SystemExit("cannot find %s anywhere under %s" % (name, ROOT))
    for pref in prefer:
        for h in hits:
            if os.path.join(ROOT, pref) in h:
                return h
    return hits[0]


FBX = locate("gb_man.fbx")
TEXDIR = os.path.dirname(locate("gb_man_color_512.png"))
OUT = os.path.join(ROOT, "artifacts/review/reveal")
COLOR = locate("gb_man_color_512.png")
NORMAL = locate("gb_man_normals_512.png")
print("FBX   = %s" % FBX)
print("COLOR = %s" % COLOR)
print("NORM  = %s" % NORMAL)

os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)

meshes = [o for o in bpy.data.objects if o.type == 'MESH']
print("imported %d meshes" % len(meshes))

# ------------------------------------------------------------- textures
for mat in bpy.data.materials:
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if bsdf is None:
        continue
    img = bpy.data.images.load(COLOR)
    tex = nt.nodes.new('ShaderNodeTexImage')
    tex.image = img
    tex.location = (-500, 300)
    nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    if os.path.exists(NORMAL):
        nm = nt.nodes.new('ShaderNodeNormalMap')
        nm.location = (-300, -200)
        timg = bpy.data.images.load(NORMAL)
        timg.colorspace_settings.name = 'Non-Color'
        tnode = nt.nodes.new('ShaderNodeTexImage')
        tnode.image = timg
        tnode.location = (-600, -200)
        nt.links.new(tnode.outputs['Color'], nm.inputs['Color'])
        nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
    # dough, not plastic: matte, slight subsurface warmth
    bsdf.inputs['Roughness'].default_value = 0.72
    if 'Metallic' in bsdf.inputs:
        bsdf.inputs['Metallic'].default_value = 0.0

# ---------------------------------------------- the field closes in
# Corn rows as plain dark blades. They are silhouette only -- they exist to put
# the cookie IN a maze at dusk rather than floating in a void.
random.seed(7)
corn_mat = bpy.data.materials.new("CornSilhouette")
corn_mat.use_nodes = True
cb = corn_mat.node_tree.nodes.get('Principled BSDF')
if cb:
    cb.inputs['Base Color'].default_value = (0.10, 0.085, 0.055, 1.0)
    cb.inputs['Roughness'].default_value = 0.85

M = 1.0
for i in range(90):
    x = random.uniform(-14, 14)
    y = random.uniform(3.5, 16.0)          # behind the cookie from camera
    h = random.uniform(1.9, 3.1)
    w = random.uniform(0.06, 0.13)
    # a blade: a tall thin box, leaning slightly
    bpy.ops.mesh.primitive_plane_add(size=1, location=(x, y, h / 2.0))
    ob = bpy.context.active_object
    ob.scale = (w, 0.02, h)
    ob.rotation_euler = (0, random.uniform(-0.22, 0.22), 0)
    ob.data.materials.append(corn_mat)

# a ground plane so the rim light catches something and grounds the figure
bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 6, 0))
ground = bpy.context.active_object
gmat = bpy.data.materials.new("NightGround")
gmat.use_nodes = True
gb = gmat.node_tree.nodes.get('Principled BSDF')
if gb:
    gb.inputs['Base Color'].default_value = (0.045, 0.040, 0.030, 1.0)
    gb.inputs['Roughness'].default_value = 1.0
ground.data.materials.append(gmat)

# --------------------------------------------------------------- world
world = bpy.data.worlds.new("W")
bpy.context.scene.world = world
world.use_nodes = True
world.node_tree.nodes['Background'].inputs[0].default_value = (0.030, 0.034, 0.055, 1.0)
world.node_tree.nodes['Background'].inputs[1].default_value = 1.0


def add_light(name, loc, energy, size, color):
    d = bpy.data.lights.new(name, type='AREA')
    d.energy = energy
    d.size = size
    d.color = color
    o = bpy.data.objects.new(name, d)
    bpy.context.scene.collection.objects.link(o)
    o.location = loc
    direction = Vector((0, 0, 0.6)) - Vector(loc)
    o.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    return o


# one warm key from low front-right (the campfire / storm-glow read), a hard cool
# rim from behind to peel the silhouette off the corn, almost no fill.
add_light("Key", (2.2, -2.6, 1.0), 900, 2.6, (1.0, 0.78, 0.52))
add_light("Rim", (-1.9, 2.4, 2.2), 700, 1.8, (0.72, 0.82, 1.0))
add_light("Fill", (-2.6, -1.6, 0.7), 60, 3.0, (0.55, 0.62, 0.85))
# The corn needs its OWN light or it renders black-on-black and the field reads as an
# empty void -- which is exactly how the first attempt failed. A big dim cool source
# behind the rows picks them out as silhouettes against the sky.
add_light("CornGlow", (0.0, 11.0, 3.5), 2400, 9.0, (0.50, 0.62, 0.95))

# ------------------------------------------------------------------ fit
mins = Vector((1e9, 1e9, 1e9))
maxs = Vector((-1e9, -1e9, -1e9))
for m in meshes:
    for c in m.bound_box:
        w = m.matrix_world @ Vector(c)
        for i in range(3):
            mins[i] = min(mins[i], w[i])
            maxs[i] = max(maxs[i], w[i])
center = (mins + maxs) / 2.0
size = max((maxs - mins)[i] for i in range(3))
print("bounds size=%.3f center=%s" % (size, tuple(round(v, 3) for v in center)))

# --------------------------------------------------------------- render
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = int(os.environ.get("SAMPLES", "24"))
scene.cycles.use_denoising = True
_RES_ENV = os.environ.get("RES", "")
if _RES_ENV:
    print("RES override -> %s" % _RES_ENV)
scene.render.film_transparent = False
scene.render.image_settings.file_format = 'PNG'
names = [v.name for v in scene.view_settings.bl_rna.properties['view_transform'].enum_items]
scene.view_settings.view_transform = 'Filmic' if 'Filmic' in names else 'Standard'
scene.view_settings.look = 'None'

cam_data = bpy.data.cameras.new("Cam")
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam


def shoot(name, azim_deg, elev_deg, dist_mult, target=None, lens=58, res=(2556, 1179)):
    if _RES_ENV:
        try:
            res = tuple(int(v) for v in _RES_ENV.lower().split("x"))
        except Exception as exc:
            print("bad RES=%r: %s" % (_RES_ENV, exc))
    scene.render.resolution_x, scene.render.resolution_y = res
    cam_data.lens = lens
    tgt = target if target is not None else center
    a, e = math.radians(azim_deg), math.radians(elev_deg)
    d = size * dist_mult
    cam.location = (tgt.x + d * math.cos(e) * math.sin(a),
                    tgt.y - d * math.cos(e) * math.cos(a),
                    tgt.z + d * math.sin(e))
    # Never let the low angle push the camera under the ground plane: at a hero distance
    # of ~4x the model size, elev -8 puts the eye at z<0 and the floor occludes the whole
    # subject (the first hero render was an empty black frame for exactly this reason).
    if cam.location[2] < 0.6:
        cam.location = (cam.location[0], cam.location[1], 0.6)
    cam.rotation_euler = (tgt - Vector(cam.location)).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = os.path.join(OUT, name)
    bpy.ops.render.render(write_still=True)
    print("wrote %s (%dx%d)" % (scene.render.filepath, res[0], res[1]))


# Hero: LOW camera looking up at him -- he is the threat, you are on the ground.
# distance is in multiples of the model's own 5.376 size, so 4.0 leaves head+feet room
# in a 2.17:1 frame; the first attempt at 1.95 cropped him top and bottom.
shoot("REVEAL-hero-iphone.png", 22, -8, 4.0, target=center, lens=52)

# The face, close and dark: hollow ring eyes, wide red ring mouth. 1.35 fits the whole
# HEAD with a little air -- at 0.52 the eyes were cut off by the top of the frame.
head = Vector((center.x, center.y, mins.z + (maxs.z - mins.z) * 0.84))
shoot("REVEAL-face-iphone.png", 14, -4, 1.35, target=head, lens=85)

# Portrait crop for a feed/post: same mood, taller frame.
shoot("REVEAL-portrait.png", 18, -7, 2.0, target=center, lens=52, res=(1350, 1688))

print("================ REVEAL DONE ================")
