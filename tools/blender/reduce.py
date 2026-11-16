"""A glTF model made lighter in Blender (run headless): its meshes joined, decimated to a number of triangles, loose
bits below a size removed, saved as a glTF binary with its textures.

    blender -b -P reduce.py -- <in.glb> <out.glb> [--faces 150000] [--min-part 0.002]

`--min-part`: parts (connected pieces) smaller than this share of the model's size are removed (floaters).
"""
import sys, argparse
import bpy, bmesh
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
a = argparse.ArgumentParser()
a.add_argument("src"); a.add_argument("dst")
a.add_argument("--faces", type=int, default=150000); a.add_argument("--min-part", type=float, default=0.002)
args = a.parse_args(argv)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=args.src)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
faces = len(obj.data.polygons)
print("faces", faces)

# Floaters: connected pieces whose box is a tiny share of the whole.
bm = bmesh.new(); bm.from_mesh(obj.data); bm.faces.ensure_lookup_table()
size = max((Vector(obj.dimensions)).to_tuple())
seen, remove = set(), []
for f in bm.faces:
    if f.index in seen:
        continue
    stack, part = [f], []
    seen.add(f.index)
    while stack:
        g = stack.pop(); part.append(g)
        for e in g.edges:
            for h in e.link_faces:
                if h.index not in seen:
                    seen.add(h.index); stack.append(h)
    vs = [v.co for p in part for v in p.verts]
    lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    if max((hi - lo).to_tuple()) < size * args.min_part:
        remove.extend(part)
bmesh.ops.delete(bm, geom=list(set(remove)), context="FACES")
bm.to_mesh(obj.data); bm.free()
print("floaters removed", len(set(remove)))

faces = len(obj.data.polygons)
if faces > args.faces:
    m = obj.modifiers.new("reduce", "DECIMATE")
    m.ratio = args.faces / faces
    bpy.ops.object.modifier_apply(modifier="reduce")
print("faces now", len(obj.data.polygons))
bpy.ops.export_scene.gltf(filepath=args.dst, export_format="GLB", use_selection=False)
