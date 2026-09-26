# DESIGN-TIME probe — answers "can this Blender build rig and export?" from output, not memory.
# Run: /Applications/Blender.app/Contents/MacOS/Blender -b -noaudio --python scripts/probe_rig_capability.py
import bpy, sys, addon_utils

print("=" * 60)
print("BLENDER", bpy.app.version_string)
print("=" * 60)

# --- 1. armature / skinning / pose operators exist? ---
ops = bpy.ops
checks = {
    "armature.add":               hasattr(ops.armature, "add"),
    "object.parent_set(ARMATURE_AUTO)": hasattr(ops.object, "parent_set"),
    "object.mode_set(POSE)":      hasattr(ops.object, "mode_set"),
    "object.vertex_group_add":    hasattr(ops.object, "vertex_group_add"),
    "pose.armature_apply":        hasattr(ops.pose, "armature_apply"),
    "object.export_scene.fbx":    hasattr(ops.export_scene, "fbx"),
}
for k, v in checks.items():
    print(f"  op {k:38s} {'YES' if v else 'NO'}")

# --- 2. FBX exporter exposes bake_anim? ---
try:
    props = ops.export_scene.fbx.get_rna_type().properties
    names = {p.identifier for p in props}
    for want in ("bake_anim", "bake_anim_use_all_bones", "use_selection", "add_leaf_bones"):
        print(f"  fbx prop {want:34s} {'YES' if want in names else 'NO'}")
    print(f"  fbx prop count = {len(names)}")
except Exception as e:
    print("  FBX exporter introspection FAILED:", e)

# --- 3. Rigify bundled but disabled? ---
try:
    ok = addon_utils.enable("rigify", default_set=True, persistent=False)
    print(f"  rigify enable() -> {ok}")
    mod = addon_utils.check("rigify")
    print(f"  rigify check (loaded, enabled) -> {mod}")
except Exception as e:
    print("  rigify FAILED:", e)

# --- 4. numpy in bundled python (needed for measurement/bakes) ---
try:
    import numpy as np
    print(f"  numpy {np.__version__} available")
except Exception as e:
    print("  numpy MISSING:", e)

# --- 5. can we actually BUILD a minimal armature + skin it, right now? ---
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

# a two-part scarecrow-ish shape: post + crossbar, as SEPARATE parts (the hinge case)
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 1.0))
post = bpy.context.active_object
post.name = "Post"
post.scale = (0.06, 0.06, 1.0)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 1.9))
arm = bpy.context.active_object
arm.name = "Crossbar"
arm.scale = (0.9, 0.05, 0.05)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

# armature with 3 bones: root, post, crossbar (hinge where a scarecrow actually hinges)
bpy.ops.object.armature_add(location=(0, 0, 0))
rig = bpy.context.active_object
rig.name = "Rig"
bpy.ops.object.mode_set(mode="EDIT")
eb = rig.data.edit_bones
eb.remove(eb[0])
root = eb.new("root");    root.head = (0, 0, 0.0);  root.tail = (0, 0, 0.2)
post_b = eb.new("post");  post_b.head = (0, 0, 0.2); post_b.tail = (0, 0, 1.6)
arm_b = eb.new("crossbar"); arm_b.head = (0, 0, 1.9); arm_b.tail = (0.9, 0, 1.9)
post_b.parent = root
arm_b.parent = post_b
bpy.ops.object.mode_set(mode="OBJECT")

# skin both parts with automatic weights, then COUNT what got weighted
unweighted_total = 0
verts_total = 0
for ob in (post, arm):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    try:
        bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    except Exception as e:
        print(f"  ARMATURE_AUTO failed on {ob.name}: {e}")
    groups = [g.name for g in ob.vertex_groups]
    unw = sum(1 for v in ob.data.vertices if not v.groups)
    verts_total += len(ob.data.vertices)
    unweighted_total += unw
    print(f"  skinned {ob.name:9s} groups={groups} unweighted={unw}/{len(ob.data.vertices)}")

# --- 6. pose it (prove it MOVES) and export with baked animation ---
bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="POSE")
pb = rig.pose.bones["crossbar"]
pb.rotation_mode = "XYZ"
pb.rotation_euler = (0.35, 0, 0)
pb.keyframe_insert("rotation_euler", frame=1)
pb.rotation_euler = (-0.35, 0, 0)
pb.keyframe_insert("rotation_euler", frame=24)
bpy.ops.object.mode_set(mode="OBJECT")

act = rig.animation_data.action if rig.animation_data else None
if act is None:
    print("  ACTION: none created")
else:
    if hasattr(act, "fcurves"):
        n = len(act.fcurves)
    else:
        n = sum(len(cb.fcurves) for layer in act.layers
                for strip in layer.strips for cb in strip.channelbags)
    print(f"  action '{act.name}' curves = {n}")

out = "/tmp/probe_scarecrow_rig.fbx"
bpy.ops.export_scene.fbx(filepath=out, use_selection=False,
                         bake_anim=True, bake_anim_use_all_bones=True)
import os
print(f"  exported {out} bytes={os.path.getsize(out) if os.path.exists(out) else 'MISSING'}")

# --- 7. PROVE IT by re-importing the export ---
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=out)
objs = list(bpy.context.scene.objects)
arms = [o for o in objs if o.type == "ARMATURE"]
meshes = [o for o in objs if o.type == "MESH"]
print("  ROUND TRIP:")
print(f"    armatures = {len(arms)} bones = {sum(len(a.data.bones) for a in arms)}")
print(f"    meshes    = {len(meshes)}")
for m in meshes:
    mods = [mo.type for mo in m.modifiers]
    vg = len(m.vertex_groups)
    unw = sum(1 for v in m.data.vertices if not v.groups)
    print(f"      {m.name:16s} vgroups={vg:2d} unweighted={unw}/{len(m.data.vertices)} modifiers={mods}")
print(f"    actions   = {len(bpy.data.actions)}")
print("=" * 60)
print(f"PROBE VERDICT: armature={len(arms) > 0} skinned={any(len(m.vertex_groups) for m in meshes)} "
      f"animated={len(bpy.data.actions) > 0} unweighted_total_after_export={unweighted_total}")
print("=" * 60)
