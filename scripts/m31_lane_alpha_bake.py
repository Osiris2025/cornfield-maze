#!/usr/bin/env python3
# DESIGN-TIME ONLY - not compiled into the Unity build, never shipped.
#
# m31_lane_alpha_bake.py -- bake the lane's alpha fade into the lane albedo.
#
# Todd, 2026-09-25: "the path way should not be layered, it should be blended via transparancy. the path
# is currenly bubbly and janky."
#
# M29 superimposed the lane as GEOMETRY: the lane mesh's boundary vertices were pulled inwards on a
# derived mask, so the join with the field was a cut line with polygon facets. A margin that wanders
# 0.42 m is still a cut. This bakes the fade instead:
#
#   T_Ground_LaneA.png = T_Ground_Lane.rgb + alpha(T_Ground_LaneAlpha)
#
# U runs ALONG the lane, V ACROSS it. The strip is solid in the middle and gone at both edges, so the
# lane dissolves into the field instead of stopping at it. URP/Lit reads the base map's alpha as opacity
# once the material is set to Alpha Blend, which keeps the lit shader -- lighting, normal maps and the
# PathMudWetness path all survive on the lane. Baking it here rather than compositing at runtime means
# neither source texture has to be Read/Write enabled in the player: the M29 report's 4 MB CPU copy of
# the mask goes away with the geometry it fed.
#
# RUN: /Applications/Blender.app/Contents/MacOS/Blender -b -noaudio --python scripts/m31_lane_alpha_bake.py
#
# OUTPUT:
#   Assets/Resources/Ground/T_Ground_LaneA.png     (RGBA, alpha = the fade)
#   artifacts/m31-lane-bake.txt                    measured: transition width in metres, edge alpha,
#                                                  whether the strip tiles along U and which axis
#                                                  sample was used because of it
import os

import bpy
import numpy as np

REPO = "/Volumes/files1/projects/cornmaze/CornFieldMaze"
GROUND = os.path.join(REPO, "Assets", "Resources", "Ground")
ALBEDO = os.path.join(GROUND, "T_Ground_Lane.png")
STRIP = os.path.join(GROUND, "T_Ground_LaneAlpha.png")
OUT = os.path.join(GROUND, "T_Ground_LaneA.png")
MEASURE = os.path.join(REPO, "artifacts", "m31-lane-bake.txt")

# The lane mesh is 2.08 m across (MazeWorldBuilder), so V 0..1 spans this many metres.
LANE_WIDTH_M = 2.08


def load(path, colorspace):
    img = bpy.data.images.load(path)
    img.colorspace_settings.name = colorspace
    w, h = img.size
    buf = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(buf)
    a = buf.reshape(h, w, 4)[::-1].copy()
    bpy.data.images.remove(img)
    return a


def save(arr, path, colorspace="sRGB"):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img = bpy.data.images.new(os.path.basename(path), width=arr.shape[1], height=arr.shape[0],
                              alpha=True, float_buffer=False)
    img.colorspace_settings.name = colorspace
    img.pixels.foreach_set(arr[::-1].reshape(-1).astype(np.float32))
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    return os.path.getsize(path)


def main():
    albedo = load(ALBEDO, "sRGB")
    strip = load(STRIP, "Non-Color")
    h, w = albedo.shape[0], albedo.shape[1]
    sh, sw = strip.shape[0], strip.shape[1]
    alpha = strip[..., 0]                      # the strip carries the fade in its first channel

    # --- what the strip actually is -------------------------------------------------------------
    profile = alpha.mean(axis=1)               # across the lane, averaged along it
    v = np.linspace(0.0, 1.0, sh)
    edge_lo, edge_hi = float(profile[0]), float(profile[-1])
    middle = float(profile[sh // 2])
    # The strip's plateau is whatever its own peak is -- the first cut looked for alpha >= 0.98 and found
    # an empty band, because this strip tops out at 0.87 at the centre line. Measure the plateau off the
    # texture instead of off an assumed 1.0.
    plateau = max(0.5, float(profile.max()) * 0.95)
    above = np.where(profile >= plateau)[0]
    below = np.where(profile <= 0.02)[0]
    lo_v = float(v[above.min()]) if len(above) else 0.0
    hi_v = float(v[above.max()]) if len(above) else 1.0
    width_m = (hi_v - lo_v) * LANE_WIDTH_M
    half_v = float(v[np.argmin(np.abs(profile - 0.5))])
    # A fade is symmetric about the lane's centre line, so the half-width is the honest number.
    half_m = abs(0.5 - half_v) * LANE_WIDTH_M

    # --- does the strip tile along U? -----------------------------------------------------------
    seam = float(np.abs(alpha[:, 0] - alpha[:, -1]).mean())
    interior_step = float(np.abs(np.diff(alpha, axis=1)).mean())
    tiles_u = seam <= max(2.0, interior_step * 3.0)
    u_source = "u (the strip's own grain along the lane)" if tiles_u else "0.5 (one column: the strip does not tile along U)"

    # --- bake -----------------------------------------------------------------------------------
    out = np.empty((h, w, 4), dtype=np.float32)
    out[..., :3] = albedo[..., :3]
    if tiles_u:
        uu = (np.linspace(0.0, 1.0, w, endpoint=False) * sw).astype(np.int64) % sw
        vv = (np.linspace(0.0, 1.0, h, endpoint=False) * sh).astype(np.int64) % sh
        out[..., 3] = alpha[np.ix_(vv, uu)]
    else:
        vv = (np.linspace(0.0, 1.0, h, endpoint=False) * sh).astype(np.int64) % sh
        out[..., 3] = np.repeat(alpha[vv, sw // 2][:, None], w, axis=1)

    size = save(out, OUT, "sRGB")

    lines = [
        "M31 lane alpha bake — measured off the textures, not asserted from the design",
        "",
        f"  source albedo      T_Ground_Lane.png      {w}x{h}",
        f"  source strip       T_Ground_LaneAlpha.png {sw}x{sh}",
        f"  output             T_Ground_LaneA.png     {w}x{h}, {size} bytes, RGBA",
        "  bake               RGB = lane albedo, A = the strip's fade, U along the lane, V across it",
        "",
        f"  strip V profile    edge {edge_lo:.3f} -> centre {middle:.3f} -> edge {edge_hi:.3f}",
        f"  alpha at the mesh's edge (V=0 / V=1): {edge_lo:.3f} / {edge_hi:.3f}  <- the geometry stops where the fade has already reached zero, so there is no line at the edge",
        f"  full-strength band  V {lo_v:.3f} .. {hi_v:.3f}  = {width_m:.2f} m of {LANE_WIDTH_M:.2f} m lane width (the plateau, measured at the strip's own peak)",
        f"  centre line alpha  {middle:.3f}  <- the lane is not fully opaque even in the middle: the field reads through it, which is what makes it a blend rather than a layer",
        f"  half-width (alpha 0.5) {half_m:.2f} m from the centre line",
        f"  U tiling           seam {seam:.2f} vs interior step {interior_step:.2f}  -> " + ("tileable" if tiles_u else "NOT tileable"),
        f"  U sample used      {u_source}",
        "",
    ]
    os.makedirs(os.path.dirname(MEASURE), exist_ok=True)
    with open(MEASURE, "w") as fh:
        fh.write("\n".join(lines) + "\n")
    print("\n".join(lines))


main()
