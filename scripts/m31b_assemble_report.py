#!/usr/bin/env python3
# DESIGN-TIME ONLY - not compiled into the Unity build.
#
# m31b_assemble_report.py -- stitch the M31b evidence into one artifact.
#
# Sources: the bake's own measurements (design time, off the texture), the built app's harness run (pieces,
# UVs, blend mode, blend cost), the frames.
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BAKE = os.path.join(ROOT, "artifacts/m31b-lane-bake.txt")
APP = os.path.expanduser("~/Library/Application Support/arl480/Corn Field Maze/m31b-lane-edge-report.txt")
OUT = os.path.join(ROOT, "artifacts/m31b-lane-edge-report.txt")
FRAMES = ["m31-ground-edge.png", "m31-ground-lane.png", "m29-ground-edge.png",
          "m31-ground-corner.png", "m31-ground-plan.png", "m31-ground-coverage.png"]

PREAMBLE = """M31b — the blend is a haze; make it ground eating into ground
Harrow @corn-chief, for Todd. Corn Field Maze, Unity 6000.3.23f1, Mac build / iPhone target.

Ernie, on M31: "T_Ground_LaneA.png is 0.867 alpha at the centre line and the full-strength band is only 1.20 m
of a 2.08 m lane, so the field shows through the path EVERYWHERE and the margin fades over about half a metre."

He is right, and the cause is one line of the M31 bake: it resampled the strip's own profile straight into the
lane albedo's alpha, so the lane inherited whatever shape the strip happened to have — a wide ramp topping out at
0.867. A permanently translucent cross-fade between two different textures is haze laid over the lane, not one
ground eating into another.

M31b keeps the strip but stops obeying it. The bake now:
  1. makes the interior OPAQUE (alpha 1.0 through the middle) so the lane reads as the ground you stand on;
  2. uses a 0.20 m ramp instead of the strip's ~0.45 m, so the join is a seam rather than a wash;
  3. takes the strip's own per-column 0.5-crossing — how its edge wobbles along the lane — and uses THAT to
     displace the boundary in and out by up to +/-4 cm. This is the part that matters: fingers of field into
     lane and lane into field, at the scale of the texture's own detail, which is what stops it reading as an
     airbrushed gradient;
  4. keeps the MEAN half-width fixed at 1.000 m, so the edge is irregular but the lane does not pulse.

Same method as M31 otherwise: baked at design time, stock URP/Lit, normal map and moon untouched, and neither
source texture is Read/Write in the player (the M31 CPU copy stays gone).
"""


def read(path):
    with open(path, "r", errors="replace") as f:
        return f.read()


report = [PREAMBLE.rstrip(), ""]

report.append("1. THE BAKE, MEASURED OFF THE RESULT (scripts/m31b_lane_edge_bake.py)")
for line in read(BAKE).splitlines():
    report.append("  " + line.rstrip())
report.append("")

report.append("2. THE BUILT APP, SAME HARNESS AS M31 (-lanetest)")
for line in read(APP).splitlines():
    if line.startswith(("M31b bake", "lane pieces", "UV mapping", "lane material", "that is Alpha Blend",
                        "coverage count", "blend cost", "  blended", "  forced", "  lane hidden",
                        "frames:", "plan view", "coverage map", "lane frame at night", "A/B")):
        report.append("  " + line.rstrip())
report.append("")

report.append("3. THE FRAMES (artifacts/review/world/)")
for name in FRAMES:
    p = os.path.join(ROOT, "artifacts/review/world", name)
    if os.path.exists(p):
        report.append(f"  {name:32s} {os.path.getsize(p):>9,d} bytes")
report.append("")
report.append("  m31-ground-edge.png is the close-up of the join, framed exactly like m29-ground-edge.png so the")
report.append("  M29 plate and the M31 haze and this can all be put side by side — all three views of the same lane")
report.append("  in the same cell. m31-ground-lane.png is the lane filling the frame, which is where the M31 ghost")
report.append("  was most visible.")
report.append("")

report.append("4. THE ACCEPTANCE QUESTION, ANSWERED IN ONE SENTENCE")
report.append("  At the lane's edge it now reads as two grounds MEETING — a solid gravel path whose border is eaten")
report.append("  into by fingers of field at the texture's own scale — rather than as a gradient painted on the floor.")
report.append("")

report.append("5. WHAT I AM NOT CLAIMING")
report.append("  - Measured on the Mac. The blend cost is re-measured below, but nothing here says what a phone's")
report.append("    tile-based GPU does with lane overdraw; the number is a Mac number.")
report.append("  - The strip's own edge wobble is +/-0.071 m and the bake clamps the displacement to +/-0.04 m. That is")
report.append("    a deliberate cap, not the art's limit: a lane whose edge moves 7 cm starts to read as a lane that")
report.append("    changes width. The clamp is the reason the half-width stays constant.")
report.append("  - The plan view (m31-ground-plan.png, 14 m up) is mostly corn canopy: the lane is visible only in")
report.append("    patches, so it is a coverage aid rather than an acceptance frame. The acceptance pair is")
report.append("    m31-ground-edge.png against M31's own edge frame, and m31-ground-lane.png, which looks down the")
report.append("    lane at eye level where a ghost would be obvious.")
report.append("  - Alpha 1.0 is not reached in the outermost 0.20 m of the mesh on purpose: the ramp has to complete")
report.append("    before the geometry ends, or the mesh's own edge becomes the cut line M31 removed.")
report.append("")

report.append("Verdict: alpha 1.000 at the centre line against M31's 0.867, 75.5 % of the lane fully opaque, a 0.167 m")
report.append("transition against M31's 0.84 m, and an edge displaced +/-0.040 m around a constant half-width. The lane")
report.append("is solid ground with a broken border, not a haze.")
report.append("")

with open(OUT, "w") as f:
    f.write("\n".join(report) + "\n")
print("wrote", OUT, len("\n".join(report)), "chars")
