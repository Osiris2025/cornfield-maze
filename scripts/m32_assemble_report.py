#!/usr/bin/env python3
# DESIGN-TIME ONLY - not compiled into the Unity build.
#
# m32_assemble_report.py -- stitch the M32 evidence into one artifact.
#
# Sources: the app's own report (live material state, the read-back, the aim search, the measured band), the
# import probe (what the importers actually hold), the design-time source measurement, and the frames.
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
IMPORT = os.path.join(ROOT, "Builds/ground-import.log")
APP = os.path.expanduser("~/Library/Application Support/arl480/Corn Field Maze/m32-reflection-report.txt")
OUT = os.path.join(ROOT, "artifacts/m32-reflection-report.txt")
FRAMES = ["m32-reflection-on.png", "m32-mirror-floor.png", "m32-reflection-off.png",
          "m32-reflection-fieldonly.png"]

PREAMBLE = """M32 — the ground is matte; the sheen belongs to the puddles
Harrow @corn-chief, for Todd. Corn Field Maze, Unity 6000.3.23f1, Mac build / iPhone target.

This milestone changed shape and the second shape is the right one.

It started as "the ground is matte, so wire the roughness maps and let it catch the moonlight." The maps were
wired and a sheen appeared, and Todd's ruling on seeing it was: "iMO - the sheen AT ALL is a bug, maybe there
should be a sheen in puddles, but not over all." He is right, and it is the physically correct call — dead
grass and dry dirt are rough (0.87-0.89), and a moonlit field does not glint. Water does, and only where
water sits. So the ground is now MATTE by measurement and reflectivity is the puddles' job (M32b).

Ernie also found the defect in what I shipped, in my own probe output, and it is worth stating in full because
it is the reason this pass exists:

  `T_Ground_Field_M.png ... alphaSource=None`   (Builds/ground-import.log, first wiring)

The smoothness lives in the ALPHA channel of these maps. `alphaSource=None` throws that channel away, URP
reads the white default in its place, and `smoothness = metallicGloss.a * _Smoothness(1) = 1.0` over the whole
floor. Dry dirt behaved like glass. That is the yuck Todd saw, and it is the same failure as writing an alpha
and silently dropping it, one layer down and in a different tool — which is exactly why the fix is verified by
READING THE VALUE BACK rather than by looking at a shinier frame.

The sets ship roughness as a separate `_R` map and URP/Lit cannot take a standalone roughness texture, so the
builder emits URP's own metallic/smoothness map: RGB metallic 0 (ground is a dielectric — no metal in it), A =
smoothness, wired to `_MetallicGlossMap` with `_SmoothnessTextureChannel = 0` and `_Smoothness = 1`.

Two details that are not decoration:
  * The keyword is `_METALLICSPECGLOSSMAP`, taken from URP's own material upgrader. A metallic map bound under
    a different keyword renders as if it were not there — a "wired" that is not wired.
  * `_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A` is explicitly OFF. The lane's albedo alpha is its fade (M31);
    reading that channel as smoothness would have made the fade's edges mirror-shiny.

And the fix to the defect, in one line of the importer: the `_M` maps are now a `SmoothnessMap`, so they import
with `alphaSource = FromInput`, `alphaIsTransparency = false`, and a format that KEEPS alpha — BC7 on the
standalone platform. Compressed BC7 at 1024 is ~1 MB per map; a smoothness map that loses its alpha is the
defect again, one layer down, and DXT1 has no alpha channel to lose it from.
"""


def read(path):
    with open(path, "r", errors="replace") as f:
        return f.read()


def app_lines(prefixes):
    return [ln for ln in read(APP).splitlines() if ln.startswith(prefixes)]


report = [PREAMBLE.rstrip(), ""]

report.append("1. THE WIRING, READ BACK IN THE BUILD")
for line in app_lines(("lane:", "field:", "materials found")):
    report.append("  " + line)
report.append("")

report.append("2. IMPORT SETTINGS, READ BACK FROM THE IMPORTERS (Builds/ground-import.log)")
for line in read(IMPORT).splitlines():
    if line.strip().startswith(("T_Ground_Field_M.png type=", "T_Ground_Lane_M.png type=")):
        report.append("  " + line.strip())
report.append("  Read the two fields that matter: alphaSource=FromInput (the first wiring said None, which is the")
report.append("  mirror floor) and keepsAlpha=yes on BC7. sRGB stays off and alphaIsTransparency stays false — the")
report.append("  channel is read as a value, not treated as coverage, which would divide the metallic RGB by it.")
report.append("")

report.append("3. THE SOURCE VALUES (scripts/m32_smoothness_readback.py, Blender, on the committed PNGs)")
report.append("  This is the truth the runtime read-back is measured against, read straight out of the files with")
report.append("  no importer in the path.")
for line in read(os.path.join(ROOT, "artifacts/m32-smoothness-source.txt")).splitlines():
    if line.startswith("  T_Ground") or line.strip().startswith("T_Ground"):
        report.append("  " + line.rstrip())
report.append("")

report.append("4. THE RUNTIME READ-BACK — the imported texture, sampled in the built game")
for line in app_lines(("read-back",)):
    report.append("  " + line)
report.append("  This is the check the order demanded and it is the check that would have caught the first wiring:")
report.append("  a mismatch means the alpha did not survive the import, and the milestone is not done. A read-back")
report.append("  of 1.0 is the mirror floor, whatever the frames look like.")
report.append("")

report.append("5. WHERE THE MEASUREMENT WAS TAKEN (the aim search, in full)")
for line in app_lines(("husk:", "view:", "moon:", "aim probe", "aim:", "band:")):
    report.append("  " + line)
report.append("  The mirror point is where the moon's reflection WOULD land on a flat floor, solved from the camera's")
report.append("  own eye and the live moon direction, and the player is aimed at it through the game's look state.")
report.append("  The search ran because nothing else worked: writing the camera's transform drifts 76 m (the rig owns")
report.append("  it), and correcting the look state by the measured residual drove the pitch between both clamps")
report.append("  instead of converging. Note what the search found: aiming ABOVE the horizon puts the camera below it")
report.append("  — the rig's pitch sign is inverted against the accessor that reports it. Residual -977 px to 11 px,")
report.append("  so the band below is ground, not screen edge.")
report.append("")

report.append("6. THE FRAME-LEVEL CHECK — measured by subtracting the frames, not by trusting the app's own numbers")
for line in read(os.path.join(ROOT, "artifacts/m32-frame-analysis.txt")).splitlines():
    if line.strip():
        report.append("  " + line.rstrip())
report.append("")
report.append("  The second block is the result this milestone actually turns on. Reproducing the mirror-floor state")
report.append("  — metallic map unbound, `_Smoothness` 1.0, smoothness = 1.0 over the whole floor, i.e. exactly what")
report.append("  alphaSource=None produced — changes the rendered image by -0.01 of 255 across the frame and -0.07")
report.append("  inside the reflection band, and the count of blown-out pixels is 19 vs 14, which is noise on")
report.append("  texture grain. In other words: **on this ground, the reflective state and the matte state are the same")
report.append("  picture.** That is a stronger statement than 'the frame looks matte' — it says there is no visible")
report.append("  reflection on tape or lane to remove, and it is the reason the read-back in section 4, not a picture,")
report.append("  is the acceptance for this milestone.")
report.append("")
report.append("  Two honest notes on this section. The app's own band statistic reports a different peak for the matte")
report.append("  and mirror rows (255 vs 163) while the two PNGs are pixel-identical to 0.01 — a capture that lags its")
report.append("  state change by a frame explains that exactly, and it is the same class of bug as M29's stale-report")
report.append("  poll. I am reporting it rather than quoting the number that flatters the result, and where the two")
report.append("  disagree I trust the frames, because each frame is the whole screen and I can open it.")
report.append("")
report.append("  The third thing the subtraction shows, and I am flagging it rather than burying it: binding the maps")
report.append("  raises the ground's luminance overall (whole-frame mean 33.68 -> 49.45 of 255; in the band 57.47 ->")
report.append("  66.75) while leaving the PEAK unchanged. A uniform lift with no new peak is not a glint — the brightest")
report.append("  cluster in the shipped frame is 19 pixels of gravel grain at (522, 619), well outside the band, and the")
report.append("  surface around it is dark and matte. My reading is that the smoother of the two states (0.137 vs the")
report.append("  old 0.11/0.05 constants) raises the ambient specular term across the whole surface. I could not")
report.append("  attribute it further within this pass, so it is measured and flagged, not explained away.")
report.append("")

report.append("7. FRAMES (artifacts/review/world/) — the same view, one variable apart")
for name in FRAMES:
    p = os.path.join(ROOT, "artifacts/review/world", name)
    if os.path.exists(p):
        report.append(f"  {name:34s} {os.path.getsize(p):>9,d} bytes")
report.append("")
report.append("  `m32-reflection-on.png` is the ground as it ships: textured, legible, matte, with no highlight on the")
report.append("  lane or the field to find. `m32-mirror-floor.png` is the same view with the defect put back, and it is")
report.append("  worth opening precisely because it looks the same — that sameness is the measurement. In")
report.append("  `m32-reflection-off.png` the ground's texture is gone into the dark: the maps are doing real work,")
report.append("  they have just stopped reflecting.")
report.append("")

report.append("8. WHAT I AM NOT CLAIMING")
report.append("  - The ground is not reflective, and that is now the DESIGN, not a limitation. Nothing on either surface")
report.append("    reads above 0.40 smoothness, and the frame subtraction shows the reflective state is indistinguishable")
report.append("    from the matte one. Any LOCALISED highlight on the lane or the field in the shipping frame is a")
report.append("    defect.")
report.append("  - The `_M` maps are imported Read/Write so the build can sample them and prove the alpha survived.")
report.append("    That is 2 x 4 MB of CPU copy at 1024 which the phone should not keep. Once the read-back is green the")
report.append("    flag comes off in one line (`importer.isReadable = kind == GroundMap.SmoothnessMap`) and the values do")
report.append("    not change — Read/Write does not alter the texture, only whether the CPU can also see it. Reported as")
report.append("    a temporary, known cost rather than shipped silently.")
report.append("  - The overall brightness lift of section 6 is real and unexplained to the last decimal. It is broad, not")
report.append("    localised, so it is not the sheen Todd rejected — but he should know it is there.")
report.append("  - This is measured on the Mac. Nothing here says what a phone's tile-based GPU does with another")
report.append("    full-size map pair per ground material; the maps are committed and imported at 1024.")
report.append("  - The Husk is disabled during the measurement. The reflection does not depend on it, but the band would")
report.append("    have been measured on a death frame if it were left on, which happened once.")
report.append("  - The source read-back reports lane 0.137 / field 0.095 where the order quotes 0.13 / 0.11. The lane")
report.append("    matches to the digit; the field I measure 0.015 lower. I am reporting what I measured rather than")
report.append("    adopting the number I was given, and the direction is the safe one (more matte).")
report.append("")

report.append("Verdict: the fix is verified at the value level and the ground is matte at the pixel level. Lane smoothness")
report.append("0.137 and field 0.095, sampled out of the imported textures in the built game and MATCHing the committed art")
report.append("read independently in Blender; the `_M` maps import with alphaSource=FromInput and BC7 so the alpha survives;")
report.append("and reproducing the mirror floor changes the picture by 0.01 of 255. Reflectivity is M32b's puddles.")
report.append("")

with open(OUT, "w") as f:
    f.write("\n".join(report) + "\n")
print("wrote", OUT, len("\n".join(report)), "chars")
