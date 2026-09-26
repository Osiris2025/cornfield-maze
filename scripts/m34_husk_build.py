# DESIGN-TIME ONLY - not compiled into the Unity build, never ships on device.
# M34 P1 - the Husk: a legged, rotten, shambling scarecrow.
# Run: /Applications/Blender.app/Contents/MacOS/Blender -b -noaudio --python scripts/m34_husk_build.py
#
# Todd, 2026-09-26: the M28 primitive version "looks like crap ... use blender or
# something to make a real scarecrow", and then supplied three views of a reference
# model (Sketchfab "terrifying scarecrow horror monster"): a wide-brim slouch hat, a
# gaunt carved sack/wood face with two hot glowing eyes, a hood/scarf over the
# shoulders, blue-grey coveralls with a rope belt and a hanging sash, straw bursting
# from the cuffs and hems, and bare three-toed feet.
#
# This recreates that reference's STRUCTURE, PALETTE and PROPORTIONS procedurally.
# Nothing is downloaded or imported - house style (§2 of the order: "procedurally
# generated, not sourced", so no licence question; a paraphrase of the reference is
# not a licence to ship someone's mesh).
#
# Gate (ORDERS-20260925-m34.md §7 P1): 4-view turnaround + wireframe, plus the measured
# height, triangle count and material count - and it must read as a legged, rotten
# scarecrow from all four views. A fused blob or a smooth mannequin fails here.
import math
import os
import random

import bmesh
import bpy
import mathutils
import numpy as np

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHOTS = os.path.join(REPO, "artifacts", "review", "world")
BLIND = os.path.join(REPO, "artifacts")
SEED = 34034
RNG = random.Random(SEED)
TARGET_HEIGHT = 2.30          # M28 was 2.31; the canopy is 2.90-3.20 m (§5)

# ------------------------------------------------------------- the palette
# Read off Todd's reference: blue-grey coveralls, tallow-yellow sack face, olive-grey
# felt hat, pale damp straw, dark wet timber, and the two hot eyes.
C_DENIM = (0.085, 0.115, 0.150)
C_DENIM_DARK = (0.055, 0.075, 0.100)
C_HOOD = (0.300, 0.280, 0.215)
C_SACK = (0.330, 0.230, 0.105)
C_SACK_DARK = (0.200, 0.135, 0.060)
C_HAT = (0.115, 0.125, 0.110)
C_STRAW = (0.270, 0.225, 0.125)
C_WOOD = (0.075, 0.060, 0.045)
C_DARK = (0.018, 0.016, 0.014)
C_GLOW = (1.000, 0.420, 0.120)


# ------------------------------------------------------------------ basic kit
def fresh():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    return bpy.context.scene


def deselect():
    for o in bpy.data.objects:
        o.select_set(False)


def set_colour(obj, rgba):
    """Every part carries its colour in a point attribute, so the finished creature is
    ONE mesh with ONE opaque material (§5.1: 1-2 materials, not 60)."""
    me = obj.data
    attr = me.color_attributes.get("Col") or me.color_attributes.new(
        name="Col", type="FLOAT_COLOR", domain="POINT")
    for d in attr.data:
        d.color = (rgba[0], rgba[1], rgba[2], 1.0)


def new_mesh(name, verts, faces, colour=C_DENIM):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.validate(verbose=False)
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    set_colour(ob, colour)
    return ob


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
        pts.append(c + t1 * (math.cos(a) * r * squash[0]) + t2 * (math.sin(a) * r * squash[1]))
    return pts


def skin_rows(rows, segs, close_top=True):
    """Faces between consecutive rings of the same length. Rows are lists of points."""
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
        base = (len(path) - 1) * segs
        faces.append(tuple(base + s for s in range(segs)))
    return new_mesh(name, verts, faces, colour)


def jitter_verts(obj, amount, rng, mask=(1, 1, 1)):
    for v in obj.data.vertices:
        v.co.x += rng.uniform(-amount, amount) * mask[0]
        v.co.y += rng.uniform(-amount, amount) * mask[1]
        v.co.z += rng.uniform(-amount, amount) * mask[2]


def activate(obj):
    deselect()
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def subdiv(obj, levels=1):
    activate(obj)
    m = obj.modifiers.new("sub", "SUBSURF")
    m.levels = levels
    bpy.ops.object.modifier_apply(modifier=m.name)
    return obj


def solidify(obj, thickness):
    activate(obj)
    m = obj.modifiers.new("solid", "SOLIDIFY")
    m.thickness = thickness
    bpy.ops.object.modifier_apply(modifier=m.name)
    return obj


def box(name, centre, half, colour, rng=None, wobble=0.0):
    cx, cy, cz = centre
    hx, hy, hz = half
    v = []
    for (sx, sy, sz) in [(-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1),
                         (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)]:
        p = [cx + sx * hx, cy + sy * hy, cz + sz * hz]
        if wobble and rng:
            p = [c + rng.uniform(-wobble, wobble) for c in p]
        v.append(tuple(p))
    f = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    return new_mesh(name, v, f, colour)


# ------------------------------------------------------------------ the parts
def build_cross(parts, rng):
    """The cross is the FRAME, not a prop beside it (§5): weathered timber runs up
    through the body as spine and hips, the bar is the shoulders, and it shows where
    the cloth has torn - the scarecrow tell M28 established. It is wet and dark."""
    spine = tube("timber_spine", [(-0.01, 0.0, 0.02), (0.0, 0.01, 0.42), (0.01, 0.0, 0.80),
                                  (0.0, -0.01, 1.30), (0.0, 0.0, 1.72)],
                 [0.070, 0.066, 0.060, 0.052, 0.046], segs=9, wobble=0.06, rng=rng,
                 colour=C_WOOD)
    jitter_verts(spine, 0.005, rng)
    parts.append(spine)
    bar = tube("timber_crossbar", [(-0.86, 0.05, 1.63), (-0.40, 0.02, 1.675), (0.02, 0.0, 1.70),
                                   (0.44, -0.01, 1.725), (0.80, -0.03, 1.76)],
               [0.038, 0.043, 0.046, 0.043, 0.036], segs=8, wobble=0.06, rng=rng,
               colour=C_WOOD)
    jitter_verts(bar, 0.004, rng)
    parts.append(bar)
    # the split end of the post, out of the top of the sack
    for i, (a, b, r0) in enumerate([((0.02, 0.01, 1.70), (0.06, 0.03, 1.86), 0.028),
                                    ((-0.01, -0.02, 1.70), (-0.05, -0.04, 1.80), 0.022)]):
        parts.append(tube(f"post_splinter{i}", [a, b], [r0, 0.004], segs=6, wobble=0.16,
                          rng=rng, colour=C_WOOD))


def build_legs(parts, rng):
    """Baggy coverall legs over a barely-there wooden underframe. One leg is shorter
    and bound differently, so the gait is uneven by construction (§5). The feet are
    bare three-toed roots - not boots."""
    for side in ("L", "R"):
        s = -1.0 if side == "L" else 1.0
        short = side == "R"
        hip = (s * 0.155, 0.0, 1.00 if not short else 0.955)
        knee = (s * 0.185, 0.055 if not short else -0.045, 0.55 if not short else 0.605)
        ankle = (s * 0.185, -0.010 if not short else -0.075, 0.135 if not short else 0.155)
        path = [hip,
                ((hip[0] + knee[0]) / 2 + s * 0.012, (hip[1] + knee[1]) / 2, (hip[2] + knee[2]) / 2),
                knee,
                ((knee[0] + ankle[0]) / 2, (knee[1] + ankle[1]) / 2 + 0.018, (knee[2] + ankle[2]) / 2),
                ankle]
        radii = [0.158 if not short else 0.170, 0.142, 0.122, 0.112, 0.120]
        leg = tube(f"leg_{side}", path, radii, segs=14, wobble=0.055, rng=rng, colour=C_DENIM)
        jitter_verts(leg, 0.006, rng)
        parts.append(leg)
        # the cloth breaks over the shin and the straw fringe shows under it
        parts.append(tube(f"leg_{side}_hem",
                          [(ankle[0], ankle[1], ankle[2] + 0.13),
                           (ankle[0], ankle[1], ankle[2] + 0.01)],
                          [0.130, 0.120], segs=14, wobble=0.17, rng=rng, colour=C_DENIM_DARK))
        # bare foot: a wooden sole with three split toes, flat on the ground
        fx, fy = s * 0.185, ankle[1]
        foot = box(f"foot_{side}", (fx, fy + 0.02, 0.045), (0.074, 0.095, 0.045), C_WOOD,
                   rng=rng, wobble=0.006)
        subdiv(foot, 1)
        parts.append(foot)
        for i, tx in enumerate((-0.045, 0.0, 0.045)):
            parts.append(tube(f"toe_{side}{i}",
                              [(fx + tx, fy - 0.06, 0.030),
                               (fx + tx, fy - 0.185 - abs(tx) * 0.35, 0.014)],
                              [0.024, 0.013], segs=6, wobble=0.10, rng=rng, colour=C_WOOD))


def build_torso(parts, rng):
    """Blue-grey coveralls: a gaunt chest, a bib with two pockets, straps over the
    shoulders, and a waist the rope belt bites into."""
    profile = [(0.90, 0.215, 0.175), (1.02, 0.185, 0.150), (1.16, 0.180, 0.145),
               (1.32, 0.205, 0.160), (1.50, 0.238, 0.175), (1.64, 0.258, 0.180),
               (1.74, 0.228, 0.165), (1.80, 0.150, 0.115)]
    SEGS = 20
    rows = [ring((0.0, 0.0, z), 1.0, SEGS, wobble=0.06, rng=rng) for (z, _, _) in profile]
    rows = [[(p.x * rx, p.y * ry, p.z) for p in row]
            for row, (_, rx, ry) in zip(rows, profile)]
    torso = new_mesh("torso", *skin_rows(rows, SEGS)[0:2], C_DENIM)
    jitter_verts(torso, 0.007, rng)
    subdiv(torso, 1)
    parts.append(torso)
    # bib pockets: two patches proud of the chest, the reference's own detail
    for i, (x, z) in enumerate(((-0.085, 1.42), (0.085, 1.42))):
        parts.append(solidify(box(f"pocket{i}", (x, -0.178, z), (0.062, 0.006, 0.062),
                                  C_DENIM_DARK), 0.010))
    # shoulder straps
    for s in (-1, 1):
        parts.append(tube(f"strap_{s}", [(s * 0.115, -0.150, 1.34), (s * 0.135, 0.020, 1.72),
                                         (s * 0.110, 0.145, 1.40)],
                          [0.038, 0.042, 0.036], segs=8, wobble=0.05, rng=rng,
                          colour=C_DENIM_DARK))
    # rope belt, tied off-centre, with a hanging knot and the pale sash on the hip
    belt = tube("belt", [(0, 0, 1.055), (0, 0, 1.115)], [0.216, 0.216], segs=20,
                cap_start=False, cap_end=False, wobble=0.05, rng=rng,
                squash=[(1.0, 1.0)] * 2, colour=C_STRAW)
    belt.scale = (1.0, 0.80, 1.0)
    parts.append(belt)
    parts.append(tube("sash", [(-0.06, -0.155, 1.10), (-0.10, -0.20, 1.02),
                               (-0.075, -0.175, 0.86), (-0.05, -0.16, 0.72)],
                      [0.030, 0.034, 0.026, 0.018], segs=7, wobble=0.08, rng=rng,
                      colour=C_HOOD))
    for i, d in enumerate(((-0.14, -0.165, 0.95), (-0.02, -0.175, 0.93))):
        parts.append(tube(f"belt_tail{i}", [(-0.09, -0.168, 1.06), d], [0.016, 0.008],
                          segs=5, colour=C_STRAW))


def build_coat_hem(parts, rng):
    """Tatters live at the hems, not over the legs - the legs have to be visible (§1)."""
    SEGS = 18
    rings = [(1.06, 0.235, 0.185), (0.98, 0.262, 0.202), (0.88, 0.288, 0.218)]
    tear = [rng.uniform(-0.085, 0.02) for _ in range(SEGS)]
    rows = []
    for ri, (z, rx, ry) in enumerate(rings):
        pts = ring((0.0, 0.0, z), 1.0, SEGS, wobble=0.09, rng=rng)
        rows.append([(p.x * rx, p.y * ry, p.z + (tear[si] if ri == len(rings) - 1 else 0.0))
                     for si, p in enumerate(pts)])
    hem = new_mesh("coat_hem", *skin_rows(rows, SEGS, close_top=False)[0:2], C_DENIM_DARK)
    jitter_verts(hem, 0.008, rng)
    solidify(hem, 0.020)
    parts.append(hem)


def build_hood(parts, rng):
    """The beige hood/scarf over the shoulders - the reference's loudest pale shape and
    the thing that stops the head reading as a ball on a stick."""
    SEGS = 18
    rings = [(1.86, 0.118), (1.80, 0.168), (1.72, 0.232), (1.64, 0.268), (1.55, 0.252)]
    rows = []
    for ri, (z, r) in enumerate(rings):
        pts = ring((0.0, 0.0, z), r, SEGS, wobble=0.07, rng=rng)
        rows.append([(p.x * 1.02, p.y * (1.10 if ri >= 2 else 1.0),
                      p.z + (rng.uniform(-0.02, 0.02) if ri > 2 else 0.0)) for p in pts])
    hood = new_mesh("hood", *skin_rows(rows, SEGS, close_top=False)[0:2], C_HOOD)
    jitter_verts(hood, 0.009, rng)
    subdiv(hood, 1)
    solidify(hood, 0.022)
    parts.append(hood)
    parts.append(tube("collar", [(0.0, 0.155, 1.78), (0.0, 0.215, 1.88)], [0.175, 0.130],
                      segs=12, cap_start=False, cap_end=False, wobble=0.12, rng=rng,
                      colour=C_HOOD))


def build_arms(parts, rng):
    """Long denim sleeves, cuffs torn open and bursting with straw, gnarly wooden hands
    that end in claws rather than fingers."""
    for side in ("L", "R"):
        s = -1.0 if side == "L" else 1.0
        shoulder = (s * 0.235, 0.0, 1.70)
        elbow = (s * 0.500, 0.055, 1.575)
        wrist = (s * 0.720, 0.020, 1.470)
        path = [shoulder, ((shoulder[0] + elbow[0]) / 2, 0.035, (shoulder[2] + elbow[2]) / 2),
                elbow, ((elbow[0] + wrist[0]) / 2, 0.035, (elbow[2] + wrist[2]) / 2), wrist]
        sleeve = tube(f"sleeve_{side}", path, [0.152, 0.130, 0.112, 0.098, 0.105],
                      segs=12, wobble=0.06, rng=rng, colour=C_DENIM)
        jitter_verts(sleeve, 0.006, rng)
        parts.append(sleeve)
        parts.append(tube(f"cuff_{side}",
                          [(wrist[0], wrist[1], wrist[2] + 0.03),
                           (wrist[0], wrist[1], wrist[2] - 0.05)],
                          [0.108, 0.118], segs=12, wobble=0.18, rng=rng, colour=C_DENIM_DARK))
        palm_c = (wrist[0] + s * 0.075, wrist[1], wrist[2] - 0.045)
        parts.append(tube(f"palm_{side}",
                          [(wrist[0] + s * 0.02, wrist[1], wrist[2] - 0.02), palm_c,
                           (palm_c[0] + s * 0.05, palm_c[1], palm_c[2] - 0.03)],
                          [0.075, 0.082, 0.055], segs=8, wobble=0.10, rng=rng, colour=C_WOOD))
        for i, (dz, dy, ln) in enumerate(((0.02, -0.05, 0.13), (0.0, 0.0, 0.15),
                                          (-0.02, 0.055, 0.12))):
            base = mathutils.Vector(palm_c) + mathutils.Vector((s * 0.03, dy, dz))
            mid = base + mathutils.Vector((s * ln * 0.6, dy * 0.4, -ln * 0.25))
            tip = base + mathutils.Vector((s * ln * 1.15, dy * 0.9, -ln * 0.75))
            parts.append(tube(f"claw_{side}{i}", [tuple(base), tuple(mid), tuple(tip)],
                              [0.026, 0.020, 0.007], segs=5, colour=C_WOOD))


def build_head(parts, rng):
    """The reference's head: a tallow-yellow sack pulled over a skull, gaunt and longer
    than it is wide, with a carved brow, hollow sockets holding two hot eyes, a
    sewn-shut grin, and a jaw that comes to a point. Cocked - M28's identity, kept."""
    RINGS, SEGS = 17, 20
    rows = []
    for i in range(RINGS):
        t = i / (RINGS - 1)                  # 0 at the crown, 1 at the chin
        if t < 0.18:                         # skull dome
            r = 0.145 + 0.075 * (t / 0.18)
        elif t < 0.55:                       # brow and cheeks
            r = 0.220 - 0.030 * ((t - 0.18) / 0.37)
        else:                                # the jaw pinches to a point
            r = 0.190 - 0.105 * ((t - 0.55) / 0.45) ** 1.25
        z = 0.30 - 0.60 * t
        row = []
        for s in range(SEGS):
            a = 2 * math.pi * s / SEGS
            x, y = math.cos(a) * r * 0.90, math.sin(a) * r * 1.02
            if y < 0:                        # the face is FLAT: a mask, not a ball
                y *= 0.86
                if t > 0.55:
                    z -= 0.010 * abs(y) / 0.19
            n = 1.0 + rng.uniform(-0.035, 0.035)
            row.append((x * n, y * n, z))
        rows.append(row)
    head = new_mesh("head", *skin_rows(rows, SEGS, close_top=True)[0:2], C_SACK)
    subdiv(head, 1)
    jitter_verts(head, 0.004, rng)
    head.location = (0.015, -0.005, 1.985)
    head.rotation_euler = (math.radians(-7), math.radians(5), math.radians(12))
    parts.append(head)

    parts.append(tube("brow", [(-0.165, -0.150, 2.130), (-0.055, -0.196, 2.165),
                               (0.060, -0.200, 2.163), (0.170, -0.150, 2.125)],
                      [0.040, 0.052, 0.052, 0.038], segs=8, wobble=0.06, rng=rng,
                      colour=C_SACK_DARK))
    for s, dz in ((-1, 0.0), (1, 0.018)):    # cheekbones, one higher than the other
        parts.append(tube(f"cheek{s}", [(s * 0.115, -0.175, 2.055 + dz),
                                        (s * 0.150, -0.090, 2.020 + dz)],
                          [0.052, 0.038], segs=7, wobble=0.08, rng=rng, colour=C_SACK_DARK))
    parts.append(tube("nose", [(0.0, -0.185, 2.075), (0.0, -0.245, 2.020)], [0.042, 0.016],
                      segs=7, colour=C_SACK))
    # the two hot eyes: an emissive ball set INTO a dark socket, not a painted dot
    for s in (-1, 1):
        c = mathutils.Vector((s * 0.088, -0.160, 2.115))
        parts.append(tube(f"socket{s}",
                          [tuple(c + mathutils.Vector((0, 0.045, 0))), tuple(c),
                           tuple(c - mathutils.Vector((0, 0.028, 0.006)))],
                          [0.062, 0.070, 0.044], segs=12, cap_start=False, wobble=0.05,
                          rng=rng, colour=C_DARK))
        parts.append(tube(f"eye{s}",
                          [tuple(c - mathutils.Vector((0, 0.010, 0.002))),
                           tuple(c - mathutils.Vector((0, 0.052, 0.010)))],
                          [0.040, 0.030], segs=10, colour=C_GLOW))
    # the sewn-shut grin: a wide arc of coarse cross-stitches
    for i in range(11):
        t = i / 10.0
        x = -0.150 + 0.300 * t
        depth = -0.150 + 0.040 * (abs(t - 0.5) * 2.0) ** 1.6
        z = 1.940 - 0.055 * math.sin(t * math.pi) + 0.030 * (abs(t - 0.5) * 2) ** 2
        parts.append(tube(f"stitch{i}", [(x - 0.012, depth, z + 0.024),
                                         (x + 0.012, depth, z - 0.024)],
                          [0.0105, 0.0105], segs=5, colour=C_DARK))
    parts.append(tube("neck_cord", [(0.0, 0.0, 1.795), (0.0, 0.0, 1.850)], [0.105, 0.115],
                      segs=12, cap_start=False, cap_end=False, wobble=0.14, rng=rng,
                      colour=C_STRAW))


def build_hat(parts, rng):
    """The reference's slouch hat: a wide drooping brim, a tall dented crown, a dark
    band, and straw sprigs poking out of it."""
    SEGS = 22
    rows = []
    for ri, (r, z) in enumerate(((0.145, 2.185), (0.235, 2.170), (0.320, 2.135),
                                 (0.375, 2.085))):
        pts = ring((0.0, 0.0, z), r, SEGS, wobble=0.05, rng=rng)
        row = []
        for p in pts:
            drop = 0.0
            if ri >= 2:                      # the brim droops, and cocks to one side
                drop = -0.038 * (p.x / 0.375) - 0.020 * (ri - 1) * max(0.0, p.y / 0.375)
            row.append((p.x, p.y, p.z + drop))
        rows.append(row)
    brim = new_mesh("hat_brim", *skin_rows(rows, SEGS, close_top=False)[0:2], C_HAT)
    jitter_verts(brim, 0.004, rng)
    solidify(brim, 0.014)
    parts.append(brim)

    crown_rings = [(2.170, 0.150), (2.235, 0.140), (2.290, 0.118), (2.330, 0.080),
                   (2.350, 0.030)]
    rows = []
    for ri, (z, r) in enumerate(crown_rings):
        pts = ring((0.008 * ri, 0.004 * ri, z), r, SEGS, wobble=0.04, rng=rng)
        rows.append([(p.x, p.y, p.z + (-0.018 if 0 < ri < 4 and -0.5 < math.atan2(p.y, p.x) < 1.4
                                       else 0.0)) for p in pts])
    crown = new_mesh("hat_crown", *skin_rows(rows, SEGS, close_top=True)[0:2], C_HAT)
    jitter_verts(crown, 0.004, rng)
    parts.append(crown)
    parts.append(tube("hat_band", [(0.0, 0.0, 2.168), (0.0, 0.0, 2.205)], [0.152, 0.150],
                      segs=SEGS, cap_start=False, cap_end=False, wobble=0.02, rng=rng,
                      colour=C_DARK))
    for i in range(4):
        a = 0.4 + i * 0.5
        base = (math.cos(a) * 0.14, math.sin(a) * 0.14, 2.215)
        tip = (math.cos(a) * 0.30, math.sin(a) * 0.26, 2.315 + rng.uniform(-0.03, 0.05))
        parts.append(tube(f"hat_straw{i}", [base, ((base[0] + tip[0]) / 2,
                                                   (base[1] + tip[1]) / 2,
                                                   (base[2] + tip[2]) / 2 + 0.01), tip],
                          [0.007, 0.005, 0.002], segs=4, colour=C_STRAW))
    parts.append(tube("hat_wire", [(0.10, 0.09, 2.24), (0.20, 0.13, 2.30), (0.24, 0.10, 2.19)],
                      [0.006, 0.006, 0.004], segs=4, colour=C_WOOD))


def build_straw(parts, rng):
    """Damp straw, grey-brown, clumped and drooping: it hangs out of the cuffs, the hem
    and the shin hems. It does not stick out like fresh decoration (§4)."""
    def tuft(origin, direction, count, length, spread, radius):
        for _ in range(count):
            d = mathutils.Vector(direction).normalized()
            d.x += rng.uniform(-spread, spread)
            d.y += rng.uniform(-spread, spread)
            d.z += rng.uniform(-spread, 0.10)
            d.normalize()
            ln = length * rng.uniform(0.55, 1.20)
            o = mathutils.Vector(origin) + mathutils.Vector(
                (rng.uniform(-0.02, 0.02), rng.uniform(-0.02, 0.02), rng.uniform(-0.02, 0.02)))
            mid = o + d * (ln * 0.5) + mathutils.Vector((0, 0, -ln * 0.16))
            end = o + d * ln + mathutils.Vector((0, 0, -ln * 0.42))
            parts.append(tube(f"straw{len(parts)}", [tuple(o), tuple(mid), tuple(end)],
                              [radius, radius * 0.7, radius * 0.22], segs=4, colour=C_STRAW))

    for s in (-1, 1):                                     # the cuffs - the loudest straw
        tuft((s * 0.735, 0.02, 1.44), (s * 0.55, 0.05, -0.80), 12, 0.28, 0.40, 0.010)
    tuft((-0.30, 0.02, 0.92), (-0.45, 0.0, -0.85), 7, 0.20, 0.35, 0.009)
    tuft((0.30, -0.02, 0.90), (0.45, 0.0, -0.85), 6, 0.18, 0.35, 0.009)
    tuft((0.0, -0.20, 0.92), (0.0, -0.25, -0.90), 5, 0.16, 0.30, 0.009)
    for s in (-1, 1):                                     # under the jaw
        tuft((s * 0.09, -0.06, 1.80), (s * 0.2, -0.5, -0.8), 5, 0.16, 0.35, 0.009)
    for s in (-1, 1):                                     # at the shin hems
        tuft((s * 0.185, -0.02, 0.30), (s * 0.3, -0.3, -0.85), 6, 0.15, 0.30, 0.009)


# ------------------------------------------------------------------- assembly
def join_all(parts, name):
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
    """Two materials, not sixty (§5.1): one opaque body material whose albedo comes from
    the mesh's own colour attribute, and one emissive for the eyes."""
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
    gb.inputs["Emission Strength"].default_value = 9.0
    gb.inputs["Roughness"].default_value = 0.45
    husk.data.materials.clear()
    husk.data.materials.append(body)
    husk.data.materials.append(glow)
    attr = husk.data.color_attributes["Col"]
    glow_verts = {i for i, d in enumerate(attr.data) if d.color[0] > 0.5 and d.color[2] < 0.4}
    for poly in husk.data.polygons:
        if all(v in glow_verts for v in poly.vertices):
            poly.material_index = 1
    n = sum(1 for p in husk.data.polygons if p.material_index == 1)
    print(f"  emissive faces (the eyes): {n}")


# -------------------------------------------------------------------- render
def setup_scene():
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_EEVEE"
    world = bpy.data.worlds.new("dusk")
    scn.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.050, 0.055, 0.070, 1.0)
    bg.inputs[1].default_value = 0.60
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
        ld.energy = energy
        ld.color = colour
        ld.size = size
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
    scn.render.resolution_x = w
    scn.render.resolution_y = h
    try:
        scn.eevee.taa_render_samples = samples
    except AttributeError:
        pass
    scn.render.filepath = path
    scn.render.image_settings.file_format = "PNG"
    bpy.ops.render.render(write_still=True)
    print("  frame:", os.path.relpath(path, REPO))


def tile(paths, out, cols, gap=10):
    """Contact sheet via numpy (Blender's python has numpy; PIL is not available here
    and the house rule forbids adding packages). Panels may differ in size."""
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
    build_cross(parts, RNG)
    build_legs(parts, RNG)
    build_torso(parts, RNG)
    build_coat_hem(parts, RNG)
    build_arms(parts, RNG)
    build_hood(parts, RNG)
    build_head(parts, RNG)
    build_hat(parts, RNG)
    build_straw(parts, RNG)
    print(f"parts built: {len(parts)}")
    husk = join_all(parts, "Husk")

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
    print("M34 P1 - THE HUSK, measured")
    print("=" * 66)
    print(f"  height        {m['height']:.3f} m  (M28 2.31 m; canopy 2.90-3.20 m)")
    print(f"  width x depth {m['width']:.3f} x {m['depth']:.3f} m")
    print(f"  triangles     {m['tris']}  (budget <= 12000)")
    print(f"  vertices      {m['verts']}")
    print(f"  materials     {len(husk.data.materials)}   (M28's primitive build: 60 renderers)")
    print(f"  renderers     1")

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

    scn = bpy.context.scene
    scn.render.use_freestyle = True
    wf = make_camera("cam_wire", (-5.0, -6.0, mid + 0.2), (0, 0, mid), ortho=ortho)
    pw = os.path.join(SHOTS, "m34-p1-wireframe.png")
    shoot(wf, pw, 560, 980)
    scn.render.use_freestyle = False

    # §8.1 acceptance: flat black, unlit, at 12 m AND 20 m. It must read as a scarecrow
    # WITH LEGS - not a person, not a pole, not a blob, not a pile of boxes.
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

    lane = make_camera("cam_lane", (0.0, -1.6, 1.62), (0.0, 3.0, 1.28), lens=48.0)
    pl = os.path.join(SHOTS, "m34-p1-lit-lane.png")
    shoot(lane, pl, 1290, 720, samples=64)

    tile(frames + [pw] + sil, os.path.join(SHOTS, "m34-p1-contact-sheet.png"), cols=3)

    blend = os.path.join(REPO, "artifacts", "m34-husk-p1.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    fbx = os.path.join(REPO, "artifacts", "m34-husk-p1.fbx")
    activate(husk)
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={"MESH"},
                             path_mode="COPY", embed_textures=False)
    with open(os.path.join(BLIND, "m34-p1-report.txt"), "w") as fh:
        fh.write("M34 P1 - the Husk, mesh pass (reference-matched)\n")
        fh.write(f"height {m['height']:.3f} m\nwidth {m['width']:.3f} m\ndepth {m['depth']:.3f} m\n")
        fh.write(f"triangles {m['tris']}\nvertices {m['verts']}\n")
        fh.write(f"materials {len(husk.data.materials)}\nrenderers 1\n")
        fh.write(f"blend {os.path.relpath(blend, REPO)}\nfbx {os.path.relpath(fbx, REPO)}\n")
    print("  saved:", os.path.relpath(blend, REPO))
    print("M34P1_RESULT done")


main()
