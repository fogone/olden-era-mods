"""A seamless texture of dry ground: warm earth with soft patches, pebbles and fine cracks.

    python ground.py <out.png> [--size 1024] [--seed 7] [--base "#9a5b3a"] [--light "#c48a5e"] [--dark "#6e3b26"]

Every noise is periodic (made in the frequency domain) and the cracks are a periodic Voronoi, so it tiles both ways.
"""
import argparse
import numpy as np
from PIL import Image

a = argparse.ArgumentParser()
a.add_argument("out"); a.add_argument("--size", type=int, default=1024); a.add_argument("--seed", type=int, default=7)
a.add_argument("--base", default="#9a5b3a"); a.add_argument("--light", default="#c4895c"); a.add_argument("--dark", default="#6a3a26")
args = a.parse_args()
N = args.size
rng = np.random.default_rng(args.seed)
rgb = lambda c: np.array([int(c[i:i + 2], 16) for i in (1, 3, 5)], float)

def noise(scale, power):
    f = np.hypot(np.fft.fftfreq(N)[None, :], np.fft.fftfreq(N)[:, None]) * N; f[0, 0] = 1
    amp = 1 / f ** power; amp[f < scale] = 0
    n = np.real(np.fft.ifft2(amp * np.exp(2j * np.pi * rng.random((N, N)))))
    return (n - n.min()) / (n.max() - n.min())

patch = noise(2, 2.2); mid = noise(10, 1.6); grain = noise(80, 1.0)
t = np.clip(patch * 0.7 + mid * 0.3, 0, 1)
col = rgb(args.dark) * (1 - t[..., None]) + rgb(args.light) * t[..., None]
col = col * 0.55 + rgb(args.base) * 0.45
col *= (0.9 + 0.2 * grain)[..., None]

# Cracks: a periodic Voronoi's cell borders (distance to the nearest point minus to the second).
pts = rng.random((70, 2)) * N
yy, xx = np.mgrid[0:N, 0:N].astype(float)
d1 = np.full((N, N), np.inf); d2 = np.full((N, N), np.inf)
for px, py in pts:
    dx = np.abs(xx - px); dx = np.minimum(dx, N - dx); dy = np.abs(yy - py); dy = np.minimum(dy, N - dy)
    d = np.hypot(dx, dy)
    d2 = np.where(d < d1, d1, np.minimum(d2, d)); d1 = np.minimum(d1, d)
edge = d2 - d1
crack = np.exp(-(edge / 1.6) ** 2) * np.clip((noise(4, 2.0) - 0.5) * 6, 0, 1)
col *= (1 - 0.35 * crack)[..., None]

# Pebbles: small light dots with a dark rim.
for _ in range(350):
    px, py, r = rng.random() * N, rng.random() * N, 1.5 + rng.random() * 3.5
    dx = np.abs(xx - px); dx = np.minimum(dx, N - dx); dy = np.abs(yy - py); dy = np.minimum(dy, N - dy)
    d = np.hypot(dx, dy)
    m = d < r * 1.6
    if not m.any(): continue
    shade = np.where(d < r, 1.12 - 0.12 * (d / r), 0.9)
    col[m] *= shade[m][:, None]

Image.fromarray(np.clip(col, 0, 255).astype(np.uint8)).save(args.out)
print("written", args.out)
