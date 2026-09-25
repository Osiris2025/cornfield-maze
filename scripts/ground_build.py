#!/usr/bin/env python3
# DESIGN-TIME ONLY - not compiled into the Unity build, never shipped.
#
# ground_build.py -- derive the maze's ground textures by blending real photographs.
#
# Todd, 2026-09-25: "can you use a combination of textures ... to derive realistic ground textures
# and superimposed path?  Blend some togther"
#
# WHY: the maze's ground was procedural noise -- Materials.Gravel / FieldGrass / PathGrass are 96-128 px
# noise tiles, which can only read as synthetic. This script derives two tiling ground materials from
# six CC0 photographs and an edge mask, so the lane can be superimposed on the field with a ragged,
# believable boundary instead of a straight cut.
#
# SOURCES (Poly Haven, CC0 -- see source-art/ground/PROVENANCE.md):
#   field : forest_floor (base) + forest_leaves_04 (leaf litter) + brown_mud_leaves_01 (wet mud)
#   lane  : gravel_ground_01 (base) + stony_dirt_path (worn stones) + grass_path_2 (creeping grass)
#
# RUN: /Applications/Blender.app/Contents/MacOS/Blender -b -noaudio --python scripts/ground_build.py
#
# OUTPUT (all tileable, 1024):
#   Assets/Resources/Ground/T_Ground_Field.png     + _N.png (normal, Non-Color) + _R.png (rough)
#   Assets/Resources/Ground/T_Ground_Lane.png      + _N.png + _R.png
#   Assets/Resources/Ground/T_Ground_LaneEdge.png  (mask: how ragged the lane/field boundary is)
#   artifacts/reference/ground-preview.png         (top-down proof: the path superimposed on the field)
import math
import os

import bpy
import numpy as np

REPO = "/Volumes/files1/projects/cornmaze/CornFieldMaze"
SRC = "/Volumes/files1/projects/cornmaze/downloads/ground_textures"   # outside the repo: Todd's drop
OUT_DIR = os.path.join(REPO, "Assets", "Resources", "Ground")
PREVIEW = os.path.join(REPO, "artifacts", "reference", "ground-preview.png")
SIZE = 1024

FIELD = ["forest_floor", "forest_leaves_04", "brown_mud_leaves_01"]
LANE = ["gravel_ground_01", "stony_dirt_path", "grass_path_2"]


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


def find(name, kind):
    """kind: diff | nor | rough | disp.

    The sets are inconsistent, and that is a fact about the files rather than a bug here:
    Blender names the OpenGL-convention normal map ``*_nor_gl_1k.exr``, roughness is a .jpg in some
    sets and an .exr in others, and the diffuse is always a .jpg. Try every spelling before failing.
    """
    spellings = ("nor_gl", "nor") if kind == "nor" else (kind,)
    for spelling in spellings:
        for ext in ("jpg", "exr", "png"):
            p = os.path.join(SRC, f"{name}_1k", "textures", f"{name}_{spelling}_1k.{ext}")
            if os.path.exists(p):
                return p
    raise SystemExit(f"ground_build: no {kind} map for {name}")


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
def blend_albedos(names, weights, masks):
    """Weighted mix of the source albedos, in linear light."""
    acc = np.zeros((SIZE, SIZE, 3), dtype=np.float32)
    wsum = np.zeros((SIZE, SIZE, 1), dtype=np.float32)
    for name, w, mask in zip(names, weights, masks):
        alb = load(find(name, "diff"), "sRGB")[:, :, :3]
        if alb.shape[0] != SIZE:
            alb = alb[:SIZE, :SIZE]
        ww = w * mask
        acc += alb * ww
        wsum += ww
    return acc / np.maximum(wsum, 1e-5)


def decode_normal(rgb):
    n = rgb * 2.0 - 1.0
    n[:, :, 2] = np.sqrt(np.clip(1.0 - n[:, :, 0] ** 2 - n[:, :, 1] ** 2, 0.0, 1.0))
    return n


def blend_normals(names, weights, masks):
    acc = np.zeros((SIZE, SIZE, 3), dtype=np.float32)
    wsum = np.zeros((SIZE, SIZE, 1), dtype=np.float32)
    for name, w, mask in zip(names, weights, masks):
        px = load(find(name, "nor"), "Non-Color")[:, :, :3]
        if px.shape[0] != SIZE:
            px = px[:SIZE, :SIZE]
        n = decode_normal(px)
        ww = w * mask
        acc += n * ww
        wsum += ww
    n = acc / np.maximum(wsum, 1e-5)
    n /= np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-5)
    return np.clip(n * 0.5 + 0.5, 0.0, 1.0)


def blend_scalars(names, kind, weights, masks):
    acc = np.zeros((SIZE, SIZE, 1), dtype=np.float32)
    wsum = np.zeros((SIZE, SIZE, 1), dtype=np.float32)
    for name, w, mask in zip(names, weights, masks):
        px = load(find(name, kind), "Non-Color")[:, :, :1]
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


def main():
    # ---- masks: three tileable fields, one per source ---------------------------------------
    m_leaf = periodic_fbm(SIZE, 3, 4, 11)
    m_mud = periodic_fbm(SIZE, 2, 3, 22)
    m_worn = periodic_fbm(SIZE, 4, 4, 33)
    m_grass = periodic_fbm(SIZE, 5, 3, 44)

    field_masks = [
        np.ones((SIZE, SIZE, 1), dtype=np.float32),                    # forest floor is the bed
        np.clip(m_leaf * 1.35 - 0.20, 0, 1)[:, :, None],               # litter over it
        np.clip(m_mud * 1.20 - 0.45, 0, 1)[:, :, None],                # wet mud in the lows
    ]
    lane_masks = [
        np.clip(1.0 - m_worn * 0.85, 0, 1)[:, :, None],                # gravel where unworn
        np.clip(m_worn * 1.30 - 0.15, 0, 1)[:, :, None],               # stones where worn
        np.clip(m_grass * 1.5 - 0.95, 0, 1)[:, :, None],               # a little creeping grass
    ]
    fw = [1.0, 0.85, 0.45]
    lw = [1.0, 1.0, 0.30]

    print("[ground] blending the field from:", ", ".join(FIELD))
    f_alb = blend_albedos(FIELD, fw, field_masks)
    f_nor = blend_normals(FIELD, fw, field_masks)
    f_rgh = blend_scalars(FIELD, "rough", fw, field_masks)

    print("[ground] blending the lane from:", ", ".join(LANE))
    l_alb = blend_albedos(LANE, lw, lane_masks)
    l_nor = blend_normals(LANE, lw, lane_masks)
    l_rgh = blend_scalars(LANE, "rough", lw, lane_masks)

    # dry leaves are matte and mud is not: pull the field's roughness up, the lane's down a touch
    f_rgh = np.clip(f_rgh * 0.35 + 0.62, 0.0, 1.0)
    l_rgh = np.clip(l_rgh * 0.55 + 0.38, 0.0, 1.0)

    # ---- grade: the photographs are sunny autumn; this game is dusk, horror register ----------
    # Faithful-to-source ground reads as a bright October afternoon under a Halloween night sky --
    # wrong on both counts (too saturated, too bright). Kill the saturation, pull the value down and
    # cool the tint so the litter reads damp and dead rather than crisp and new. One knob per axis, so
    # the call is reviewable instead of buried in the blend weights.
    def grade(arr, saturation, gain, tint):
        lum = arr @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
        out = lum[:, :, None] * (1.0 - saturation) + arr * saturation
        out = out * gain * np.array(tint, dtype=np.float32)[None, None, :]
        return np.clip(out, 0.0, 1.0)

    f_alb_raw, l_alb_raw = np.clip(f_alb * 1.18, 0, 1), np.clip(l_alb * 1.10, 0, 1)
    # field: keep some leaf colour so it is litter and not mud. lane: pull it greyer AND lighter,
    # because a worn gravel path reads as the pale thing in a dark field -- graded dark, it vanished
    # into the litter and the lane stopped being readable at all.
    f_alb = grade(f_alb_raw, 0.72, 0.84, (1.00, 0.96, 0.90))
    l_alb = grade(l_alb_raw, 0.45, 1.10, (1.00, 0.99, 0.97))

    # ---- the edge mask: the ragged boundary the runtime uses to superimpose the path ---------
    edge = periodic_fbm(SIZE, 6, 4, 55)
    edge = np.clip(edge * 1.6 - 0.25, 0.0, 1.0)

    written = []
    for name, arr, cs in (("T_Ground_Field", f_alb, "sRGB"),
                          ("T_Ground_Field_N", f_nor, "Non-Color"),
                          ("T_Ground_Field_R", f_rgh, "Non-Color"),
                          ("T_Ground_Lane", l_alb, "sRGB"),
                          ("T_Ground_Lane_N", l_nor, "Non-Color"),
                          ("T_Ground_Lane_R", l_rgh, "Non-Color"),
                          ("T_Ground_LaneEdge", np.repeat(edge[:, :, None], 3, axis=2), "Non-Color")):
        if arr.shape[2] == 1:                      # roughness is single-channel; PNG is not
            arr = np.repeat(arr, 3, axis=2)
        rgba = np.concatenate([arr, np.ones((SIZE, SIZE, 1), dtype=np.float32)], axis=2)
        p = os.path.join(OUT_DIR, name + ".png")
        n = save(rgba, p, cs)
        written.append((name, n))
        print(f"[ground] wrote {p}  ({n // 1024} KB)")

    for label, arr in (("field", f_alb), ("lane", l_alb)):
        rx, ry = seam_ratio(arr)
        print(f"[ground] tileability {label}: wrap step is {rx:.2f}x / {ry:.2f}x a normal column step "
              f"(1.0 = seamless)")

    # ---- preview: the path superimposed on the field -----------------------------------------
    # A top-down 1024 of what the player walks on: field everywhere, a curving lane laid over it,
    # the boundary broken by the same edge mask the runtime will use, with dirt packed at the edges.
    def tile(arr, tiles):
        return np.tile(arr, (tiles, tiles, 1))[:SIZE, :SIZE]

    ground = tile(f_alb, 1)
    lane = tile(l_alb, 1)
    e = tile(edge, 1)

    ys, xs = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32)
    # a lane that bends like a maze corridor: piecewise-linear centreline, distance to it in pixels
    pts = [(40.0, 200.0), (260.0, 240.0), (430.0, 470.0), (520.0, 800.0), (700.0, 980.0)]
    dist = np.full((SIZE, SIZE), 1e9, dtype=np.float32)
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        px, py = x1 - x0, y1 - y0
        seg = np.maximum(px * px + py * py, 1e-6)
        t = np.clip(((xs - x0) * px + (ys - y0) * py) / seg, 0.0, 1.0)
        cx, cy = x0 + t * px, y0 + t * py
        dist = np.minimum(dist, np.sqrt((xs - cx) ** 2 + (ys - cy) ** 2))

    half = 78.0 + 26.0 * (e[:, :, 0] - 0.5) * 2.0   # the lane's half-width wobbles with the mask
    lane_amt = np.clip((half - dist) / 22.0, 0.0, 1.0)
    rim = np.clip(1.0 - np.abs(dist - half) / 14.0, 0.0, 1.0) * 0.45   # packed dirt at the rim

    prev = ground * (1.0 - lane_amt[:, :, None]) + lane * lane_amt[:, :, None]
    prev = np.clip(prev * (1.0 + 0.10 * rim[:, :, None]), 0.0, 1.0)

    # the same lane drawn in the RAW blend, so Todd can see what the grade is doing and overrule it
    raw_ground, raw_lane = tile(f_alb_raw, 1), tile(l_alb_raw, 1)
    raw = raw_ground * (1.0 - lane_amt[:, :, None]) + raw_lane * lane_amt[:, :, None]
    raw = np.clip(raw * (1.0 + 0.10 * rim[:, :, None]), 0.0, 1.0)

    for path, img, label in ((PREVIEW, prev, "graded (dusk)"),
                             (PREVIEW.replace(".png", "-raw.png"), raw, "raw blend (sunny)")):
        rgba = np.concatenate([img, np.ones((SIZE, SIZE, 1), dtype=np.float32)], axis=2)
        n = save(rgba, path, "sRGB")
        print(f"[ground] wrote {path}  ({n // 1024} KB)  <- {label}: the path superimposed on the field")
    print("[ground] done")


if __name__ == "__main__":
    main()
