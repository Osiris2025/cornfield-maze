# DESIGN-TIME ONLY - not compiled into the Unity build, never ships on device.
# Corn block kit: the two source models -> stalks -> the 2 x 2 m block set.
# Rebuild with scripts/corn_block_build.py; design facts in docs/CORN-BRIEF.md.
#!/usr/bin/env python3
"""Convert the two source models Blender 5 cannot read into OBJ, standard library
only:
  - corn-corn-corn/source/corn.fbx  (ASCII FBX 6.1, Blender 2.70 exporter -
    values come as bare comma lists, not the 'a: ...' wrapper later exporters use)
  - (the Collada model is converted by corn_dae_to_obj.py; that file's geometry
    is <polylist>, not <triangles>, which is why the path that used to live here
    produced nothing)
Reports bounding boxes so the scale of each source is a measured fact.
"""
import os
import re
import xml.etree.ElementTree as ET

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, "artifacts", "corn-blocks", "src")
os.makedirs(OUT, exist_ok=True)
NUM = re.compile(r"[-+]?\d*\.?\d+(?:[eE][-+]?\d+)?")


def fbx_property(lines, name):
    """FBX 6.1 ASCII: 'Vertices: 1,2,3,' then continuation lines, until the next
    'Name:' property line."""
    out, taking = [], False
    for ln in lines:
        s = ln.strip()
        if s.startswith(name + ":"):
            taking = True
            out += NUM.findall(s[len(name) + 1:])
            continue
        if taking:
            if re.match(r"^[A-Za-z_][A-Za-z0-9_]*:", s):   # next property
                break
            out += NUM.findall(s)
    return [float(x) for x in out]


def fbx_strings(lines, name):
    return [ln.strip().split(":", 1)[1].strip().strip('"')
            for ln in lines if ln.strip().startswith(name + ":")]


def parse_fbx(path):
    lines = open(path, errors="ignore").read().splitlines()
    verts = fbx_property(lines, "Vertices")
    poly = fbx_property(lines, "PolygonVertexIndex")
    uvs = fbx_property(lines, "UV")
    uvindex = fbx_property(lines, "UVIndex")
    mats = [s for s in fbx_strings(lines, "Material")
            if s and not s[0].isdigit() and "::" in s]
    tex = fbx_strings(lines, "RelativeFilename")
    return verts, poly, uvs, uvindex, mats, tex


def fbx_to_obj(src, dst):
    """UVs in this file are per polygon-vertex: 'UV' holds the pairs and
    'UVIndex' indexes them, one entry per polygon-vertex. Writing the pairs out
    in array order and pairing them with vertex indices (as the first pass did)
    leaves most of the mesh unmapped, which is why the plant rendered flat."""
    verts, poly, uvs, uvindex, mats, tex = parse_fbx(src)
    if len(verts) < 9 or len(poly) < 3:
        raise SystemExit(f"{src}: no geometry found")
    faces, cur, curuv = [], [], []
    for n, idx in enumerate(poly):
        i = int(idx)
        end = i < 0
        cur.append(-i - 1 if end else i)
        curuv.append(int(uvindex[n]) if n < len(uvindex) else 0)
        if end:
            faces.append((cur, curuv))
            cur, curuv = [], []
    if cur:
        faces.append((cur, curuv))
    pairs = [(uvs[i], uvs[i + 1]) for i in range(0, len(uvs) - 1, 2)]
    used = sorted({u for _, uv in faces for u in uv})
    remap = {u: n + 1 for n, u in enumerate(used)}
    xs = verts[0::3]; ys = verts[1::3]; zs = verts[2::3]
    with open(dst, "w") as f:
        f.write("# from ASCII FBX 6.1 - corn_convert.py (per-vertex UVs)\n")
        for i in range(0, len(verts), 3):
            f.write(f"v {verts[i]:.6f} {verts[i+1]:.6f} {verts[i+2]:.6f}\n")
        for u in used:
            f.write(f"vt {pairs[u][0]:.6f} {pairs[u][1]:.6f}\n")
        f.write("usemtl corn\n")
        for fc, uv in faces:
            f.write("f " + " ".join(f"{i + 1}/{remap[u]}" for i, u in zip(fc, uv)) + "\n")
    mapped = sum(1 for _, uv in faces for u in uv)
    return dict(verts=len(verts) // 3, tris=len(faces), uv_pairs=len(pairs),
                uv_index_entries=len(uvindex), uv_refs=mapped, uv_used=len(used),
                size=(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)),
                min=(min(xs), min(ys), min(zs)), max=(max(xs), max(ys), max(zs)),
                materials=mats, textures=tex)


# ---------------------------------------------------------------------- DAE

def convert():
    """Convert the supplied sources into OBJ next to the block kit's src dir."""
    r = fbx_to_obj("/Users/toddadams/Downloads/corn-corn-corn/source/corn.fbx",
                   os.path.join(OUT, "corn-corn-corn.obj"))
    print("corn-corn-corn/source/corn.fbx (ASCII FBX 6.1)")
    print(f"   verts {r['verts']}  tris {r['tris']}")
    print(f"   UVs {r['uv_pairs']} pairs from {r['uv_index_entries']} index entries "
          f"-> {r['uv_used']} used, {r['uv_refs']} face refs")
    print(f"   size  {tuple(round(v, 3) for v in r['size'])}  min {tuple(round(v,3) for v in r['min'])} max {tuple(round(v,3) for v in r['max'])}")
    print(f"   materials {r['materials']}  textures {r['textures']}")
    print(f"wrote {os.path.join(OUT, 'corn-corn-corn.obj')}")


if __name__ == "__main__":
    convert()
