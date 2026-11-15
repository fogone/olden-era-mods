# Blender: a skinned model (.glb with its armature) rendered in its bind pose and with a few bones turned, to see that
# its weights follow the bones: blender -b -P check_skin.py -- <skinned.glb> <out.png> [--az <degrees>] [--bones <name>...]
import bpy, sys, math, os
from mathutils import Vector, Euler

argv = sys.argv[sys.argv.index("--") + 1:]
glb, out = argv[0], argv[1]
az = float(argv[argv.index("--az") + 1]) if "--az" in argv else 270
bones = [b for b in argv[argv.index("--bones") + 1:] if not b.startswith("--")] if "--bones" in argv else []
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.name != "Icosphere"]
for o in bpy.context.scene.objects:
    if o.name == "Icosphere":
        o.hide_render = True
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE"
scene.render.resolution_x, scene.render.resolution_y = 1600, 800
world = bpy.data.worlds.new("w"); scene.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.8, 0.8, 0.8, 1)
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); scene.collection.objects.link(sun)
sun.data.energy = 3; sun.rotation_euler = (math.radians(50), 0, math.radians(30))
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scene.collection.objects.link(cam); scene.camera = cam
cam.data.type = "ORTHO"

def bounds():
    dg = bpy.context.evaluated_depsgraph_get(); pts = []
    for o in meshes:
        e = o.evaluated_get(dg); m = e.to_mesh(); pts += [e.matrix_world @ v.co for v in m.vertices]; e.to_mesh_clear()
    lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
    return lo, hi

lo, hi = bounds(); c = (lo + hi) / 2; r = (hi - lo).length / 2
cam.data.ortho_scale = r * 2.4
scene.render.resolution_x = 800
a = math.radians(az)
cam.location = c + Vector((math.sin(a), -math.cos(a), 0.15)) * r * 4
cam.rotation_euler = (c - cam.location).to_track_quat("-Z", "Y").to_euler()
frames = []
scene.render.filepath = out.replace(".png", "_bind.png"); bpy.ops.render.render(write_still=True); frames.append(scene.render.filepath)
# Turned: each bone named (or by default arms, head, spine) by 35 degrees.
names = bones or [b.name for b in arm.pose.bones if any(k in b.name for k in ("Shoulder", "Elbow", "Head", "Spine", "UpLeg", "Knee", "Wing", "Neck"))]
for b in arm.pose.bones:
    if b.name in names:
        b.rotation_mode = "XYZ"; b.rotation_euler = Euler((math.radians(35), math.radians(15), 0))
bpy.context.view_layer.update()
scene.render.filepath = out.replace(".png", "_posed.png"); bpy.ops.render.render(write_still=True); frames.append(scene.render.filepath)
print("turned", len(names), "bones")
