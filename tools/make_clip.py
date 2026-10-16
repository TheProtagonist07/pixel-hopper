#!/usr/bin/env python3
"""Turn the PNG frames written by ClipRecorder into a promo video (MP4) and a small GIF.

Usage: python3 tools/make_clip.py FRAMES_DIR OUT_PREFIX [--fps 30] [--width 540]
Example: python3 tools/make_clip.py /tmp/clip/frames marketing/promo/pixel-hopper-clip-v1.1.0
Writes OUT_PREFIX.mp4 (H.264, 9:16, web-friendly) and OUT_PREFIX.gif (10 fps, 270 px wide, small).

The game view is not exactly 9:16, so the black bars around the game are cropped off first (the same way tools/crop_shots.py
does it), then the picture is scaled with nearest-neighbour so the pixel art stays sharp. Needs ffmpeg and Pillow.
"""
import argparse
import os
import subprocess
import sys

from PIL import Image


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("frames")
    ap.add_argument("out_prefix")
    ap.add_argument("--fps", type=int, default=30)
    ap.add_argument("--width", type=int, default=540)
    a = ap.parse_args()

    first = sorted(f for f in os.listdir(a.frames) if f.endswith(".png"))
    if not first:
        sys.exit("no PNG frames in " + a.frames)
    w, h = Image.open(os.path.join(a.frames, first[0])).size
    crop_w = round(h * 9 / 16)
    if crop_w <= w:
        crop = f"crop={crop_w}:{h}:{(w - crop_w) // 2}:0"
    else:
        crop_h = round(w * 16 / 9)
        crop = f"crop={w}:{crop_h}:0:{(h - crop_h) // 2}"
    height = round(a.width * 16 / 9)
    vf = f"{crop},scale={a.width}:{height}:flags=neighbor"
    src = ["-framerate", str(a.fps), "-i", os.path.join(a.frames, "%05d.png")]

    subprocess.run(["ffmpeg", "-v", "error", "-y", *src, "-vf", vf + ",format=yuv420p", "-c:v", "libx264", "-crf", "18",
                    "-preset", "slow", "-movflags", "+faststart", a.out_prefix + ".mp4"], check=True)
    gif_w = 270
    gif_h = round(gif_w * 16 / 9)
    gvf = f"{crop},scale={gif_w}:{gif_h}:flags=neighbor,fps=10,split[a][b];[a]palettegen=max_colors=96[p];[b][p]paletteuse=dither=none"
    subprocess.run(["ffmpeg", "-v", "error", "-y", *src, "-vf", gvf, a.out_prefix + ".gif"], check=True)
    for ext in ("mp4", "gif"):
        p = f"{a.out_prefix}.{ext}"
        print(f"{p}: {os.path.getsize(p) / 1024:.0f} KB")


if __name__ == "__main__":
    main()
