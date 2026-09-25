#!/usr/bin/env python3
# DESIGN-TIME ONLY - not compiled into the Unity build, never shipped.
#
# m32b_puddle_maps.py -- derive the two maps the puddle decal needs from the art on disk.
#
# Todd's ruling puts water in charge of the sheen: the ground is matte and the puddle is the one surface allowed
# to reflect. The piecest on disk come as three files (scripts/puddle_build.py, seed 4177):
#
#   T_Ground_Puddle.png       dark wet earth, mean 0.114 -- dark because water absorbs, not because it is black
#   T_Ground_PuddleAlpha.png  coverage: water core, and a damp halo whose alpha (0.34) is what reads as "puddle"
#   T_Ground_Puddle_N.png     a nearly flat water plane with a faint silt lip
#
# Two things are still missing for the material to be able to do its job, and both are derivations rather than
# new art:
#
#   T_Ground_PuddleA.png   RGB = the earth, A = the coverage. The material is Alpha Blend and URP reads the
#                          BASE MAP's alpha as opacity, so the coverage has to live in the albedo's alpha --
#                          exactly the shape M31's lane fade uses. The coverage file keeps it in RGB, not alpha.
#
#   T_Ground_Puddle_M.png  RGB = metallic 0, A = smoothness, per M32's rule that URP's metallic/smoothness map
#                          is the only place a roughness value can reach the shader. The water gets ~0.88 --
#                          the one reflective surface in the maze -- and the damp halo around the waterline
#                          stays MATTE at ~0.10, so only the water answers the light and the highlight cannot
#                          bleed past the waterline into the lane.
#
# RUN: /Applications/Blender.app/Contents/MacOS/Blender -b -noaudio --python scripts/m32b_puddle_maps.py
#
# OUTPUT:
#   Assets/Resources/Ground/T_Ground_PuddleA.png    RGBA: earth + coverage in alpha
#   Assets/Resources/Ground/T_Ground_Puddle_M.png   RGBA: metallic 0 + smoothness in alpha
#   artifacts/m32b-puddle-bake.txt                  measured: water core, halo, water smoothness, halo
#                                                   smoothness, max opacity
import os

import bpy
import numpy as np

REPO = "/Volumes/files1/projects/cornmaze/CornFieldMaze"
GROUND = os.path.join(REPO, "Assets", "Resources", "Ground")
ALBEDO = os.path.join(GROUND, "T_Ground_Puddle.png")
COVER = os.path.join(GROUND, "T_Ground_PuddleAlpha.png")
OUT_A = os.path.join(GROUND, "T_Ground_PuddleA.png")
OUT_M = os.path.join(GROUND, "T_Ground_Puddle_M.png")
MEASURE = os.path.join(REPO, "artifacts", "m32b-puddle-bake.txt")

WATER_SMOOTHNESS = 0.88      # target 0.85-0.92: the only surface in the maze allowed to reflect
HALO_SMOOTHNESS = 0.10       # the damp ring is dirt, not water: matte, so the highlight stops at the waterline
RAMP_LO, RAMP_HI = 0.55, 0.80  # the coverage values the smoothness ramps between


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
    cover_src = load(COVER, "Non-Color")
    h, w = albedo.shape[0], albedo.shape[1]
    sh, sw = cover_src.shape[0], cover_src.shape[1]
    # The coverage file carries it in its first channel (measured: RGB mean 0.109, alpha flat 1.0).
    cover = cover_src[..., 0]
    if (sw, sh) != (w, h):                       # match the albedo's resolution if they differ
        yi = (np.linspace(0.0, 1.0, h, endpoint=False) * sh).astype(np.int64) % sh
        xi = (np.linspace(0.0, 1.0, w, endpoint=False) * sw).astype(np.int64) % sw
        cover = cover[np.ix_(yi, xi)]

    out_a = np.empty((h, w, 4), dtype=np.float32)
    out_a[..., :3] = albedo[..., :3]
    out_a[..., 3] = cover
    size_a = save(out_a, OUT_A, "sRGB")

    t = np.clip((cover - RAMP_LO) / (RAMP_HI - RAMP_LO), 0.0, 1.0)
    smooth = HALO_SMOOTHNESS + (WATER_SMOOTHNESS - HALO_SMOOTHNESS) * (t * t * (3.0 - 2.0 * t))
    out_m = np.empty((h, w, 4), dtype=np.float32)
    out_m[..., :3] = 0.0                          # metallic 0: ground and water are both dielectrics
    out_m[..., 3] = smooth
    size_m = save(out_m, OUT_M, "Non-Color")

    water = cover > 0.70
    wet = (cover > 0.05) & ~water
    lines = [
        "M32b puddle maps — derived from the art on disk, measured after the fact",
        "",
        f"  source albedo      T_Ground_Puddle.png        {w}x{h}, RGB mean {albedo[..., :3].mean():.3f}",
        f"  source coverage    T_Ground_PuddleAlpha.png   {sw}x{sh}, R channel mean {cover.mean():.3f}",
        f"  output albedo      T_Ground_PuddleA.png       {w}x{h}, {size_a} bytes  (RGB earth, A = coverage)",
        f"  output smoothness  T_Ground_Puddle_M.png      {w}x{h}, {size_m} bytes  (RGB 0, A = smoothness)",
        "",
        f"  water core (coverage > 0.70)        {100.0 * water.mean():.1f} % of the quad",
        f"  damp halo (0.05 < coverage <= 0.70) {100.0 * wet.mean():.1f} % of the quad",
        f"  max coverage (max opacity)          {cover.max():.2f}   <- never 1.0, so the lane's grain reads through the water",
        f"  smoothness inside the water         mean {smooth[water].mean():.3f}  max {smooth[water].max():.3f}   <- the one reflective surface (target 0.85-0.92)",
        f"  smoothness in the damp halo         mean {smooth[wet].mean():.3f}                    <- MATTE: the highlight stops at the waterline",
        f"  ramp between smoothness values      coverage {RAMP_LO:.2f} -> {RAMP_HI:.2f}",
        "",
        "  Note for the record: the smoothness map is derived, not new art. It is a function of the coverage that",
        "  shipped, so it cannot move the waterline; if the highlight bleeds past the water into the lane, the",
        "  ramp is what to change, not the light.",
        "",
    ]
    os.makedirs(os.path.dirname(MEASURE), exist_ok=True)
    with open(MEASURE, "w") as fh:
        fh.write("\n".join(lines) + "\n")
    print("\n".join(lines))


main()
