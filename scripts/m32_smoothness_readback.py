#!/usr/bin/env python3
# DESIGN-TIME ONLY - not compiled into the Unity build.
#
# m32_smoothness_readback.py -- measure the smoothness that ACTUALLY lives in the ground maps on disk.
#
# The M32 defect Todd saw: the metallic/smoothness maps were imported with alphaSource=None, so the alpha
# channel -- which holds the smoothness -- was thrown away and URP read the white default, giving
# smoothness = 1.0 everywhere: a mirror floor of dry dirt. The fix is an import setting, and an import
# setting has to be verified by reading the value back, not by looking at a picture.
#
# This is the SOURCE half of that read-back (design-time, on the committed PNGs). The runtime half samples
# the same values through the importer in the built app, so the two can be compared.
import sys
import numpy as np
import bpy

ROOT = "Assets/Resources/Ground/"


def load(path, colorspace="Non-Color"):
    img = bpy.data.images.load(ROOT + path)
    img.colorspace_settings.name = colorspace
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    return a, w, h


def stats(label, values, note=""):
    v = values.reshape(-1)
    p10, p50, p90 = np.percentile(v, [10, 50, 90])
    over = 100.0 * float(np.sum(v > 0.40)) / v.size
    print(f"  {label:34s} mean {v.mean():.3f}  p10 {p10:.3f}  median {p50:.3f}  p90 {p90:.3f}  "
          f">0.40 {over:5.1f}%   {note}")


print("== source smoothness (the alpha channel of the metallic/smoothness maps)")
for name in ("T_Ground_Field_M.png", "T_Ground_Lane_M.png"):
    a, w, h = load(name)
    print(f"  {name}: {w}x{h}, RGB mean {a[:, :, :3].mean():.4f} (metallic, must be 0)")
    stats(name + " alpha", a[:, :, 3])

print("== source roughness (the R channel of the roughness maps, = 1 - smoothness)")
for name in ("T_Ground_Field_R.png", "T_Ground_Lane_R.png"):
    a, w, h = load(name)
    stats(name + " R", a[:, :, 0], "(roughness)")

print("== puddles")
for name in ("T_Ground_Puddle.png", "T_Ground_PuddleAlpha.png"):
    try:
        a, w, h = load(name, "sRGB" if "Alpha" not in name else "Non-Color")
        print(f"  {name}: {w}x{h}, RGB mean {a[:, :, :3].mean():.4f}")
        if "Alpha" in name:
            stats(name + " (coverage)", a[:, :, 3])
            core = 100.0 * float(np.sum(a[:, :, 3] > 0.80)) / a[:, :, 3].size
            damp = 100.0 * float(np.sum(a[:, :, 3] > 0.10)) / a[:, :, 3].size
            print(f"     water core (alpha>0.80) {core:.1f}% of the quad, water+damp (alpha>0.10) {damp:.1f}%, "
                  f"max {a[:, :, 3].max():.2f}")
    except Exception as e:
        print(f"  {name}: cannot read ({e})")
