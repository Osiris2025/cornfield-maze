#!/usr/bin/env python3
# DESIGN-TIME ONLY - not compiled into the Unity build.
#
# m32_frame_diff.py -- where do the M32 frames actually differ, and is anything in them unreasonably bright?
#
# "It looks shinier" is not evidence in either direction: a matte surface still has a broad, dim specular lobe,
# and a pale albedo under a blue moon reads as frosting whether or not anything is reflecting. So instead of
# judging by eye, subtract the frames pixel by pixel -- same view, same light, same albedo, one variable apart --
# and report where the difference lives and how bright the brightest pixel in the band really is.
import os
import sys
import numpy as np
import bpy

ROOT = "/Volumes/files1/projects/cornmaze/CornFieldMaze/artifacts/review/world"


def load(name):
    img = bpy.data.images.load(os.path.join(ROOT, name))
    img.colorspace_settings.name = "Non-Color"   # the stored bytes, not a view transform
    w, h = img.size
    return np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1], w, h, img


def luma(a):
    return 0.2126 * a[:, :, 0] + 0.7152 * a[:, :, 1] + 0.0722 * a[:, :, 2]


def report(name):
    a, w, h, _ = load(name)
    Y = luma(a)
    print(f"  {name}: {w}x{h}  mean luma {255 * Y.mean():.2f}  p99 {255 * np.percentile(Y, 99):.1f}  "
          f"max {255 * Y.max():.1f} at {bool(Y.max())}")
    # The pixels at the very top of the range: are they texture, or is something blown out?
    hot = Y > 0.95
    print(f"     pixels above 0.95 luma: {int(hot.sum())} ({100.0 * hot.sum() / Y.size:.4f}%), "
          f"above 0.80: {int((Y > 0.80).sum())}")
    if hot.sum():
        ys, xs = np.nonzero(hot)
        print(f"     brightest cluster around x={int(np.median(xs))} y={int(np.median(ys))} "
              f"(frame {w}x{h}, band centre 661,421)")


print("== each frame, on its own")
for n in ("m32-reflection-on.png", "m32-mirror-floor.png", "m32-reflection-off.png"):
    try:
        report(n)
    except Exception as e:
        print(f"  {n}: cannot read ({e})")

print("== the difference: matte (as shipped) minus pre-M32 constants, in the band around the mirror point")
try:
    on, w, h, _ = load("m32-reflection-on.png")
    off, _, _, _ = load("m32-reflection-off.png")
    mirror, _, _, _ = load("m32-mirror-floor.png")
    band = (slice(421 - 65, 421 + 65), slice(661 - 65, 661 + 65))
    d = luma(on) - luma(off)
    print(f"  band mean luma matte {255 * luma(on)[band].mean():.2f} vs pre-M32 {255 * luma(off)[band].mean():.2f} "
          f"-> difference {255 * d[band].mean():+.2f} of 255")
    print(f"  the difference is concentrated where? mean |d| over the whole frame "
          f"{255 * np.abs(d).mean():.2f}, largest single pixel {255 * np.abs(d).max():.0f}")
    dm = luma(mirror) - luma(on)
    print(f"  mirror state minus matte: band mean {255 * dm[band].mean():+.2f}, whole frame "
          f"{255 * dm.mean():+.2f} -> the mirror state is "
          f"{'BRIGHTER' if dm.mean() > 0 else 'DARKER'} overall")
except Exception as e:
    print(f"  cannot diff ({e})")
