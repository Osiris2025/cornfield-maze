#!/usr/bin/env python3
"""MOTION reveal: a slow push-in through the corn, ending on the cookie's face.

Stills do not hold a room. This is the same scene as render-reveal.py, animated:
the camera creeps from a wide low hero shot to a close face shot, the corn sways,
and the key light flickers and then falls away so the face half-drops into dark.

No bone animation -- the FBX ships ZERO animation clips (verified: no AnimationStack),
so the life in this shot comes from camera, corn and light, all of which are safe to
drive from a script. Walk cycles are the crew's job in code, not this render's.

Frames -> /tmp/cornframes_motion/frame_%04d.png, encoded separately with ffmpeg.

Run: /Applications/Blender.app/Contents/MacOS/Blender --background --python scripts/render-reveal-motion.py
Env: FRAMES (default 72), RES (default 1280x590), SAMPLES (default 12)
"""
import bpy
import os
import math
import random
from mathutils import Vector

ROOT = "/Volumes/files2/CornFieldMaze"
FRAMES = int(os.environ.get("FRAMES", "72"))
SAMPLES = int(os.environ.get("SAMPLES", "12"))
RES = tuple(int(v) for v in os.environ.get("RES", "1280x590").lower().split("x"))
OUTDIR = "/tmp/cornframes_motion"


def locate(name):
    """Find an asset wherever it currently lives -- the crew MOVES these files."""
    hits = []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in (".git", "Library", "Logs", "Temp", "obj")]
        if name in filenames:
            hits.append(os.path.join(dirpath, name))
    if not hits:
        raise SystemExit("cannot find %s under %s" % (name, ROOT))
    for h in hits:
        if os.path.join(ROOT, "Assets/Resources") in h:
            return h
    for h in hits:
        if os.path.join(ROOT, "Assets") in h:
            return h
    return hits[0]


os.makedirs(OUTDIR, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=locate("gb_man.fbx"))
meshes = [o for o in bpy.data.objects if o.type == 'MESH']

COLOR = locate("gb_man_color_512.png")
NORMAL = locate("gb_man_normals_512.png")
for mat in bpy.data.materials:
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if bsdf is None:
        continue
    if os.path.exists(COLOR):
        tex = nt.nodes.new('ShaderNodeTexImage')
        tex.image = bpy.data.images.load(COLOR)
        tex.location = (-500, 300)
        nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    if os.path.exists(NORMAL):
        nm = nt.nodes.new('ShaderNodeNormalMap')
        tnode = nt.nodes.new('ShaderNodeTexImage')
        tnode.image = bpy.data.images.load(NORMAL)
        tnode.image.colorspace_settings.name = 'Non-Color'
        tnode.location = (-600, -200)
        nm.location = (-300, -150)
        nt.links.new(tnode.outputs['Color'], nm.inputs['Color'])
        nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
    bsdf.inputs['Roughness'].default_value = 0.72
    if 'Metallic' in bsdf.inputs:
        bsdf.inputs['Metallic'].default_value = 0.0

# ------------------------------------------------- corn, with sway
random.seed(7)
corn_mat = bpy.data.materials.new("CornSilhouette")
corn_mat.use_nodes = True
cb = corn_mat.node_tree.nodes.get('Principled BSDF')
if cb:
    cb.inputs['Base Color'].default_value = (0.10, 0.085, 0.055, 1.0)
    cb.inputs['Roughness'].default_value = 0.85

corn = []
for i in range(90):
    x = random.uniform(-14, 14)
    y = random.uniform(3.5, 16.0)
    h = random.uniform(1.9, 3.1)
    w = random.uniform(0.06, 0.13)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(x, y, h / 2.0))
    ob = bpy.context.active_object
    ob.scale = (w, 0.02, h)
    base = random.uniform(-0.22, 0.22)
    ob.rotation_euler = (0, base, 0)
    ob.data.materials.append(corn_mat)
    corn.append((ob, base, random.uniform(0, math.tau)))

bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 6, 0))
ground = bpy.context.active_object
gmat = bpy.data.materials.new("NightGround")
gmat.use_nodes = True
gb = gmat.node_tree.nodes.get('Principled BSDF')
if gb:
    gb.inputs['Base Color'].default_value = (0.045, 0.040, 0.030, 1.0)
    gb.inputs['Roughness'].default_value = 1.0
ground.data.materials.append(gmat)

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
    o.rotation_euler = (Vector((0, 0, 0.6)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    return o


KEY = add_light("Key", (2.2, -2.6, 1.0), 900, 2.6, (1.0, 0.78, 0.52))
add_light("Rim", (-1.9, 2.4, 2.2), 700, 1.8, (0.72, 0.82, 1.0))
add_light("Fill", (-2.6, -1.6, 0.7), 60, 3.0, (0.55, 0.62, 0.85))
# corn needs its own light or the field renders as an empty void
add_light("CornGlow", (0.0, 11.0, 3.5), 2400, 9.0, (0.50, 0.62, 0.95))

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
head = Vector((center.x, center.y, mins.z + (maxs.z - mins.z) * 0.84))

scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = SAMPLES
scene.cycles.use_denoising = True
scene.render.film_transparent = False
scene.render.resolution_x, scene.render.resolution_y = RES
scene.render.image_settings.file_format = 'PNG'
names = [v.name for v in scene.view_settings.bl_rna.properties['view_transform'].enum_items]
scene.view_settings.view_transform = 'Filmic' if 'Filmic' in names else 'Standard'
scene.view_settings.look = 'None'
scene.frame_start, scene.frame_end = 1, FRAMES

cam_data = bpy.data.cameras.new("Cam")
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam


def ease(t):
    return t * t * (3 - 2 * t)          # smoothstep: settle, no snap


def lerp(a, b, t):
    return a + (b - a) * t


for f in range(1, FRAMES + 1):
    t = (f - 1) / float(FRAMES - 1)
    e = ease(t)

    # camera creeps from the wide low hero to a close face shot
    azim, elev = lerp(22, 12, e), lerp(-8, -3, e)
    dist = lerp(4.0, 1.45, e)
    lens = lerp(52, 66, e)
    tgt = center.lerp(head, e)

    a, el = math.radians(azim), math.radians(elev)
    d = size * dist
    cam_data.lens = lens
    cam.location = (tgt.x + d * math.cos(el) * math.sin(a),
                    tgt.y - d * math.cos(el) * math.cos(a),
                    tgt.z + d * math.sin(el))
    if cam.location[2] < 0.6:                      # never drop under the ground plane
        cam.location = (cam.location[0], cam.location[1], 0.6)
    cam.rotation_euler = (tgt - Vector(cam.location)).to_track_quat('-Z', 'Y').to_euler()

    # corn breathes
    for ob, base, phase in corn:
        ob.rotation_euler = (0, base + 0.07 * math.sin(math.tau * t * 2.0 + phase), 0)

    # the light flickers, then falls away so the face half-drops into dark
    flicker = 1.0 + 0.05 * math.sin(6.0 * t * math.tau)
    fade = 1.0 if t < 0.55 else lerp(1.0, 0.68, (t - 0.55) / 0.45)
    KEY.data.energy = 900.0 * flicker * fade

    scene.render.filepath = os.path.join(OUTDIR, "frame_%04d.png" % f)
    bpy.ops.render.render(write_still=True)
    if f % 12 == 0 or f == FRAMES:
        print("frame %d/%d" % (f, FRAMES))

print("================ MOTION FRAMES DONE (%d frames @ %dx%d) ================" % (FRAMES, RES[0], RES[1]))
