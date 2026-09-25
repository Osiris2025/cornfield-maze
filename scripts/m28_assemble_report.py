#!/usr/bin/env python3
"""DESIGN-TIME ONLY — never runs in the game, never ships.

Assembles artifacts/m28-scarecrow-report.txt from three things that were actually produced on this machine:

  1. the app harness's own report (m28-scarecrow-report.txt in the player's persistent data)
  2. M23's throw harness, re-run against the new creature — the hit-volume regression
  3. the silhouette verdict, which is a judgement of the frame and says so

Run from the project root:
    python3 scripts/m28_assemble_report.py
"""
import os
import pathlib
import shutil

ROOT = pathlib.Path(__file__).resolve().parent.parent
PLAYER = pathlib.Path.home() / "Library/Application Support/arl480/Corn Field Maze"
OUT = ROOT / "artifacts/m28-scarecrow-report.txt"

VERDICT = [
    "",
    "SILHOUETTE TEST — the acceptance test, and the answer is yes.",
    "artifacts/review/world/m28-scarecrow-silhouette.png: 12 m out at night (night01=1.00), every renderer",
    "swapped to a flat black unlit material, so nothing but the shape is left. The shape reads as a",
    "scarecrow: a box head set on a slight tilt, a thin stake under it, and the crossbar straight out to",
    "both sides at shoulder height. The T is unmistakable at 12 m and it is unmistakable as a stake with a",
    "sack on it — not a sphere, not a person, not a blob. It also survives being small in frame, which is",
    "the state the player actually meets it in.",
    "",
    "THE FRAMES",
    "artifacts/review/world/m28-scarecrow-lane.png      dusk, at a lane's end, walking toward the camera.",
    "                                                   Cross, tilted sack head, dark sockets, tattered coat,",
    "                                                   post showing under the torn hem, straw at the cuffs.",
    "                                                   (M29's ragged lane margin is in this frame too.)",
    "artifacts/review/world/m28-scarecrow-close.png     the head: two hollow sockets with the faint glow,",
    "                                                   a seam of small dark stitches across the sack, and",
    "                                                   the whole head set on a wrong tilt.",
    "artifacts/review/world/m28-scarecrow-silhouette.png the acceptance test, described above.",
    "",
    "WHAT THE FRAMES COST TO GET, because the first three runs were wrong and the report says so:",
    "  - run 1: the harness teleported the player, the creature caught him, and every frame carried a",
    "    'Caught by the Husk' overlay while the lurch measured 0.00 m/s on a creature that had stopped.",
    "  - run 2: the capture waited on a file that already existed from run 1, so it reported run 1's",
    "    pictures as run 2's evidence. Frames are now checked for freshness, not existence.",
    "  - run 3: the close-up was taken from behind the creature and read as a headless pole.",
    "  - run 4: the glow was buried behind the socket box, where nothing could see it.",
    "  - run 5: the seam read as a toothy grin; thirteen small stitches replaced nine large ones.",
    "  - run 6: the walk was measured with the player parked 88 m off, where the creature will not path;",
    "    it stood still for 5719 of 5723 frames. The measured window is a real 16-22 m chase.",
]


def main():
    lines = []
    app_report = PLAYER / "m28-scarecrow-report.txt"
    if app_report.exists():
        lines.append("# M28 — the chaser becomes a scarecrow (owner: Dough; Furrow on the hit volume and walk)")
        lines.append("")
        lines.append("## Measured on the built Mac app")
        lines.append("")
        lines.append(app_report.read_text().rstrip())
    else:
        lines.append("MISSING: the app harness report was never written.")

    m23 = PLAYER / "m23-throw-report.txt"
    if m23.exists():
        lines += ["", "## M23's cob, re-run against the new creature (the hit-volume regression)", ""]
        for line in m23.read_text().splitlines():
            if any(k in line for k in ("husk visible height", "husk hit:", "husk stagger:",
                                       "ground bought", "after three hits", "range: level-ground")):
                lines.append(line)
        # Refresh the milestone artifact too: it is the same harness, against the same creature, and a
        # stale copy of it in artifacts/ is how a reader ends up checking yesterday's numbers.
        shutil.copyfile(m23, ROOT / "artifacts/m23-throw-report.txt")

    lines += VERDICT
    OUT.write_text("\n".join(lines) + "\n")
    print("wrote", OUT, OUT.stat().st_size, "bytes")


if __name__ == "__main__":
    main()
