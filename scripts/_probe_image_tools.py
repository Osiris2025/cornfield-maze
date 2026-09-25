#!/usr/bin/env python3
"""DESIGN-TIME ONLY — scratch probe, never runs in the game.

Answers two questions before the M31 bake is written:
  1. what image libraries are available in this interpreter?
  2. what are the lane albedo and the alpha strip actually made of — size, mode, and is the strip
     tileable along U (its "along the lane" axis)?
"""
import pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
GROUND = ROOT / "Assets/Resources/Ground"

print("--- libraries")
try:
    import PIL
    from PIL import Image
    print("PIL", PIL.__version__)
except Exception as exc:                                   # noqa: BLE001
    print("PIL unavailable:", exc)
try:
    import numpy as np
    print("numpy", np.__version__)
except Exception as exc:                                   # noqa: BLE001
    print("numpy unavailable:", exc)

print("--- files")
for name in ("T_Ground_Lane.png", "T_Ground_LaneAlpha.png", "T_Ground_LaneEdge.png"):
    path = GROUND / name
    print(name, path.stat().st_size if path.exists() else "MISSING")

try:
    from PIL import Image
    import numpy as np
except Exception:
    raise SystemExit(0)

alb = np.asarray(Image.open(GROUND / "T_Ground_Lane.png").convert("RGB"), dtype=np.float32)
strip = np.asarray(Image.open(GROUND / "T_Ground_LaneAlpha.png"))
print("--- albedo", alb.shape, "strip", strip.shape, strip.dtype, "mode-planes", 1 if strip.ndim == 2 else strip.shape[2])

s = strip if strip.ndim == 2 else strip[..., 0]
print("strip min/max/mean", float(s.min()), float(s.max()), round(float(s.mean()), 4))

# The strip's V profile: alpha across the lane (mean over U).
prof = s.astype(np.float32).mean(axis=1)
print("V profile (across the lane), 16 samples:", [round(float(v), 1) for v in prof[:: max(1, len(prof) // 16)]])

# Tileability along U: how far apart are the first and last columns?
col0, coln = s.astype(np.float32)[:, 0], s.astype(np.float32)[:, -1]
print("U seam |col0 - colN-1| mean/max:", round(float(np.abs(col0 - coln).mean()), 2),
      round(float(np.abs(col0 - coln).max()), 2))
print("U column-to-column step mean:", round(float(np.abs(np.diff(s.astype(np.float32), axis=1)).mean()), 2))
