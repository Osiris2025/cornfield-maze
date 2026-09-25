#!/usr/bin/env python3
# DESIGN-TIME ONLY - not compiled into the Unity build.
#
# m31_assemble_report.py -- stitch the M31 evidence into one artifact.
#
# Sources, in the order a sceptic should read them:
#   1. artifacts/m31-lane-bake.txt     the fade measured off the textures (Blender bake)
#   2. Builds/ground-import.log        the import settings, read back from the TextureImporter
#   3. the app's own report            the blend state on the live material, the UV check, the A/B cost
#   4. the frames                      artefacts a person can look at
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BAKE = os.path.join(ROOT, "artifacts/m31-lane-bake.txt")
IMPORT = os.path.join(ROOT, "Builds/ground-import.log")
APP = os.path.expanduser("~/Library/Application Support/arl480/Corn Field Maze/m31-lane-blend-report.txt")
OUT = os.path.join(ROOT, "artifacts/m31-lane-blend-report.txt")
FRAMES = ["m31-ground-edge.png", "m29-ground-lane.png", "m29-ground-field.png",
          "m31-ground-corner.png", "m31-ground-plan.png", "m29-ground-edge.png"]


def read(path):
    with open(path, "r", errors="replace") as f:
        return f.read()


def import_lines():
    """The two textures M31 added, as the importer reports them after writing."""
    out = []
    text = read(IMPORT)
    for name in ("T_Ground_LaneA.png", "T_Ground_LaneAlpha.png"):
        for line in text.splitlines():
            if line.strip().startswith(name + " type="):
                out.append(line.strip())
    return out


def app_lines():
    keep = ("lane material:", "lane pieces:", "UV mapping check:", "frames:", "frame ", "corner frame:",
            "plan view:", "coverage count", "blend cost", "lane hidden", "A/B", "blend ")
    return [l for l in read(APP).splitlines() if any(l.startswith(k) or l.lstrip().startswith(k) for k in keep)]


def frame_table():
    rows = []
    for name in FRAMES:
        p = os.path.join(ROOT, "artifacts/review/world", name)
        if os.path.exists(p):
            tag = "BEFORE (M29, committed f46b9e0)" if name == "m29-ground-edge.png" else "this pass"
            rows.append(f"  {name:28s} {os.path.getsize(p):>9,d} bytes   {tag}")
    return rows


report = []
report.append("M31 — the lane blends by transparency, not geometry")
report.append("Harrow @corn-chief, for Todd. Corn Field Maze, Unity 6000.3.23f1, Mac build / iPhone target.")
report.append("")
report.append("Todd, 2026-09-25: \"the path way should not be layered, it should be blended via transparency. the")
report.append("path is currently bubbly and janky.\" He was looking at a preview; the built game had the same")
report.append("defect, and M29's own report named the cause in one line: \"Blend method: geometry.\"")
report.append("")
report.append("What changed: the lane's margin is now an alpha fade, not a mesh. M29 pulled the lane's boundary")
report.append("vertices inwards on a derived mask, so the join was a cut line with polygon facets — a margin")
report.append("that wanders 0.42 m is still a cut. The lane is now a plain rectangle laid over the field and the")
report.append("whole boundary is the alpha strip, reaching zero exactly at the mesh's edge.")
report.append("")
report.append("The fade is baked into the lane albedo's alpha at DESIGN time (scripts/m31_lane_alpha_bake.py,")
report.append("Blender), so the material stays stock URP/Lit and the lane keeps the moon, its normal map and the")
report.append("PathMudWetness path. Baking also deletes the CPU-readable mask copy M29 needed — the phone cost")
report.append("that milestone asked to remove.")
report.append("")
report.append("1. THE FADE, MEASURED OFF THE TEXTURE (not asserted from the design)")
report.append(read(BAKE).rstrip())
report.append("")
report.append("2. IMPORT SETTINGS, READ BACK FROM THE IMPORTER (Builds/ground-import.log)")
for line in import_lines():
    report.append("  " + line)
report.append("  T_Ground_LaneA is colour (sRGB on) with alphaSource=FromInput and alphaIsTransparency=True:")
report.append("  its alpha IS transparency. T_Ground_LaneAlpha is data (sRGB off, alpha NOT transparency): it is")
report.append("  the strip the bake reads, kept in the repo as the source of truth for the fade.")
report.append("")
report.append("3. THE LIVE MATERIAL, READ BACK IN THE BUILD")
for line in app_lines():
    report.append("  " + line)
report.append("")
report.append("4. FRAMES (artifacts/review/world/)")
report.extend(frame_table())
report.append("")
report.append("  m29-ground-edge.png is the BEFORE frame, straight out of the M29 commit: read them side by side.")
report.append("  m31-ground-corner.png is a corner, because that is where a linear fade has to give up something.")
report.append("")
report.append("5. WHAT I AM NOT CLAIMING")
report.append("  - At a corner the cell's core piece fades the axis the lane runs along, and its OTHER corn-facing")
report.append("    edge keeps a hard line. A linear strip cannot fade two adjacent sides, and the alternative — two")
report.append("    overlapping pieces — was measured as a brighter plate with a straight edge, which is the exact")
report.append("    defect this milestone removes. The corner frame shows which edge is left hard. Fixing it needs a")
report.append("    second fade direction, not a bigger number.")
report.append("  - The A/B of blend cost was run by switching the live lane material between Alpha Blend and Opaque")
report.append("    over the same view. It reads as nothing measurable on the Mac. That is an honest reading of a")
report.append("    machine that is not fill-bound at 3 ms/frame, not a claim that overdraw is free on a phone.")
report.append("  - A flat magenta tint on the lane material did NOT reach the drawn pixels in the coverage frame")
report.append("    (0 magenta pixels, checked numerically). So the lane's runtime colour is driven from somewhere")
report.append("    other than this material's _BaseColor, and the plan view is reported without the tint rather")
report.append("    than pretending it worked. The coverage COUNT below does not depend on the tint.")
report.append("  - The bright quadrilateral on the ground in the night lane frame is present in M29's committed")
report.append("    frame of the same view, unchanged: it is not a regression from this pass and not the lane's")
report.append("    boundary. It is unidentified and left alone.")
report.append("  - Do not change: the lane's navigable footprint (M22's lane-centring, the corridor constraint),")
report.append("    the eat sequence, the HUD, the maze layout and M26's threat term were not touched.")
report.append("")
report.append("Verdict: at the lane's edge the gravel dissolves into the field with no cut line, no facets and no")
report.append("visible edge where the layer stops — compare m29-ground-edge.png against m31-ground-edge.png. Open:")
report.append("the corner's remaining hard edge, and the phone-side overdraw that the Mac cannot measure.")
report.append("")

with open(OUT, "w") as f:
    f.write("\n".join(report) + "\n")
print("wrote", OUT, len("\n".join(report)), "chars")
