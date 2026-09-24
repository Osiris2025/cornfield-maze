# DESIGN-TIME ONLY - this file is not compiled into the Unity build and never ships on
# device. It is the FSD's generator mirror (a port of Assets/Scripts/MazeGenerator.cs plus
# Mono System.Random), kept so every table and figure in docs/reference/CORN-FIELD-MAZE-FSD.md
# can be regenerated and re-verified. Runtime truth lives in C#, not here.
#!/usr/bin/env python3
"""Sample maze renderer for the Corn Field Maze FSD.

Faithful port of Assets/Scripts/MazeGenerator.cs (same neighbour order, same
straight-bias draw order, same FarthestCell BFS with the same tie-breaking) plus
a port of Mono/.NET System.Random's subtractive generator, so a maze printed here
for a given (size, seed, bias) is the maze Unity would build for that seed.

Cross-check: the shipped maze (seed 1661, 25x21) is documented in the C# comment as
"long solution path plus several deep dead ends (one ~25 cells)". If the port is
faithful that property reproduces; if the RNG port is wrong, it will not.
"""
import math, random

MBIG = 2147483647
MSEED = 161803398


class DotNetRandom:
    """System.Random (Knuth subtractive) as Mono/Unity implement it."""

    def __init__(self, seed):
        self.seed_array = [0] * 56
        subtraction = seed if seed != -2147483648 else 2147483647
        mj = MSEED - abs(subtraction)
        self.seed_array[55] = mj
        mk = 1
        for i in range(1, 55):
            ii = (21 * i) % 55
            self.seed_array[ii] = mk
            mk = mj - mk
            if mk < 0:
                mk += MBIG
            mj = self.seed_array[ii]
        for _ in range(4):
            for i in range(1, 56):
                self.seed_array[i] -= self.seed_array[1 + (i + 30) % 55]
                if self.seed_array[i] < 0:
                    self.seed_array[i] += MBIG
        self.inext = 0
        self.inextp = 21

    def sample(self):
        self.inext += 1
        if self.inext >= 56:
            self.inext = 1
        self.inextp += 1
        if self.inextp >= 56:
            self.inextp = 1
        ret = self.seed_array[self.inext] - self.seed_array[self.inextp]
        if ret == MBIG:
            ret -= 1
        if ret < 0:
            ret += MBIG
        self.seed_array[self.inext] = ret
        return ret * (1.0 / MBIG)

    def next_double(self):
        return self.sample()

    def next(self, n):
        return int(self.sample() * n)


UP, DOWN, LEFT, RIGHT = (0, 1), (0, -1), (-1, 0), (1, 0)
DIRS = [(0, 2), (0, -2), (2, 0), (-2, 0)]          # nonzero maze steps
STEPS = [UP, DOWN, LEFT, RIGHT]                    # C# build order for BFS


def build(width, height, seed, straight_bias):
    rng = DotNetRandom(seed)
    wall = [[True] * height for _ in range(width)]
    start = (1, height - 2)
    wall[start[0]][start[1]] = False

    stack = [start]
    last_dir = (0, 0)
    while stack:
        cell = stack[-1]
        neighbours = []
        for d in DIRS:
            nxt = (cell[0] + d[0], cell[1] + d[1])
            if 0 < nxt[0] < width - 1 and 0 < nxt[1] < height - 1 and wall[nxt[0]][nxt[1]]:
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
    return wall, start


def bfs_farthest(wall, width, height, start):
    dist = {start: 0}
    queue = [start]
    farthest, best = start, 0
    while queue:
        cell = queue.pop(0)
        if dist[cell] > best:
            best, farthest = dist[cell], cell
        for s in STEPS:
            nxt = (cell[0] + s[0], cell[1] + s[1])
            if not (0 <= nxt[0] < width and 0 <= nxt[1] < height):
                continue
            if wall[nxt[0]][nxt[1]] or nxt in dist:
                continue
            dist[nxt] = dist[cell] + 1
            queue.append(nxt)
    return farthest, dist


def solution_path(wall, width, height, start, gold):
    """BFS parent map anchored at gold, walked from start (mirrors BuildGoldGuidance)."""
    parent = {gold: gold}
    queue = [gold]
    while queue:
        cell = queue.pop(0)
        for s in STEPS:
            nxt = (cell[0] + s[0], cell[1] + s[1])
            if not (0 <= nxt[0] < width and 0 <= nxt[1] < height):
                continue
            if wall[nxt[0]][nxt[1]] or nxt in parent:
                continue
            parent[nxt] = cell
            queue.append(nxt)
    if start not in parent:
        return []
    path, cursor = [], start
    while cursor != gold:
        path.append(cursor)
        cursor = parent[cursor]
    path.append(gold)
    return path


def dead_ends(wall, width, height):
    """Path cells with exactly one path neighbour, with the corridor depth behind each."""
    out = []
    for x in range(width):
        for y in range(height):
            if wall[x][y]:
                continue
            n = sum(1 for s in STEPS
                    if 0 <= x + s[0] < width and 0 <= y + s[1] < height
                    and not wall[x + s[0]][y + s[1]])
            if n == 1:
                # walk back until a junction
                prev, cur, depth = (x, y), (x, y), 0
                while True:
                    nb = [(cur[0] + s[0], cur[1] + s[1]) for s in STEPS
                          if 0 <= cur[0] + s[0] < width and 0 <= cur[1] + s[1] < height
                          and not wall[cur[0] + s[0]][cur[1] + s[1]]]
                    nxt = [c for c in nb if c != prev]
                    if len(nb) > 2 or not nxt:
                        break
                    prev, cur = cur, nxt[0]
                    depth += 1
                out.append(((x, y), depth))
    return out


def stats(wall, width, height, seed, bias, level):
    start = (1, height - 2)
    gold, dist = bfs_farthest(wall, width, height, start)
    path = solution_path(wall, width, height, start, gold)
    de = dead_ends(wall, width, height)
    cells = sum(1 for x in range(width) for y in range(height) if not wall[x][y])
    need = math.ceil(0.18 * cells)
    deep = [d for _, d in de if d >= 6]
    return dict(start=start, gold=gold, path=path, dead=de, cells=cells,
                need=need, deep=len(deep), longest=max([d for _, d in de] or [0]),
                dist=dist, seed=seed)


if __name__ == "__main__":
    # --- cross-check the port against the shipped maze's documented property ---
    w, h = 25, 21
    wall, _ = build(w, h, 1661, 0.58)
    s = stats(wall, w, h, 1661, 0.58, 9)
    print("SHIPPED MAZE PORT CHECK  seed 1661  25x21  bias 0.58")
    print("  solution length  :", len(s["path"]), "cells")
    print("  dead ends        :", len(s["dead"]), " longest", s["longest"],
          " deep(>=6):", s["deep"], " of", len(s["dead"]))
    print("  C# comment claims: long solution path + several deep dead ends, one ~25 cells")
    print()
    # --- the FSD's level table, generated from a script (never by hand) ---
    print("FSD LEVEL TABLE (generated)")
    print(f"  {'node':4} {'maze':7} {'seed':7} {'cells':5} {'sol':4} {'dead':5} {'need':5} "
          f"{'deep':5} {'longest':7}")
    for L in range(1, 21):
        ch = (L - 1) // 4 + 1
        W, H = 21 + 2 * (ch - 1), 17 + 2 * (ch - 1)
        seed = 1661 + 7919 * L
        bias = [0.58, 0.56, 0.54, 0.52, 0.50][ch - 1]
        wl, _ = build(W, H, seed, bias)
        st = stats(wl, W, H, seed, bias, L)
        print(f"  L{L:<3} {W}x{H:<4} {seed:<7} {st['cells']:<5} {len(st['path']):<4} "
              f"{len(st['dead']):<5} {st['need']:<5} {st['deep']:<5} {st['longest']:<7}")
