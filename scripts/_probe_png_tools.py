"""Availability probe (scratch): which image modules can read a PNG here?

Run: python3 scripts/_probe_png_tools.py
Not part of the build; safe to delete.
"""
import sys

print("python", sys.version.split()[0])
for mod in ("PIL", "numpy", "imageio", "cv2"):
    try:
        __import__(mod)
        print("HAVE", mod)
    except Exception as e:
        print("MISSING", mod, type(e).__name__)
