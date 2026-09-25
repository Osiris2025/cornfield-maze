#!/usr/bin/env python3
# DESIGN-TIME ONLY - not compiled into the Unity build, never shipped.
#
# m31b_lane_edge_bake.py -- M31b: a SOLID lane whose EDGE is broken up, not a haze over the lane.
#
# Ernie, 2026-09-25: "M31 is a real fix -- the cut line is gone and the lane now dissolves -- but read your
# own numbers back: T_Ground_LaneA.png is 0.867 alpha at the centre line and the full-strength band is only
# 1.20 m of a 2.08 m lane, so the field shows through the path EVERYWHERE and the margin fades over about
# half a metre. A wide, permanently translucent cross-fade between two different textures does not read as
# one ground eating into another; it reads as haze laid over the lane."
#
# He is right, and the M31 bake was a straight resample of the strip: whatever profile the strip had, the
# lane inherited. This bake replaces the PROFILE while keeping the strip's own GRAIN as the thing that
# displaces the edge:
#
#   1. interior opaque    alpha = 1.0 through the middle, so the lane reads as the ground you stand on
#   2. narrow transition  0.20 m, measured off the result, not asserted
#   3. displaced edge     the strip's own 0.5-crossing, per column along the lane, moves the edge in and
#                         out by up to +/-4 cm -- fingers of field into lane and lane into field, at the
#                         scale of the texture's detail rather than of a smooth curve
#   4. constant half-width: the edge wobbles, the MEAN edge position does not
#
# Same method as M31 otherwise: baked into the lane albedo's alpha at design time, stock URP/Lit, normal map
# and moon untouched, and neither source texture is Read/Write in the player.
#
# RUN: /Applications/Blender.app/Contents/MacOS/Blender -b -noaudio --python scripts/m31b_lane_edge_bake.py
#
# OUTPUT:
#   Assets/Resources/Ground/T_Ground_LaneA.png   (RGBA, alpha = solid lane + broken edge)
#   artifacts/m31b-lane-bake.txt                 measured: interior alpha, transition width in metres,
#                                                edge displacement, alpha at the mesh edge, U seam
import os

import bpy
import numpy as np

REPO = "/Volumes/files1/projects/cornmaze/CornFieldMaze"
GROUND = os.path.join(REPO, "Assets", "Resources", "Ground")
ALBEDO = os.path.join(GROUND, "T_Ground_Lane.png")
STRIP = os.path.join(GROUND, "T_Ground_LaneAlpha.png")
OUT = os.path.join(GROUND, "T_Ground_LaneA.png")
MEASURE = os.path.join(REPO, "artifacts", "m31b-lane-bake.txt")

LANE_WIDTH_M = 2.08      # MazeWorldBuilder: the lane mesh is this wide, so V 0..1 spans it
TRANSITION_M = 0.20      # item 2: 0.15-0.25 m. This is a 2.08 m lane, so 0.20 is roughly a tenth of it
EDGE_M = 1.00            # the mean half-width. 1.04 is the mesh edge, so the ramp always completes first
DISPLACE_MAX_M = 0.04    # item 3: "a few centimetres"


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


def crossing(col, rising):
    """The V (0..1 within this column) where the strip's fade passes 0.5, searched from the centre outwards."""
    n = len(col)
    idx = np.arange(n)
    if rising:                                    # V from 0 towards the centre: the first crossing
        hits = idx[:-1][(col[:-1] < 0.5) & (col[1:] >= 0.5)]
        if not len(hits):
            return None
        i = int(hits[0])
    else:                                         # V from the centre towards 1: the last crossing
        hits = idx[:-1][(col[:-1] >= 0.5) & (col[1:] < 0.5)]
        if not len(hits):
            return None
        i = int(hits[-1])
    # Linear interpolation between the two samples either side of 0.5, so the number is not quantised to a
    # texel -- 8.1 mm per texel across this strip, and the displacement I am measuring is centimetres.
    a, b = float(col[i]), float(col[i + 1])
    t = 0.5 if b == a else (0.5 - a) / (b - a)
    return (i + t) / (n - 1)


def main():
    albedo = load(ALBEDO, "sRGB")
    strip = load(STRIP, "Non-Color")
    h, w = albedo.shape[0], albedo.shape[1]
    sh, sw = strip.shape[0], strip.shape[1]
    ramp = strip[..., 0]                        # the strip carries the fade in its first channel

    # --- the strip's own edge, per column along the lane -------------------------------------------
    top_v, bot_v = [], []
    for x in range(sw):
        c = ramp[:, x]
        t = crossing(c, True)
        b = crossing(c, False)
        top_v.append(t if t is not None else 0.5)
        bot_v.append(b if b is not None else 0.5)
    top_v = np.array(top_v)
    bot_v = np.array(bot_v)
    centre_v = 0.5
    # Distance in metres from the centre line to each edge, per column. The MEAN of these is the lane's
    # half-width; the wobble around it is the grain that does the displacing.
    top_m = (centre_v - top_v) * LANE_WIDTH_M
    bot_m = (bot_v - centre_v) * LANE_WIDTH_M
    wobble_top = top_m - top_m.mean()
    wobble_bot = bot_m - bot_m.mean()
    raw_wobble_m = float(max(np.abs(wobble_top).max(), np.abs(wobble_bot).max()))
    disp_top = np.clip(wobble_top, -DISPLACE_MAX_M, DISPLACE_MAX_M)
    disp_bot = np.clip(wobble_bot, -DISPLACE_MAX_M, DISPLACE_MAX_M)

    # --- build the new alpha ------------------------------------------------------------------------
    # V is the axis across the lane: row j sits |v-0.5|*LANE_WIDTH metres from the centre line. The ramp runs
    # from alpha 1.0 at (edge - TRANSITION) to 0.0 at edge, and the edge itself is displaced per column by the
    # strip's own grain, so the boundary is irregular at texture scale instead of airbrushed.
    v = np.linspace(0.0, 1.0, h)
    d_m = np.abs(v - 0.5) * LANE_WIDTH_M                    # (h,) at the ALBEDO's resolution; the strip is
                                                            # only 256 rows across, and the edge it carries is
                                                            # per-column, so the ramp is rebuilt at 1024
    x_idx = (np.linspace(0.0, 1.0, w, endpoint=False) * sw).astype(np.int64) % sw
    edge_top = EDGE_M - disp_top[x_idx]                     # (w,) the V=0 side
    edge_bot = EDGE_M - disp_bot[x_idx]                     # (w,) the V=1 side

    t_top = np.clip((edge_top[None, :] - d_m[:, None]) / TRANSITION_M, 0.0, 1.0)
    t_bot = np.clip((edge_bot[None, :] - d_m[:, None]) / TRANSITION_M, 0.0, 1.0)
    a_top = t_top * t_top * (3.0 - 2.0 * t_top)
    a_bot = t_bot * t_bot * (3.0 - 2.0 * t_bot)
    alpha = np.minimum(a_top, a_bot)

    out = np.empty((h, w, 4), dtype=np.float32)
    out[..., :3] = albedo[..., :3]
    out[..., 3] = alpha
    size = save(out, OUT, "sRGB")

    # --- measure the RESULT, not the design ---------------------------------------------------------
    centre_alpha = float(alpha[h // 2].mean())                              # the lane's centre line
    interior = d_m <= (EDGE_M - DISPLACE_MAX_M - TRANSITION_M)               # none of the ramp can reach here
    interior_alpha = float(alpha[interior].mean()) if interior.any() else float("nan")
    interior_frac = float(interior.mean())
    # Transition width measured off the baked column. The headline number is the FULL ramp -- where the lane
    # stops being solid to where it is gone -- because that is what "the fade band" means and it is what the
    # order asks to be 0.15-0.25 m. The 0.9-to-0.1 span is reported beside it: it is the visible crease, and
    # on a smoothstep it is always narrower than the band.
    col = alpha[:, w // 2]
    full_lo = float(d_m[np.argmin(np.abs(col - 0.98))])
    full_hi = float(d_m[np.argmin(np.abs(col - 0.02))])
    transition_m = abs(full_hi - full_lo)
    crease_lo = float(d_m[np.argmin(np.abs(col - 0.9))])
    crease_hi = float(d_m[np.argmin(np.abs(col - 0.1))])
    crease_m = abs(crease_hi - crease_lo)
    edge_alpha = float(alpha[0].mean()), float(alpha[-1].mean())             # at the mesh's own edge
    # Displacement actually applied, after the clamp.
    applied = float(max(np.abs(disp_top).max(), np.abs(disp_bot).max()))
    seam = float(np.abs(alpha[:, 0] - alpha[:, -1]).mean())
    step = float(np.abs(np.diff(alpha, axis=1)).mean())

    opaque = float((alpha >= 0.999).mean())
    lines = [
        "M31b lane edge bake — a solid lane with a broken edge, measured off the result",
        "",
        f"  source albedo      T_Ground_Lane.png      {w}x{h}",
        f"  source strip       T_Ground_LaneAlpha.png {sw}x{sh}  (edge grain, per column)",
        f"  output             T_Ground_LaneA.png     {w}x{h}, {size} bytes, RGBA",
        f"  lane width         {LANE_WIDTH_M:.2f} m, so V 0..1 spans it; the mesh edge is at {LANE_WIDTH_M / 2:.2f} m",
        "",
        "  ITEM 1 — interior opaque",
        f"    alpha on the centre line          {centre_alpha:.3f}      (M31 was 0.867: the field read through the lane)",
        f"    mean alpha inside the flat zone   {interior_alpha:.3f}      over the inner {2 * (EDGE_M - DISPLACE_MAX_M - TRANSITION_M):.2f} m of the lane",
        f"    lane area fully opaque (1.000)    {100 * opaque:.1f} %",
        "  ITEM 2 — narrow transition",
        f"    transition width                  {transition_m:.3f} m   (target 0.15-0.25 m; M31 faded over 0.84 m from the centre line)",
        f"    the visible crease (0.9 -> 0.1)   {crease_m:.3f} m   <- always narrower than the band on a smoothstep",
        f"    ramp runs from alpha 1.0 at        {EDGE_M - TRANSITION_M:.2f} m from the centre line",
        f"                        to alpha 0.0 at {EDGE_M:.2f} m from the centre line",
        "  ITEM 3 — the edge is displaced, not just softened",
        f"    strip's own edge wobble           +/-{raw_wobble_m:.3f} m over the columns",
        f"    displacement applied (clamped)    +/-{applied:.3f} m   (clamp +/-{DISPLACE_MAX_M:.2f} m)",
        f"    mean half-width (constant)        {EDGE_M:.3f} m  <- the wobble is around a FIXED half-width, so the lane does not pulse",
        "  ITEM 4 — no line at the mesh edge, and the strip still tiles",
        f"    alpha at the mesh's own edge      {edge_alpha[0]:.3f} / {edge_alpha[1]:.3f}",
        f"    U seam {seam:.4f} vs interior step {step:.4f} -> " + ("tileable" if seam <= max(0.02, step * 3.0) else "NOT tileable"),
        "",
    ]
    os.makedirs(os.path.dirname(MEASURE), exist_ok=True)
    with open(MEASURE, "w") as fh:
        fh.write("\n".join(lines) + "\n")
    print("\n".join(lines))


main()
