"""A seamless texture of layered rock (canyon sandstone): horizontal bands of colours, warped by noise, with cracks.

    python strata.py <out.png> [--size 1024] [--seed 3] [--colors "#8a4a32,#b8724a,#d9a77a,#7a3f2c,#c98a5c,#e6c39a"]

Every noise is periodic (made in the frequency domain), so the texture tiles both ways.
"""
import argparse
import numpy as np
from PIL import Image

a = argparse.ArgumentParser()
a.add_argument("out"); a.add_argument("--size", type=int, default=1024); a.add_argument("--seed", type=int, default=3)
a.add_argument("--colors", default="#7c3f2a,#a65f3d,#c98a5c,#e2b98c,#8e4a31,#b8764c,#d7a576,#6e3626")
args = a.parse_args()
N = args.size
rng = np.random.default_rng(args.seed)

def noise(scale, power=2.0):
    """Periodic fractal noise: random phases, amplitude falling with frequency; scale = the largest feature's count."""
    fx = np.fft.fftfreq(N)[None, :] * N; fy = np.fft.fftfreq(N)[:, None] * N
    f = np.hypot(fx, fy); f[0, 0] = 1
    amp = 1 / f ** power; amp[f < scale] = 0; amp[0, 0] = 0
    n = np.real(np.fft.ifft2(amp * np.exp(2j * np.pi * rng.random((N, N)))))
    return (n - n.min()) / (n.max() - n.min())

y = np.arange(N)[:, None] / N
warp = noise(2, 2.2) - 0.5
fine = noise(8, 1.6) - 0.5
# Bands: a periodic band index along y, warped; their colours from the list, picked in a repeating random order.
colors = np.array([[int(c[i:i + 2], 16) for i in (1, 3, 5)] for c in args.colors.split(",")], float)
bands = 14
order = rng.integers(0, len(colors), bands)
t = ((y + warp * 0.08 + fine * 0.01) % 1.0) * bands
i0 = np.floor(t).astype(int) % bands; i1 = (i0 + 1) % bands; w = t - np.floor(t)
w = np.clip((w - 0.8) / 0.2, 0, 1)                    # sharp band edges
base = colors[order[i0]] * (1 - w[..., None]) + colors[order[i1]] * w[..., None]
# Grain and shading.
grain = noise(64, 1.2); detail = noise(16, 1.8)
shade = 0.82 + 0.25 * detail + 0.08 * (grain - 0.5)
# Cracks: thin dark lines where a noise crosses its middle, mostly vertical (stretched noise).
cr = noise(6, 2.0)
cracks = np.exp(-((cr - 0.5) / 0.012) ** 2) * (noise(4, 2.0) > 0.45)
shade *= 1 - 0.45 * cracks
img = np.clip(base * shade[..., None], 0, 255).astype(np.uint8)
Image.fromarray(img).save(args.out)
print("written", args.out)
