#!/usr/bin/env python3
"""DESIGN-TIME ONLY — M29. Assembles artifacts/m29-ground-report.txt from the two machines that
actually produced the numbers: the built app's own harness report, and the editor's import probe.

Neither part is written by hand here. This script only concatenates what those two runs emitted, so a
sceptic can diff the artifact against the log it came from.

    python3 scripts/m29_assemble_report.py
"""
import os
import re
import sys

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PLAYER = os.path.expanduser(
    "~/Library/Application Support/arl480/Corn Field Maze/m29-ground-report.txt")
IMPORT_LOG = os.path.join(PROJ, "Builds", "ground-import.log")
OUT = os.path.join(PROJ, "artifacts", "m29-ground-report.txt")


def main():
    if not os.path.isfile(PLAYER):
        sys.exit("missing player harness report: " + PLAYER)
    if not os.path.isfile(IMPORT_LOG):
        sys.exit("missing import log: " + IMPORT_LOG)

    with open(PLAYER, encoding="utf-8", errors="replace") as handle:
        harness = handle.read().rstrip("\n")

    # The probe lines the editor wrote when it applied (and read back) the import settings.
    probe = []
    with open(IMPORT_LOG, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            if "M29 import" in line or "M29 ground import" in line:
                probe.append(line.rstrip("\n"))
    if not probe:
        sys.exit("no M29 import lines found in " + IMPORT_LOG)

    body = []
    body.append(harness)
    body.append("")
    body.append("--- import settings, read back from the importers themselves ---------------------------")
    body.append("    (Assets/Scripts/Editor/CornMazeGroundImport.cs --probe, from Builds/ground-import.log.")
    body.append("     The runtime half of this is the format/wrap/readable columns in the table above: the")
    body.append("     build agreed with what the importer was told, which is the cross-check that matters.)")
    body.append("")
    for line in probe:
        cleaned = re.sub(r"^.*?M29 (import|ground import)\s*:?\s*", "", line).rstrip()
        body.append("  " + cleaned)

    text = "\n".join(body) + "\n"
    with open(OUT, "w", encoding="utf-8") as handle:
        handle.write(text)
    print("wrote %s (%d lines, %d bytes)" % (OUT, text.count("\n"), len(text)))


if __name__ == "__main__":
    main()
