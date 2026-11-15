"""A sky for a town scene: a half cylinder (inside faces) with a sky picture on it, as a glTF binary.

    python sky.py <out.glb> [--image sky.png] [--radius 60] [--height 45] [--arc 200]

Without an image a sky is drawn: blue fading to a pale horizon, soft clouds (periodic noise, so its sides meet).
The cylinder stands on y = 0 around the origin, its open side towards -z (the camera's side); `oe-bundle model` makes
it a prefab, drawn unlit (the template's material).
"""
import argparse, math, io
import numpy as np
import trimesh
from PIL import Image

a = argparse.ArgumentParser()
a.add_argument("out"); a.add_argument("--image"); a.add_argument("--radius", type=float, default=60)
a.add_argument("--height", type=float, default=45); a.add_argument("--arc", type=float, default=200)
a.add_argument("--seed", type=int, default=5)
args = a.parse_args()

if args.image:
    img = Image.open(args.image).convert("RGB")
else:
    W, H = 2048, 512
    rng = np.random.default_rng(args.seed)
    def noise(scale, power):
        fx = np.fft.fftfreq(W)[None, :] * W; fy = np.fft.fftfreq(H)[:, None] * H
        f = np.hypot(fx, fy * 2); f[0, 0] = 1
        amp = 1 / f ** power; amp[f < scale] = 0
        n = np.real(np.fft.ifft2(amp * np.exp(2j * np.pi * rng.random((H, W)))))
        return (n - n.min()) / (n.max() - n.min())
    v = np.linspace(0, 1, H)[:, None]                  # 0 at the top
    top, horizon = np.array([62, 112, 186.0]), np.array([196, 214, 228.0])
    sky = top * (1 - v[..., None] ** 1.6) + horizon * v[..., None] ** 1.6
    clouds = noise(3, 1.9) * 0.7 + noise(12, 1.5) * 0.3
    clouds = np.clip((clouds - 0.52) / 0.25, 0, 1) * (0.35 + 0.65 * v)      # thinner high up
    sky = sky * (1 - clouds[..., None] * 0.85) + np.array([250, 248, 244.0]) * clouds[..., None] * 0.85
    sky = np.broadcast_to(sky, (H, W, 3))
    img = Image.fromarray(np.clip(sky, 0, 255).astype(np.uint8))

# The half cylinder: columns along the arc, two rows (bottom, top); its faces both ways (seen from inside).
n = 64
arc = math.radians(args.arc)
verts, uvs, faces = [], [], []
for i in range(n + 1):
    t = i / n
    ang = -arc / 2 + arc * t                              # 0 = straight ahead (+z)
    x, z = args.radius * math.sin(ang), args.radius * math.cos(ang)
    for j, y in enumerate((0.0, args.height)):
        verts.append((x, y, z)); uvs.append((t, 1 - j))   # glTF: v = 0 at the image's top
for i in range(n):
    b, t_ = 2 * i, 2 * i + 1
    nb, nt = 2 * (i + 1), 2 * (i + 1) + 1
    faces += [(b, nt, t_), (b, nb, nt), (b, t_, nt), (b, nt, nb)]   # both sides
mesh = trimesh.Trimesh(np.array(verts), np.array(faces), process=False)
mesh.visual = trimesh.visual.TextureVisuals(uv=np.array(uvs), image=img)
mesh.export(args.out)
print("written", args.out)
