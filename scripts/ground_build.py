#!/usr/bin/env python3
# DESIGN-TIME ONLY - not compiled into the Unity build, never shipped.
#
# ground_build.py -- derive the maze's ground textures by blending real photographs.
#
# Todd, 2026-09-25: "can you use a combination of textures ... to derive realistic ground textures
# and superimposed path?  Blend some togther"
# then: "There are of course, other textures out there in the world besides those" /
#       "and poly haven has many more also"
#
# WHY: the maze's ground was procedural noise -- Materials.Gravel / FieldGrass / PathGrass are 96-128 px
# noise tiles, which can only read as synthetic. This script derives two tiling ground materials from
# CC0 photographs and an edge mask, so the lane can be superimposed on the field with a ragged,
# believable boundary instead of a straight cut.
#
# TWO VARIANTS, same recipe, different photographs -- because the batch Todd supplied was 1K leaf
# litter, and Poly Haven's own library (862 texture sets, to 8K) has sets that read as a harvested
# October cornfield rather than a forest floor:
#
#   supplied : Todd's own drop (1K, Poly Haven ids). forest_floor + forest_leaves_04 + brown_mud_leaves_01
#              / gravel_ground_01 + stony_dirt_path + grass_path_2
#   dry      : Poly Haven at 2K. dry_mud_field_001 + withered_grass + dry_decay_leaves
#              / stony_dirt_path + rocky_gravel + gravel_road
#
# Only the variant named in SHIP writes into Assets/. Every variant writes a preview, so the choice is
# made by looking at a frame rather than by guessing.
#
# RUN: /Applications/Blender.app/Contents/MacOS/Blender -b -noaudio --python scripts/ground_build.py
#
# OUTPUT (all tileable):
#   Assets/Resources/Ground/T_Ground_{Field,Lane}.png  + _N.png (normal, Non-Color) + _R.png (rough)
#   Assets/Resources/Ground/T_Ground_LaneEdge.png      (mask: how ragged the lane/field boundary is)
#   artifacts/reference/ground-preview[-<variant>][-raw].png   top-down proof of the superimposed path
import os
import sys

import bpy
import numpy as np

REPO = "/Volumes/files1/projects/cornmaze/CornFieldMaze"
OUT_DIR = os.path.join(REPO, "Assets", "Resources", "Ground")
REF = os.path.join(REPO, "artifacts", "reference")

# Which variant's textures ship into Assets/. Flip this line, re-run, and the other one is live.
SHIP = os.environ.get("GROUND_SHIP", "supplied")

VARIANTS = {
    "supplied": {
        "src": "/Volumes/files1/projects/cornmaze/downloads/ground_textures",   # outside the repo
        "res": "1k", "size": 1024, "layout": "textures_subdir",
        "field": ["forest_floor", "forest_leaves_04", "brown_mud_leaves_01"],
        "lane": ["gravel_ground_01", "stony_dirt_path", "grass_path_2"],
    },
    "dry": {
        "src": "/Volumes/files1/projects/cornmaze/downloads/ground_textures_ph",  # outside the repo
        "res": "2k", "size": 2048, "layout": "flat",
        "field": ["withered_grass", "dry_mud_field_001", "dry_decay_leaves"],
        "lane": ["gravel_road", "rocky_gravel", "stony_dirt_path"],
    },
}

SRC = None      # set per variant
SIZE = None     # set per variant


# ----------------------------------------------------------------------------- image plumbing
def load(path, colorspace):
    img = bpy.data.images.load(path)
    img.colorspace_settings.name = colorspace
    w, h = img.size
    buf = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(buf)
    a = buf.reshape(h, w, 4)[::-1].copy()
    bpy.data.images.remove(img)
    return a


def find(name, kind, res, layout):
    """kind: diff | nor | rough. The two batches keep their files in different places, and the sets
    are internally inconsistent about case and container: the OpenGL normal map is ``nor_gl``, and
    roughness is a .jpg in some sets and an .exr in others. Try every spelling before failing."""
    spellings = ("nor_gl", "nor") if kind == "nor" else (kind,)
    for spelling in spellings:
        for ext in ("jpg", "exr", "png"):
            leaf = f"{name}_{spelling}_{res}.{ext}"
            if layout == "textures_subdir":
                p = os.path.join(SRC, f"{name}_{res}", "textures", leaf)
            else:
                p = os.path.join(SRC, f"{name}_{res}", leaf)
            if os.path.exists(p):
                return p
    raise SystemExit(f"ground_build: no {kind} map for {name} in {os.path.join(SRC, name + '_' + res)}")


def save(arr, path, colorspace="sRGB"):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img = bpy.data.images.new(os.path.basename(path), width=arr.shape[1], height=arr.shape[0],
                              alpha=False, float_buffer=False)
    img.colorspace_settings.name = colorspace
    flat = arr[::-1].reshape(-1).astype(np.float32)
    img.pixels.foreach_set(flat)
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    return os.path.getsize(path)


# ----------------------------------------------------------------------------- tileable noise
def periodic_fbm(size, cells, octaves, seed):
    """Value-noise fbm that WRAPS at the tile edge, so the mask never seams the blend."""
    rng = np.random.RandomState(seed)
    out = np.zeros((size, size), dtype=np.float32)
    amp, total = 1.0, 0.0
    for o in range(octaves):
        c = cells * (2 ** o)
        lattice = rng.rand(c, c).astype(np.float32)
        ys = (np.arange(size, dtype=np.float32) / size * c)
        xs = ys.copy()
        y0 = np.floor(ys).astype(np.int32) % c
        x0 = np.floor(xs).astype(np.int32) % c
        y1 = (y0 + 1) % c
        x1 = (x0 + 1) % c
        fy = (ys - np.floor(ys))[:, None]
        fx = (xs - np.floor(xs))[None, :]
        sy = fy * fy * (3 - 2 * fy)
        sx = fx * fx * (3 - 2 * fx)
        v00 = lattice[np.ix_(y0, x0)]
        v01 = lattice[np.ix_(y0, x1)]
        v10 = lattice[np.ix_(y1, x0)]
        v11 = lattice[np.ix_(y1, x1)]
        top = v00 * (1 - sx) + v01 * sx
        bot = v10 * (1 - sx) + v11 * sx
        out += amp * (top * (1 - sy) + bot * sy)
        total += amp
        amp *= 0.5
    return out / total


# ----------------------------------------------------------------------------- blending
def _src(name, kind, res, layout):
    px = load(find(name, kind, res, layout), "sRGB" if kind == "diff" else "Non-Color")[:, :, :3]
    if px.shape[0] != SIZE:
        px = px[:SIZE, :SIZE]
    return px


def blend_albedos(names, weights, masks, res, layout):
    """Weighted mix of the source albedos, in linear light."""
    acc = np.zeros((SIZE, SIZE, 3), dtype=np.float32)
    wsum = np.zeros((SIZE, SIZE, 1), dtype=np.float32)
    for name, w, mask in zip(names, weights, masks):
        ww = w * mask
        acc += _src(name, "diff", res, layout) * ww
        wsum += ww
    return acc / np.maximum(wsum, 1e-5)


def decode_normal(rgb):
    n = rgb * 2.0 - 1.0
    n[:, :, 2] = np.sqrt(np.clip(1.0 - n[:, :, 0] ** 2 - n[:, :, 1] ** 2, 0.0, 1.0))
    return n


def blend_normals(names, weights, masks, res, layout):
    acc = np.zeros((SIZE, SIZE, 3), dtype=np.float32)
    wsum = np.zeros((SIZE, SIZE, 1), dtype=np.float32)
    for name, w, mask in zip(names, weights, masks):
        ww = w * mask
        acc += decode_normal(_src(name, "nor", res, layout)) * ww
        wsum += ww
    n = acc / np.maximum(wsum, 1e-5)
    n /= np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-5)
    return np.clip(n * 0.5 + 0.5, 0.0, 1.0)


def blend_scalars(names, kind, weights, masks, res, layout):
    acc = np.zeros((SIZE, SIZE, 1), dtype=np.float32)
    wsum = np.zeros((SIZE, SIZE, 1), dtype=np.float32)
    for name, w, mask in zip(names, weights, masks):
        px = load(find(name, kind, res, layout), "Non-Color")[:, :, :1]
        if px.shape[0] != SIZE:
            px = px[:SIZE, :SIZE]
        ww = w * mask
        acc += px * ww
        wsum += ww
    return acc / np.maximum(wsum, 1e-5)


def seam_ratio(arr):
    """The honest tileability number: the wrap step divided by a typical neighbouring-column step.

    1.0 = the seam is no worse than any other column boundary (tileable). A big number = a visible
    seam. Comparing column 0 with the last column and calling anything above zero a failure would be
    wrong: side-by-side columns differ by the texture's own gradient anyway.
    """
    steps_x = np.abs(np.diff(arr[:, :, :3], axis=1)).mean()
    steps_y = np.abs(np.diff(arr[:, :, :3], axis=0)).mean()
    wrap_x = np.abs(arr[:, 0, :3] - arr[:, -1, :3]).mean()
    wrap_y = np.abs(arr[0, :, :3] - arr[-1, :, :3]).mean()
    return (float(wrap_x / max(steps_x, 1e-6)), float(wrap_y / max(steps_y, 1e-6)))


def build(variant):
    """Blend one variant and return everything it produced, without writing anything."""
    global SRC, SIZE
    spec = VARIANTS[variant]
    SRC, SIZE = spec["src"], spec["size"]
    res, layout = spec["res"], spec["layout"]

    # ---- masks: tileable fields, one per source --------------------------------------------
    m_secondary = periodic_fbm(SIZE, 3, 4, 11)      # the second source's patches
    m_third = periodic_fbm(SIZE, 2, 3, 22)          # the third source's patches
    m_worn = periodic_fbm(SIZE, 4, 4, 33)           # wear on the lane
    m_grass = periodic_fbm(SIZE, 5, 3, 44)          # the creeping third
    ones = np.ones((SIZE, SIZE, 1), dtype=np.float32)

    if variant == "dry":
        # grass carpet with bare earth showing through it, and a compacted lane wearing through to
        # loose stones and ruts underneath
        field_masks = [
            ones,                                                    # withered grass: the carpet
            np.clip(m_secondary * 1.25 - 0.35, 0, 1)[:, :, None],    # bare earth, in patches
            np.clip(m_third * 1.50 - 0.72, 0, 1)[:, :, None],        # fallen leaves, sparse
        ]
        lane_masks = [
            np.clip(1.0 - m_worn * 0.70, 0, 1)[:, :, None],          # the compacted crown
            np.clip(m_worn * 1.25 - 0.35, 0, 1)[:, :, None],         # loose stones where worn
            np.clip(m_grass * 1.50 - 0.80, 0, 1)[:, :, None],        # ruts and dirt
        ]
        fw = [1.0, 0.90, 0.50]
        lw = [1.0, 0.90, 0.45]
    else:
        field_masks = [
            ones,                                                    # forest floor is the bed
            np.clip(m_secondary * 1.35 - 0.20, 0, 1)[:, :, None],    # litter over it
            np.clip(m_third * 1.20 - 0.45, 0, 1)[:, :, None],        # wet mud in the lows
        ]
        lane_masks = [
            np.clip(1.0 - m_worn * 0.85, 0, 1)[:, :, None],          # gravel where unworn
            np.clip(m_worn * 1.30 - 0.15, 0, 1)[:, :, None],         # stones where worn
            np.clip(m_grass * 1.5 - 0.95, 0, 1)[:, :, None],         # a little creeping grass
        ]
        fw = [1.0, 0.85, 0.45]
        lw = [1.0, 1.0, 0.30]

    print(f"[ground:{variant}] blending the field from: {', '.join(spec['field'])}  ({res})")
    f_alb = blend_albedos(spec["field"], fw, field_masks, res, layout)
    f_nor = blend_normals(spec["field"], fw, field_masks, res, layout)
    f_rgh = blend_scalars(spec["field"], "rough", fw, field_masks, res, layout)

    print(f"[ground:{variant}] blending the lane from:  {', '.join(spec['lane'])}  ({res})")
    l_alb = blend_albedos(spec["lane"], lw, lane_masks, res, layout)
    l_nor = blend_normals(spec["lane"], lw, lane_masks, res, layout)
    l_rgh = blend_scalars(spec["lane"], "rough", lw, lane_masks, res, layout)

    # dry leaves and dead grass are matte, packed dirt is not: pull the field's roughness up,
    # the lane's down a touch
    f_rgh = np.clip(f_rgh * 0.35 + 0.62, 0.0, 1.0)
    l_rgh = np.clip(l_rgh * 0.55 + 0.38, 0.0, 1.0)

    # ---- grade: the photographs are sunny daylight; this game is dusk, horror register -------
    # Faithful-to-source ground reads as a bright afternoon under a Halloween night sky -- wrong on
    # both counts (too saturated, too bright). Kill the saturation, pull the value down and cool the
    # tint, so the ground reads dead rather than crisp. One knob per axis, so the call is reviewable
    # instead of buried in the blend weights.
    def grade(arr, saturation, gain, tint):
        lum = arr @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
        out = lum[:, :, None] * (1.0 - saturation) + arr * saturation
        out = out * gain * np.array(tint, dtype=np.float32)[None, None, :]
        return np.clip(out, 0.0, 1.0)

    f_alb_raw = np.clip(f_alb * 1.18, 0, 1)
    # The dry variant is already the right register -- bare earth and dead grass, no leaf colour to
    # tame -- so it takes a lighter grade. Grading it as hard as the litter batch would flatten the
    # only thing that makes it read as a field.
    if variant == "dry":
        l_alb_raw = np.clip(l_alb * 1.05, 0, 1)
        f_alb = grade(f_alb_raw, 0.80, 0.90, (1.00, 0.97, 0.92))
        l_alb = grade(l_alb_raw, 0.50, 1.06, (1.00, 0.99, 0.97))
    else:
        l_alb_raw = np.clip(l_alb * 1.10, 0, 1)
        f_alb = grade(f_alb_raw, 0.72, 0.84, (1.00, 0.96, 0.90))
        l_alb = grade(l_alb_raw, 0.45, 1.10, (1.00, 0.99, 0.97))

    # ---- the edge mask: the ragged boundary the runtime uses to superimpose the path ---------
    edge = periodic_fbm(SIZE, 6, 4, 55)
    edge = np.clip(edge * 1.6 - 0.25, 0.0, 1.0)

    for label, arr in (("field", f_alb), ("lane", l_alb)):
        rx, ry = seam_ratio(arr)
        print(f"[ground:{variant}] tileability {label}: wrap step is {rx:.2f}x / {ry:.2f}x a normal "
              f"column step (1.0 = seamless)")

    # ---- the lane's transparency ------------------------------------------------------------
    # Todd, 2026-09-25: "the path way should not be layered, it should be blended via transparancy.
    # the path is currenly bubbly and janky". He is right, and the cause was the *method*: the lane
    # was composited as a strip with its half-width pulsing on a low-frequency mask, so the boundary
    # came out as large smooth blobs -- bubbles -- and read as a separate layer laid on the field.
    #
    # Transparency instead: the lane fades out along its width over a wide band and the fade is eaten
    # into at a FINE scale, so the margin dissolves into the field rather than being drawn on it.
    # Shipped as a strip: U runs along the lane, V across it.
    strip = np.empty((SIZE // 4, SIZE, 3), dtype=np.float32)
    v = (np.arange(SIZE // 4, dtype=np.float32) / (SIZE // 4 - 1))[:, None]
    # across the width: solid in the middle, fading to nothing at both edges
    ramp = np.clip(v / 0.16, 0, 1) * np.clip((1.0 - v) / 0.16, 0, 1)
    ramp = ramp * ramp * (3.0 - 2.0 * ramp)
    grain = periodic_fbm(SIZE, 22, 4, 77)[:SIZE // 4, :]          # fine, and tileable in U
    grit = np.clip((grain - 0.30) / 0.55, 0.0, 1.0)              # sparse holes in the fade
    lane_alpha = np.clip(ramp * (0.55 + 0.75 * grit), 0.0, 1.0)
    lane_alpha = np.clip((lane_alpha - 0.10) / 0.80, 0.0, 1.0)
    lane_alpha = np.repeat(lane_alpha[:, :, None], 3, axis=2)

    return dict(variant=variant, size=SIZE, field_alb=f_alb, field_nor=f_nor, field_rgh=f_rgh,
                lane_alb=l_alb, lane_nor=l_nor, lane_rgh=l_rgh, edge=edge, lane_alpha=lane_alpha,
                field_raw=f_alb_raw, lane_raw=l_alb_raw, ship=(variant == SHIP))


def previews(res_map):
    """Draw the same lane over both variants' fields: the path superimposed, graded and raw."""
    out = []
    for r in res_map:
        variant, size = r["variant"], r["size"]
        ground, lane, e = r["field_alb"], r["lane_alb"], r["edge"]

        ys, xs = np.mgrid[0:size, 0:size].astype(np.float32)
        # a lane that bends like a maze corridor: piecewise-linear centreline, distance in pixels
        k = size / 1024.0
        pts = [(x * k, y * k) for x, y in ((40, 200), (260, 240), (430, 470), (520, 800), (700, 980))]
        dist = np.full((size, size), 1e9, dtype=np.float32)
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            px, py = x1 - x0, y1 - y0
            seg = np.maximum(px * px + py * py, 1e-6)
            t = np.clip(((xs - x0) * px + (ys - y0) * py) / seg, 0.0, 1.0)
            cx, cy = x0 + t * px, y0 + t * py
            dist = np.minimum(dist, np.sqrt((xs - cx) ** 2 + (ys - cy) ** 2))

        # Transparency, not a layer: a wide smooth fade with a FINE grain eating into it. The
        # half-width is constant -- a lane does not pulse -- so nothing reads as a bubble.
        half = 70.0 * k
        t = np.clip((dist - 0.50 * half) / (0.90 * half), 0.0, 1.0)
        falloff = 1.0 - (t * t * (3.0 - 2.0 * t))            # smoothstep down across the margin
        grain = periodic_fbm(size, 20, 4, 77)                 # fine erosion, tileable
        grit = np.clip((grain - 0.30) / 0.55, 0.0, 1.0)
        lane_amt = np.clip(falloff * (0.55 + 0.75 * grit), 0.0, 1.0)
        lane_amt = np.clip((lane_amt - 0.10) / 0.80, 0.0, 1.0)
        # the ground underneath keeps its own colour where the lane is thin, so the two mix rather
        # than one covering the other
        rim = np.clip(1.0 - np.abs(dist - half * 0.95) / (0.5 * half), 0.0, 1.0) * 0.30

        prev = np.clip((ground * (1.0 - lane_amt[:, :, None]) + lane * lane_amt[:, :, None])
                       * (1.0 + 0.06 * rim[:, :, None]), 0.0, 1.0)
        raw = np.clip((r["field_raw"] * (1.0 - lane_amt[:, :, None])
                       + r["lane_raw"] * lane_amt[:, :, None])
                      * (1.0 + 0.06 * rim[:, :, None]), 0.0, 1.0)

        base = os.path.join(REF, "ground-preview" if r["ship"] else f"ground-preview-{variant}")
        for path, img, label in ((f"{base}.png", prev, "graded (dusk)"),
                                 (f"{base}-raw.png", raw, "raw blend (daylight)")):
            rgba = np.concatenate([img, np.ones((size, size, 1), dtype=np.float32)], axis=2)
            n = save(rgba, path, "sRGB")
            print(f"[ground:{variant}] wrote {path}  ({n // 1024} KB)  <- {label}")
            out.append(path)
    return out


def main():
    wanted = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(VARIANTS)
    built = [build(v) for v in wanted if v in VARIANTS]

    # only the SHIP variant writes textures into the game
    for r in built:
        if not r["ship"]:
            print(f"[ground:{r['variant']}] not the shipping variant ({SHIP}) -- preview only, "
                  f"Assets/ untouched")
            continue
        for name, arr, cs in ((f"T_Ground_Field", r["field_alb"], "sRGB"),
                              (f"T_Ground_Field_N", r["field_nor"], "Non-Color"),
                              (f"T_Ground_Field_R", r["field_rgh"], "Non-Color"),
                              (f"T_Ground_Lane", r["lane_alb"], "sRGB"),
                              (f"T_Ground_Lane_N", r["lane_nor"], "Non-Color"),
                              (f"T_Ground_Lane_R", r["lane_rgh"], "Non-Color"),
                              (f"T_Ground_LaneEdge", np.repeat(r["edge"][:, :, None], 3, axis=2),
                               "Non-Color"),
                              (f"T_Ground_LaneAlpha", r["lane_alpha"], "Non-Color")):
            if arr.ndim == 3 and arr.shape[2] == 1:     # roughness is single-channel; PNG is not
                arr = np.repeat(arr, 3, axis=2)
            rgba = np.concatenate([arr, np.ones((arr.shape[0], arr.shape[1], 1), dtype=np.float32)], axis=2)
            p = os.path.join(OUT_DIR, name + ".png")
            n = save(rgba, p, cs)
            print(f"[ground:{r['variant']}] SHIP wrote {p}  ({n // 1024} KB)  {r['size']}px")

    previews(built)
    print("[ground] done")


if __name__ == "__main__":
    main()
