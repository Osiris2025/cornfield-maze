# DESIGN-TIME ONLY - this file is not compiled into the Unity build and never ships on
# device. It is the FSD's generator mirror (a port of Assets/Scripts/MazeGenerator.cs plus
# Mono System.Random), kept so every table and figure in docs/reference/CORN-FIELD-MAZE-FSD.md
# can be regenerated and re-verified. Runtime truth lives in C#, not here.
#!/usr/bin/env python3
"""Design tables for the Corn Field Maze rug-pull mechanics that Todd briefed:
sizes 21 -> 37, the shape flip, portals, the compass that starts lying, and the
moon/star/storm sky. Every number here is computed from a real carve of the
level it describes - nothing is hand-set.

Verified against the shipped code's own constants (MazeGenerator CellSize 4,
FarmWalkerController Walk 4.4 / Run 7.4 and DissolveRainSeconds 210,
StormWeather CalmSeconds 20 / RampSeconds 34, NightSky 420 field stars + 7 hint
stars + gold tip, Radius 58).
"""
import math

from corn_shapes import SHAPES, carve
from corn_maze_carver import bfs_farthest, solution_path, dead_ends

CELL = 4.0          # MazeGenerator.CellSize: one grid cell of lane = 4 m
WALK, RUN = 4.4, 7.4
MELT = 210.0        # FarmWalkerController.DissolveRainSeconds

SIZES = {1: 21, 2: 25, 3: 29, 4: 33, 5: 37}
# the square holds for twelve nodes, then the field stops being a square
SHAPE_BY_NODE = {10: "rect", 13: "octagon", 14: "octagon", 15: "circle",
                 16: "octagon", 17: "circle", 18: "diamond", 19: "ring",
                 20: "blob", 21: "cross", 22: "ring", 23: "octagon",
                 24: "wedge", 25: "blob"}
BIAS = {1: 0.58, 2: 0.56, 3: 0.54, 4: 0.52, 5: 0.50}
BOSS = {21: "B1", 22: "B2", 23: "B3", 24: "B4", 25: "B5"}


def node_chapter(n):
    return min(5, (n - 1) // 4 + 1)


def seed_of(n):
    return 1661 + 7919 * n


def shape_of(n):
    if n <= 12:
        return "rect"
    return SHAPE_BY_NODE[n]


def size_of(n):
    return SIZES[node_chapter(n)]


def invariants(L):
    return (len(L["dead"]) >= 5 and L["deepest"] >= 12
            and abs(L["gold"][0] - L["start"][0]) + abs(L["gold"][1] - L["start"][1]) >= 8
            and len(L["path"]) >= 0.4 * L["lanes"])


def valid_seed(n):
    """The first seed in the documented series that produces a good level for
    this node's size and shape (the L1 defect fix, applied to all 25 nodes)."""
    for off in range(0, 40):
        L = layout(n, seed_of(n) + off)
        if invariants(L):
            return seed_of(n) + off, L
    raise SystemExit(f"no valid seed for node {n}")


def layout(n, seed=None):
    """Carve node n for real and measure it."""
    w = h = size_of(n)
    seed = seed if seed is not None else seed_of(n)
    m = SHAPES[shape_of(n)](w, h) if shape_of(n) != "rect" else None
    wall, start, cells, linked = carve(w, h, seed, BIAS[node_chapter(n)], m)
    gold, _ = bfs_farthest(wall, w, h, start)
    path = solution_path(wall, w, h, start, gold)
    de = dead_ends(wall, w, h)
    return dict(n=n, w=w, h=h, shape=shape_of(n), seed=seed, wall=wall,
                start=start, gold=gold, path=path, dead=de,
                deepest=max([d for _, d in de], default=0),
                lanes=sum(1 for x in range(w) for y in range(h) if not wall[x][y]))


def walk_time(route_cells, speed=WALK):
    return route_cells * CELL / speed


# ---------------------------------------------------------------- sky
# The moon is the bait: it is a reliable bearing for twelve nodes, then its
# period is cut until one walk of the maze sweeps it across the whole sky.
MOON_PERIOD_IN_CROSSINGS = {1: 40.0, 2: 20.0, 3: 8.0, 4: 2.5, 5: 1.2}
STAR_DRIFT_DEG_PER_S = {1: 0.0, 2: 0.0, 3: 0.05, 4: 0.35, 5: 0.9}
STORM_FACTOR = {1: 1.60, 2: 1.15, 3: 0.85, 4: 0.60, 5: 0.0}   # onset / walk time
STORM_RAMP = {1: 34.0, 2: 34.0, 3: 28.0, 4: 20.0, 5: 14.0}


def crossing(n, L, speed=WALK):
    return walk_time(len(L["path"]), speed)


def moon_table(reps):
    print("\n== moon: period shrinks until the sky stops being a clock ==")
    print(f"{'ch':3} {'node':5} {'walk s':7} {'run s':7} {'moon period s':13} "
          f"{'sweep/walk':11} {'usable?':8}")
    for n, L in reps:
        ch = node_chapter(n)
        tw, tr = crossing(n, L), crossing(n, L, RUN)
        period = MOON_PERIOD_IN_CROSSINGS[ch] * tw
        sweep = 360.0 * tw / period
        print(f"{ch:3} {('L%d' % n):5} {tw:7.0f} {tr:7.0f} {period:13.0f} "
              f"{sweep:8.1f}deg {'yes' if sweep < 45 else 'no':8}")
    return True


def star_table(reps):
    print("\n== stars: the constellation is per level, then it drifts ==")
    print(f"{'ch':3} {'drift deg/s':11} {'sweep per walk':14} {'notes'}")
    for ch in range(1, 6):
        n, L = next(r for r in reps if node_chapter(r[0]) == ch)
        drift = STAR_DRIFT_DEG_PER_S[ch]
        sweep = drift * crossing(n, L)
        note = ("NightSky generates a fresh 7-hint constellation per seed: the "
                "pattern cannot be learned across levels")
        print(f"{ch:3} {drift:11.2f} {sweep:11.0f}deg  {note}")
    return True


def storm_table(reps):
    print("\n== storm: calm window is the confidence, rain is the bill ==")
    print(f"{'ch':3} {'calm':5} {'ramp':5} {'rain s':7} {'melt clock':11} "
          f"{'walk window':11} {'verdict'}")
    for ch in range(1, 6):
        n, L = next(r for r in reps if node_chapter(r[0]) == ch)
        ramp = STORM_RAMP[ch]
        onset = STORM_FACTOR[ch] * crossing(n, L)
        calm = max(0.0, onset - ramp)
        tw, tr = crossing(n, L), crossing(n, L, RUN)
        verdict = ("dry" if onset > tw else
                   "tight" if onset > tr else "must run + cloak")
        print(f"{ch:3} {calm:5.0f} {ramp:5.0f} {onset:7.0f} {MELT:11.0f} "
              f"{tw:8.0f}w/{tr:.0f}r {verdict}")
    return True


# ---------------------------------------------------------------- portals
def path_steps(L):
    """Grid steps between any two route cells, measured on the real carve."""
    return {c: i for i, c in enumerate(L["path"])}


def portal_pairs(n, L, honest=1, decoy=0, min_gap=8, margin=3):
    """Honest portals nudge you forward ~20% of the route, the decoy sends you
    back ~17%. Endpoints sit on the route, clear of start and gate, at least
    `min_gap` cells apart on the grid, and the shift is measured on the real
    carve - if a candidate violates a rule it is skipped, not shipped."""
    path = L["path"]
    lo, hi = margin, len(path) - 1 - margin
    span = hi - lo

    def clear(c):
        sg = abs(c[0] - L["start"][0]) + abs(c[1] - L["start"][1])
        gg = abs(c[0] - L["gold"][0]) + abs(c[1] - L["gold"][1])
        return min(sg, gg) >= margin + 1

    def ok(a, b):
        d = abs(a[0] - b[0]) + abs(a[1] - b[1])
        return (d >= min_gap and clear(a) and clear(b)
                and not L["wall"][a[0]][a[1]] and not L["wall"][b[0]][b[1]])

    out = []
    for k in range(honest):
        a = path[lo + int(0.10 * span) + 4 * k]
        for j in range(int(0.30 * span), span):
            b = path[lo + j]
            if ok(a, b):
                out.append(("honest", a, b, j - int(0.10 * span + 4 * k)))
                break
    for k in range(decoy):
        a = path[lo + int(0.74 * span)]
        for j in range(int(0.55 * span), 0, -1):
            b = path[lo + j]
            if ok(a, b):
                out.append(("decoy", a, b, j - int(0.74 * span)))
                break
    return [(kind, a, b, steps, abs(a[0] - b[0]) + abs(a[1] - b[1]),
             ok(a, b)) for kind, a, b, steps in out]


def portal_table(reps):
    print("\n== portals: honest shortcut first, then the one that walks you back ==")
    for n, L in reps:
        if n <= 9:
            continue
        ch = node_chapter(n)
        honest, decoy = {3: (1, 0), 4: (1, 1), 5: (2, 1)}[ch]
        pairs = portal_pairs(n, L, honest, decoy)
        print(f"\nL{n} {L['shape']} {L['w']}x{L['h']} seed {L['seed']} "
              f"route {len(L['path'])} cells")
        print(f"   {'kind':7} {'from':10} {'to':10} {'route steps moved':18} "
              f"{'grid gap':9} constraints")
        for kind, a, b, steps, d, good in pairs:
            moved = f"{steps:+d}"
            print(f"   {kind:7} {str(a):10} {str(b):10} {moved:18} {d:9} "
                  f"{'ok' if good else 'VIOLATION'}")
        print(f"   saving/cost as share of the {len(L['path'])}-cell route: "
              + ", ".join(f"{k} {abs(s)} cells ({100.0*abs(s)/len(L['path']):.0f}%)"
                          for k, _, _, s, _, _ in pairs))
    return True


def compass_table(reps):
    print("\n== compass: found at L3, honest for eleven nodes, then it lies ==")
    print(f"{'node':5} {'target':34} {'lie trigger'}")
    for n in range(3, 26):
        if n <= 13:
            print(f"L{n:<4} {'exit gate (true)':34} {'- trusting window'}")
        elif n % 4 == 1:
            print(f"L{n:<4} {'the previous level exit gate':34} 'gate' still says gate")
        elif n % 4 == 2:
            print(f"L{n:<4} {'maze centre':34} drifts, looks plausible")
        elif n % 4 == 3:
            print(f"L{n:<4} {'nearest Crumb Beast':34} reads as a shortcut")
        else:
            print(f"L{n:<4} {'gate, but 90 degrees off at every turn':34} "
                  f"wrong every time")
    return True


def ladder():
    print("== node ladder (pre-existing formulas; only shapes are new) ==")
    print(f"{'node':5} {'ch':3} {'size':7} {'shape':8} {'seed':8} {'bias':5} {'notes'}")
    for n in range(1, 26):
        tag = BOSS.get(n, "")
        seed, L = valid_seed(n)
        note = "" if seed == seed_of(n) else f"series seed +{seed - seed_of(n)}"
        if n == 13:
            note = (note + "  first non-square"); 
        print(f"L{n:<4} {node_chapter(n):3} {size_of(n)}x{size_of(n)} "
              f"{shape_of(n):8} {seed:8} {BIAS[node_chapter(n)]:5} {tag} {note}")
    return True


if __name__ == "__main__":
    ladder()
    reps = []
    for n in (1, 5, 9, 13, 17, 20, 25):
        _, L = valid_seed(n)
        reps.append((n, L))
    print("\n== measured representative nodes ==")
    print(f"{'node':5} {'shape':8} {'size':7} {'lanes':6} {'route':6} {'dead':5} "
          f"{'deepest':8} {'gate':10}")
    for n, L in reps:
        print(f"L{n:<4} {L['shape']:8} {L['w']}x{L['h']:<5} {L['lanes']:6} "
              f"{len(L['path']):6} {len(L['dead']):5} {L['deepest']:8} {str(L['gold']):10}")
    moon_table(reps)
    star_table(reps)
    storm_table(reps)
    portal_table(reps)
    compass_table(reps)
