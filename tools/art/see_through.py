"""Round 20: turns an opaque invisible-man sprite see-through.

The body keeps ~40 % opacity (the stage shows through it), the outline stays brighter and more solid so the shape still
reads, and the colour is lifted toward a pale icy white so it looks like a haze, not a grey cut-out.
"""
import numpy as np
from PIL import Image, ImageFilter


def see_through(img, body=.4, rim=.85, icy=.45):
    img = img.convert("RGBA")
    a = np.asarray(img.split()[3]).astype(np.float32) / 255
    # the rim: alpha minus a shrunk alpha, softened
    inner = np.asarray(img.split()[3].filter(ImageFilter.MinFilter(15)).filter(ImageFilter.GaussianBlur(6))).astype(np.float32) / 255
    edge = np.clip(a - inner, 0, 1)
    alpha = np.clip(a * body + edge * rim, 0, 1) * a
    rgb = np.asarray(img.convert("RGB")).astype(np.float32)
    white = np.array([235, 245, 255], np.float32)
    rgb = rgb * (1 - icy) + white * icy + edge[..., None] * 30
    out = np.dstack([np.clip(rgb, 0, 255), alpha * 255]).astype(np.uint8)
    return Image.fromarray(out, "RGBA")
