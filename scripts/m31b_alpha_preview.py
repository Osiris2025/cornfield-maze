#!/usr/bin/env python3
# DESIGN-TIME ONLY - not compiled into the Unity build.
#
# m31b_alpha_preview.py -- write the baked lane alpha out as a picture, so the edge can be looked at directly
# instead of inferred from a render. The lane quad's V runs vertically here and U horizontally, so the top and
# bottom of this image are the two edges of the lane as the player walks along it.
import os

import bpy
import numpy as np

REPO = "/Volumes/files1/projects/cornmaze/CornFieldMaze"
GROUND = os.path.join(REPO, "Assets", "Resources", "Ground")
OUT = os.path.join(REPO, "artifacts", "reference", "m31b-lane-alpha-preview.png")

img = bpy.data.images.load(os.path.join(GROUND, "T_Ground_LaneA.png"))
img.colorspace_settings.name = "Non-Color"
w, h = img.size
buf = np.empty(w * h * 4, dtype=np.float32)
img.pixels.foreach_get(buf)
a = buf.reshape(h, w, 4)[::-1]
alpha = a[..., 3]
bpy.data.images.remove(img)

# The alpha as greyscale, over the FULL lane width so both edges are in the picture.
strip = np.repeat(alpha[:, :, None], 3, axis=2)

out = bpy.data.images.new("m31b-alpha", width=strip.shape[1], height=strip.shape[0], alpha=False,
                          float_buffer=False)
out.colorspace_settings.name = "Non-Color"
rgba = np.concatenate([strip, np.ones_like(strip[:, :, :1])], axis=2)
out.pixels.foreach_set(rgba[::-1].reshape(-1).astype(np.float32))
out.filepath_raw = OUT
out.file_format = "PNG"
out.save()
bpy.data.images.remove(out)

# The same measurement the eye makes: how far the 0.5 crossing wanders along the lane, per column.
cross = []
for x in range(w):
    col = alpha[:, x]
    hits = np.nonzero(col >= 0.5)[0]
    if len(hits):
        cross.append(float((hits.min() + (h - 1 - hits.max())) / 2.0))
cross = np.array(cross)
print(f"wrote {OUT} ({os.path.getsize(OUT)} bytes)")
print(f"  alpha>0.5 half-thickness in pixels: min {cross.min():.1f} mean {cross.mean():.1f} max {cross.max():.1f} "
      f"-> spread {cross.max() - cross.min():.1f} px over {w} columns")
print(f"  at 2.08 m per {h} px that spread is {2.08 * (cross.max() - cross.min()) / h:.3f} m of edge travel")
