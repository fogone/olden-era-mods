"""Pictures of a model from the cameras of a World Labs Atlas export and from around (Blender, headless; flat colour
from the model's colour attribute, Workbench).

    blender -b -P look.py -- <model.ply|glb> <cameras.json> <out prefix> [--frames 0,6] [--orbit 30,-30]
"""
import sys, json, math, argparse
import bpy
from mathutils import Matrix, Quaternion, Vector

argv = sys.argv[sys.argv.index("--") + 1:]
a = argparse.ArgumentParser()
a.add_argument("model"); a.add_argument("cameras"); a.add_argument("out")
a.add_argument("--frames", default="0"); a.add_argument("--orbit", default="")
a.add_argument("--flip", action="store_true", help="the model's y and z negated (OpenCV axes, as Marble's meshes may be)")
args = a.parse_args(argv)

bpy.ops.wm.read_factory_settings(use_empty=True)
if args.model.lower().endswith(".ply"):
    bpy.ops.wm.ply_import(filepath=args.model)
else:
    bpy.ops.import_scene.gltf(filepath=args.model)
obj = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
if args.flip:
    obj.matrix_world = Matrix.Diagonal((1, -1, -1, 1)) @ obj.matrix_world
scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "FLAT"
scene.display.shading.color_type = "VERTEX" if args.model.lower().endswith(".ply") else "TEXTURE"
scene.render.resolution_x, scene.render.resolution_y = 1280, 720
cam_data = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cam_data); scene.collection.objects.link(cam)
scene.camera = cam
cam_data.clip_end = 2000

# Atlas: y up (OpenGL); Blender: z up. A point (x, y, z) of Atlas is (x, -z, y) in Blender, as the PLY importer puts it.
to_blender = Matrix(((1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
frames = json.load(open(args.cameras))
def shoot(name):
    scene.render.filepath = f"{args.out}_{name}.png"
    bpy.ops.render.render(write_still=True)

for i in [int(x) for x in args.frames.split(",") if x != ""]:
    c = frames[i]["camera"]; k = c["intrinsics"]; e = c["extrinsics"]
    x, y, z, w = e["quaternion"]
    pose = Matrix.Translation(Vector(e["position"])) @ Quaternion((w, x, y, z)).to_matrix().to_4x4()
    cam.matrix_world = to_blender @ pose      # Blender's camera looks down -z with y up: OpenGL's
    cam_data.sensor_fit = "HORIZONTAL"; cam_data.sensor_width = 36
    cam_data.lens = k["fx"] / k["width"] * 36
    shoot(f"frame{i}")

centre = sum((obj.matrix_world @ Vector(b) for b in obj.bound_box), Vector()) / 8
size = max(obj.dimensions)
for deg in [float(d) for d in args.orbit.split(",") if d != ""]:
    t = math.radians(deg)
    cam.location = centre + Vector((math.sin(t) * size * 0.9, -math.cos(t) * size * 0.9, size * 0.45))
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam_data.lens = 30
    shoot(f"orbit{int(deg)}")
