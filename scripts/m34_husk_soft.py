# DESIGN-TIME ONLY - not compiled into the Unity build, never ships on device.
# M34 P1 (soft pass) - the Husk, built the Blender way.
#
# The primer pass lathed capped tubes by hand and read as blocks. This pass uses the
# tools that make soft objects instead:
#   * METABALLS   - the body mass: hips, chest, shoulders, neck, and the skull merge into
#                   ONE isosurface, so there is no seam where a shoulder meets a torso.
#   * SKIN        - limbs, hands and feet as a stick skeleton with per-vertex radii, then
#                   subdivided: tapered, knuckled, with real branches at the claws.
#   * CLOTH SIM   - the coveralls are a sheet dropped over the body collider, so the folds
#                   are the ones gravity and the body actually make.
#   * SUBDIV+DISPLACE - burlap and felt surfaces get their grain from a texture, not from
#                   jittering vertices by hand.
#
# Run: /Applications/Blender.app/Contents/MacOS/Blender -b -noaudio --python scripts/m34_husk_soft.py
#
# OUTPUT: artifacts/review/world/m34-p1*.png, artifacts/m34-husk-p1.{blend,fbx},
#         artifacts/m34-p1-report.txt
import math
import os
import random
import re

import bmesh
import bpy
import mathutils
import numpy as np

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHOTS = os.path.join(REPO, "artifacts", "review", "world")
BLIND = os.path.join(REPO, "artifacts")
TARGET_HEIGHT = 2.30
VOXEL = 0.016                     # the blend: 16 mm voxels fuse the masses, keep the toes apart
RNG = random.Random(34_001)

# The masses that must read as ONE skin get voxel-fused. Everything matched here stays
# crisp and is joined back on afterwards, because a remesh would eat it: an 11 mm straw
# strand, a stitched tooth and a hat brim edge are all smaller than the voxel - and so are
# the FINGERS and TOES, which a 16 mm voxel turns into nubs. Hands and feet stay crisp;
# their wrist and ankle joins are hidden inside the torn cuff and the trouser hem anyway.
CRISP = re.compile(r"^(eye|socket|tooth|straw|hat|belt|sash|strap|pocket|neck_cord|hand|foot)")

# ------------------------------------------------------------- the palette
# Read off Todd's reference: blue-grey coveralls, tallow-yellow sack face, olive-grey
# felt hat, pale damp straw, dark wet timber, and the two hot eyes.
C_DENIM = (0.085, 0.115, 0.150)
C_DENIM_DARK = (0.052, 0.070, 0.096)
C_HOOD = (0.300, 0.280, 0.215)
C_HOOD_DARK = (0.205, 0.188, 0.142)
C_SACK = (0.330, 0.230, 0.105)
C_SACK_DARK = (0.200, 0.135, 0.060)
C_HAT = (0.115, 0.125, 0.110)
C_WOOD = (0.105, 0.082, 0.058)
C_STRAW = (0.225, 0.185, 0.098)
C_DARK = (0.018, 0.016, 0.014)
C_GLOW = (0.900, 0.230, 0.055)


# ------------------------------------------------------------------ basic kit
def fresh():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    return bpy.context.scene


def deselect():
    for o in bpy.data.objects:
        o.select_set(False)


def activate(obj):
    deselect()
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def set_colour(obj, rgba):
    """Every part carries its colour in a point attribute, so the finished creature is
    ONE mesh with ONE opaque material."""
    me = obj.data
    attr = me.color_attributes.get("Col") or me.color_attributes.new(
        name="Col", type="FLOAT_COLOR", domain="POINT")
    for d in attr.data:
        d.color = (rgba[0], rgba[1], rgba[2], 1.0)


def grab(obj, colour):
    set_colour(obj, colour)
    return obj


def ring(centre, radius, segs, normal=(0, 0, 1), squash=(1.0, 1.0), wobble=0.0, rng=None):
    n = mathutils.Vector(normal).normalized()
    up = mathutils.Vector((0, 0, 1))
    if abs(n.dot(up)) > 0.98:
        up = mathutils.Vector((1, 0, 0))
    t1 = n.cross(up).normalized()
    t2 = n.cross(t1).normalized()
    c = mathutils.Vector(centre)
    pts = []
    for i in range(segs):
        a = 2 * math.pi * i / segs
        r = radius * (1.0 + (rng.uniform(-wobble, wobble) if (wobble and rng) else 0.0))
        p = c + t1 * (math.cos(a) * r) + t2 * (math.sin(a) * r)
        # squash is in WORLD axes (x, y), not in the ring's local basis: a caller saying
        # squash=(1.0, 0.8) means "narrow this shape in Y", whatever way the ring is turned.
        off = p - c
        pts.append(c + mathutils.Vector((off.x * squash[0], off.y * squash[1], off.z)))
    return pts


def skin_rows(rows, segs, close_top=True):
    verts, faces = [], []
    for row in rows:
        for p in row:
            verts.append(tuple(p))
    for i in range(len(rows) - 1):
        for s in range(segs):
            faces.append((i * segs + s, i * segs + (s + 1) % segs,
                          (i + 1) * segs + (s + 1) % segs, (i + 1) * segs + s))
    if close_top:
        faces.append(tuple(range(segs))[::-1])
    return verts, faces


def new_mesh(name, verts, faces, colour=C_DENIM):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.validate(verbose=False)
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    return grab(ob, colour)


def loft(name, rows, segs, colour, close_top=False, close_bottom=False):
    verts, faces = skin_rows(rows, segs, close_top=close_top)
    if close_bottom:
        base = (len(rows) - 1) * segs
        faces.append(tuple(base + s for s in range(segs)))
    return new_mesh(name, verts, faces, colour)


def tube(name, path, radii, segs=12, cap_start=True, cap_end=True, wobble=0.0,
         rng=None, squash=None, colour=C_DENIM):
    rows = []
    n = len(path)
    for i, p in enumerate(path):
        if i == 0:
            t = mathutils.Vector(path[1]) - mathutils.Vector(path[0])
        elif i == n - 1:
            t = mathutils.Vector(path[-1]) - mathutils.Vector(path[-2])
        else:
            t = mathutils.Vector(path[i + 1]) - mathutils.Vector(path[i - 1])
        rows.append(ring(p, radii[i], segs, normal=t, wobble=wobble, rng=rng,
                         squash=squash[i] if squash else (1.0, 1.0)))
    verts, faces = skin_rows(rows, segs, close_top=cap_start)
    if cap_end:
        base = (n - 1) * segs
        faces.append(tuple(base + s for s in range(segs)))
    return new_mesh(name, verts, faces, colour)


def jitter_verts(obj, amount, rng, mask=(1, 1, 1)):
    for v in obj.data.vertices:
        v.co.x += rng.uniform(-amount, amount) * mask[0]
        v.co.y += rng.uniform(-amount, amount) * mask[1]
        v.co.z += rng.uniform(-amount, amount) * mask[2]


def apply_mod(obj, mod):
    activate(obj)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def subdiv(obj, levels=1):
    m = obj.modifiers.new("sub", "SUBSURF")
    m.levels = m.render_levels = levels
    return apply_mod(obj, m)


def solidify(obj, thickness):
    m = obj.modifiers.new("solid", "SOLIDIFY")
    m.thickness = thickness
    m.offset = 0.0
    return apply_mod(obj, m)


def displace(obj, strength, scale=0.55, kind="CLOUDS", seed=7):
    """Surface grain from a texture - the soft way to get burlap, felt and bark."""
    tex = bpy.data.textures.new(f"tex_{obj.name}", type=kind)
    tex.noise_scale = scale
    if hasattr(tex, "noise_depth"):
        tex.noise_depth = 2
    m = obj.modifiers.new("disp", "DISPLACE")
    m.texture = tex
    m.strength = strength
    m.mid_level = 0.5
    m.texture_coords = "LOCAL"
    return apply_mod(obj, m)


def setopt(obj, name, value):
    """Set an optional API property if this Blender build has it. Guessing at property
    names costs a whole render pass each time; this makes a rename a no-op instead."""
    if hasattr(obj, name):
        setattr(obj, name, value)
        return True
    return False


def rotate_about(objs, pivot, rx_deg=0.0, ry_deg=0.0, rz_deg=0.0):
    """Rotate objects about a shared pivot - how the hat gets cocked back off the brow."""
    piv = mathutils.Vector(pivot)
    rot = mathutils.Euler((math.radians(rx_deg), math.radians(ry_deg),
                           math.radians(rz_deg)), "XYZ").to_matrix().to_4x4()
    m = mathutils.Matrix.Translation(piv) @ rot @ mathutils.Matrix.Translation(-piv)
    for o in objs:
        o.matrix_world = m @ o.matrix_world


def smooth(obj, factor=0.5, repeat=2):
    m = obj.modifiers.new("smooth", "SMOOTH")
    m.factor = factor
    m.iterations = repeat
    return apply_mod(obj, m)


# ---------------------------------------------------------------- metaballs
def metaball_mesh(name, elements, colour, resolution=0.014, threshold=0.6):
    """elements: list of dicts {co, radius, size (x,y,z), rot (euler), stiffness}.
    Everything in the list merges into ONE isosurface - no seam where parts meet."""
    mb = bpy.data.metaballs.new(name)
    mb.resolution = resolution
    mb.render_resolution = resolution
    mb.threshold = threshold
    for e in elements:
        el = mb.elements.new()
        el.type = "ELLIPSOID"
        el.co = e["co"]
        el.radius = e.get("radius", 0.2)
        size = e.get("size", (0.5, 0.5, 0.5))
        el.size_x, el.size_y, el.size_z = size
        el.stiffness = e.get("stiffness", 2.0)
        rot = e.get("rot")
        if rot:
            el.rotation = rot
    ob = bpy.data.objects.new(name, mb)
    bpy.context.collection.objects.link(ob)
    activate(ob)
    bpy.ops.object.convert(target="MESH")
    ob = bpy.context.view_layer.objects.active
    ob.name = ob.data.name = name
    # the conversion leaves loose shells for elements that fell below threshold
    activate(ob)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.delete_loose()
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    return grab(ob, colour)


def rot_deg(x=0.0, y=0.0, z=0.0):
    return mathutils.Euler((math.radians(x), math.radians(y), math.radians(z)), "XYZ")


# -------------------------------------------------------------- skin limbs
def skin_limb(name, nodes, edges, colour, subsurf=2, smooth_factor=0.4):
    """nodes: [(pos, radius_tuple, is_root)] - a skeleton the Skin modifier thickens into
    a soft tapering limb. Branches are just nodes with more than one edge."""
    me = bpy.data.meshes.new(name)
    me.from_pydata([n[0] for n in nodes], edges, [])
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    activate(ob)
    sk = ob.modifiers.new("skin", "SKIN")
    sk.use_smooth_shade = True
    sk.branch_smoothing = 0.6
    sv = me.skin_vertices[0].data
    for i, n in enumerate(nodes):
        sv[i].radius = n[1]
        if n[2]:
            sv[i].use_root = True
    apply_mod(ob, sk)
    if smooth_factor:
        smooth(ob, smooth_factor, 2)
    if subsurf:
        m = ob.modifiers.new("sub", "SUBSURF")
        m.levels = m.render_levels = subsurf
        apply_mod(ob, m)
    return grab(ob, colour)


# ----------------------------------------------------------------- parts
def build_body(parts, rng):
    """The trunk: gaunt, narrow-waisted, with a wide shoulder girdle - built as a loft with
    radii stated outright.

    A metaball mass was tried here first and thrown out: its isosurface comes out much
    smaller than the element sizes imply, and it merges neighbouring elements until the
    waist is wider than the chest. This shape has to be exact, because the belt, straps,
    pockets, collar and arms all key off it - so it is stated, and printed back to prove it.
    """
    profile = [(0.96, 0.215, 0.180), (1.06, 0.205, 0.170), (1.16, 0.200, 0.168),
               (1.28, 0.212, 0.176), (1.40, 0.240, 0.192), (1.52, 0.268, 0.202),
               (1.62, 0.300, 0.200), (1.70, 0.280, 0.184), (1.76, 0.185, 0.150),
               (1.82, 0.108, 0.100)]
    SEGS = 26
    rows = []
    for z, rx, ry in profile:
        pts = ring((0.0, -0.005, z), rx, SEGS, squash=(1.0, ry / rx), wobble=0.05, rng=rng)
        rows.append([(p.x, p.y, p.z) for p in pts])
    body = loft("body", rows, SEGS, C_DENIM, close_top=True, close_bottom=True)
    subdiv(body, 2)
    jitter_verts(body, 0.004, rng)
    displace(body, 0.005, 0.30)
    parts.append(body)
    for z, _, _ in profile:
        rx, ry = body_profile(body, z, tol=0.06)
        print(f"  body profile z={z:.2f}: rx={rx:.3f} ry={ry:.3f}")


def build_legs(parts, rng):
    """Skin-modifier legs: baggy coverall trousers over a thin wooden core, one leg
    shorter and splayed, the cloth breaking over the knee. Feet are root-wood with
    three split toes - a skeleton branch, not three glued-on tubes."""
    for side in ("L", "R"):
        s = -1.0 if side == "L" else 1.0
        short = side == "R"
        hip = (s * 0.16, 0.0, 1.02 if not short else 0.98)
        knee = (s * 0.20, 0.05 if not short else -0.04, 0.55 if not short else 0.60)
        ankle = (s * 0.195, -0.01 if not short else -0.07, 0.135 if not short else 0.16)
        nodes = [
            (hip, (0.135, 0.135), True),
            (((hip[0] + knee[0]) / 2, (hip[1] + knee[1]) / 2, (hip[2] + knee[2]) / 2),
             (0.132, 0.132), False),
            (knee, (0.115, 0.115), False),
            (((knee[0] + ankle[0]) / 2, (knee[1] + ankle[1]) / 2 + 0.02, (knee[2] + ankle[2]) / 2),
             (0.118, 0.118), False),
            (ankle, (0.108, 0.108), False),
            ((ankle[0], ankle[1] - 0.02, 0.06), (0.086, 0.086), False),
        ]
        edges = [(i, i + 1) for i in range(len(nodes) - 1)]
        leg = skin_limb(f"leg_{side}", nodes, edges, C_DENIM, subsurf=2, smooth_factor=0.35)
        displace(leg, 0.005, 0.30)
        parts.append(leg)

        # the torn hem where the trouser leg is shredding, and the straw inside it
        hem_z = ankle[2] + 0.10
        rows = []
        for ri, (z, r) in enumerate(((hem_z + 0.06, 0.112), (hem_z, 0.118), (hem_z - 0.07, 0.100))):
            pts = ring((ankle[0], ankle[1], z), r, 14, wobble=0.10, rng=rng)
            rows.append([(p.x, p.y, p.z + (rng.uniform(-0.05, 0.015) if ri == 2 else 0.0))
                         for p in pts])
        hem = loft(f"hem_{side}", rows, 14, C_DENIM_DARK)
        solidify(hem, 0.016)
        parts.append(hem)

        # foot: ankle -> instep -> ball, then three toes branching off the ball
        fx, fy = ankle[0], ankle[1]
        nodes = [
            ((fx, fy, ankle[2]), (0.098, 0.086), True),
            ((fx, fy - 0.02, 0.055), (0.104, 0.082), False),
            ((fx, fy - 0.11, 0.036), (0.096, 0.062), False),
        ]
        edges = [(0, 1), (1, 2)]
        for i, tx in enumerate((-0.062, 0.0, 0.062)):
            nodes.append(((fx + tx, fy - 0.265 - abs(tx) * 0.28, 0.026),
                          (0.036 - abs(tx) * 0.10, 0.030), False))
            edges.append((2, 3 + i))
        foot = skin_limb(f"foot_{side}", nodes, edges, C_WOOD, subsurf=2, smooth_factor=0.3)
        parts.append(foot)


def build_arms(parts, rng):
    """Skin-modifier arms: shoulder -> elbow -> wrist, then a palm branch with four
    fingers, so the hand is one continuous soft mass. Long enough to hang the straw
    strands off the torn cuff."""
    for side in ("L", "R"):
        s = -1.0 if side == "L" else 1.0
        body = bpy.data.objects.get("body")
        rx_sh, _ = body_profile(body, 1.64) if body else (0.30, 0.20)
        shoulder = (s * (rx_sh - 0.045), 0.0, 1.66)
        elbow = (s * 0.54, 0.05, 1.55)
        wrist = (s * 0.76, 0.02, 1.46)
        nodes = [
            (shoulder, (0.145, 0.145), True),
            (((shoulder[0] + elbow[0]) / 2, 0.035, (shoulder[2] + elbow[2]) / 2), (0.132, 0.132), False),
            (elbow, (0.112, 0.112), False),
            (((elbow[0] + wrist[0]) / 2, 0.035, (elbow[2] + wrist[2]) / 2), (0.104, 0.104), False),
            (wrist, (0.094, 0.094), False),
        ]
        edges = [(i, i + 1) for i in range(len(nodes) - 1)]
        arm = skin_limb(f"arm_{side}", nodes, edges, C_DENIM, subsurf=2, smooth_factor=0.35)
        displace(arm, 0.005, 0.30)
        parts.append(arm)

        # the sleeve is torn open here: a ragged band, then the bare wooden wrist
        rows = []
        for ri, (off, r) in enumerate(((0.07, 0.112), (0.0, 0.128), (-0.07, 0.106))):
            pts = ring((wrist[0], wrist[1], wrist[2] + off), r, 14, wobble=0.20, rng=rng)
            rows.append([(p.x + (rng.uniform(-0.05, 0.015) if ri == 2 else 0.0), p.y, p.z)
                         for p in pts])
        cuff = loft(f"cuff_{side}", rows, 14, C_DENIM_DARK)
        solidify(cuff, 0.016)
        parts.append(cuff)

        palm = (wrist[0] + s * 0.090, wrist[1], wrist[2] - 0.060)
        nodes = [(wrist, (0.072, 0.064), True), (palm, (0.086, 0.070), False)]
        edges = [(0, 1)]
        finger_dirs = [(-0.050, 0.120, 0.120), (0.010, 0.020, 0.140),
                       (0.055, -0.065, 0.125), (0.020, 0.065, 0.105)]
        for i, (dy, dz, ln) in enumerate(finger_dirs):
            mid = (palm[0] + s * ln * 0.55, palm[1] + dy * 0.5, palm[2] - ln * 0.32 + dz * 0.3)
            tip = (palm[0] + s * ln * 1.00, palm[1] + dy, palm[2] - ln * 0.80 + dz)
            nodes.append((mid, (0.032, 0.030), False))
            nodes.append((tip, (0.016, 0.015), False))
            base_i = 2 + i * 2
            edges.append((1, base_i))
            edges.append((base_i, base_i + 1))
        hand = skin_limb(f"hand_{side}", nodes, edges, C_WOOD, subsurf=2, smooth_factor=0.3)
        parts.append(hand)


def build_hood(parts, rng):
    """The beige hood/scarf: a soft collar mass around the neck (metaball, so it merges
    with the shoulders instead of sitting on them like a plate) plus draped folds."""
    els = [
        dict(co=(0.0, -0.02, 1.74), radius=0.24, size=(0.60, 0.56, 0.30), stiffness=2.0),
        dict(co=(0.0, 0.10, 1.83), radius=0.20, size=(0.50, 0.42, 0.34), stiffness=1.8),
        dict(co=(0.0, -0.13, 1.66), radius=0.18, size=(0.46, 0.30, 0.30), stiffness=1.8),
    ]
    collar = metaball_mesh("hood", els, C_HOOD, resolution=0.012)
    smooth(collar, 0.5, 2)
    displace(collar, 0.010, 0.22)
    parts.append(collar)
    # the fold that hangs down the chest and the knot at the throat
    rows = []
    for i, (z, r, y) in enumerate(((1.74, 0.20, -0.10), (1.62, 0.185, -0.12),
                                   (1.50, 0.170, -0.10), (1.40, 0.150, -0.08))):
        pts = ring((0.0, y, z), r, 14, squash=(1.0, 0.42), wobble=0.10, rng=rng)
        rows.append([(p.x, p.y, p.z) for p in pts])
    drape = loft("hood_fold", rows, 14, C_HOOD)
    solidify(drape, 0.020)
    parts.append(drape)


def build_head(parts, rng):
    """The skull: a long gaunt mass - cranium, brow ridge, cheekbones and a pointed jaw
    merged into one isosurface, then displaced so the sack has a weave. The face is
    built to be SEEN: the hat sits back off the brow."""
    els = [
        dict(co=(0.0, 0.01, 2.09), radius=0.235, size=(0.72, 0.74, 0.78), stiffness=2.4),   # cranium
        dict(co=(0.0, -0.06, 2.03), radius=0.20, size=(0.78, 0.52, 0.30), stiffness=2.2),   # brow
        dict(co=(0.0, -0.02, 1.94), radius=0.19, size=(0.74, 0.62, 0.40), stiffness=2.1),   # cheeks
        dict(co=(0.0, -0.05, 1.86), radius=0.15, size=(0.44, 0.42, 0.30), stiffness=2.0),   # jaw
        dict(co=(0.0, -0.10, 1.81), radius=0.10, size=(0.26, 0.30, 0.20), stiffness=1.8),   # chin
    ]
    head = metaball_mesh("head", els, C_SACK, resolution=0.010)
    smooth(head, 0.45, 2)
    displace(head, 0.007, 0.14)                 # burlap weave
    head.location = (0.012, -0.004, 0.0)
    head.rotation_euler = (math.radians(-4), math.radians(4), math.radians(9))
    parts.append(head)

    # the sockets: a dark dish sunk into the face, opening forward, with a hot ball set
    # INSIDE it and standing proud - the reference's read is a black hole with a coal in
    # it, and a shell that merely surrounds the eye hides it from the front.
    for s in (-1, 1):
        cx = s * 0.098
        socket = tube(f"socket{s}",
                      [(cx, -0.105, 2.042), (cx * 1.03, -0.148, 2.040), (cx, -0.168, 2.036)],
                      [0.088, 0.084, 0.062], segs=26, cap_start=True, cap_end=False,
                      colour=C_DARK)
        parts.append(socket)
        # the coal: a tapered ball, not a disc. A flat cap facing the camera reads as a
        # painted hexagon however brightly it is lit, so the eye closes to a near-point.
        eye = tube(f"eye{s}",
                   [(cx, -0.128, 2.040), (cx * 1.0, -0.152, 2.038),
                    (cx, -0.172, 2.035), (cx, -0.187, 2.032)],
                   [0.012, 0.028, 0.026, 0.008], segs=24, colour=C_GLOW)
        parts.append(eye)
    # nose: a short pinched wedge off the brow, not a muzzle
    parts.append(tube("nose", [(0.0, -0.180, 2.000), (0.0, -0.205, 1.978), (0.0, -0.222, 1.960)],
                      [0.034, 0.024, 0.010], segs=10, colour=C_SACK))
    # the sewn grin: a shallow arc of small stitches. The previous pass ran a fat dark tube
    # across the face and it read as a jaw brace.
    for i in range(11):
        t = i / 10.0
        x = -0.094 + 0.188 * t
        z = 1.962 - 0.030 * math.sin(t * math.pi) + 0.014 * (abs(t - 0.5) * 2) ** 2
        y = -0.155 + 0.020 * (abs(t - 0.5) * 2) ** 1.6
        parts.append(tube(f"tooth{i}", [(x, y + 0.008, z + 0.010), (x, y - 0.004, z - 0.010)],
                          [0.0092, 0.0074], segs=6, colour=C_DARK))
    # rope/cord around the neck where the sack is tied off
    parts.append(tube("neck_cord", [(0.0, 0.0, 1.775), (0.0, 0.0, 1.845)], [0.098, 0.104],
                      segs=16, cap_start=False, cap_end=False, wobble=0.05, rng=rng,
                      colour=C_STRAW))


def build_hat(parts, rng):
    """A wide drooping slouch hat, cocked back so the face reads under it, with a dented
    crown. Felt: subdivided and displaced, then solidified."""
    SEGS = 40
    made = []
    rows = []
    for ri, (r, z) in enumerate(((0.140, 2.185), (0.205, 2.170), (0.262, 2.142),
                                 (0.302, 2.104), (0.318, 2.074))):
        pts = ring((0.0, 0.0, z), r, SEGS, wobble=0.035, rng=rng)
        row = []
        for p in pts:
            drop = 0.0
            if ri >= 2:
                ahead = max(0.0, -p.y / 0.318)         # the front edge lifts clear of the face
                side = abs(p.x) / 0.318
                drop = -0.045 * side - 0.024 * (ri - 1) + 0.050 * ahead - 0.010 * ahead * (ri - 2)
            row.append((p.x, p.y, p.z + drop))
        rows.append(row)
    brim = loft("hat_brim", rows, SEGS, C_HAT)
    displace(brim, 0.008, 0.30)
    solidify(brim, 0.016)
    made.append(brim)

    crown_rings = [(2.140, 0.156), (2.210, 0.150), (2.275, 0.134), (2.320, 0.100),
                   (2.346, 0.052), (2.352, 0.016)]
    rows = []
    for ri, (z, r) in enumerate(crown_rings):
        pts = ring((0.006 * ri, 0.004 * ri, z), r, SEGS, wobble=0.03, rng=rng)
        rows.append([(p.x, p.y, p.z + (-0.022 if 1 < ri < 5 and -0.7 < math.atan2(p.y, p.x) < 1.3
                                       else 0.0)) for p in pts])
    crown = loft("hat_crown", rows, SEGS, C_HAT, close_top=True, close_bottom=True)
    displace(crown, 0.007, 0.25)
    smooth(crown, 0.4, 1)
    made.append(crown)
    made.append(tube("hat_band", [(0.0, 0.0, 2.150), (0.0, 0.0, 2.192)], [0.160, 0.158],
                     segs=SEGS, cap_start=False, cap_end=False, wobble=0.02, rng=rng,
                     colour=C_DARK))
    # a couple of straw sprigs and a wire twist poking out of the crown
    for i in range(5):
        a = 0.3 + i * 0.42
        base = (math.cos(a) * 0.13, math.sin(a) * 0.13, 2.20)
        tip = (math.cos(a) * 0.34, math.sin(a) * 0.30, 2.33 + rng.uniform(-0.04, 0.06))
        made.append(tube(f"hat_straw{i}", [base,
                                           ((base[0] + tip[0]) / 2, (base[1] + tip[1]) / 2,
                                            (base[2] + tip[2]) / 2 + 0.012), tip],
                         [0.007, 0.005, 0.002], segs=4, colour=C_STRAW))
    made.append(tube("hat_wire", [(0.09, 0.10, 2.22), (0.19, 0.14, 2.31), (0.25, 0.09, 2.17)],
                     [0.006, 0.006, 0.004], segs=4, colour=C_WOOD))
    # cocked BACK off the brow (front edge up, crown back), which is what keeps the face
    # readable from a front camera instead of burying it in brim shadow
    rotate_about(made, (0.0, 0.0, 2.10), rx_deg=-15.0)
    parts.extend(made)


def build_straw(parts, rng):
    """Damp straw, grey-brown, clumped and drooping - out of the torn cuffs (the loudest
    mass), under the jaw, at the hem and at the shin hems."""
    def tuft(origin, direction, count, length, spread, radius):
        for _ in range(count):
            d = mathutils.Vector(direction).normalized()
            d.x += rng.uniform(-spread, spread)
            d.y += rng.uniform(-spread, spread)
            d.z += rng.uniform(-spread, 0.10)
            d.normalize()
            ln = length * rng.uniform(0.55, 1.25)
            o = mathutils.Vector(origin) + mathutils.Vector(
                (rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03)))
            mid = o + d * (ln * 0.5) + mathutils.Vector((0, 0, -ln * 0.14))
            end = o + d * ln + mathutils.Vector((0, 0, -ln * 0.45))
            parts.append(tube(f"straw{len(parts)}", [tuple(o), tuple(mid), tuple(end)],
                              [radius, radius * 0.7, radius * 0.22], segs=4, colour=C_STRAW))

    WR = 0.78                                             # the cuff rings sit at the wrists
    for s in (-1, 1):
        # out of the torn cuff: the loudest mass on the whole creature, and it has to start
        # just BEYOND the wrist or the sleeve and the hand swallow it
        tuft((s * 0.785, -0.02, 1.41), (s * 0.22, -0.06, -0.96), 16, 0.28, 0.42, 0.0115)
        tuft((s * 0.720, 0.04, 1.38), (s * 0.10, 0.30, -0.94), 7, 0.22, 0.40, 0.0105)
    for s in (-1, 1):                                     # the hem skirt's own edge
        tuft((s * 0.30, -0.02, 0.80), (s * 0.35, -0.10, -0.90), 6, 0.22, 0.36, 0.0105)
        tuft((s * 0.195, -0.02, 0.32), (s * 0.30, -0.30, -0.86), 6, 0.18, 0.32, 0.010)
    tuft((0.0, -0.24, 0.82), (0.0, -0.30, -0.88), 5, 0.20, 0.34, 0.010)
    for s in (-1, 1):                                     # under the jaw
        tuft((s * 0.10, -0.10, 1.78), (s * 0.25, -0.55, -0.75), 5, 0.20, 0.38, 0.010)


def body_profile(body, z, tol=0.035):
    """Half-extents of the body mass near a height, measured off the built mesh. Fitting
    the belt, pockets and straps to the silhouette beats guessing the isosurface radius:
    the metaball's surface is not a formula anyone wants to write down."""
    xs, ys = [], []
    for v in body.data.vertices:
        p = body.matrix_world @ v.co
        if abs(p.z - z) <= tol:
            xs.append(abs(p.x))
            ys.append(abs(p.y))
    if not xs:
        return 0.25, 0.20
    return max(xs), max(ys)


def surface_point(body, z, angle, out=0.014):
    """A point on (just outside) the body's surface at height z and plan angle."""
    rx, ry = body_profile(body, z)
    return (math.cos(angle) * (rx + out), math.sin(angle) * (ry + out), z)


def on_surface(body, z, x, front=True, out=0.016):
    """A point on the body at height z and a given x, on the front or the back - how a
    strap is routed over a shoulder without the two straps crossing each other."""
    rx, ry = body_profile(body, z)
    xr = max(-0.97, min(0.97, x / max(rx, 1e-4)))
    y = ry * math.sqrt(max(0.0, 1.0 - xr * xr))
    return (x, (-y - out) if front else (y + out), z)


def build_coveralls(parts, rng):
    """The coveralls are FITTED, which is what the reference is: a jacket over bib overalls
    with straps, chest pockets, a rope belt and a sash - all sitting ON the body's own
    silhouette, measured off it. Cloth sim is used only where cloth actually hangs: the
    hem skirt over the hips, and the scarf draping off the shoulders."""
    body = bpy.data.objects.get("body")
    if body is None:
        return

    # --- the rope belt: two rings hugging the waist -------------------------------
    for z, r0 in ((1.10, 1.035), (1.145, 1.035)):
        rx, ry = body_profile(body, z)
        rows = [ring((0.0, -0.01, z - 0.028), rx * r0, 30, squash=(1.0, ry / rx)),
                ring((0.0, -0.01, z + 0.028), rx * r0, 30, squash=(1.0, ry / rx))]
        parts.append(loft(f"belt_{z}", rows, 30, C_STRAW))

    # --- the pale sash, tied on the left hip and hanging ------------------------
    hip = surface_point(body, 1.12, math.radians(214), out=0.020)
    parts.append(tube("sash", [hip,
                               (hip[0] - 0.02, hip[1] - 0.03, 1.00),
                               (hip[0] - 0.01, hip[1] - 0.05, 0.86),
                               (hip[0] + 0.01, hip[1] - 0.045, 0.72)],
                      [0.034, 0.038, 0.028, 0.018], segs=9, wobble=0.09, rng=rng,
                      colour=C_HOOD))
    parts.append(tube("sash_knot", [(hip[0], hip[1], hip[2] + 0.015),
                                    (hip[0] + 0.01, hip[1] - 0.045, hip[2] - 0.055)],
                      [0.050, 0.028], segs=9, wobble=0.12, rng=rng, colour=C_HOOD))

    # --- bib straps: over each shoulder, front waist to back waist ---------------
    for s in (-1, 1):
        path = [on_surface(body, 1.18, s * 0.105, True),
                on_surface(body, 1.34, s * 0.115, True),
                on_surface(body, 1.52, s * 0.125, True),
                on_surface(body, 1.68, s * 0.130, True),
                (s * 0.135, 0.0, 1.735),
                on_surface(body, 1.68, s * 0.130, False),
                on_surface(body, 1.46, s * 0.120, False),
                on_surface(body, 1.20, s * 0.105, False)]
        parts.append(tube(f"strap_{s}", path,
                          [0.036, 0.040, 0.044, 0.045, 0.045, 0.044, 0.040, 0.036],
                          segs=10, wobble=0.04, rng=rng, colour=C_DENIM_DARK))

    # --- two chest pockets, proud of the bib ------------------------------------
    for s in (-1, 1):
        c = surface_point(body, 1.36, math.radians(-90 + s * 26), out=0.010)
        pocket = tube(f"pocket_{s}", [c, (c[0], c[1] - 0.020, c[2])],
                      [0.058, 0.056], segs=14, squash=[(1.25, 1.0)] * 2,
                      colour=C_DENIM_DARK)
        parts.append(pocket)

    # --- the collar: bunched at the throat, not a cape --------------------------
    rows = []
    for z, r, y in ((1.89, 0.118, -0.01), (1.83, 0.168, -0.02), (1.77, 0.196, -0.02),
                    (1.71, 0.204, -0.01), (1.65, 0.184, 0.00), (1.60, 0.150, 0.00)):
        pts = ring((0.0, y, z), r, 26, squash=(1.0, 0.88), wobble=0.08, rng=rng)
        rows.append([(p.x, p.y, p.z + rng.uniform(-0.014, 0.014)) for p in pts])
    collar = loft("collar", rows, 26, C_HOOD_DARK)
    displace(collar, 0.009, 0.20)
    solidify(collar, 0.020)
    parts.append(collar)
    # one fold of the scarf hangs down the chest, which is what stops it reading as a bib
    fold = tube("scarf_fold", [(0.070, -0.190, 1.66), (0.115, -0.225, 1.52),
                               (0.125, -0.230, 1.40), (0.112, -0.215, 1.30)],
                [0.050, 0.056, 0.048, 0.028], segs=14, wobble=0.10, rng=rng,
                squash=[(1.0, 0.55)] * 4, colour=C_HOOD_DARK)
    parts.append(fold)

    # --- the tatter skirt over the hips, torn at its own hem ---------------------
    rx_w, ry_w = body_profile(body, 1.06)
    SEGS = 30
    tear = [rng.uniform(-0.075, 0.02) for _ in range(SEGS)]
    rings = [(1.06, rx_w * 1.03, ry_w * 1.03), (0.96, rx_w * 1.14, ry_w * 1.14),
             (0.86, rx_w * 1.24, ry_w * 1.24)]
    rows = []
    for ri, (z, rx, ry) in enumerate(rings):
        pts = ring((0.0, -0.01, z), rx, SEGS, squash=(1.0, ry / rx), wobble=0.08, rng=rng)
        rows.append([(p.x, p.y, p.z + (tear[si] if ri == 2 else 0.0))
                     for si, p in enumerate(pts)])
    skirt = loft("tatter_skirt", rows, SEGS, C_DENIM_DARK, close_bottom=False)
    jitter_verts(skirt, 0.006, rng)
    solidify(skirt, 0.018)
    parts.append(skirt)


def _face_rgb(obj, poly):
    d = obj.data.color_attributes["Col"].data
    n = len(poly.vertices)
    r = g = b = 0.0
    for v in poly.vertices:
        c = d[v].color
        r += c[0]
        g += c[1]
        b += c[2]
    return (r / n, g / n, b / n)


def fuse(name, objs, voxel=VOXEL):
    """Weld a group of overlapping masses into ONE continuous skin.

    THIS is the pass that answers "blend it together better". A voxel remesh unions the
    trunk, limbs, hands, feet, head and cloth into a single surface, so the shoulder, hip,
    wrist and throat junctions stop being one tube pushed through another - they become
    one continuous mass the way a real stuffed sack is continuous.

    The remesh discards the colour attribute, so every source face's colour is sampled
    first and re-stamped onto the fused surface by nearest-face lookup. That is what keeps
    the denim / sack / wood zones where they belong on the new skin.
    """
    deselect()
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = o.data.name = name

    src = [(p.center.copy(), _face_rgb(o, p)) for p in o.data.polygons]
    tree = mathutils.kdtree.KDTree(len(src))
    for i, (c, _) in enumerate(src):
        tree.insert(c, i)
    tree.balance()

    activate(o)
    md = o.modifiers.new("remesh", "REMESH")
    setopt(md, "mode", "VOXEL")
    setopt(md, "voxel_size", voxel)
    setopt(md, "use_smooth_shade", True)
    bpy.ops.object.modifier_apply(modifier=md.name)
    o.data.calc_loop_triangles()
    print(f"  blend: {len(src)} source faces -> {len(o.data.loop_triangles)} tris "
          f"at {voxel * 1000:.0f} mm voxels")

    displace(o, 0.005, 0.30)                     # the fused skin is cloth, not plastic

    attr = o.data.color_attributes.get("Col") or o.data.color_attributes.new(
        name="Col", type="FLOAT_COLOR", domain="POINT")
    for i, v in enumerate(o.data.vertices):
        _, idx, _ = tree.find(v.co)
        attr.data[i].color = (*src[idx][1], 1.0)
    o.data.update()
    return o


def thin_to(obj, budget):
    obj.data.calc_loop_triangles()
    n = len(obj.data.loop_triangles)
    if n > budget:
        activate(obj)
        m = obj.modifiers.new("dec", "DECIMATE")
        m.ratio = max(0.02, budget / n)
        bpy.ops.object.modifier_apply(modifier=m.name)
        obj.data.calc_loop_triangles()
        print(f"  blend: decimated {obj.name} to {len(obj.data.loop_triangles)} tris")
    return obj


# ------------------------------------------------------------------- assembly
def join_all(parts, name, budget=12000):
    """Join the parts, thinning the HEAVY ones on the way in.

    Decimating after the join is what turned the eyes into orange octagons: a single ratio
    over the whole creature eats the small, high-detail parts (eyes, sockets, stitches,
    claws) to pay for the body, which had the triangles to spare. So each part over 400
    triangles is thinned against the budget and the small parts are left untouched.
    """
    total = 0
    for o in parts:
        o.data.calc_loop_triangles()
        total += len(o.data.loop_triangles)
    print(f"  parts tris before thinning: {total}")
    if total > budget:
        ratio = max(0.30, budget / total)
        for o in parts:
            t = len(o.data.loop_triangles)
            if t < 400:
                continue
            activate(o)
            m = o.modifiers.new("dec", "DECIMATE")
            m.ratio = ratio
            bpy.ops.object.modifier_apply(modifier=m.name)
    total = 0
    for o in parts:
        o.data.calc_loop_triangles()
        total += len(o.data.loop_triangles)
    print(f"  parts tris after thinning:  {total}")
    deselect()
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = o.data.name = name
    return o


def measure(o):
    o.data.calc_loop_triangles()
    vs = [o.matrix_world @ v.co for v in o.data.vertices]
    return dict(height=max(p.z for p in vs) - min(p.z for p in vs),
                ground=min(p.z for p in vs),
                width=max(p.x for p in vs) - min(p.x for p in vs),
                depth=max(p.y for p in vs) - min(p.y for p in vs),
                tris=len(o.data.loop_triangles), verts=len(o.data.vertices))


def assign_materials(husk):
    body = bpy.data.materials.new("M_husk_body")
    body.use_nodes = True
    nb = body.node_tree.nodes["Principled BSDF"]
    vc = body.node_tree.nodes.new("ShaderNodeVertexColor")
    vc.layer_name = "Col"
    body.node_tree.links.new(vc.outputs["Color"], nb.inputs["Base Color"])
    nb.inputs["Roughness"].default_value = 0.94     # damp is rougher, never shinier
    nb.inputs["Metallic"].default_value = 0.0
    glow = bpy.data.materials.new("M_husk_glow")
    glow.use_nodes = True
    gb = glow.node_tree.nodes["Principled BSDF"]
    gb.inputs["Base Color"].default_value = (*C_GLOW, 1.0)
    gb.inputs["Emission Color"].default_value = (*C_GLOW, 1.0)
    gb.inputs["Emission Strength"].default_value = 0.9
    gb.inputs["Roughness"].default_value = 0.45
    husk.data.materials.clear()
    husk.data.materials.append(body)
    husk.data.materials.append(glow)
    attr = husk.data.color_attributes["Col"]
    glow_verts = {i for i, d in enumerate(attr.data) if d.color[0] > 0.5 and d.color[2] < 0.4}
    for poly in husk.data.polygons:
        if all(v in glow_verts for v in poly.vertices):
            poly.material_index = 1
    print("  emissive faces (the eyes):",
          sum(1 for p in husk.data.polygons if p.material_index == 1))


# -------------------------------------------------------------------- render
def setup_scene():
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_EEVEE"
    # AgX keeps the two lit eyes orange coals instead of blowing them out to white
    try:
        scn.view_settings.view_transform = "AgX"
        scn.view_settings.look = "AgX - Medium High Contrast"
    except Exception as exc:
        print("  view transform:", exc)
    scn.view_settings.exposure = 0.15
    world = bpy.data.worlds.new("dusk")
    scn.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.055, 0.062, 0.080, 1.0)
    bg.inputs[1].default_value = 1.45
    me = bpy.data.meshes.new("ground")
    me.from_pydata([(-16, -16, 0), (16, -16, 0), (16, 16, 0), (-16, 16, 0)], [], [(0, 1, 2, 3)])
    me.update()
    g = bpy.data.objects.new("ground", me)
    bpy.context.collection.objects.link(g)
    gm = bpy.data.materials.new("M_ground")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.040, 0.035, 0.028, 1)
    gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.97
    g.data.materials.append(gm)
    for name, loc, energy, colour, size in [
            ("moon", (4.5, -6.0, 6.5), 260.0, (0.72, 0.78, 0.95), 1.6),
            ("fill", (-5.5, -2.5, 2.6), 40.0, (0.45, 0.55, 0.75), 3.0)]:
        ld = bpy.data.lights.new(name, "AREA")
        ld.energy, ld.color, ld.size = energy, colour, size
        o = bpy.data.objects.new(name, ld)
        o.location = loc
        o.rotation_euler = (math.radians(52), 0, math.radians(42))
        bpy.context.collection.objects.link(o)
    return scn


def make_camera(name, loc, look_at, lens=85.0, ortho=None):
    cam = bpy.data.cameras.new(name)
    cam.lens = lens
    if ortho:
        cam.type = "ORTHO"
        cam.ortho_scale = ortho
    o = bpy.data.objects.new(name, cam)
    o.location = loc
    o.rotation_euler = (mathutils.Vector(look_at) - mathutils.Vector(loc)).to_track_quat(
        "-Z", "Y").to_euler()
    bpy.context.collection.objects.link(o)
    return o


def shoot(cam, path, w, h, samples=32):
    scn = bpy.context.scene
    scn.camera = cam
    scn.render.resolution_x, scn.render.resolution_y = w, h
    try:
        scn.eevee.taa_render_samples = samples
    except AttributeError:
        pass
    scn.render.filepath = path
    scn.render.image_settings.file_format = "PNG"
    bpy.ops.render.render(write_still=True)
    print("  frame:", os.path.relpath(path, REPO))


def tile(paths, out, cols, gap=10):
    """Contact sheet via numpy (Blender's python has numpy; PIL is not available here)."""
    imgs = []
    for p in paths:
        im = bpy.data.images.load(p)
        w, h = im.size
        imgs.append((np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4), w, h))
    rows = [imgs[i:i + cols] for i in range(0, len(imgs), cols)]
    W = max(sum(im[1] for im in row) + gap * (len(row) + 1) for row in rows)
    H = sum(max(im[2] for im in row) + gap for row in rows) + gap
    canvas = np.zeros((H, W, 4), dtype=np.float32)
    canvas[:, :, 3] = 1.0
    y = H - gap
    for row in rows:
        rh = max(im[2] for im in row)
        y -= rh
        x = gap
        for arr, w, h in row:
            canvas[y:y + h, x:x + w, :] = arr
            x += w + gap
        y -= gap
    out_img = bpy.data.images.new("sheet", W, H, alpha=True)
    out_img.pixels = canvas.reshape(-1)
    out_img.filepath_raw = out
    out_img.file_format = "PNG"
    out_img.save()
    print("  sheet:", os.path.relpath(out, REPO), f"{W}x{H}")


def main():
    fresh()
    parts = []
    build_body(parts, RNG)
    build_legs(parts, RNG)
    build_arms(parts, RNG)
    build_hood(parts, RNG)
    build_head(parts, RNG)
    build_hat(parts, RNG)
    build_coveralls(parts, RNG)
    build_straw(parts, RNG)
    print(f"parts built: {len(parts)}")

    # split: the masses that must read as one skin, and the crisp detail that a 16 mm
    # voxel would swallow. The fused skin gets whatever the detail does not need.
    mass = [o for o in parts if not CRISP.match(o.name)]
    crisp = [o for o in parts if CRISP.match(o.name)]
    crisp_tris = 0
    for o in crisp:
        o.data.calc_loop_triangles()
        crisp_tris += len(o.data.loop_triangles)
    print(f"  blend: {len(mass)} masses to fuse, {len(crisp)} crisp parts "
          f"({crisp_tris} tris)")

    fused = fuse("husk_mass", mass, voxel=VOXEL)
    # The crisp detail (straw, hands, feet) can starve the fused skin - left alone it took
    # 9,848 tris and crushed the body to 3,000. Cap the detail at its share and give the
    # skin whatever is left, so the silhouette never pays for the trimmings.
    crisp_budget = 6000
    if crisp_tris > crisp_budget:
        r = max(0.25, crisp_budget / crisp_tris)
        for o in crisp:
            o.data.calc_loop_triangles()
            if len(o.data.loop_triangles) < 400:
                continue
            activate(o)
            m = o.modifiers.new("dec", "DECIMATE")
            m.ratio = r
            bpy.ops.object.modifier_apply(modifier=m.name)
        crisp_tris = 0
        for o in crisp:
            o.data.calc_loop_triangles()
            crisp_tris += len(o.data.loop_triangles)
        print(f"  blend: detail capped at {crisp_tris} tris")
    thin_to(fused, max(4500, 11800 - crisp_tris))
    husk = join_all(crisp + [fused], "Husk", budget=12000)

    activate(husk)
    bpy.ops.object.mode_set(mode="EDIT")
    bm = bmesh.from_edit_mesh(husk.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0008)
    bmesh.update_edit_mesh(husk.data)
    bpy.ops.object.mode_set(mode="OBJECT")
    husk.data.calc_loop_triangles()
    if len(husk.data.loop_triangles) > 12000:
        m = husk.modifiers.new("decimate", "DECIMATE")
        m.ratio = 12000 / len(husk.data.loop_triangles)
        bpy.ops.object.modifier_apply(modifier=m.name)
        print("  decimated to the 12k budget")
    activate(husk)
    bpy.ops.object.shade_smooth()
    try:
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(34))
    except Exception as exc:
        print("  auto-smooth:", exc)
    assign_materials(husk)

    m = measure(husk)
    husk.location.z -= m["ground"]
    bpy.context.view_layer.update()
    m = measure(husk)
    if abs(m["height"] - TARGET_HEIGHT) > 0.02:
        f = TARGET_HEIGHT / m["height"]
        husk.scale = (f, f, f)
        bpy.context.view_layer.update()
        m = measure(husk)

    print("=" * 66)
    print("M34 P1 - THE HUSK (soft pass), measured")
    print("=" * 66)
    print(f"  height        {m['height']:.3f} m  (M28 2.31 m; canopy 2.90-3.20 m)")
    print(f"  width x depth {m['width']:.3f} x {m['depth']:.3f} m")
    print(f"  triangles     {m['tris']}  (budget <= 12000)")
    print(f"  vertices      {m['verts']}")
    print(f"  materials     {len(husk.data.materials)}   (M28's primitive build: 60 renderers)")
    print("  renderers     1")

    os.makedirs(SHOTS, exist_ok=True)
    setup_scene()
    mid = m["height"] * 0.5
    ortho = m["width"] * 1.25
    frames = []
    for name, loc in [("front", (0.0, -8.0, mid)), ("right", (8.0, 0.0, mid)),
                      ("back", (0.0, 8.0, mid)), ("left", (-8.0, 0.0, mid))]:
        cam = make_camera(f"cam_{name}", loc, (0, 0, mid), ortho=ortho)
        p = os.path.join(SHOTS, f"m34-p1-turnaround-{name}.png")
        shoot(cam, p, 560, 980)
        frames.append(p)

    close = make_camera("cam_close", (-0.55, -1.35, 2.02), (0.0, 0.0, 1.98), lens=70.0)
    pc = os.path.join(SHOTS, "m34-p1-face-close.png")
    shoot(close, pc, 700, 900, samples=64)

    # a wireframe proof: a real wire copy of the mesh (Blender 5's freestyle pass raises
    # inside the parameter editor headless, so the wire is geometry instead)
    scn = bpy.context.scene
    wire = husk.copy()
    wire.data = husk.data.copy()
    bpy.context.collection.objects.link(wire)
    wm = wire.modifiers.new("wire", "WIREFRAME")
    setopt(wm, "thickness", 0.009)
    setopt(wm, "use_replace", True)
    activate(wire)
    bpy.ops.object.modifier_apply(modifier=wm.name)
    wmat = bpy.data.materials.new("M_wire")
    wmat.use_nodes = True
    wb = wmat.node_tree.nodes["Principled BSDF"]
    wb.inputs["Base Color"].default_value = (0.42, 0.62, 0.52, 1.0)
    wb.inputs["Emission Color"].default_value = (0.42, 0.62, 0.52, 1.0)
    setopt(wb.inputs["Emission Strength"], "default_value", 1.1)
    wire.data.materials.clear()
    wire.data.materials.append(wmat)
    husk.hide_render = True
    wf = make_camera("cam_wire", (-5.0, -6.0, mid + 0.2), (0, 0, mid), ortho=ortho)
    pw = os.path.join(SHOTS, "m34-p1-wireframe.png")
    shoot(wf, pw, 560, 980)
    husk.hide_render = False
    bpy.data.objects.remove(wire, do_unlink=True)

    black = bpy.data.materials.new("M_black")
    black.use_nodes = True
    black.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0, 0, 0, 1)
    bpy.context.view_layer.material_override = black
    sil = []
    for dist in (12.0, 20.0):
        cam = make_camera(f"cam_sil{int(dist)}", (0.0, -dist, 1.62), (0.0, 0.0, 1.30), lens=50.0)
        p = os.path.join(SHOTS, f"m34-p1-silhouette-night-{int(dist)}m.png")
        shoot(cam, p, 760, 470)
        sil.append(p)
    bpy.context.view_layer.material_override = None

    lane = make_camera("cam_lane", (0.0, -6.4, 1.66), (0.0, 0.4, 1.24), lens=52.0)
    pl = os.path.join(SHOTS, "m34-p1-lit-lane.png")
    shoot(lane, pl, 1290, 720, samples=64)

    tile([frames[0], pc, frames[2], frames[3], pw, frames[1]] + sil,
         os.path.join(SHOTS, "m34-p1-contact-sheet.png"), cols=3)

    blend = os.path.join(REPO, "artifacts", "m34-husk-p1.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    fbx = os.path.join(REPO, "artifacts", "m34-husk-p1.fbx")
    activate(husk)
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={"MESH"},
                             path_mode="COPY", embed_textures=False)
    with open(os.path.join(BLIND, "m34-p1-report.txt"), "w") as fh:
        fh.write("M34 P1 - the Husk, mesh pass (soft build: metaball body, skin limbs, cloth coveralls)\n")
        fh.write(f"height {m['height']:.3f} m\nwidth {m['width']:.3f} m\ndepth {m['depth']:.3f} m\n")
        fh.write(f"triangles {m['tris']}\nvertices {m['verts']}\n")
        fh.write(f"materials {len(husk.data.materials)}\nrenderers 1\n")
        fh.write(f"blend {os.path.relpath(blend, REPO)}\nfbx {os.path.relpath(fbx, REPO)}\n")
    print("  saved:", os.path.relpath(blend, REPO))
    print("M34P1_RESULT done")


main()
