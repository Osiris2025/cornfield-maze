#!/usr/bin/env python3
# DESIGN-TIME ONLY - not compiled into the Unity build.
#
# m32_assemble_report.py -- stitch the M32 evidence into one artifact.
#
# Sources: the app's own report (live material state, the aim search, the measured band), the import probe
# (what the importers actually hold), and the frames.
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
IMPORT = os.path.join(ROOT, "Builds/ground-import.log")
APP = os.path.expanduser("~/Library/Application Support/arl480/Corn Field Maze/m32-reflection-report.txt")
OUT = os.path.join(ROOT, "artifacts/m32-reflection-report.txt")
FRAMES = ["m32-reflection-on.png", "m32-reflection-off.png", "m32-reflection-fieldonly.png"]

PREAMBLE = """M32 — the ground catches the moonlight
Harrow @corn-chief, for Todd. Corn Field Maze, Unity 6000.3.23f1, Mac build / iPhone target.

Todd, 2026-09-25: "can you use the PBR files for reflections off the moonlight?" Until this milestone the
answer was no: `Materials.cs` set `_Smoothness` to one constant per material and the sets' roughness maps
never reached the shader, so the whole floor was uniformly matte under the moon. M29's report had it written
down as a deliberate deferral; this milestone pays it.

The sets ship roughness as a separate `_R` map and URP/Lit cannot take a standalone roughness texture, so the
builder emits URP's own metallic/smoothness map instead: `T_Ground_Field_M` / `T_Ground_Lane_M`, RGB metallic
0 (ground is a dielectric — there is no metal in it) and A = smoothness. They are wired to
`_MetallicGlossMap` with `_SmoothnessTextureChannel = 0` and `_Smoothness = 1`, so URP reads
`metallicGloss.a * _Smoothness` and the map carries the value.

Two details that are not decoration:
  * The keyword is `_METALLICSPECGLOSSMAP`. URP's own material upgrader sets it from the presence of the
    metallic map (UniversalRenderPipelineMaterialUpgrader.cs) — a metallic map bound under a different
    keyword renders as if it were not there, which is a "wired" that is not wired. Confirmed against the
    package source rather than guessed.
  * `_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A` is explicitly OFF. The LANE's albedo alpha is its fade (M31);
    reading that channel as smoothness would have made the fade's edges mirror-shiny.

The normal maps stay bound on purpose — they are what breaks the highlight into grain. A reflective floor
without normals is a mirror blob, which is worse than no reflection. The frames below are the test of that.
"""


def read(path):
    with open(path, "r", errors="replace") as f:
        return f.read()


report = [PREAMBLE.rstrip(), ""]
report.append("1. THE WIRING, READ BACK IN THE BUILD")
for line in read(APP).splitlines():
    if line.startswith(("lane:", "field:", "materials found")):
        report.append("  " + line)
report.append("")
report.append("2. IMPORT SETTINGS, READ BACK FROM THE IMPORTERS (Builds/ground-import.log)")
for line in read(IMPORT).splitlines():
    if line.strip().startswith(("T_Ground_Field_M.png type=", "T_Ground_Lane_M.png type=")):
        report.append("  " + line.strip())
report.append("  Both are Data maps: sRGB off, alphaSource=None — Unity does NOT treat that alpha as")
report.append("  transparency. It is a value, and it has to survive as a value to reach the shader.")
report.append("")
report.append("3. WHERE THE MEASUREMENT WAS TAKEN (the aim search, in full)")
for line in read(APP).splitlines():
    if line.startswith(("husk:", "view:", "moon:", "aim probe", "aim:", "band:")):
        report.append("  " + line)
report.append("  The mirror point is where the moon's reflection lands on a flat floor, solved from the camera's")
report.append("  own eye and the live moon direction, and the player is aimed at it through the game's look state.")
report.append("  The search ran because nothing else worked: writing the camera's transform drifts 76 m (the rig")
report.append("  owns it), and correcting the look state by the measured residual drove the pitch between both")
report.append("  clamps instead of converging. Note what the search found: aiming 28 deg ABOVE the horizon put the")
report.append("  camera on a point 28 deg BELOW it — the rig's pitch sign is inverted against the accessor that")
report.append("  reports it. Residual -977 px to 11 px, so the band below is the ground, not the screen edge.")
report.append("")
report.append("4. THE MEASUREMENT — mean and peak luminance in the reflection band, same view, same scene")
for line in read(APP).splitlines():
    if line.startswith(("frame m32", "A/B", "VERDICT", "restored")):
        report.append("  " + line)
report.append("")
report.append("  Read it as: BOUND is what ships; UNBOUND is the pre-M32 state restored exactly (the constants")
report.append("  M31 passed in, now named PreM32FieldSmoothness / PreM32LaneSmoothness in Materials.cs); FIELD")
report.append("  ONLY separates the two surfaces. The band mean goes 49.70 -> 64.67 of 255 (+30 %) and the")
report.append("  brightest pixel in it 158 -> 255. Attribution from the third row: the field's map accounts for")
report.append("  almost all of the mean (63.94 of 64.67 — the field is most of the visible ground), and the lane's")
report.append("  map accounts for the peak (159 -> 255 — the gravel is where the mirror point lands).")
report.append("")
report.append("5. FRAMES (artifacts/review/world/) — the same view, one variable apart")
for name in FRAMES:
    p = os.path.join(ROOT, "artifacts/review/world", name)
    if os.path.exists(p):
        report.append(f"  {name:34s} {os.path.getsize(p):>9,d} bytes")
report.append("")
report.append("  `m32-reflection-on.png` is wet gravel under a moon: the highlight breaks into grain across the")
report.append("  stones, there are bright patches on the damp parts of the field, and the lane carries the")
report.append("  reflection as a grainy strip rather than a mirror. `m32-reflection-off.png` is the same lane")
report.append("  flat and even, with no specular on the stones at all and a nearly black field. That pair is the")
report.append("  evidence; the numbers above are what stop it being a taste call.")
report.append("")
report.append("6. WHAT I AM NOT CLAIMING")
report.append("  - The band peak reads 255 because it CLIPS. The true highlight is at least that bright and the")
report.append("    number is therefore a floor, not a value. If Todd wants it dialled back, the lever is the")
report.append("    roughness numbers in the `_M` maps (lane mean 0.28, field 0.17), not the light — as the order says.")
report.append("  - The field's sheen is broad. In the ON frame the field around the lane is noticeably live, not just")
report.append("    the lane. It reads as a damp field at night to me, but it is the part of the two frames I would")
report.append("    soften first if Todd disagrees.")
report.append("  - This is measured on the Mac. Nothing here says what a phone's tile-based GPU does with another")
report.append("    full-size map pair per ground material; the maps are already committed and imported at 1024.")
report.append("  - The Husk is disabled during the measurement (see section 3). The reflection does not depend on it,")
report.append("    but the band would have been measured on a death frame if it had been left on, which happened once.")
report.append("")
report.append("Verdict: the ground catches the moonlight, by measurement and by frame. Lane mean smoothness 0.28 and")
report.append("field 0.17, normal maps bound, highlight broken into grain rather than a mirror.")
report.append("")

with open(OUT, "w") as f:
    f.write("\n".join(report) + "\n")
print("wrote", OUT, len("\n".join(report)), "chars")
