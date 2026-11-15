"""A coloured mesh from a Gaussian splat (its centres, colours, opacities, sizes as numpy arrays: export them from
Blender, which imports SPZ): the dense small splats' centres made a surface (screened Poisson), coloured by them.

    python splat_mesh.py <splats.npz> <out.ply> [--cameras cameras.json] [--voxel 0.08] [--depth 11]
           [--min-opacity 0.7] [--max-size 0.3] [--trim 0.02]

`--cameras` (an Atlas export's cameras.json, OpenGL axes, put into Blender's z-up as the importer does): normals are
turned to face the cameras. `--trim`: the share of the least supported surface cut off (Poisson closes the unseen).
"""
import argparse, json
import numpy as np
import open3d as o3d

a = argparse.ArgumentParser()
a.add_argument("npz"); a.add_argument("out"); a.add_argument("--cameras")
a.add_argument("--voxel", type=float, default=0.08); a.add_argument("--depth", type=int, default=11)
a.add_argument("--min-opacity", type=float, default=0.7); a.add_argument("--max-size", type=float, default=0.3)
a.add_argument("--trim", type=float, default=0.02)
args = a.parse_args()

z = np.load(args.npz)
pos, col, scale = z["pos"], z["col"], z["scale"]
keep = (col[:, 3] >= args.min_opacity) & (scale.max(1) <= args.max_size)
pos, rgb = pos[keep], np.clip(0.5 + 0.28209479 * col[keep, :3], 0, 1)
print("splats kept", keep.sum(), "of", len(keep))
pc = o3d.geometry.PointCloud(o3d.utility.Vector3dVector(pos.astype(np.float64)))
pc.colors = o3d.utility.Vector3dVector(rgb.astype(np.float64))
pc = pc.voxel_down_sample(args.voxel)
pc, _ = pc.remove_statistical_outlier(nb_neighbors=20, std_ratio=2.0)
print("points", len(pc.points))
pc.estimate_normals(o3d.geometry.KDTreeSearchParamHybrid(radius=args.voxel * 6, max_nn=30))
if args.cameras:
    cams = np.array([f["camera"]["extrinsics"]["position"] for f in json.load(open(args.cameras))])
    cams = np.stack([cams[:, 0], -cams[:, 2], cams[:, 1]], 1)      # OpenGL y-up -> Blender z-up
    pc.orient_normals_towards_camera_location(cams.mean(0))
else:
    pc.orient_normals_consistent_tangent_plane(15)
mesh, density = o3d.geometry.TriangleMesh.create_from_point_cloud_poisson(pc, depth=args.depth)
density = np.asarray(density)
mesh.remove_vertices_by_mask(density < np.quantile(density, args.trim))
mesh.remove_unreferenced_vertices()
print("vertices", len(mesh.vertices), "triangles", len(mesh.triangles))
o3d.io.write_triangle_mesh(args.out, mesh)
