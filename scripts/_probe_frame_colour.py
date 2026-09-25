#!/usr/bin/env python3
# DESIGN-TIME ONLY - not compiled into the Unity build.
# _probe_frame_colour.py -- read the pixels of two M31 frames and report what is actually in them.
#
# The lane was tinted flat magenta for the coverage frame and the frame shows no magenta, which means either
# the tint did not reach the geometry or the band I have been calling "the lane" is not lane geometry at all.
# A grid of mean RGB values settles it without another build.
import sys
import numpy as np
import bpy

def load(path):
    img = bpy.data.images.load(path)
    img.colorspace_settings.name = "Non-Color"    # read the stored bytes, no display transform
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    a = a[::-1]                                    # Blender rows run bottom-up
    return a, w, h

def report(path, label):
    a, w, h = load(path)
    print("==", label, path, w, "x", h)
    gx, gy = 8, 5
    print("   mean RGB per tile (row 0 = top), and a flag when R>G and B>G (magenta):")
    for iy in range(gy):
        row = []
        for ix in range(gx):
            tile = a[iy * h // gy:(iy + 1) * h // gy, ix * w // gx:(ix + 1) * w // gx, :3]
            m = tile.reshape(-1, 3).mean(axis=0)
            flag = "M" if (m[0] > m[1] * 1.15 and m[2] > m[1] * 1.15) else " "
            row.append(" ".join(f"{v:.2f}" for v in m) + flag)
        print("   " + " | ".join(row))
    flat = a[:, :, :3].reshape(-1, 3)
    mag = int(np.sum((flat[:, 0] > flat[:, 1] * 1.15) & (flat[:, 2] > flat[:, 1] * 1.15)))
    print(f"   magenta-ish pixels: {mag} of {flat.shape[0]} ({100.0 * mag / flat.shape[0]:.2f}%)")

for p, l in [(p.split("=", 1)[0], p.split("=", 1)[1]) for p in sys.argv[sys.argv.index("--") + 1:]]:
    report(p, l)
