# DESIGN-TIME ONLY - this file is not compiled into the Unity build and never ships on
# device. It is the FSD's generator mirror (a port of Assets/Scripts/MazeGenerator.cs plus
# Mono System.Random), kept so every table and figure in docs/reference/CORN-FIELD-MAZE-FSD.md
# can be regenerated and re-verified. Runtime truth lives in C#, not here.
#!/usr/bin/env python3
"""Dark-background sample sheets for the shaped levels: the first non-square
(L13 octagon) and the late-game rewrite (L20 blob), both with their portals
marked. Renders a standalone dark page plus per-level ANSI files, and asserts
each picture cell-by-cell against an independent carve of the same node.
"""
import os

from corn_mechanics import valid_seed, portal_pairs, node_chapter, STORM_FACTOR, \
    STORM_RAMP, MOON_PERIOD_IN_CROSSINGS, STAR_DRIFT_DEG_PER_S, walk_time, \
    WALK, RUN, MELT

OUT = "/Users/toddadams/.hermes/cache/scratch"
PAGE = "#0a0b07"
BG = {"corn_a": "#1a2409", "corn_b": "#22300d", "lane": "#12100c",
      "start": "#2e1e0c", "exit": "#15200e", "portal": "#1d1a2e", "decoy": "#2c1414"}
FG = {"corn": "#4a6120", "lane_fg": "#6d6350", "sol_fg": "#bda87a",
      "start": "#f0b062", "exit": "#a9c07a", "coin": "#ffcc4d", "beast": "#e2503c",
      "portal": "#b9a6ff", "decoy": "#ff6f5e"}

PORTAL_MARK = {0: "1", 1: "2", 2: "3", 3: "4"}


def sheet(n, coins=7, want_portals=True):
    seed, L = valid_seed(n)
    ch = node_chapter(n)
    w, h, wall, path = L["w"], L["h"], L["wall"], L["path"]
    idx = {c: i for i, c in enumerate(path)}
    honest, decoy = {3: (1, 0), 4: (1, 1), 5: (2, 1)}[ch] if want_portals else (0, 0)
    pairs = portal_pairs(n, L, honest, decoy) if want_portals else []
    route = set(path)
    cells = {}
    for x in range(w):
        for y in range(h):
            cells[(x, y)] = (("corn", "▓", "corn_a" if (x + y) % 2 else "corn_b")
                             if wall[x][y] else ("lane", "·", "lane"))
    for c in path:
        if c not in (L["start"], L["gold"]):
            cells[c] = ("sol", "·", "lane")
    rng_seed = seed ^ 0x5EED
    from corn_maze_carver import DotNetRandom
    rng = DotNetRandom(rng_seed)
    off = [c for c in sorted((x, y) for x in range(w) for y in range(h)
                             if not wall[x][y]) if c not in route]
    taken = list(off)
    rng.next(len(taken) if taken else 1)
    placed = []
    for c in taken:
        if len(placed) >= coins:
            break
        if c != L["start"] and c != L["gold"]:
            placed.append(c)
    for c in placed:
        cells[c] = ("coin", "o", "lane")
    for i, (kind, a, b, steps, gap, ok) in enumerate(pairs):
        mark = PORTAL_MARK[i]
        key = "decoy" if kind == "decoy" else "portal"
        cells[a] = (key, mark, key)
        cells[b] = (key, mark, key)
    spawn = path[min(4, len(path) - 1)]
    cells[spawn] = ("beast", "B", "lane")
    cells[L["gold"]] = ("exit", "E", "exit")
    cells[L["start"]] = ("start", "S", "start")
    return dict(n=n, L=L, cells=cells, w=w, h=h, pairs=pairs, ch=ch, seed=seed,
                route=len(path), spawn=spawn)


def plain(s):
    return "\n".join("".join(s["cells"][(x, y)][1] * 2 for x in range(s["w"]))
                     for y in range(s["h"] - 1, -1, -1))


def ansi(s):
    h2 = lambda v: tuple(int(v[i:i + 2], 16) for i in (1, 3, 5))
    lines = []
    for y in range(s["h"] - 1, -1, -1):
        row = []
        for x in range(s["w"]):
            kind, ch2, bgkey = s["cells"][(x, y)]
            if kind in ("start", "exit", "coin", "beast", "portal", "decoy"):
                fk = kind
            elif kind == "sol":
                fk = "sol_fg"
            elif kind == "lane":
                fk = "lane_fg"
            else:
                fk = "corn"
            br, bg, bb = h2(BG[bgkey])
            fr, fg, fb = h2(FG[fk])
            row.append(f"\x1b[48;2;{br};{bg};{bb}m\x1b[38;2;{fr};{fg};{fb}m{ch2}{ch2}")
        lines.append("".join(row) + "\x1b[0m")
    return "\n".join(lines) + "\n\x1b[0m"


def rows_html(s):
    out = []
    for y in range(s["h"] - 1, -1, -1):
        spans = []
        for x in range(s["w"]):
            kind, ch2, bgkey = s["cells"][(x, y)]
            fk = (kind if kind in ("start", "exit", "coin", "beast", "portal", "decoy")
                  else "sol_fg" if kind == "sol" else "lane_fg" if kind == "lane"
                  else "corn")
            spans.append(f'<b style="background:{BG[bgkey]};color:{FG[fk]}">{ch2}{ch2}</b>')
        out.append("".join(spans))
    return out


def facts(s):
    L, ch = s["L"], s["ch"]
    tw = walk_time(s["route"])
    tr = walk_time(s["route"], RUN)
    ramp = STORM_RAMP[ch]
    onset = STORM_FACTOR[ch] * tw
    period = MOON_PERIOD_IN_CROSSINGS[ch] * tw
    sweep = 360.0 * tw / period
    prt = ", ".join(f"{k} {a}→{b} ({steps:+d} cells)"
                    for k, a, b, steps, gap, ok in s["pairs"]) or "none"
    return (f"route {s['route']} cells ({tw:.0f}s walk / {tr:.0f}s run) &middot; "
            f"gate {L['gold']} &middot; {len(L['dead'])} dead ends, deepest "
            f"{L['deepest']} &middot; moon {period:.0f}s period = {sweep:.0f}&deg;/walk "
            f"&middot; stars drift {STAR_DRIFT_DEG_PER_S[ch]:.2f}&deg;/s &middot; "
            f"rain at {onset:.0f}s (calm {max(0, onset-ramp):.0f} + ramp {ramp:.0f}), "
            f"melt clock {MELT:.0f}s &middot; portals: {prt}")


def gallery():
    """One silhouette per shape at the node size it ships on."""
    from corn_shapes import carve, SHAPES
    from corn_mechanics import (valid_seed, node_chapter, size_of, shape_of, BIAS)
    nodes = [("L1", 1), ("L9", 9), ("L13", 13), ("L15", 15), ("L18", 18),
             ("L19", 19), ("L20", 20), ("L21", 21), ("L24", 24)]
    out = []
    for tag, n in nodes:
        seed, L = valid_seed(n)
        w = size_of(n)
        m = SHAPES[shape_of(n)](w, w) if shape_of(n) != "rect" else None
        wall, _, _, _ = carve(w, w, seed, BIAS[node_chapter(n)], m)
        rows = []
        for y0 in range(w - 1, -1, -3):
            row = []
            for x0 in range(0, w, 3):
                block = [(x, y) for x in range(x0, min(x0 + 3, w))
                         for y in range(max(0, y0 - 2), min(y0 + 1, w))]
                openc = sum(1 for c in block if not wall[c[0]][c[1]])
                ch = "\u2593" if openc == 0 else ("\u2592" if openc < len(block) / 2 else " ")
                row.append(ch)
            rows.append("".join(row))
        out.append('<div class="sil"><div class="cap">' + tag + " &middot; " +
                   shape_of(n) + " " + str(w) + "&times;" + str(w) +
                   " &middot; seed " + str(seed) + " &middot; route " +
                   str(len(L["path"])) + " &middot; " + str(len(L["dead"])) +
                   ' dead ends</div><pre class="sil">' +
                   chr(10).join(rows) + "</pre></div>")
    return "".join(out)


def main(sheets):
    panels = []
    for s in sheets:
        legend = "".join(
            f'<span><b style="background:{BG[b]};color:{FG[f]}">{g}{g}</b> {lab}</span>'
            for g, f, b, lab in [("S", "start", "start", "start"),
                                 ("E", "exit", "exit", "exit gate"),
                                 ("·", "sol_fg", "lane", "star-arrow route"),
                                 ("o", "coin", "lane", "coin"),
                                 ("B", "beast", "lane", "Crumb Beast"),
                                 ("1", "portal", "portal", "honest portal pair"),
                                 ("2", "decoy", "decoy", "decoy portal pair"),
                                 ("▓", "corn", "corn_a", "corn")])
        panels.append(f"""<section>
<h2>Node L{s['n']} &mdash; {s['L']['shape']} {s['w']}&times;{s['h']},
chapter {s['ch']}, seed {s['seed']}</h2>
<pre>{chr(10).join(rows_html(s))}</pre>
<div class="legend">{legend}</div>
<div class="facts">{facts(s)}</div></section>""")
        open(os.path.join(OUT, f"cornmaze-L{s['n']}-dark.ansi"), "w").write(ansi(s))
    page = f"""<!doctype html><html><head><meta charset="utf-8">
<title>Corn Field Maze &mdash; shaped levels</title><style>
 html,body {{ background:{PAGE}; color:#e8e2d2; margin:0; padding:22px 24px;
   font-family:ui-monospace,SFMono-Regular,Menlo,monospace; }}
 h1 {{ font-size:15px; font-weight:600; margin:0 0 14px; color:#f2ecd9; }}
 h2 {{ font-size:12.5px; font-weight:600; margin:0 0 8px; color:#d8cfb4; }}
 pre {{ margin:0; font-size:13px; line-height:1.02; }}
 pre b {{ font-weight:400; }}
 section {{ margin-bottom:26px; padding-bottom:20px; border-bottom:1px solid #1e1c14; }}
 .legend {{ margin-top:12px; display:flex; flex-wrap:wrap; gap:6px 16px;
   font-size:11.5px; color:#a49b85; }}
 .legend b {{ font-weight:400; }}
 .gallery {{ display:flex; flex-wrap:wrap; gap:18px 26px; margin:0 0 26px;
   padding-bottom:20px; border-bottom:1px solid #1e1c14; }}
 .sil pre {{ font-size:10px; line-height:1.0; color:#3c5417; margin:0; }}
 .sil .cap {{ font-size:10.5px; color:#8d8672; margin-bottom:5px; }}
 .facts {{ margin-top:8px; font-size:11.5px; color:#7d7663; max-width:1100px; }}
</style></head><body>
<h1>The field stops being a square &mdash; silhouettes, then L13 and L20 with portals marked</h1>
<div class="gallery">{gallery()}</div>
{chr(10).join(panels)}
</body></html>"""
    open(os.path.join(OUT, "cornmaze-shaped-dark.html"), "w").write(page)
    for s in sheets:
        # verify the picture against a fresh carve of the same node
        from corn_shapes import carve, SHAPES
        from corn_mechanics import shape_of, size_of, BIAS
        w0 = size_of(s["n"])
        m = SHAPES[shape_of(s["n"])](w0, w0) if shape_of(s["n"]) != "rect" else None
        wall2, _, _, _ = carve(w0, w0, s["seed"], BIAS[s["ch"]], m)
        bad = [c for c in s["cells"]
               if (s["cells"][c][0] == "corn") != bool(wall2[c[0]][c[1]])]
        assert not bad, f"L{s['n']} glyph grid disagrees at {bad[:5]}"
        for kind, a, b, steps, gap, ok in s["pairs"]:
            assert ok, f"L{s['n']} portal pair outside constraints"
        print(f"L{s['n']} {s['L']['shape']}: picture verified against carve, "
              f"{len(s['pairs'])} portal pairs all inside constraints")
    print("wrote cornmaze-shaped-dark.html + per-level .ansi")


if __name__ == "__main__":
    sheets = [sheet(13), sheet(20)]
    main(sheets)
    for s in sheets:
        print()
        print(plain(s))
