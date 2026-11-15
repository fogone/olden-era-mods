# Blender: a unit's model (.glb with its armature) rendered in its bind pose from several sides, for an image model to
# repaint in the same pose: blender -b -P render_pose.py -- <model.glb> <out folder> [--hide <mesh name>...] [--size 1024]
import bpy, sys, math, os
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
glb, out = argv[0], argv[1]
hide = argv[argv.index("--hide") + 1:] if "--hide" in argv else []
hide = [h for h in hide if not h.startswith("--")]
size = int(argv[argv.index("--size") + 1]) if "--size" in argv else 1024
os.makedirs(out, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.name != "Icosphere"]
for o in bpy.context.scene.objects:
    if o.name == "Icosphere" or any(h in o.name for h in hide):
        o.hide_render = True
shown = [o for o in meshes if not o.hide_render]
dg = bpy.context.evaluated_depsgraph_get()
pts = []
for o in shown:
    e = o.evaluated_get(dg); m = e.to_mesh()
    pts += [e.matrix_world @ v.co for v in m.vertices]
    e.to_mesh_clear()
lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
c = (lo + hi) / 2; r = (hi - lo).length / 2
print("bounds", tuple(lo), tuple(hi))

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE"
scene.render.resolution_x = scene.render.resolution_y = size
scene.render.film_transparent = False
world = bpy.data.worlds.new("w"); scene.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (1, 1, 1, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); scene.collection.objects.link(sun)
sun.data.energy = 3; sun.rotation_euler = (math.radians(50), 0, math.radians(30))
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scene.collection.objects.link(cam); scene.camera = cam
cam.data.type = "ORTHO"; cam.data.ortho_scale = r * 2.2
# Views around the vertical (Z in Blender), named by the angle of the camera about the model.
for name, az in [("a000", 0), ("a045", 45), ("a090", 90), ("a135", 135), ("a180", 180), ("a225", 225), ("a270", 270), ("a315", 315)]:
    a = math.radians(az); el = math.radians(10)
    cam.location = c + Vector((math.sin(a) * math.cos(el), -math.cos(a) * math.cos(el), math.sin(el))) * r * 4
    cam.rotation_euler = (c - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(out, name + ".png")
    bpy.ops.render.render(write_still=True)
