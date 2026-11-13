#!/usr/bin/env python3
"""Van Gogh-style torch-lit Boat of Heaven - a procedural painterly animation.

The scene (sky, water, quay, boat, torches, the Octaquetra, temple light, dawn) is computed as an
"underpainting" at half resolution; every brushstroke of a precomputed stroke layout then takes the
average colour beneath it and is repainted in ultramarine and gold. One painting is made every two
seconds and each video frame dissolves between the two nearest paintings.

Needs numpy, scipy and Pillow.

As a script:
  paint_vangogh.py maps              build the three stroke layouts (once; seeds are fixed)
  paint_vangogh.py still T out.png   one painting at time T (seconds), on layout 0
  paint_vangogh.py frame T out.png   the video frame at time T (the dissolve between two paintings)

As a module (this is how the VanGoghPaintingDemo application drives it):
  init(map_dir)                      build or load the stroke layouts kept in map_dir
  frame_into(t, address)             write the RGBA video frame at time t into W*H*4 bytes at address
  shutdown()                         stop the background painter
"""
import sys, os, math, threading
from concurrent.futures import ThreadPoolExecutor
import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import gaussian_filter, zoom, distance_transform_edt

W, H = 1080, 1920
HW, HH = W // 2, H // 2
YH = 824.0                      # horizon (full-res px)
QY = 1655.0                     # foreground quay edge
TOTAL = 305.56
KEY_SECONDS = 2.0               # one painting every two seconds
MAP_COUNT = 3                   # stroke layouts, used in turn
MAP_DIR = os.path.dirname(os.path.abspath(__file__))

# ---------------- timeline (seconds) ----------------
# video time = audio time + 8 s (8 s lead for a title card)
TL = dict(
    fade_in=(8.0, 17.0),
    quay_torches=[9.5, 13.0, 16.0, 18.5, 20.5],
    boat_in=(14.0, 28.0),         # boat fades up out of the dark
    approach=(14.0, 184.0),       # far -> docked
    oct=(96.0, 114.0),            # the Octaquetra grows 102-114
    doors=(186.0, 198.0),         # temple doors open to the sky
    dawn=(218.0, 272.0),          # golden tide -> dawn
    fade_out=(302.5, 305.56),
)

def ss(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)

def quay_y(x):
    return QY + 5 * np.sin(x / 41.0) + 3 * np.sin(x / 13.0 + 1.0)

# ---------------- the Octaquetra (eight-pointed star) ----------------
STAR_C = (655.0, 390.0)
R_STAR = 215.0
VALLEY = 0.426

def star_grow(t):
    a, b = TL["oct"]
    return float(ss(a + 6, b, t))

def star_edge_r(phi, R):
    # radius of the 8-point star edge at angle phi (0 = up), equal points, valley ratio VALLEY
    seg = math.pi / 8
    a = (phi % (2 * seg))
    a = a if a <= seg else 2 * seg - a
    p1 = np.array([R, 0.0]); p2 = np.array([R * VALLEY * math.cos(seg), R * VALLEY * math.sin(seg)])
    dv = p2 - p1; d = np.array([math.cos(a), math.sin(a)])
    return (p1[0] * dv[1] - p1[1] * dv[0]) / (d[0] * dv[1] - d[1] * dv[0])

# ---------------- stroke layouts ----------------
VG_SCALE = 2.4
def vg_sky_angle(x, y):
    # swirl around the star blended with rolling horizontal waves
    dx, dy = x - STAR_C[0], y - STAR_C[1]
    r = math.hypot(dx, dy) + 1e-3
    tang = math.atan2(dy, dx) + math.pi / 2
    w = math.exp(-max(0.0, r - 60) / 260)
    roll = 0.55 * math.sin(x / 170 + y / 140) + 0.35 * math.sin(y / 90 - x / 260)
    vx = (1 - w) * math.cos(roll) + w * math.cos(tang)
    vy = (1 - w) * math.sin(roll) + w * math.sin(tang)
    return math.atan2(vy, vx)

def gen_map(seed):
    sc = VG_SCALE
    rng = np.random.default_rng(seed)
    S = []
    # sky
    y = -12.0
    while y < YH + 14:
        wid = rng.uniform(13, 21) * sc
        x = -40 + rng.uniform(0, 40)
        while x < W + 40:
            ln = rng.uniform(50, 88) * sc
            ang = vg_sky_angle(x, y) + rng.normal(0, 0.07)
            S.append((x + rng.normal(0, 6), y + rng.normal(0, 4), ln, wid, ang))
            x += ln * 0.5
        y += wid * 0.55
    # below horizon: water + land
    y = YH - 2
    while y < H + 14:
        p = max(0.0, (y - YH) / (H - YH))
        wid0 = (3.0 + 12 * p) * (1.6 + 1.2 * p)
        x = -40 + rng.uniform(0, 30)
        while x < W + 40:
            if y > quay_y(x) + 4:
                ln = rng.uniform(0.35, 0.6) * (20 + 80 * p) * sc
                wid = wid0 * rng.uniform(1.1, 1.6)
                ang = rng.normal(0, 0.5)
            else:
                ln = rng.uniform(0.7, 1.25) * (18 + 85 * p) * sc
                wid = wid0 * rng.uniform(0.8, 1.2)
                ang = rng.normal(0, 0.05)
            S.append((x + rng.normal(0, 3), y + rng.normal(0, wid0 * 0.3), ln, wid, ang))
            x += ln * 0.5
        y += wid0 * 0.5
    # detail pass over the focal band
    NDET = int(5000 / sc ** 1.6)
    for _ in range(NDET):
        x = rng.uniform(150, W); y = rng.uniform(250, 1560)
        if y < YH:
            ln = rng.uniform(26, 46) * sc * 0.8; wid = rng.uniform(7, 12) * sc * 0.8
            ang = vg_sky_angle(x, y) + rng.normal(0, 0.1)
        else:
            p = (y - YH) / (H - YH)
            ln = (12 + 50 * p) * rng.uniform(0.6, 1.1) * sc * 0.8; wid = (2.5 + 8 * p) * rng.uniform(0.8, 1.2) * sc * 0.8
            ang = rng.normal(0, 0.25) if y < QY else rng.normal(0, 0.6)
        S.append((x, y, ln, wid, ang))
    nstar = 0
    Rs, (scx, scy) = R_STAR, STAR_C
    for _ in range(900):            # transition ring: strokes shrink toward the star
        r = Rs * rng.uniform(0.95, 2.4); th = rng.uniform(0, 2 * math.pi)
        x, y = scx + r * math.cos(th), scy + r * math.sin(th)
        g = 0.42 + 0.5 * min(1.0, (r / Rs - 0.95) / 1.45)
        S.append((x, y, rng.uniform(50, 88) * sc * g, rng.uniform(13, 21) * sc * g,
                  vg_sky_angle(x, y) + rng.normal(0, 0.07)))
        nstar += 1
    for _ in range(1700):           # star body: strokes run along each ray, curl round the disc
        r = Rs * 1.06 * math.sqrt(rng.uniform(0, 1)); th = rng.uniform(0, 2 * math.pi)
        x, y = scx + r * math.sin(th), scy - r * math.cos(th)
        if r < 0.25 * Rs:
            ang = math.atan2(y - scy, x - scx) + math.pi / 2 + rng.normal(0, 0.15)
            ln, wd = rng.uniform(0.11, 0.17) * Rs, rng.uniform(0.045, 0.06) * Rs
        elif r < star_edge_r(th, Rs) * 1.04:
            j = round(th / (math.pi / 4)); axis = j * math.pi / 4
            ang = math.atan2(-math.cos(axis), math.sin(axis)) + rng.normal(0, 0.08)
            ln, wd = rng.uniform(0.17, 0.30) * Rs, rng.uniform(0.05, 0.075) * Rs * (1.1 - 0.4 * r / Rs)
        else:
            ang = math.atan2(y - scy, x - scx) + math.pi / 2 + rng.normal(0, 0.1)
            ln, wd = rng.uniform(0.18, 0.26) * Rs, rng.uniform(0.06, 0.08) * Rs
        S.append((x, y, ln, wd, ang))
        nstar += 1
    S = np.array(S, dtype=np.float64)
    nb = len(S) - nstar
    order = rng.permutation(nb)
    det0 = nb - NDET
    order = np.concatenate([order[order < det0], order[order >= det0], np.arange(nb, len(S))])
    S = S[order]
    N = len(S) + 1
    # paint every stroke with its own index as the colour: later strokes cover earlier ones
    img = Image.new("RGB", (W, H), (0, 0, 0))
    d = ImageDraw.Draw(img)
    sgrid = np.linspace(-0.5, 0.5, 9)
    prof = (1 - (2 * sgrid) ** 4) ** 0.5
    bends = rng.normal(0, 0.13, len(S))
    for i, (cx, cy, ln, wid, ang) in enumerate(S, start=1):
        ca, sa = math.cos(ang), math.sin(ang)
        al = sgrid * ln
        off = bends[i - 1] * ln * (4 * sgrid ** 2 - 1)
        top = off + prof * wid / 2
        bot = off - prof * wid / 2
        px = np.concatenate([al, al[::-1]]); py = np.concatenate([top, bot[::-1]])
        X = cx + px * ca - py * sa; Y = cy + px * sa + py * ca
        d.polygon(list(zip(X.tolist(), Y.tolist())), fill=(i >> 16, (i >> 8) & 255, i & 255))
    a = np.asarray(img).astype(np.int32)
    L = (a[..., 0] << 16) | (a[..., 1] << 8) | a[..., 2]
    # pixels no stroke covered go to the nearest stroke
    _, (iy, ix) = distance_transform_edt(L == 0, return_indices=True)
    L = L[iy, ix]
    cx = np.concatenate([[0], S[:, 0]]); cy = np.concatenate([[0], S[:, 1]])
    ln = np.concatenate([[50], S[:, 2]]); an = np.concatenate([[0], S[:, 4]])
    ph = rng.uniform(0, 6.283, N)
    Yg, Xg = np.mgrid[0:H, 0:W].astype(np.float32)
    A_ = an[L]; dx = Xg - cx[L]; dy = Yg - cy[L]
    perp = -dx * np.sin(A_) + dy * np.cos(A_)
    along = dx * np.cos(A_) + dy * np.sin(A_)
    ka = 1.9
    # bristle ridges across the stroke, and a slight ramp along it
    br = 1 + ka * (0.07 * np.sin(perp * 0.9 + ph[L]) + 0.045 * np.sin(perp * 0.35 + 2 * ph[L])) \
        + 0.07 * ka * (along / ln[L])
    # stroke outlines
    e = ((L != np.roll(L, 1, 0)) | (L != np.roll(L, 1, 1))).astype(np.float32)
    e = gaussian_filter(e, 1.3)
    os.makedirs(MAP_DIR, exist_ok=True)
    np.savez(os.path.join(MAP_DIR, f"vgmap{seed}.npz"), L=L.astype(np.int32),
             br=br.astype(np.float16), edge=np.clip(e * 1.6, 0, 1).astype(np.float16), n=np.int32(N))
    return N

_MAPS = {}
def get_map(k):
    if k not in _MAPS:
        z = np.load(os.path.join(MAP_DIR, f"vgmap{k}.npz"))
        _MAPS[k] = (z["L"].ravel(), int(z["n"]), z["br"].astype(np.float32), z["edge"].astype(np.float32))
    return _MAPS[k]

# ---------------- Van Gogh palette remap ----------------
COOL = np.array([[0.03, 0.04, 0.20], [0.08, 0.12, 0.52], [0.14, 0.26, 0.78], [0.30, 0.55, 0.88], [0.78, 0.88, 0.96]])
WARM = np.array([[0.30, 0.10, 0.06], [0.82, 0.33, 0.06], [0.97, 0.58, 0.10], [1.00, 0.82, 0.30], [1.00, 0.96, 0.74]])
ACC_C = np.array([[0.10, 0.45, 0.48], [0.32, 0.18, 0.55], [0.05, 0.30, 0.62]])   # teal, violet, prussian
ACC_W = np.array([[0.85, 0.20, 0.10], [0.95, 0.88, 0.25], [0.70, 0.45, 0.10]])   # vermilion, lemon, ochre
def ramp(pal, t):
    x = np.clip(t, 0, 1) * (len(pal) - 1)
    i = np.minimum(x.astype(int), len(pal) - 2); f = (x - i)[:, None]
    return pal[i] * (1 - f) + pal[i + 1] * f
_VGR = np.random.default_rng(5)
VG_R = _VGR.random((200000, 4))
def vg_remap(m):
    n = len(m)
    R = VG_R[:n]
    lum = m @ np.array([0.30, 0.59, 0.11])
    t = np.clip(lum * 2.4, 0, 1) ** 0.55 + (R[:, 0] - 0.5) * 0.16
    warmth = (m[:, 0] - m[:, 2]) / (lum + 0.03)
    w = np.clip((warmth - 0.35) / 0.9, 0, 1)
    w = w * w * (3 - 2 * w)
    c = ramp(COOL, t) * (1 - w[:, None]) + ramp(WARM, t * 0.95 + 0.05) * w[:, None]
    acc = R[:, 1] < 0.13
    k = (R[:, 2] * 3).astype(int)
    ac = np.where(w[:, None] > 0.5, ACC_W[k], ACC_C[k]) * (0.55 + 0.6 * t[:, None])
    c = np.where(acc[:, None], c * 0.45 + ac * 0.55, c)
    return c

# ---------------- static scene bits ----------------
Yh, Xh = np.mgrid[0:HH, 0:HW].astype(np.float32)
Xh = Xh * 2 + 1; Yh = Yh * 2 + 1          # full-res coords on half grid
P = np.clip((Yh - YH) / (H - YH), 0, 1)
SKY = Yh < YH
LAND = (~SKY) & (Yh > quay_y(Xh))
WATER = (~SKY) & (~LAND)
_rng = np.random.default_rng(7)
STARS = np.stack([_rng.uniform(0, W, 220), _rng.uniform(0, YH - 40, 220) ** 1.0,
                  _rng.uniform(0.2, 1, 220) ** 2, _rng.uniform(0, 6.28, 220)], 1)
LOWN = gaussian_filter(_rng.normal(0, 1, (HH, HW)).astype(np.float32), 18) * 8
LOWN2 = gaussian_filter(_rng.normal(0, 1, (HH, HW)).astype(np.float32), 5) * 3
Yf, Xf = np.mgrid[0:H, 0:W].astype(np.float32)
VIG = (1 - 0.38 * (((Xf - W / 2) / (W * 0.75)) ** 2 + ((Yf - H * 0.48) / (H * 0.62)) ** 2)).clip(0.4, 1)
del Yf, Xf

def zig_poly():
    # the stepped temple tower on the far shore
    x0, base = 40, YH + 2
    steps = [(0, 230, 0), (25, 205, 22), (52, 178, 40), (80, 150, 56), (100, 130, 68)]
    left = []
    for (a, b, h) in steps:
        top = base - h - 16
        left += [(x0 + a, base - h), (x0 + a, top)]
    right = []
    for (a, b, h) in reversed(steps):
        top = base - h - 16
        right += [(x0 + b, top), (x0 + b, base - h)]
    return [(x0 - 40, base)] + left + right + [(x0 + 320, base)]
ZIG = zig_poly()

def mask_from_polys(polys):
    im = Image.new("L", (HW, HH), 0); d = ImageDraw.Draw(im)
    for poly in polys:
        if len(poly) > 2:
            d.polygon([(x / 2, y / 2) for x, y in poly], fill=255)
    return np.asarray(im, dtype=np.float32) / 255

def ellipse_mask(cx, cy, r):
    im = Image.new("L", (HW, HH), 0); d = ImageDraw.Draw(im)
    d.ellipse([(cx - r) / 2, (cy - r) / 2, (cx + r) / 2, (cy + r) / 2], fill=255)
    return np.asarray(im, dtype=np.float32) / 255

ZIGM = mask_from_polys([ZIG])
def _shore():
    pts = [(300, YH + 3)]
    rng = np.random.default_rng(11)
    for x in np.linspace(300, W + 10, 60):
        pts.append((x, YH - 3 - 4 * rng.random() - (10 * rng.random() if rng.random() < 0.15 else 0)))
    pts.append((W + 10, YH + 3))
    return mask_from_polys([pts])
SHORE = _shore()

# ---------------- boat geometry (local units, 1 = 1000px*scale) ----------------
def boat_parts():
    xs = np.linspace(0.0, 1.45, 30)
    top = -0.10 - 0.06 * ((xs - 0.72) / 0.72) ** 2 - 0.12 * np.clip((xs - 1.25) / 0.2, 0, 1) ** 2
    hull = [(-0.03, -0.06), (-0.02, -0.11)] + list(zip(xs, top)) + [(1.52, -0.30), (1.5, -0.2), (1.45, 0.0), (0.0, 0.0)]
    cl = np.array([(0.00, -0.06), (-0.035, -0.2), (-0.03, -0.34), (0.01, -0.47), (0.08, -0.56),
                   (0.155, -0.585), (0.205, -0.555), (0.215, -0.51)])
    wd = np.linspace(0.075, 0.014, len(cl))
    tg = np.gradient(cl, axis=0); nrm = np.stack([-tg[:, 1], tg[:, 0]], 1)
    nrm /= np.linalg.norm(nrm, axis=1, keepdims=True)
    horn = [tuple(p) for p in (cl + nrm * wd[:, None] / 2)] + [tuple(p) for p in (cl - nrm * wd[:, None] / 2)[::-1]]
    shrine = [(0.50, -0.13), (0.50, -0.30), (0.56, -0.345), (0.63, -0.355), (0.70, -0.345), (0.76, -0.30), (0.76, -0.13)]
    door = [(0.585, -0.14), (0.585, -0.27), (0.63, -0.29), (0.675, -0.27), (0.675, -0.14)]
    poles = [[(0.335, -0.12), (0.345, -0.12), (0.345, -0.47), (0.335, -0.47)],
             [(1.015, -0.15), (1.025, -0.15), (1.025, -0.45), (1.015, -0.45)]]
    return dict(hull=hull, horn=horn, shrine=shrine, door=door, poles=poles,
                flames=[(0.34, -0.47), (1.02, -0.45)], glow=(0.63, -0.22))
BP = boat_parts()

def boat_state(t):
    a, b = TL["approach"]
    u = np.clip((t - a) / (b - a), 0, 1)
    u = 1 - (1 - u) ** 1.6                    # slows as it nears the quay
    d = 0.035 + (1 - 0.035) * u
    yw = YH + d * (1500 - YH)
    sc = (yw - YH) / (1380 - YH)
    xp = 760 + (330 - 760) * u ** 1.3
    bob = math.sin(t * 0.8) * 5 * sc
    tilt = 0.010 * math.sin(t * 0.8 + 0.7)
    vis = float(ss(*TL["boat_in"], t))
    return xp, yw + bob, sc, tilt, vis

def to_img(pts, xp, yw, sc, tilt):
    c, s = math.cos(tilt), math.sin(tilt)
    out = []
    for (x, y) in pts:
        X = x * 1000 * sc; Y = y * 1000 * sc
        out.append((xp + X * c - Y * s, yw + X * s + Y * c))
    return out

# ---------------- flames / points of light ----------------
def _flame(E, fx, fy, h, t, ph, I):
    """fx,fy = flame base (full-res); h = height full-res px."""
    if I <= 0.01 or h < 1.5:
        return
    hh = max(h, 3.0)
    x0 = int(max(0, (fx - hh) / 2)); x1 = int(min(HW, (fx + hh) / 2 + 2))
    y0 = int(max(0, (fy - hh * 1.4) / 2)); y1 = int(min(HH, (fy + hh * 0.3) / 2 + 2))
    if x1 <= x0 or y1 <= y0:
        return
    X = Xh[y0:y1, x0:x1]; Y = Yh[y0:y1, x0:x1]
    v = (fy - Y) / hh                       # 0 base .. 1 tip
    sway = hh * 0.12 * np.sin(v * 5 + t * 11 + ph) * v + hh * 0.05 * math.sin(t * 3.1 + ph)
    wdt = hh * 0.30 * np.clip(1 - v, 0, 1) ** 0.7 * (0.2 + 0.8 * np.clip(v * 4, 0, 1)) + 0.6
    dx = (X - fx - sway) / wdt
    tip = 1.0 + 0.15 * math.sin(t * 17 + ph) + 0.08 * math.sin(t * 31 + ph * 3)
    inside = np.exp(-dx ** 2) * np.clip(1 - v / tip, 0, 1) ** 0.8 * (v > -0.15) * np.exp(-np.clip(-v, 0, None) * 12)
    core = inside ** 2.5
    E[y0:y1, x0:x1, 0] += I * (1.0 * inside + 0.4 * core)
    E[y0:y1, x0:x1, 1] += I * (0.42 * inside + 0.55 * core)
    E[y0:y1, x0:x1, 2] += I * (0.08 * inside + 0.35 * core)

def add_flame(E, fx, fy, h, t, ph, I):
    _flame(E, fx, fy, h, t, ph, I)
    if h > 20:
        _flame(E, fx - 0.16 * h, fy - 0.02 * h, h * (0.55 + 0.1 * math.sin(t * 7 + ph)), t * 1.3, ph + 2.0, I * 0.6)
        _flame(E, fx + 0.15 * h, fy - 0.03 * h, h * (0.48 + 0.12 * math.sin(t * 9 + ph * 2)), t * 1.2, ph + 4.1, I * 0.55)

def splat(img, x, y, r, col, a):
    x0 = int(max(0, x - 3 * r)); x1 = int(min(W, x + 3 * r + 1))
    y0 = int(max(0, y - 3 * r)); y1 = int(min(H, y + 3 * r + 1))
    if x1 <= x0 or y1 <= y0:
        return
    yy, xx = np.mgrid[y0:y1, x0:x1]
    g = np.exp(-((xx - x) ** 2 + (yy - y) ** 2) / (r * r)) * a
    img[y0:y1, x0:x1] += g[..., None] * np.asarray(col, np.float32)

def flicker(t, ph):
    return 0.86 + 0.09 * math.sin(t * 13 + ph) + 0.05 * math.sin(t * 29 + ph * 2.3)

def paint_star_base(img, f):
    # the eight-pointed star as flat paint colours in the underpainting; the stroke pass does the rest
    R = R_STAR * 1.08 * (0.35 + 0.65 * f)
    dx = Xh - STAR_C[0]; dy = Yh - STAR_C[1]
    r = np.sqrt(dx * dx + dy * dy); phi = np.arctan2(dx, -dy)
    seg = math.pi / 8
    a = np.mod(phi, 2 * seg); a = np.where(a <= seg, a, 2 * seg - a)
    p1x, p1y = R, 0.0; p2x, p2y = R * VALLEY * math.cos(seg), R * VALLEY * math.sin(seg)
    dvx, dvy = p2x - p1x, p2y - p1y
    re = (p1x * dvy - p1y * dvx) / (np.cos(a) * dvy - np.sin(a) * dvx)
    q = r / re                                   # <1 inside outer star
    u = r / R
    gold = np.array([0.50, 0.21, 0.0]); orange = np.array([0.36, 0.09, 0.0])
    light = np.array([0.56, 0.29, 0.02]); cream = np.array([0.62, 0.40, 0.10])
    c = gold + (orange - gold) * np.clip((u - 0.55) / 0.45, 0, 1)[..., None]
    c = np.where((q < 0.62)[..., None], light, c)
    c = np.where((u < 0.235)[..., None], cream, c)
    c = np.where(((u > 0.13) & (u < 0.18))[..., None], orange, c)
    c = np.where((u < 0.05)[..., None], np.array([0.03, 0.04, 0.15]), c)
    m = np.clip((1.0 - q) * R / 4 + 0.5, 0, 1) * float(np.clip(f * 1.4, 0, 1))
    return img * (1 - m[..., None]) + c * m[..., None]

# ---------------- scene ----------------
def sky_colour(t, D):
    v = np.clip((YH - Yh) / YH, 0, 1)
    nt = np.array([0.006, 0.010, 0.035]); nh = np.array([0.055, 0.075, 0.19])
    night = nh + (nt - nh) * (v[..., None] ** 0.55)
    dt_ = np.array([0.07, 0.10, 0.26]); dm = np.array([0.36, 0.28, 0.46]); dh = np.array([1.0, 0.60, 0.33])
    v3 = v[..., None]
    dawn = np.where(v3 < 0.25, dh + (dm - dh) * (v3 / 0.25), dm + (dt_ - dm) * np.clip((v3 - 0.25) / 0.75, 0, 1) ** 0.8)
    c = night * (1 - D) + dawn * D
    c = c * (1 + 0.10 * LOWN[..., None] * (0.5 + v3))
    return c

def render(t):
    """The underpainting at time t: (scene + lights, lights alone, fade-out factor), half resolution."""
    D = float(ss(*TL["dawn"], t))
    expo = float(ss(*TL["fade_in"], t)) * 0.85 + 0.15
    E = np.zeros((HH, HW, 3), np.float32)       # emissive
    lights = []                                  # (x, y, I, r)

    # torches: two tall ones on the foreground quay, three small ones at the far temple
    qdef = [(115, 1900, 1150, 1.0), (975, 1900, 1185, 1.0), (118, YH - 52, YH - 66, 0.07),
            (205, YH - 82, YH - 96, 0.07), (300, YH - 30, YH - 42, 0.06)]
    qt = []
    for i, ((bx, by, fy, pp), t_on) in enumerate(zip(qdef, TL["quay_torches"])):
        on = float(ss(t_on, t_on + 0.8, t)) * (1 + 0.6 * math.exp(-max(0, t - t_on - 0.4) * 2.5) * (t > t_on))
        I = on * flicker(t, i * 1.7)
        qt.append((bx, by, fy, pp, I))
        if I > 0:
            lights.append((bx, fy - 60 * pp, (1.2 if pp > 0.5 else 0.5) * I, 760 * pp + 25))

    xp, yw, sc, tilt, vis = boat_state(t)
    bflames = []
    for j, (lx, ly) in enumerate(BP["flames"]):
        (fx, fy), = to_img([(lx, ly)], xp, yw, sc, tilt)
        I = vis * flicker(t, 4 + j * 2.1)
        bflames.append((fx, fy, 90 * sc, I))
        lights.append((fx, fy - 40 * sc, 1.25 * I, 620 * sc + 15))
    (gx, gy), = to_img([BP["glow"]], xp, yw, sc, tilt)
    lights.append((gx, gy, 0.45 * vis, 260 * sc + 8))

    Ldoor = float(ss(*TL["doors"], t))

    # light field
    Lf = np.zeros((HH, HW), np.float32)
    for (lx, ly, I, r) in lights:
        Lf += I * np.exp(-((Xh - lx) ** 2 + ((Yh - ly) * 1.15) ** 2) / (r * r))
    warm = np.array([1.0, 0.52, 0.2], np.float32)
    Lw = Lf[..., None] * warm
    door_l = Ldoor * np.exp(-((Xh - 205) ** 2 + ((Yh - YH) * 2.5) ** 2) / (380.0 ** 2))
    Lw += door_l[..., None] * np.array([1.0, 0.72, 0.38], np.float32) * 0.5

    img = np.zeros((HH, HW, 3), np.float32)
    # ---- sky
    sk = sky_colour(t, D) * expo
    sk = sk + Lw * 0.05
    sun = (D ** 2) * np.exp(-((Xh - 780) ** 2 + ((Yh - YH) * 1.6) ** 2) / 240.0 ** 2)
    sk = sk + sun[..., None] * np.array([1.0, 0.55, 0.25]) * 0.8
    img[SKY] = sk[SKY]
    # sun disk
    sd = float(ss(TL["dawn"][0] + 22, TL["dawn"][1], t))
    if sd > 0:
        sy = YH + 30 - 55 * sd
        sm = ellipse_mask(780, sy, 34) * SKY
        E += sm[..., None] * np.array([1.0, 0.45, 0.12]) * 0.9 * sd
    # silhouettes on horizon
    zc = np.array([0.012, 0.012, 0.028]) * (1 - D) + np.array([0.20, 0.10, 0.12]) * D
    img = img * (1 - ZIGM[..., None]) + ZIGM[..., None] * (zc * expo + Lw * 0.02)
    img = img * (1 - SHORE[..., None]) + SHORE[..., None] * (zc * expo * 1.1)
    # ---- water
    hzc = (np.array([0.045, 0.065, 0.165]) * (1 - D) + np.array([0.85, 0.52, 0.34]) * D) * expo
    deep = np.array([0.012, 0.022, 0.055]) * (1 - D) + np.array([0.10, 0.09, 0.16]) * D
    pw = P[..., None]
    wbase = hzc * (1 - pw) ** 3.0 * 0.75 + deep * expo * (1 - (1 - pw) ** 3.0)
    per = 3.0 + 26 * P
    wave = np.sin(Yh / per + 2.4 * np.sin(Xh / (60 + 260 * P) + t * 0.55) + 1.3 * np.sin(Xh / (23 + 90 * P) - t * 0.8) + t * 1.3 + LOWN2 * 0.9)
    wave2 = np.sin(Yh / (per * 1.7) - Xh / (240 + 500 * P) - t * 0.9)
    patch = np.clip(0.55 + 0.6 * np.sin(Xh / (35 + 160 * P) + 1.7 * np.sin(Yh / (9 + 40 * P)) + LOWN * 0.8 - t * 0.35), 0, 1)
    hi = np.clip(wave * 0.7 + wave2 * 0.5 - 0.55, 0, 1) ** 1.4 * patch
    wc = wbase * (0.85 + 0.25 * wave2[..., None]) + hi[..., None] * (hzc * 0.55 + Lw * 0.55) + Lw * 0.06
    # broken reflections of boat torches + sun
    refl = np.zeros((HH, HW), np.float32)
    def reflect(x, ytop, length, wdt, I, ph):
        if I <= 0.01:
            return
        y0 = int(max(0, ytop / 2)); y1 = int(min(HH, (ytop + length) / 2))
        x0 = int(max(0, (x - 4 * wdt) / 2)); x1 = int(min(HW, (x + 4 * wdt) / 2 + 1))
        if y1 <= y0 or x1 <= x0:
            return
        X = Xh[y0:y1, x0:x1]; Y = Yh[y0:y1, x0:x1]; pp = P[y0:y1, x0:x1]
        wv = np.sin(Y / (2.2 + 14 * pp) + 1.2 * np.sin(X / 7 + t * 1.7 + ph) + t * 2.2 + ph)
        brk = np.clip(wv * 1.4 - 0.1, 0, 1)
        fall = np.clip(1 - (Y - ytop) / length, 0, 1) ** 1.2
        widen = wdt * (1 + 0.8 * (Y - ytop) / length)
        refl[y0:y1, x0:x1] += I * np.exp(-((X - x) / widen) ** 2) * brk * fall
    for j, (fx, fy, fh, I) in enumerate(bflames):
        reflect(fx, yw + 4 * sc, 560 * sc + 30, 16 * sc + 3, 0.95 * I, j * 2.0)
    reflect(gx, yw + 4 * sc, 300 * sc + 10, 20 * sc + 2, 0.35 * vis, 5.0)
    for (bx, by, fy, pp, I) in qt:
        if pp < 0.5:
            reflect(bx, YH + 3, 160, 4, 0.5 * I, bx)
    reflect(205, YH + 3, 600, 9, 0.5 * Ldoor, 21.0)
    for k in range(7):
        reflect(780 + 30 * math.sin(k * 1.7), YH + 2, 1100, 46 - k * 3, 0.55 * (D ** 2) * (1 if k == 0 else 0.35), 9 + k)
    f = star_grow(t)
    if f > 0:
        reflect(STAR_C[0], YH + 4, 420, 10, 0.22 * f * (1 - 0.5 * D), 13.0)
    wc = wc + refl[..., None] * np.array([1.0, 0.55, 0.22])
    # foam / lapping along hull waterline and quay edge
    lap = np.exp(-((Yh - (yw + 5 * sc)) / (6 * sc + 1.2)) ** 2) * (Xh > xp) * (Xh < xp + 1450 * sc) \
        * np.clip(np.sin(Xh / (16 * sc + 2) - t * 2.6) + 0.3, 0, 1)
    qe = quay_y(Xh)
    lapq = np.exp(-((Yh - qe + 6 + 4 * np.sin(t * 1.4 + Xh / 90)) / 9) ** 2) * np.clip(np.sin(Xh / 21 - t * 1.9) + 0.4 * np.sin(Xh / 7 + t * 3) + 0.2, 0, 1) * WATER
    wc = wc + (lap * vis + lapq)[..., None] * (hzc * 0.6 + Lw * 0.9 + 0.012)
    img[WATER] = wc[WATER]
    # ---- foreground quay (paving)
    q = np.clip((Yh - QY) / (H - QY), 0, 1)
    row = np.floor(np.sqrt(q) * 7.0)
    jx = np.mod(Xh / (150 + 160 * q) + row * 0.5, 1.0)
    joint = (np.mod(np.sqrt(q) * 7.0, 1.0) < 0.09) | (jx < 0.05)
    stone = np.array([0.045, 0.040, 0.037]) * (1 + 0.3 * LOWN2[..., None] / 3) * (1 + 0.15 * np.sin(row * 2.3 + np.floor(Xh / (150 + 160 * q)) * 1.7))[..., None]
    stone = stone * (1 - D) + np.array([0.16, 0.12, 0.11]) * D
    lc = stone * expo + stone * Lw * 3.0 + Lw * 0.01
    lc = np.where(joint[..., None], lc * 0.45, lc)
    lip = np.exp(-((Yh - quay_y(Xh) - 8) / 7) ** 2)
    lc = lc + lip[..., None] * (Lw * 0.25 + hzc * 0.3)
    img[LAND] = lc[LAND]

    # ---- boat
    if vis > 0.001:
        def M(key):
            return mask_from_polys([to_img(BP[key], xp, yw, sc, tilt)])
        hull = np.clip(M("hull") + M("horn"), 0, 1)
        sh = M("shrine"); dr = M("door")
        poles = mask_from_polys([to_img(p, xp, yw, sc, tilt) for p in BP["poles"]])
        reed = (0.7 + 0.3 * np.sin((Xh - xp) / (9 * sc + 1) + (Yh - yw) / (30 * sc + 1)))[..., None]
        wood = np.array([0.055, 0.034, 0.020]) * reed
        def lit(alb, k=4.5):
            return alb * expo * 0.7 + alb * Lw * k + Lw * 0.008
        bc = lit(wood)
        img = img * (1 - hull[..., None] * vis) + hull[..., None] * vis * bc
        # rim light on top edges
        allm = np.clip(hull + sh, 0, 1)
        rim = np.clip(allm - np.roll(allm, 2, 0), 0, 1) + 0.5 * np.clip(allm - np.roll(allm, -2, 1), 0, 1)
        rim = gaussian_filter(rim, 0.8)
        img = img * (1 - sh[..., None] * vis) + sh[..., None] * vis * lit(np.array([0.075, 0.05, 0.03]))
        img = img * (1 - poles[..., None] * vis) + poles[..., None] * vis * lit(np.array([0.04, 0.025, 0.015]))
        img = img + (rim * vis)[..., None] * (Lw * 0.55 + 0.004)
        E += dr[..., None] * vis * np.array([1.0, 0.70, 0.30]) * 0.55 * (0.9 + 0.1 * math.sin(t * 2.3))
        for j, (fx, fy, fh, I) in enumerate(bflames):
            add_flame(E, fx, fy, fh, t, 4 + j * 2.1, I * 1.1)
    for i, (bx, by, fy, pp, I) in enumerate(qt):
        if pp > 0.5:
            pole = mask_from_polys([[(bx - 11, by), (bx + 11, by), (bx + 7, fy + 30), (bx - 7, fy + 30)]])
            shade = np.clip(0.55 + (Xh - bx) / 22, 0.15, 1)[..., None]
            img = img * (1 - pole[..., None]) + pole[..., None] * (np.array([0.03, 0.019, 0.011]) * expo + np.array([0.05, 0.03, 0.015]) * Lw * 5 * shade)
            bowl = mask_from_polys([[(bx - 42, fy), (bx + 42, fy), (bx + 24, fy + 52), (bx - 24, fy + 52)]])
            img = img * (1 - bowl[..., None]) + bowl[..., None] * (np.array([0.03, 0.02, 0.012]) * expo + np.array([0.06, 0.035, 0.018]) * Lw * 6)
            add_flame(E, bx, fy + 4, 165, t, i * 1.7, I)
        else:
            add_flame(E, bx, fy, 15, t, i * 1.7, I * 0.9)
    # temple doors open: doorway glow + pillar of light to the sky
    if Ldoor > 0:
        dm = mask_from_polys([[(196, YH - 84), (214, YH - 84), (214, YH - 108), (205, YH - 114), (196, YH - 108)]])
        E += dm[..., None] * np.array([1.0, 0.8, 0.45]) * Ldoor * 1.2
        col = np.exp(-((Xh - 205) / 3.0 / (22 + 50 * np.clip((YH - 110 - Yh) / 700, 0, 1))) ** 2) * (Yh < YH - 100) \
            * np.clip(1 - (YH - 110 - Yh) / 820, 0, 1) ** 1.5
        E += col[..., None] * np.array([1.0, 0.78, 0.45]) * 0.30 * Ldoor
    # the Octaquetra: halo, rings of light, and the star itself as flat paint
    if f > 0:
        rr = np.sqrt((Xh - STAR_C[0]) ** 2 + (Yh - STAR_C[1]) ** 2)
        halo = f * np.exp(-rr ** 2 / 170.0 ** 2) * 0.38
        halo = halo + f * np.exp(-rr / 260) * 0.20 * (0.5 + 0.5 * np.cos(rr / 26 - t * 0.8)) ** 3
        img += halo[..., None] * np.array([0.9, 0.7, 0.45]) * SKY[..., None]
        img = paint_star_base(img, f)

    fo = 1 - float(ss(*TL["fade_out"], t))
    img = img * (1 - 0.38 * D); E = E * (1 - 0.2 * D)
    return (img + E) * fo, E * fo, fo

def stars(t, out, D, expo):
    vis = (1 - D) * expo
    if vis <= 0.01:
        return
    for (x, y, b, ph) in STARS:
        if SHORE[int(y / 2), int(x / 2)] > 0 or ZIGM[int(y / 2), int(x / 2)] > 0:
            continue
        a = b * vis * (0.65 + 0.35 * math.sin(t * 2.0 + ph)) * 0.55
        splat(out, x, y, 1.3, (0.75, 0.8, 1.0), a)

def paint(t, k):
    """One painting: the scene at time t in brushwork, on stroke layout k. uint8 (H, W, 3)."""
    base_h, E_h, fo = render(t)
    L, N, br, edge = get_map(k % MAP_COUNT)
    base = np.repeat(np.repeat(base_h, 2, 0), 2, 1).reshape(-1, 3)
    # every stroke takes the average colour beneath it ...
    cnt = np.maximum(np.bincount(L, minlength=N), 1)
    means = np.stack([np.bincount(L, weights=base[:, c], minlength=N) for c in range(3)], 1) / cnt[:, None]
    # ... and is repainted in ultramarine and gold, with bristle ridges and dark outlines
    cols = vg_remap(means) * fo
    out = cols[L].reshape(H, W, 3).astype(np.float32) * br[..., None]
    ink = np.array([0.02, 0.03, 0.16], np.float32) * fo
    out = out * (1 - 0.75 * edge[..., None]) + ink * 0.75 * edge[..., None]
    # keep a little of the lights themselves so small ones stay crisp
    hi = np.repeat(np.repeat(E_h, 2, 0), 2, 1)
    out = out + hi * 0.15
    # bloom
    sm = E_h.reshape(HH // 4, 4, HW // 4, 4, 3).mean((1, 3))
    b1 = gaussian_filter(sm, (3, 3, 0)); b2 = gaussian_filter(sm, (14, 14, 0))
    bl = zoom((b1 * 0.8 + b2 * 0.9) * 0.6, (8, 8, 1), order=1)
    out = out + bl[:H, :W]
    D = float(ss(*TL["dawn"], t)); expo = float(ss(*TL["fade_in"], t)) * 0.85 + 0.15
    stars(t, out, D, expo * fo)
    out = out * VIG[..., None]
    out = np.clip(out / (1 + 0.15 * out) * 1.08, 0, 1)
    return (np.nan_to_num(out) * 255 + 0.5).astype(np.uint8)

# ---------------- keyframes and dissolves ----------------
_lock = threading.Lock()
_pool = None
_keys = {}                      # keyframe index -> future of a float32 (H, W, 3) painting

def _paint_key(k):
    return paint(k * KEY_SECONDS, k).astype(np.float32)

def _key(k):
    global _pool
    with _lock:
        if _pool is None:
            _pool = ThreadPoolExecutor(max_workers=1)
        fut = _keys.get(k)
        if fut is None:
            for old in [o for o in _keys if o < k - 2 or o > k + 2]:
                _keys.pop(old).cancel()
            fut = _keys[k] = _pool.submit(_paint_key, k)
        return fut

def frame(t):
    """The video frame at time t: the dissolve between the two nearest paintings. uint8 (H, W, 3)."""
    k = int(math.floor(t / KEY_SECONDS)); u = t / KEY_SECONDS - k
    fa, fb = _key(k), _key(k + 1)
    a, b = fa.result(), fb.result()
    _key(k + 2)                              # paint the next one while these two are on screen
    return (a * (1 - u) + b * u + 0.5).astype(np.uint8)

def _say(text):
    print(text, flush=True)

def init(map_dir=None, log=_say):
    """Builds (first run) or loads the stroke layouts kept in map_dir."""
    global MAP_DIR
    if map_dir:
        MAP_DIR = map_dir
    for k in range(MAP_COUNT):
        if not os.path.exists(os.path.join(MAP_DIR, f"vgmap{k}.npz")):
            log(f"building stroke layout {k + 1} of {MAP_COUNT} ...")
            n = gen_map(k)
            log(f"stroke layout {k + 1}: {n - 1} strokes")
        get_map(k)

def frame_into(t, address):
    """Writes the video frame at time t as RGBA bytes (W * H * 4) into memory at address."""
    import ctypes
    dst = np.ctypeslib.as_array((ctypes.c_ubyte * (W * H * 4)).from_address(int(address))).reshape(H, W, 4)
    dst[..., :3] = frame(float(t))
    dst[..., 3] = 255

def shutdown():
    """Stops the background painter."""
    global _pool
    with _lock:
        pool, _pool = _pool, None
        for fut in _keys.values():
            fut.cancel()
        _keys.clear()
    if pool is not None:
        pool.shutdown(wait=True)

def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    if cmd == "maps":
        for k in range(MAP_COUNT):
            print("stroke layout", k, "strokes", gen_map(k) - 1)
    elif cmd == "still":
        init()
        Image.fromarray(paint(float(sys.argv[2]), 0)).save(sys.argv[3])
    elif cmd == "frame":
        init()
        Image.fromarray(frame(float(sys.argv[2]))).save(sys.argv[3])
        shutdown()
    else:
        print(__doc__)

if __name__ == "__main__":
    main()
