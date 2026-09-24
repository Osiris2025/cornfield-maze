# DESIGN-TIME ONLY - this file is not compiled into the Unity build and never ships on
# device. It is the FSD's generator mirror (a port of Assets/Scripts/MazeGenerator.cs plus
# Mono System.Random), kept so every table and figure in docs/reference/CORN-FIELD-MAZE-FSD.md
# can be regenerated and re-verified. Runtime truth lives in C#, not here.
#!/usr/bin/env python3
"""Emits the markdown tables for FSD section 23 straight from the generator, so
the doc's numbers are code output rather than transcription."""
from corn_mechanics import (valid_seed, portal_pairs, node_chapter, size_of,
                            shape_of, seed_of, BIAS, MOON_PERIOD_IN_CROSSINGS,
                            STAR_DRIFT_DEG_PER_S, walk_time, WALK, RUN, MELT,
                            layout, invariants)
from corn_shapes import SHAPES, carve, components

SHAPE_USE = {13: "octagon", 15: "circle", 18: "diamond", 19: "ring",
             20: "blob", 21: "cross", 24: "wedge"}
REPS = {1: "square, 12 nodes of it", 12: "last square node", 13: "first shaped node",
        15: "", 17: "", 18: "", 19: "", 20: "", 21: "", 24: "", 25: ""}


def t_shapes():
    print("| Shape | Size | Node | Seed | Lanes | Route | % of lanes | Dead ends | "
          "Deepest | Gate | Parts | Invariants |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|")
    rows = [(1, "rect"), (9, "rect"), (13, "octagon"), (15, "circle"),
            (18, "diamond"), (19, "ring"), (20, "blob"), (21, "cross"),
            (24, "wedge"), (25, "blob")]
    for n, shape in rows:
        seed, L = valid_seed(n)
        m = SHAPES[shape](size_of(n), size_of(n)) if shape != "rect" else None
        _, _, _, linked = carve(size_of(n), size_of(n), seed, BIAS[node_chapter(n)], m)
        comps = len(components(linked))
        ok = invariants(L) and comps == 1
        print(f"| {shape} | {size_of(n)}×{size_of(n)} | L{n} | {seed} | {L['lanes']} | "
              f"{len(L['path'])} | {100.0*len(L['path'])/L['lanes']:.0f}% | "
              f"{len(L['dead'])} | {L['deepest']} | {L['gold']} | {comps} | "
              f"{'pass' if ok else 'FAIL'} |")


def t_sky():
    print("\n| Chapter | Size | Walk time | Run time | Moon period | Moon sweep per "
          "walk | Stars drift | Sweep per walk |")
    print("|---|---|---|---|---|---|---|---|")
    for ch in range(1, 6):
        n = next(x for x in range(1, 26) if node_chapter(x) == ch)
        _, L = valid_seed(n)
        tw, tr = walk_time(len(L["path"])), walk_time(len(L["path"]), RUN)
        period = MOON_PERIOD_IN_CROSSINGS[ch] * tw
        print(f"| {ch} | {size_of(n)}×{size_of(n)} | {tw:.0f} s | {tr:.0f} s | "
              f"{period:.0f} s | {360.0*tw/period:.0f}° | "
              f"{STAR_DRIFT_DEG_PER_S[ch]:.2f}°/s | "
              f"{STAR_DRIFT_DEG_PER_S[ch]*tw:.0f}° |")


def t_melt():
    print("\n| Chapter | Rain window (§6) | Walk | Run | Run with Faster Feet T3 | "
          "Icing Seal T3 survivable storm | Verdict at zero upgrades |")
    print("|---|---|---|---|---|---|---|")
    window = {1: 210, 2: 180, 3: 150, 4: 120, 5: 100}
    for ch in range(1, 6):
        n = next(x for x in range(1, 26) if node_chapter(x) == ch)
        _, L = valid_seed(n)
        tw, tr = walk_time(len(L["path"])), walk_time(len(L["path"]), RUN)
        fast = walk_time(len(L["path"]), 7.4 * 1.15)
        surv = window[ch] / 0.55
        verdict = ("walkable" if tw < window[ch] else
                   "must run" if tr < window[ch] else "needs upgrades")
        print(f"| {ch} | {window[ch]} s | {tw:.0f} s | {tr:.0f} s | {fast:.0f} s | "
              f"{surv:.0f} s | {verdict} |")


def t_portals():
    print("\n| Node | Shape | Route | Honest portals | Decoy portals | Saving | Cost |")
    print("|---|---|---|---|---|---|---|")
    for n in (13, 17, 20, 25):
        seed, L = valid_seed(n)
        ch = node_chapter(n)
        honest, decoy = {3: (1, 0), 4: (1, 1), 5: (2, 1)}[ch]
        pairs = portal_pairs(n, L, honest, decoy)
        hs = [s for k, _, _, s, _, _ in pairs if k == "honest"]
        ds = [s for k, _, _, s, _, _ in pairs if k == "decoy"]
        htxt = ", ".join(f"+{s} cells ({100.0*s/len(L['path']):.0f}%)" for s in hs)
        dtxt = ", ".join(f"−{abs(s)} cells ({100.0*abs(s)/len(L['path']):.0f}%)" for s in ds)
        print(f"| L{n} | {L['shape']} | {len(L['path'])} | {len(hs)} | {len(ds)} | "
              f"{htxt} | {dtxt} |")
    print("\nEvery pair above is generated from the real carve; each one is checked "
          "against the §23.4 constraints (min grid gap 8, min 4 cells clear of start "
          "and gate, both ends on lane).")


if __name__ == "__main__":
    t_shapes()
    t_sky()
    t_melt()
    t_portals()
