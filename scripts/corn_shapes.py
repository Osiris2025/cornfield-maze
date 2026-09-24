# DESIGN-TIME ONLY - this file is not compiled into the Unity build and never ships on
# device. It is the FSD's generator mirror (a port of Assets/Scripts/MazeGenerator.cs plus
# Mono System.Random), kept so every table and figure in docs/reference/CORN-FIELD-MAZE-FSD.md
# can be regenerated and re-verified. Runtime truth lives in C#, not here.
#!/usr/bin/env python3
"""Shaped corn mazes: the same straight-biased DFS carver as the shipped
MazeGenerator, but restricted to a cell mask, so the field can be a square, an
octagon, a ring, a cross or an organic blob instead of always a rectangle.

Nothing here touches the Unity project; it is the FSD's own generator survey.
"""
import math

from corn_maze_carver import DotNetRandom, bfs_farthest, solution_path, dead_ends

DIRS = [(0, 2), (0, -2), (2, 0), (-2, 0)]


# ---------------------------------------------------------------- shapes
def m_rect(w, h):
    return lambda x, y: True


def m_octagon(w, h, cut=0.34):
    """Square with the corners chamfered off - the first 'wait, that's not a square'."""
    a, b = (w - 2) / 2.0, (h - 2) / 2.0
    k = cut * (a + b)
    return lambda x, y: (abs(x - w // 2) <= a and abs(y - h // 2) <= b
                         and abs(x - w // 2) + abs(y - h // 2) <= a + b - k)


def m_circle(w, h, fill=1.0):
    rx, ry = (w - 2) / 2.0 * fill, (h - 2) / 2.0 * fill
    return lambda x, y: ((x - w // 2) / rx) ** 2 + ((y - h // 2) / ry) ** 2 <= 1.0


def m_diamond(w, h):
    rx, ry = (w - 2) / 2.0, (h - 2) / 2.0
    return lambda x, y: abs(x - w // 2) / rx + abs(y - h // 2) / ry <= 1.0


def m_cross(w, h, arm=0.30):
    a, b = (w - 2) / 2.0, (h - 2) / 2.0
    ax, ay = arm * a, arm * b
    return lambda x, y: abs(x - w // 2) <= ax or abs(y - h // 2) <= ay


def m_ring(w, h, inner=0.46, spokes=1):
    """Hollow ring - the courtyard you can see across but must walk around.
    `spokes` bridges keep it one connected field."""
    rx, ry = (w - 2) / 2.0, (h - 2) / 2.0
    irx, iry = inner * rx, inner * ry
    def inside(x, y):
        dx, dy = x - w // 2, y - h // 2
        outer = (dx / rx) ** 2 + (dy / ry) ** 2 <= 1.0
        innr = (dx / irx) ** 2 + (dy / iry) ** 2 <= 1.0
        if outer and not innr:
            return True
        if not innr:
            return False
        for s in range(spokes):
            ang = math.pi * s / spokes
            # a straight bridge through the courtyard, +/- half a cell wide
            ux, uy = math.cos(ang), math.sin(ang)
            t = dx * ux + dy * uy
            perp = abs(-dx * uy + dy * ux)
            if (-irx * 1.2 <= t <= irx * 1.2) and perp < 0.9:
                return True
        return False
    return inside


def m_blob(w, h, lobes=3, amp=0.16, phase=0.0):
    """Organic field edge: the radius wobbles with angle."""
    rx, ry = (w - 2) / 2.0, (h - 2) / 2.0
    def inside(x, y):
        dx, dy = x - w // 2, y - h // 2
        th = math.atan2(dy / ry if ry else 0, dx / rx if rx else 0)
        r = math.hypot(dx / rx, dy / ry)
        return r <= 1.0 + amp * math.sin(lobes * th + phase)
    return inside


def m_wedge(w, h, span=0.68, rot=0.0):
    """A pie slice, rotated - the shape that stops matching last level's map."""
    rx, ry = (w - 2) / 2.0, (h - 2) / 2.0
    def inside(x, y):
        dx, dy = (x - w // 2) / rx, (y - h // 2) / ry
        if math.hypot(dx, dy) > 1.0:
            return False
        th = (math.atan2(dy, dx) - rot) % (2 * math.pi)
        return th <= span * 2 * math.pi
    return inside


SHAPES = {
    "rect": m_rect,
    "octagon": m_octagon,
    "circle": m_circle,
    "diamond": m_diamond,
    "cross": m_cross,
    "ring": m_ring,
    "blob": m_blob,
    "wedge": m_wedge,
}


# ---------------------------------------------------------------- carver
def carve(width, height, seed, straight_bias, mask=None):
    """DFS carver over the cell mask. Cells with no in-mask neighbour are left
    as corn (they cannot be linked); everything reachable is carved."""
    rng = DotNetRandom(seed)
    wall = [[True] * height for _ in range(width)]
    inside = (mask or m_rect(width, height))
    cells = set()
    for x in range(1, width - 1, 2):
        for y in range(1, height - 1, 2):
            if inside(x, y):
                cells.add((x, y))
    linked = {c for c in cells
              if any((c[0] + d[0], c[1] + d[1]) in cells for d in DIRS)}
    if not linked:
        return wall, (1, height - 2), cells, linked
    start = min(linked, key=lambda c: (-c[1], c[0]))   # (1, Height-2), as in the C#
    wall[start[0]][start[1]] = False
    stack, last_dir = [start], (0, 0)
    while stack:
        cell = stack[-1]
        neighbours = []
        for d in DIRS:
            nxt = (cell[0] + d[0], cell[1] + d[1])
            if nxt in linked and wall[nxt[0]][nxt[1]]:
                neighbours.append((nxt, d))
        if not neighbours:
            stack.pop()
            continue
        if last_dir != (0, 0):
            straight = [n for n in neighbours if n[1] == last_dir]
            if straight and rng.next_double() < straight_bias:
                pick = straight[rng.next(len(straight))]
            else:
                pick = neighbours[rng.next(len(neighbours))]
        else:
            pick = neighbours[rng.next(len(neighbours))]
        wall[pick[0][0]][pick[0][1]] = False
        wall[cell[0] + pick[1][0] // 2][cell[1] + pick[1][1] // 2] = False
        stack.append(pick[0])
        last_dir = pick[1]
    return wall, start, cells, linked


def components(linked):
    seen, comps = set(), []
    for c in sorted(linked):
        if c in seen:
            continue
        stack, comp = [c], []
        seen.add(c)
        while stack:
            cur = stack.pop()
            comp.append(cur)
            for d in DIRS:
                n = (cur[0] + d[0], cur[1] + d[1])
                if n in linked and n not in seen:
                    seen.add(n)
                    stack.append(n)
        comps.append(comp)
    return comps


def survey(width, height, seed, bias, name, mask=None):
    wall, start, cells, linked = carve(width, height, seed, bias, mask)
    comps = components(linked)
    lanes = sum(1 for x in range(width) for y in range(height) if not wall[x][y])
    if comps and len(comps[0]) == 0:
        return None
    gold, _ = bfs_farthest(wall, width, height, start)
    path = solution_path(wall, width, height, start, gold)
    de = dead_ends(wall, width, height)
    deepest = max([d for _, d in de], default=0)
    reach = len(path)
    grid = abs(gold[0] - start[0]) + abs(gold[1] - start[1])
    return dict(shape=name, size=f"{width}x{height}", seed=seed, comps=len(comps),
                lanes=lanes, route=reach, dead=len(de), deepest=deepest,
                gate=gold, grid=grid,
                route_pct=round(100.0 * reach / max(lanes, 1)),
                ok=(len(comps) == 1 and grid >= 8 and len(de) >= 5
                    and deepest >= 12 and reach >= 0.4 * lanes))


if __name__ == "__main__":
    print(f"{'shape':9} {'size':9} {'parts':5} {'lanes':5} {'route':5} {'%':4} "
          f"{'dends':5} {'deep':4} {'gate':4} {'grid':4} ok")
    for size in (21, 29, 37):
        for name in ("rect", "octagon", "circle", "diamond", "cross", "ring",
                     "blob", "wedge"):
            seed = 1661 + 7919 * (size // 10)
            for s in (seed, seed + 1, seed + 2, seed + 3, seed + 4):
                r = survey(size, size, s, 0.56, name, SHAPES[name](size, size))
                if r and r["ok"]:
                    print(f"{name:9} {r['size']:9} {r['comps']:5} {r['lanes']:5} "
                          f"{r['route']:5} {r['route_pct']:4} {r['dead']:5} "
                          f"{r['deepest']:4} {str(r['gate']):6} {r['grid']:4} {r['ok']}")
                    break
            else:
                r = survey(size, size, seed, 0.56, name, SHAPES[name](size, size))
                print(f"{name:9} {r['size']:9} {r['comps']:5} {r['lanes']:5} "
                      f"{r['route']:5} {r['route_pct']:4} {r['dead']:5} "
                      f"{r['deepest']:4} {str(r['gate']):6} {r['grid']:4} FAIL(no seed)")
