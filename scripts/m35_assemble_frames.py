#!/usr/bin/env python3
# DESIGN-TIME ONLY - not compiled into the Unity build, never ships on device.
#
# m35_assemble_frames.py — lift the harness's own frames out of the player's data folder and into
# artifacts/, so what is reviewed is the file the app wrote this run and not a stale copy beside it.
#
# The freshness rule is the point: a frame is only accepted if it is newer than the run's start, and
# the report is copied whole. A missing frame is reported as missing rather than skipped silently.
import os
import pathlib
import shutil
import sys
import time

REPO = pathlib.Path(__file__).resolve().parent.parent
PLAYER = pathlib.Path.home() / "Library/Application Support/arl480/Corn Field Maze"
SHOTS = REPO / "artifacts/review/world"

FRAMES = ["m35-lane-dusk.png", "m35-chase-deep.png", "m35-chase-close.png",
          "m35-lane-night.png", "m35-chase-night.png"]

start = time.time() - 3600.0
if len(sys.argv) > 1:
    start = float(sys.argv[1])

SHOTS.mkdir(parents=True, exist_ok=True)
missing = []
for name in FRAMES:
    src = PLAYER / name
    if not src.exists():
        missing.append(name)
        continue
    age = time.time() - src.stat().st_mtime
    shutil.copy2(src, SHOTS / name)
    print(f"  {name:28s} {src.stat().st_size:>9,d} bytes  written {age:6.0f} s ago"
          f"  {'FRESH' if src.stat().st_mtime >= start else 'STALE — predates this run'}")
    if src.stat().st_mtime < start:
        missing.append(name + " (stale)")

rep = PLAYER / "m35-chase-report.txt"
if rep.exists():
    shutil.copy2(rep, REPO / "artifacts/m35-chase-report.txt")
    print(f"  report -> artifacts/m35-chase-report.txt ({rep.stat().st_size:,d} bytes)")
else:
    print("  report MISSING: the harness did not write one")
    missing.append("m35-chase-report.txt")

print("MISSING:" if missing else "all frames fresh")
for m in missing:
    print("  " + m)
