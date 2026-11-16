"""A coloured mesh fused from the frames of a World Labs Atlas export (images, depth, cameras): each frame's depth
integrated into a truncated signed distance volume, its surface taken out with the frames' colours.

    python fuse.py <export folder> <out.ply> [--voxel 0.3] [--trunc-voxels 4] [--min-weight 1] [--max-depth 160] [--min-confidence 0.5]

The export's cameras are OpenGL's ("rub": x right, y up, z back), positions and quaternions (x, y, z, w) camera to
world; its depth maps (EXR, channel Y) are taken as distances along the view axis.
"""
import argparse, json, os
import numpy as np
import OpenEXR
import open3d as o3d
import open3d.core as o3c
from PIL import Image

a = argparse.ArgumentParser()
a.add_argument("folder"); a.add_argument("out")
a.add_argument("--voxel", type=float, default=0.3); a.add_argument("--trunc-voxels", type=float, default=4.0)
a.add_argument("--blocks", type=int, default=150000); a.add_argument("--min-weight", type=float, default=1.0)
a.add_argument("--max-depth", type=float, default=160.0); a.add_argument("--min-confidence", type=float, default=0.5)
args = a.parse_args()

frames = json.load(open(os.path.join(args.folder, "cameras.json")))
dev = o3c.Device("CPU:0")
grid = o3d.t.geometry.VoxelBlockGrid(attr_names=("tsdf", "weight", "color"), attr_dtypes=(o3c.float32, o3c.float32, o3c.float32),
                                     attr_channels=((1), (1), (3)), voxel_size=args.voxel, block_resolution=8,
                                     block_count=args.blocks, device=dev)
for f in frames:
    cam = f["camera"]; k = cam["intrinsics"]; e = cam["extrinsics"]
    depth = list(OpenEXR.File(os.path.join(args.folder, f["depth"]["depthAsset"]["url"])).channels().values())[0].pixels.astype(np.float32)
    conf = np.asarray(Image.open(os.path.join(args.folder, f["depth"]["confidenceAsset"]["url"])).convert("L"), np.float32) / 255
    depth[(conf < args.min_confidence) | ~np.isfinite(depth)] = 0
    color = Image.open(os.path.join(args.folder, f["imageAsset"]["url"])).convert("RGB")
    if color.size != (depth.shape[1], depth.shape[0]):
        color = color.resize((depth.shape[1], depth.shape[0]))
    color = np.asarray(color, np.float32) / 255
    K = o3c.Tensor([[k["fx"], 0, k["cx"]], [0, k["fy"], k["cy"]], [0, 0, 1]], o3c.float64)
    x, y, z, w = e["quaternion"]
    R = o3d.geometry.get_rotation_matrix_from_quaternion([w, x, y, z]) @ np.diag([1, -1, -1])   # OpenGL -> OpenCV camera
    pose = np.eye(4); pose[:3, :3] = R; pose[:3, 3] = e["position"]
    E = o3c.Tensor(np.linalg.inv(pose))
    d = o3d.t.geometry.Image(o3c.Tensor(np.ascontiguousarray(depth)))
    c = o3d.t.geometry.Image(o3c.Tensor(np.ascontiguousarray(color)))
    try:
        blocks = grid.compute_unique_block_coordinates(d, K, E, 1.0, float(args.max_depth), float(args.trunc_voxels))
        grid.integrate(blocks, d, c, K, K, E, 1.0, float(args.max_depth), float(args.trunc_voxels))
        print("integrated", f["imageAsset"]["url"], int((depth > 0).sum()), "depths")
    except RuntimeError as x:
        print("skipped", f["imageAsset"]["url"], int((depth > 0).sum()), "depths:", str(x).splitlines()[-1][:120])
mesh = grid.extract_triangle_mesh(weight_threshold=args.min_weight).to_legacy()
mesh.compute_vertex_normals()
print("vertices", len(mesh.vertices), "triangles", len(mesh.triangles))
o3d.io.write_triangle_mesh(args.out, mesh)
