"""Build the puddle pieces: the ONLY reflective ground surface in the game.

DESIGN-TIME ONLY - not compiled into the Unity build, never shipped.

Todd, 2026-09-25: "the sheen AT ALL is a bug, maybe there should be a sheen in puddles, but not over
all." The ground was made matte in response (scripts/ground_build.py -- roughness 0.87-0.89 everywhere).
A puddle is therefore not a roughness value smeared over the floor; it is a discrete place where water
sits, and it is the only thing in the maze that can throw the moon or a lantern back at the camera.

What this writes, into the ground output dir:
  T_Ground_PuddleAlpha.png   512x512  coverage mask: 1 inside the water, 0 on dry ground, soft rim
  T_Ground_Puddle.png        512x512  albedo: dark wet earth, darker than any shipped ground (water reads
                                      dark because it absorbs, and it sits in a low spot that collects silt)
  T_Ground_Puddle_N.png      512x512  normal: a nearly flat water plane with a faint rim lip and slow
                                      ripple, so a specular highlight breaks up instead of reading as a disc

Puddles are NOT tiled across the world. They are decals placed at build time in lane low spots (a handful
per level, not a carpet); this script only supplies the shape and the surface. Elongated along the lane
axis so a puddle fills a rut instead of looking like a coin.
"""
import os
import numpy as np
from PIL import Image

OUT = os.environ.get("PUDDLE_OUT", "/tmp/puddle")
os.makedirs(OUT, exist_ok=True)
SIZE = 512
SEED = 4177
rng = np.random.default_rng(SEED)


def value_noise(size, cells, rng):
    g = rng.random((cells + 1, cells + 1))
    # wrap-free upsample (decals are placed, not tiled, so seams do not matter here)
    from numpy import linspace
    x = linspace(0, cells, size, endpoint=False)
    xi = np.floor(x).astype(int)
    xf = x - xi
    xf = xf * xf * (3 - 2 * xf)
    gx = g[np.ix_(xi, xi)]
    gx1 = g[np.ix_(np.minimum(xi + 1, cells), xi)]
    gy = g[np.ix_(xi, np.minimum(xi + 1, cells))]
    gy1 = g[np.ix_(np.minimum(xi + 1, cells), np.minimum(xi + 1, cells))]
    a = gx * (1 - xf)[:, None] + gx1 * xf[:, None]
    b = gy * (1 - xf)[:, None] + gy1 * xf[:, None]
    return a * (1 - xf)[None, :] + b * xf[None, :]


def fbm(size, rng, octaves=6, base=3):
    out = np.zeros((size, size))
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        out += amp * value_noise(size, base * (2 ** o), rng)
        tot += amp
        amp *= 0.55
    return out / tot


# --- the blot ------------------------------------------------------------------------------------
# First attempt read as a painted black hole: bulbous round lobes with a hard rim and no damp ring
# around the waterline. A puddle read from above is carried by three cues, in this order:
#   1. a DAMP RING wider than the water, where the ground is darker but not yet wet,
#   2. a ragged, warped waterline -- water edges follow the ground's low spots, they do not arc,
#   3. water that is dark but not black, and still shows the ground's grain underneath it.
yy, xx = np.mgrid[0:SIZE, 0:SIZE].astype(float)
u = xx / SIZE          # across the lane
v = yy / SIZE          # along the lane (puddles stretch this way)

# domain warp: push the coordinates through noise FIRST, so no lobe can stay round
warp = fbm(SIZE, rng, octaves=4, base=3)
warp2 = fbm(SIZE, rng, octaves=4, base=5)
uw = u + 0.11 * (warp - 0.5)
vw = v + 0.16 * (warp2 - 0.5)

field = np.zeros((SIZE, SIZE))
for _ in range(rng.integers(3, 5)):
    cu = rng.uniform(0.34, 0.66)
    cv = rng.uniform(0.26, 0.74)
    ru = rng.uniform(0.075, 0.130)        # narrower across than along, but not a stripe
    rv = rng.uniform(0.095, 0.175)        # three parallel streaks read as scratches, not water
    ang = rng.uniform(-0.22, 0.22)
    du, dv = uw - cu, vw - cv
    du2 = du * np.cos(ang) - dv * np.sin(ang)
    dv2 = du * np.sin(ang) + dv * np.cos(ang)
    field = np.maximum(field, 1.0 - np.sqrt((du2 / ru) ** 2 + (dv2 / rv) ** 2))

# ragged waterline, then the water and the damp ring around it
edge = field + 0.34 * (fbm(SIZE, rng, octaves=5, base=4) - 0.5)
water = np.clip((edge - 0.00) / 0.16, 0.0, 1.0)             # 1 = wet, fades out over the rim
water = water * water * (3 - 2 * water)
# the damp ring: ground outside the water, darkened but not covered
# The ring must HUG the waterline and die before the decal's border, and its width has to be a real
# distance from the water edge. Deriving it from a second noise field does not work: a band-limited
# field clusters around its mean, so a band of +/- 0.08 in field units covered 94% of the quad and the
# whole decal became one big damp patch. Width now comes from the distance to the waterline itself.
try:
    from scipy.ndimage import gaussian_filter
    near = gaussian_filter(water, 5.0)
except Exception:                                        # no scipy: a box blur is close enough
    k = 9
    pad = np.pad(water, k, mode="edge")
    c = np.cumsum(np.cumsum(pad, 0), 1)
    c = np.pad(c, ((1, 0), (1, 0)))
    near = (c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]) / float(k * k)
damp = np.clip(near * 2.6, 0.0, 1.0) * (1.0 - water)    # a halo of wet ground outside the waterline
damp *= np.clip((0.5 - np.hypot(u - 0.5, v - 0.5)) / 0.12, 0.0, 1.0)   # and nothing at the quad border

ALPHA_WATER = 0.86     # never fully opaque: the ground's grain must still read through the water
ALPHA_DAMP = 0.34      # the wet ring only darkens
alpha = np.clip(water * ALPHA_WATER + damp * ALPHA_DAMP, 0.0, ALPHA_WATER)
core = water > 0.95

# --- albedo: dark wet earth, and not black ---------------------------------------------------------
mud = fbm(SIZE, rng, octaves=7, base=6)
# 0.10 is dark enough to read as water and light enough to keep the ground's structure underneath
base_rgb = np.stack([0.105 + 0.035 * mud, 0.098 + 0.033 * mud, 0.092 + 0.031 * mud], axis=-1)
alb = base_rgb                                                  # constant under water and ring:
# the decal is ALPHA blended over the ground, so the darkening is done by the alpha, not the colour

# --- normal: a flat plane with a faint lip and a slow ripple --------------------------------------
# height: flat inside the water, a small lip at the rim (silt banks up), gentle swell in the middle
h = alpha * 0.35 + core * 0.06 * fbm(SIZE, rng, octaves=3, base=5)
gy, gx = np.gradient(h)
strength = 2.2
n = np.stack([-gx * strength, -gy * strength, np.ones_like(h)], axis=-1)
n /= np.linalg.norm(n, axis=-1, keepdims=True)
nrm = ((n * 0.5 + 0.5) * 255).astype(np.uint8)

stats = {
    "water core (alpha>0.8)": float((alpha > 0.8).mean()),
    "wet or damp (alpha>0.15)": float((alpha > 0.15).mean()),
    "composited value in water (over the lane)": float((0.86 * alb[..., 0][core].mean() + 0.14 * 0.45)),
    "albedo is not black": float(alb[..., 0][core].mean()),
    "max opacity (must be < 1 so grain survives)": float(alpha.max()),
}

Image.fromarray((np.clip(alpha, 0, 1) * 255).astype(np.uint8), "L").save(f"{OUT}/T_Ground_PuddleAlpha.png")
Image.fromarray((np.clip(alb, 0, 1) * 255).astype(np.uint8), "RGB").save(f"{OUT}/T_Ground_Puddle.png")
Image.fromarray(nrm, "RGB").save(f"{OUT}/T_Ground_Puddle_N.png")

print(f"[puddle] {OUT} seed {SEED}, {SIZE}px")
for k, v in stats.items():
    print(f"[puddle]   {k}: {v:.4f}")
print("[puddle] a puddle is dark as well as smooth: water absorbs, and it sits in the low spot")
print("[puddle] NOTE: place these as decals in lane low spots, a handful per level -- never tiled")
