# DESIGN-TIME ONLY - not compiled into the Unity build, never ships on device.
# Probe the supplied GLB: what meshes, bones, animations and textures are actually inside.
# Run: Blender -b -noaudio --python scripts/probe_glb.py -- <path.glb>
import os
import sys

import bpy

argv = sys.argv
glb = argv[argv.index("--") + 1] if "--" in argv else None
if not glb:
    raise SystemExit("usage: --python probe_glb.py -- <path.glb>")

print("=" * 70)
print("GLB:", glb, os.path.getsize(glb), "bytes")
print("=" * 70)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)

meshes = [o for o in bpy.data.objects if o.type == "MESH"]
arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
print(f"objects: {len(bpy.data.objects)}  meshes: {len(meshes)}  armatures: {len(arms)}")
print(f"collections: {[c.name for c in bpy.data.collections]}")
print(f"actions (animation clips): {len(bpy.data.actions)}")
for a in bpy.data.actions:
    # Blender 5 moved fcurves behind slots/layers; an Action is no longer guaranteed .fcurves
    curves = getattr(a, "fcurves", None)
    if curves is None:
        curves = []
        for layer in getattr(a, "layers", []):
            for strip in getattr(layer, "strips", []):
                for cb in getattr(strip, "channelbags", []):
                    curves.extend(cb.fcurves)
    print(f"   action '{a.name}' frames {a.frame_range[0]:.0f}-{a.frame_range[1]:.0f}"
          f" fcurves {len(curves)}")

tris = 0
for o in meshes:
    o.data.calc_loop_triangles()
    n = len(o.data.loop_triangles)
    tris += n
    bb = [o.matrix_world @ __import__("mathutils").Vector(c) for c in o.bound_box]
    xs = [p.x for p in bb]
    ys = [p.y for p in bb]
    zs = [p.z for p in bb]
    print(f"  mesh '{o.name}': {n} tris, {len(o.data.vertices)} verts,"
          f" {len(o.data.materials)} materials,"
          f" dims {max(xs)-min(xs):.3f} x {max(ys)-min(ys):.3f} x {max(zs)-min(zs):.3f}")
    print(f"      uv layers: {[l.name for l in o.data.uv_layers]}")
    print(f"      colour attrs: {[c.name for c in o.data.color_attributes]}")
    vg = [g.name for g in o.vertex_groups]
    print(f"      vertex groups ({len(vg)}): {vg[:8]}{' ...' if len(vg) > 8 else ''}")
    print(f"      modifiers: {[(m.type, m.name) for m in o.modifiers]}")
print("TOTAL tris:", tris)

for a in arms:
    print(f"  armature '{a.name}': {len(a.data.bones)} bones")
    print(f"      bones: {[b.name for b in a.data.bones][:20]}")

for m in bpy.data.materials:
    bsdf = None
    if m.use_nodes:
        bsdf = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    imgs = []
    if m.use_nodes:
        for n in m.node_tree.nodes:
            if n.type == "TEX_IMAGE" and n.image:
                imgs.append((n.image.name, tuple(n.image.size)))
    print(f"  material '{m.name}': nodes={m.use_nodes} images={imgs}")

for i in bpy.data.images:
    print(f"  image '{i.name}': {tuple(i.size)}  packed={bool(i.packed_file)}"
          f"  source={i.source}")

print("=" * 70)
