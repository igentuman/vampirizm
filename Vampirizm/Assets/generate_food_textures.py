"""Generate blood-themed food textures from the installed game's vanilla assets.

Extracts the vanilla diffuse textures with UnityPy, then repaints them: food areas
get a blood-red hue remap with value curves and mottling, while plates, bowls and
wood keep their look apart from light blood staining. Writes tex_*.png next to this
script; the PNGs are embedded in the DLL and swapped onto the item materials at runtime.

Usage:
    uv venv venv && VIRTUAL_ENV=venv uv pip install UnityPy Pillow numpy
    venv/bin/python generate_food_textures.py [--valheim <game dir>]
"""
import argparse
import pathlib
import re

import numpy as np
import UnityPy
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument("--valheim", default="/home/igentuman/.local/share/Steam/steamapps/common/Valheim")
args = parser.parse_args()

HERE = pathlib.Path(__file__).resolve().parent
SOFTREF = pathlib.Path(args.valheim) / "valheim_Data/StreamingAssets/SoftRef"
ORIG = HERE / "_vanilla_textures"  # extraction cache; not embedded, safe to delete
OUT = HERE
ORIG.mkdir(exist_ok=True)

TARGETS = [
    "Assets/GameElements/Items/_res/Wolfjerky/model/WolfJerky_d.png",
    "Assets/GameElements/Items/_res/MinceMeatSauce/model/MinceMeatSauce_d.png",
    "Assets/GameElements/Items/consumables/_res/bloodpudding/model/bloodpudding_d.png",
    "Assets/GameElements/Items/_res/seekeraspic/model/Seeker Aspic_d.png",
    "Assets/GameElements/Items/_res/stew/serpentstew.png",
    "Assets/world/Props/Dvergr/RoyalJelly/model/Jelly_d.png",
    "Assets/world/Props/Dvergr/RoyalJelly/model/JellyOuter.png",
]


def extract():
    text = (SOFTREF / "manifest_extended").read_text(errors="ignore")
    path_to_bundle = {}
    for m in re.finditer(r"bundle: (\S+)\n  path in bundle: (.+)", text):
        path_to_bundle[m.group(2).strip()] = m.group(1)
    by_bundle = {}
    for t in TARGETS:
        name = pathlib.Path(t).stem.replace(" ", "")
        if (ORIG / f"{name}.png").exists():
            continue
        by_bundle.setdefault(path_to_bundle[t], []).append(t)
    for bundle, paths in by_bundle.items():
        env = UnityPy.load(str(SOFTREF / "Bundles" / bundle))
        wanted = {p.lower(): p for p in paths}
        for obj in env.objects:
            if obj.type.name != "Texture2D":
                continue
            container = (obj.container or "").lower()
            if container in wanted:
                name = pathlib.Path(wanted[container]).stem.replace(" ", "")
                obj.read().image.save(ORIG / f"{name}.png")
                print("extracted", name)


extract()


rng = np.random.default_rng(7)


def to_hsv(rgb):
    # rgb float 0..1 -> h,s,v float arrays
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    maxc = rgb.max(-1)
    minc = rgb.min(-1)
    v = maxc
    c = maxc - minc
    s = np.where(maxc > 0, c / np.maximum(maxc, 1e-9), 0)
    h = np.zeros_like(v)
    m = c > 1e-9
    rc = np.where(m, (maxc - r) / np.maximum(c, 1e-9), 0)
    gc = np.where(m, (maxc - g) / np.maximum(c, 1e-9), 0)
    bc = np.where(m, (maxc - b) / np.maximum(c, 1e-9), 0)
    h = np.where((maxc == r) & m, bc - gc, h)
    h = np.where((maxc == g) & m, 2.0 + rc - bc, h)
    h = np.where((maxc == b) & m, 4.0 + gc - rc, h)
    return (h / 6.0) % 1.0, s, v


def from_hsv(h, s, v):
    i = np.floor(h * 6.0).astype(int) % 6
    f = h * 6.0 - np.floor(h * 6.0)
    p = v * (1 - s)
    q = v * (1 - s * f)
    t = v * (1 - s * (1 - f))
    r = np.choose(i, [v, q, p, p, t, v])
    g = np.choose(i, [t, v, v, q, p, p])
    b = np.choose(i, [p, p, t, v, v, q])
    return np.stack([r, g, b], -1)


def load(name):
    im = Image.open(ORIG / f"{name}.png").convert("RGBA")
    a = np.asarray(im).astype(np.float32) / 255.0
    return a[..., :3], a[..., 3]


def save(name, rgb, alpha):
    rgba = np.concatenate([rgb, alpha[..., None]], -1)
    out = (np.clip(rgba, 0, 1) * 255).astype(np.uint8)
    Image.fromarray(out, "RGBA").save(OUT / f"{name}.png")
    print("wrote", name)


def mottle(shape, scale, lo, hi):
    """Smooth random value field for organic variation."""
    small = rng.random((max(2, shape[0] // scale), max(2, shape[1] // scale)))
    im = Image.fromarray((small * 255).astype(np.uint8)).resize((shape[1], shape[0]), Image.BILINEAR)
    f = np.asarray(im).astype(np.float32) / 255.0
    return lo + f * (hi - lo)


def bloodify(rgb, mask, hue=0.985, sat_mul=1.5, sat_min=0.55, val_mul=0.75, val_pow=1.15, mott=0.12):
    """Remap masked pixels to blood tones, keeping the original light/dark detail."""
    h, s, v = to_hsv(rgb)
    h2 = np.where(mask, hue, h)
    s2 = np.where(mask, np.clip(np.maximum(s * sat_mul, sat_min), 0, 0.97), s)
    vv = np.clip(v, 0, 1) ** val_pow * val_mul
    vv = vv * (1.0 + (mottle(v.shape, 6, -mott, mott)))
    v2 = np.where(mask, np.clip(vv, 0.02, 1), v)
    return from_hsv(h2, s2, v2)


def stain(rgb, mask, strength=0.25):
    """Blend a dark blood tone over masked pixels (splatter on plates/wood)."""
    blob = mottle(rgb.shape[:2], 4, 0, 1)
    splat = (blob > 0.72) & mask
    blood = np.array([0.32, 0.02, 0.04])
    k = strength * np.clip((blob - 0.72) / 0.28, 0, 1)[..., None]
    return np.where(splat[..., None], rgb * (1 - k) + blood * k, rgb)


# --- Crimson jerky (WolfJerky_d): everything is meat -> deep cured-blood strips.
rgb, a = load("WolfJerky_d")
h, s, v = to_hsv(rgb)
out = bloodify(rgb, np.ones(v.shape, bool), hue=0.99, sat_mul=1.6, sat_min=0.6, val_mul=0.8, val_pow=1.1, mott=0.18)
save("tex_crimson_jerky", out, a)

# --- Hunter's tartare (MinceMeatSauce_d): orange mince/sauce -> raw blood-red meat; bowl stays, stained.
rgb, a = load("MinceMeatSauce_d")
h, s, v = to_hsv(rgb)
food = (s > 0.55) & (h > 0.02) & (h < 0.14)  # saturated orange = mince + sauce square
out = bloodify(rgb, food, hue=0.985, sat_mul=1.5, sat_min=0.65, val_mul=0.72, val_pow=1.2, mott=0.2)
out = stain(out, ~food, strength=0.5)
save("tex_hunters_tartare", out, a)

# --- Black blood pudding (bloodpudding_d): the vanilla pudding is already near-black red, so it is lifted to a
# deep glossy crimson to read as a different food; wood stays, stained.
rgb, a = load("bloodpudding_d")
h, s, v = to_hsv(rgb)
leaf = (h > 0.2) & (h < 0.45) & (s > 0.3)
pudding = (v < 0.3) & ~leaf  # the dark dish, its gray rim and the berries
out = bloodify(rgb, pudding, hue=0.995, sat_mul=1.5, sat_min=0.85, val_mul=2.1, val_pow=0.8, mott=0.3)
out = stain(out, ~pudding & ~leaf, strength=0.45)
save("tex_black_blood_pudding", out, a)

# --- Serpent blood aspic (Seeker Aspic_d): the gel -> translucent blood red; plate lightly stained.
rgb, a = load("SeekerAspic_d")
h, s, v = to_hsv(rgb)
gel = (s > 0.4) & (v > 0.5) & (h > 0.03) & (h < 0.18)  # bright saturated orange only; dark board keeps its look
out = bloodify(rgb, gel, hue=0.99, sat_mul=1.6, sat_min=0.7, val_mul=0.85, val_pow=1.0, mott=0.12)
out = stain(out, ~gel, strength=0.3)
save("tex_serpent_blood_aspic", out, a)

# --- Bloody stew (serpentstew): stew contents -> red broth, meat chunks darker; bowl stays.
rgb, a = load("serpentstew")
h, s, v = to_hsv(rgb)
contents = (s > 0.5) | ((h > 0.2) & (h < 0.5) & (s > 0.3))  # stew surface and herbs; the tan bowl is s~0.4
out = bloodify(rgb, contents, hue=0.985, sat_mul=1.45, sat_min=0.6, val_mul=0.7, val_pow=1.1, mott=0.2)
save("tex_bloody_stew", out, a)

# --- Seeker blood jelly (Jelly_d + JellyOuter): whole jelly -> deep red with bright mottling.
rgb, a = load("Jelly_d")
out = bloodify(rgb, np.ones(rgb.shape[:2], bool), hue=0.99, sat_mul=1.5, sat_min=0.75, val_mul=0.68, val_pow=1.1, mott=0.22)
save("tex_seeker_blood_jelly", out, a)

rgb, a = load("JellyOuter")
h, s, v = to_hsv(rgb)
# Translucent membrane: keep alpha, push the faint color toward red.
out = from_hsv(np.full_like(h, 0.99), np.clip(s * 2.0 + 0.55, 0, 0.85), v * 0.75)
save("tex_seeker_blood_jelly_outer", out, a)
