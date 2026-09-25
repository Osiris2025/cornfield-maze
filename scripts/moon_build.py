#!/usr/bin/env python3
# DESIGN-TIME ONLY - not compiled into the Unity build, never shipped.
#
# moon_build.py -- build the game's full-moon sprite from a photographic source.
#
# WHY A PHOTOGRAPH: the first moon (M25) was a perlin-noise patch in DuskSky.SkyAtlas and read as a
# blob -- no maria, no craters, a mushy limb. Todd, 2026-09-24: "that picture of the 'moon' doesnt
# show a halloween moon, it shows wha appears to me to be a blob."
#
# SOURCE: NASA SVS "Moon Phase and Libration", full-phase frame -- NASA imagery is public domain,
# which is the only provenance this project can ship (a CC BY-SA moon would carry an attribution and
# a share-alike obligation). Recorded in source-art/moon/PROVENANCE.md with its URL and hash.
#
# RUN:  /Applications/Blender.app/Contents/MacOS/Blender -b -noaudio --python scripts/moon_build.py
#
# OUTPUT GEOMETRY (the wiring in DuskSky must match it):
#   T_Moon_Full.png, square, RGBA.
#   * the lunar DISC is centred and its diameter is DISC_FRACTION (0.62) of the image width,
#   * the remaining margin is the AMBER GLOW (atmospheric scatter -- the bloom around the moon in
#     every Halloween reference), which fades to zero at the image edge,
#   * RGB is the disc's own albedo (so the quad's colour multiplies real maria and crater detail),
#     gently warmed toward the harvest read,
#   * A is the disc's mask, softened over the outer few percent so the limb is a limb and not a cut.
import math
import os
import sys

import bpy
import numpy as np

REPO = "/Volumes/files1/projects/cornmaze/CornFieldMaze"
SRC = os.path.join(REPO, "source-art", "moon", "moon_nasa_phase_full.jpg")
OUT_TEX = os.path.join(REPO, "Assets", "Resources", "Sky", "T_Moon_Full.png")
OUT_PREV = os.path.join(REPO, "artifacts", "reference", "moon-built.png")

SIZE = 1024                 # texture size
DISC_FRACTION = 0.62        # disc diameter as a fraction of the image width (the rest is glow)
LIMB_SOFT = 0.020           # alpha falls to 0 over the outer 2% of the disc radius
GLOW_STRENGTH = 0.38        # glow alpha just outside the limb
GLOW_FALLOFF = 2.6          # how quickly the glow dies away
DISC_RGB_GAIN = (1.00, 0.94, 0.86)   # keep the warmth in the albedo itself
GLOW_RGB = (1.00, 0.70, 0.40)        # amber scatter


def read_pixels(path):
    """Load an image through Blender and return (w, h, float32 RGBA array, top-down rows)."""
    img = bpy.data.images.load(path)
    img.colorspace_settings.name = "sRGB"
    w, h = img.size
    buf = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(buf)
    px = buf.reshape(h, w, 4)[::-1]          # bpy rows run bottom-up -> flip to top-down
    bpy.data.images.remove(img)
    return w, h, px


def find_disc(px):
    """Locate the moon in the frame by luminance instead of trusting a hand-picked crop."""
    lum = px[:, :, :3].mean(axis=2)
    lit = lum > 0.02
    ys, xs = np.nonzero(lit)
    if len(xs) == 0:
        raise SystemExit("moon_build: source frame has no lit pixels -- wrong file?")
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    cx, cy = (x0 + x1) / 2.0, (y0 + y1) / 2.0
    radius = max(x1 - x0, y1 - y0) / 2.0
    covered = lit.sum() / (math.pi * radius * radius)
    print(f"[moon] disc bbox x[{x0}:{x1}] y[{y0}:{y1}] centre=({cx:.1f},{cy:.1f}) "
          f"r={radius:.1f} fill={covered:.3f}")
    if not (0.70 < covered < 1.01):
        raise SystemExit(f"moon_build: the lit region is not disc-like (fill={covered:.3f})")
    return cx, cy, radius


def sample(px, xs, ys):
    """Bilinear sample of an (H,W,4) frame at float coordinates, clamped. Vectorised."""
    h, w = px.shape[:2]
    xs = np.clip(xs, 0.0, w - 1.001)
    ys = np.clip(ys, 0.0, h - 1.001)
    x0 = np.floor(xs).astype(np.int32)
    y0 = np.floor(ys).astype(np.int32)
    fx = (xs - x0)[:, :, None]
    fy = (ys - y0)[:, :, None]
    p = px[:, :, :3].astype(np.float32)
    top = p[y0, x0] * (1.0 - fx) + p[y0, x0 + 1] * fx
    bot = p[y0 + 1, x0] * (1.0 - fx) + p[y0 + 1, x0 + 1] * fx
    return top * (1.0 - fy) + bot * fy


def build():
    if not os.path.exists(SRC):
        raise SystemExit(f"moon_build: missing source {SRC}")
    sw, sh, src = read_pixels(SRC)
    print(f"[moon] source {sw}x{sh}")
    cx, cy, radius = find_disc(src)

    out = np.zeros((SIZE, SIZE, 4), dtype=np.float32)
    half = (SIZE - 1) / 2.0
    disc_r = half * DISC_FRACTION
    glow_r = half

    yy, xx = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32)
    dx = (xx - half) / disc_r
    dy = (yy - half) / disc_r
    r_norm = np.sqrt(dx * dx + dy * dy)
    inside = r_norm <= 1.0

    # --- the disc: real albedo from the photograph -------------------------------------------
    disc = np.zeros((SIZE, SIZE, 3), dtype=np.float32)
    albedo = sample(src, cx + dx * radius, cy + dy * radius)
    lum = np.clip(albedo.mean(axis=2) * 1.06, 0.0, 1.0)
    for i, gain in enumerate(DISC_RGB_GAIN):
        disc[:, :, i] = lum * gain
    out[:, :, :3] = np.where(inside[:, :, None], disc, 0.0)
    alpha = np.clip((1.0 - r_norm) / LIMB_SOFT, 0.0, 1.0)
    alpha = np.where(inside, np.minimum(1.0, alpha), 0.0)

    # --- the glow: amber scatter filling the margin --------------------------------------------
    t = np.clip((r_norm - 1.0) / (glow_r / disc_r - 1.0), 0.0, 1.0)
    glow = GLOW_STRENGTH * np.power(1.0 - t, GLOW_FALLOFF)
    glow = np.where(r_norm > 1.0, glow, 0.0)
    for i, ch in enumerate(GLOW_RGB):
        out[:, :, i] = np.where(glow > 0, GLOW_RGB[i] * glow + out[:, :, i] * (1.0 - glow), out[:, :, i])
    out[:, :, 3] = np.maximum(alpha, glow)

    # --- write ---------------------------------------------------------------------------------
    for path in (OUT_TEX, OUT_PREV):
        os.makedirs(os.path.dirname(path), exist_ok=True)
    img = bpy.data.images.new("T_Moon_Full", width=SIZE, height=SIZE, alpha=True)
    img.colorspace_settings.name = "sRGB"
    flat = out[::-1].reshape(-1).astype(np.float32)       # back to bpy's bottom-up order
    img.pixels.foreach_set(flat)
    img.filepath_raw = OUT_TEX
    img.file_format = "PNG"
    img.save()
    print(f"[moon] wrote {OUT_TEX}")

    prev = bpy.data.images.new("moon_preview", width=SIZE, height=SIZE, alpha=True)
    prev.colorspace_settings.name = "sRGB"
    checker = out.copy()
    bg = 0.06
    checker[:, :, :3] = np.where(checker[:, :, 3:4] > 0.001, checker[:, :, :3],
                                 np.full_like(checker[:, :, :3], bg))
    checker[:, :, 3] = 1.0
    prev.pixels.foreach_set(checker[::-1].reshape(-1).astype(np.float32))
    prev.filepath_raw = OUT_PREV
    prev.file_format = "PNG"
    prev.save()
    print(f"[moon] wrote {OUT_PREV}")

    # --- numbers the wiring has to honour -------------------------------------------------------
    print(f"[moon] GEOMETRY disc_fraction={DISC_FRACTION} -> a quad of size S must be sized "
          f"S = wanted_angular_size / {DISC_FRACTION}")
    print(f"[moon] disc pixels across = {disc_r * 2:.0f} of {SIZE}")


if __name__ == "__main__":
    build()
    print("[moon] done")
