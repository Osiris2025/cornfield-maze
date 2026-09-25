# DESIGN-TIME ONLY -- runs under Blender's python, never in the shipped game.
#
# m32c_frame_delta.py -- "did these two shots even share a camera?"
#
# A material ablation is only evidence if the camera did not move between the two frames. A moved camera
# makes the whole frame differ; a changed material makes a localised patch differ. The 4x4 grid below is the
# cheap way to tell those apart before any number from the pair is believed.
#
# usage: Blender --background --python scripts/m32c_frame_delta.py -- <a.png> <b.png>
import sys
import numpy as np
import bpy

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[-2:]
A, B = argv[0], argv[1]


def load(path):
    img = bpy.data.images.load(path)
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    return a


a, b = load(A), load(B)
if a.shape != b.shape:
    print("different sizes %s vs %s -- cannot compare" % (a.shape, b.shape))
    raise SystemExit(1)

d = np.abs(a[..., :3] - b[..., :3]).max(axis=2) * 255.0
h, w = d.shape
print("%s  vs  %s" % (A.split("/")[-1], B.split("/")[-1]))
print("  mean abs diff %6.3f of 255   p99 %6.1f   max %6.1f" % (d.mean(), np.percentile(d, 99), d.max()))
print("  pixels differing by more than 8: %5.2f %%   more than 32: %5.2f %%"
      % (100.0 * np.mean(d > 8), 100.0 * np.mean(d > 32)))
print("  4x4 grid of mean abs diff (top row of the frame first):")
for r in range(4):
    cells = []
    for c in range(4):
        cell = d[r * h // 4:(r + 1) * h // 4, c * w // 4:(c + 1) * w // 4]
        cells.append("%7.1f" % cell.mean())
    print("    " + " ".join(cells))
