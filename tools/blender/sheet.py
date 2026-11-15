"""A model's check sheet: pictures from the front, the side, the back, three-quarters and above, side by side
(Blender, headless, flat texture colour, Workbench), to see what an image-to-3D tool made before using the model.

    blender -b -P sheet.py -- <model.glb> <out.png> [--size 320]
"""
import sys, math, argparse, os, tempfile
import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
a = argparse.ArgumentParser(); a.add_argument("model"); a.add_argument("out"); a.add_argument("--size", type=int, default=320)
args = a.parse_args(argv)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=args.model)
objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
lo = Vector((min((o.matrix_world @ Vector(c)).x for o in objs for c in o.bound_box), min((o.matrix_world @ Vector(c)).y for o in objs for c in o.bound_box), min((o.matrix_world @ Vector(c)).z for o in objs for c in o.bound_box)))
hi = Vector((max((o.matrix_world @ Vector(c)).x for o in objs for c in o.bound_box), max((o.matrix_world @ Vector(c)).y for o in objs for c in o.bound_box), max((o.matrix_world @ Vector(c)).z for o in objs for c in o.bound_box)))
centre, size = (lo + hi) / 2, max(hi - lo)
scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"; scene.display.shading.color_type = "TEXTURE"
scene.render.resolution_x = scene.render.resolution_y = args.size
scene.render.film_transparent = False
cam_data = bpy.data.cameras.new("cam"); cam_data.type = "ORTHO"; cam_data.ortho_scale = size * 1.25
cam = bpy.data.objects.new("cam", cam_data); scene.collection.objects.link(cam); scene.camera = cam
# glTF's front (+z) is Blender's -y after import.
views = [("front", 0, 15), ("side", 90, 15), ("back", 180, 15), ("3/4", 45, 30), ("top", 0, 85)]
tmp = tempfile.mkdtemp(); files = []
for name, yaw, pitch in views:
    t, p = math.radians(yaw), math.radians(pitch)
    d = Vector((math.sin(t) * math.cos(p), -math.cos(t) * math.cos(p), math.sin(p)))
    cam.location = centre + d * size * 3
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    f = os.path.join(tmp, f"{len(files)}.png"); scene.render.filepath = f
    bpy.ops.render.render(write_still=True); files.append(f)
# Side by side.
imgs = [bpy.data.images.load(f) for f in files]
w, h = args.size, args.size
sheet = bpy.data.images.new("sheet", w * len(imgs), h)
px = [0.0] * (w * len(imgs) * h * 4)
for k, im in enumerate(imgs):
    src = list(im.pixels)
    for y in range(h):
        row = src[y * w * 4:(y + 1) * w * 4]
        start = (y * w * len(imgs) + k * w) * 4
        px[start:start + w * 4] = row
sheet.pixels = px
sheet.filepath_raw = args.out; sheet.file_format = "PNG"; sheet.save()
