"""A Unity terrain's heightmap and splat map from a glTF model of a landscape (an image-to-3D tool's diorama).

    python heightmap.py <model.glb> <out folder> [--size 80] [--scale 20] [--res 513] [--turn 180]

The model is put into Unity's space (x mirrored), turned about the vertical axis by --turn degrees and scaled by
--scale; the terrain (--size units square, the model at its centre) gets the model's top surface as heights, the
ground beyond the model at the model's floor height. Writes heights.r16 (Unity's RAW: 16-bit, rows from z = 0) and heights.png (the same, to look at), splat.png
(RGBA: the weights of the terrain material's four textures: R and G ground in patches, A rock by slope) and terrain.json (the terrain's
size and where the floor is, for placing buildings).
"""
import argparse, json, math, os
import numpy as np
import trimesh
from PIL import Image

a = argparse.ArgumentParser()
a.add_argument("model"); a.add_argument("out")
a.add_argument("--size", type=float, default=80); a.add_argument("--scale", type=float, default=20)
a.add_argument("--res", type=int, default=513); a.add_argument("--turn", type=float, default=180)
a.add_argument("--rock", type=float, default=32, help="slope in degrees where rock begins")
args = a.parse_args()

mesh = trimesh.load(args.model, force="mesh")
v = mesh.vertices.copy()
v[:, 0] = -v[:, 0]                                    # glTF -> Unity
t = math.radians(args.turn)
x, z = v[:, 0] * math.cos(t) + v[:, 2] * math.sin(t), -v[:, 0] * math.sin(t) + v[:, 2] * math.cos(t)
v = np.stack([x, v[:, 1], z], 1) * args.scale

# The floor: the most common height of upward faces.
tri = v[mesh.faces]
n = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]); n /= np.linalg.norm(n, axis=1, keepdims=True) + 1e-12
up = np.abs(n[:, 1]) > 0.95
h, e = np.histogram(tri[up].mean(1)[:, 1], bins=60); i = h.argmax(); floor = (e[i] + e[i + 1]) / 2
low = v[:, 1].min()

# Top surface by rasterizing every triangle seen from above into a height grid (the highest wins).
R = args.res; S = args.size; cell = S / (R - 1)
grid = np.full((R, R), -np.inf)
gx = (tri[:, :, 0] + S / 2) / cell; gz = (tri[:, :, 2] + S / 2) / cell; gy = tri[:, :, 1]
for k in range(len(tri)):
    xs, zs, ys = gx[k], gz[k], gy[k]
    x0, x1 = max(int(math.floor(xs.min())), 0), min(int(math.ceil(xs.max())), R - 1)
    z0, z1 = max(int(math.floor(zs.min())), 0), min(int(math.ceil(zs.max())), R - 1)
    if x0 > x1 or z0 > z1: continue
    px, pz = np.meshgrid(np.arange(x0, x1 + 1), np.arange(z0, z1 + 1))
    d = (zs[1] - zs[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (zs[0] - zs[2])
    if abs(d) < 1e-9: continue
    w0 = ((zs[1] - zs[2]) * (px - xs[2]) + (xs[2] - xs[1]) * (pz - zs[2])) / d
    w1 = ((zs[2] - zs[0]) * (px - xs[2]) + (xs[0] - xs[2]) * (pz - zs[2])) / d
    w2 = 1 - w0 - w1
    inside = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6)
    if not inside.any(): continue
    hy = w0 * ys[0] + w1 * ys[1] + w2 * ys[2]
    sub = grid[z0:z1 + 1, x0:x1 + 1]
    np.maximum(sub, np.where(inside, hy, -np.inf), out=sub)

# Beyond the model and below its floor (its base's sides): the floor.
grid = np.where(np.isfinite(grid), grid, floor)
grid = np.maximum(grid, floor)
top = grid.max() + 1
heights = (grid - floor) / (top - floor)              # 0 at the floor
img = np.round(heights * 65535).astype(np.uint16)
os.makedirs(args.out, exist_ok=True)
Image.fromarray(img).save(os.path.join(args.out, "heights.png"))
img.astype("<u2").tofile(os.path.join(args.out, "heights.r16"))   # Unity's RAW

# Splat: rock where steep, ground elsewhere, blended over a few degrees.
dz, dx = np.gradient(grid, cell)
slope = np.degrees(np.arctan(np.hypot(dx, dz)))
rock = np.clip((slope - args.rock) / 12, 0, 1)
# The ground: two textures in patches (a smooth noise), R and G; rock A.
rng = np.random.default_rng(1)
f = np.hypot(np.fft.fftfreq(R - 1)[None, :], np.fft.fftfreq(R - 1)[:, None]) * (R - 1); f[0, 0] = 1
amp = np.where(f < 3, 0, 1 / f ** 2.4)
patches = np.real(np.fft.ifft2(amp * np.exp(2j * np.pi * rng.random((R - 1, R - 1)))))
patches = np.clip((patches - patches.mean()) / (patches.std() * 1.5) + 0.5, 0, 1)
splat = np.zeros((R - 1, R - 1, 4), np.uint8)
rk = rock[:-1, :-1]
splat[..., 0] = np.round((1 - rk) * (1 - patches) * 255); splat[..., 1] = np.round((1 - rk) * patches * 255)
splat[..., 3] = np.round(rk * 255)
Image.fromarray(splat).save(os.path.join(args.out, "splat.png"))

json.dump({"size": [S, float(top - floor), S], "floor": 0.0, "position": [-S / 2, float(floor), -S / 2],
           "modelFloor": float(floor), "modelLow": float(low)}, open(os.path.join(args.out, "terrain.json"), "w"), indent=2)
print("floor", round(floor, 2), "top", round(top, 2), "rock share", round(float(rock.mean()), 3))
