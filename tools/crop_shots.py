#!/usr/bin/env python3
"""Crop the black bars off game screenshots and save them as PNG (and a smaller copy for web pages).

Usage: python3 tools/crop_shots.py INPUT_DIR OUTPUT_DIR [--width 720]
The game draws a 9:16 view in the middle of the game window and fills the rest with black. This keeps only that 9:16
rectangle. --width sets the width of the saved images in pixels (default 720, so 720 x 1280); the resize uses nearest
neighbour so the pixel art stays sharp.
"""
import argparse
import os
import sys

from PIL import Image


def crop_nine_sixteen(img: Image.Image) -> Image.Image:
    w, h = img.size
    target_w = round(h * 9 / 16)
    if target_w <= w:                      # window wider than 9:16: remove left and right bars
        left = (w - target_w) // 2
        return img.crop((left, 0, left + target_w, h))
    target_h = round(w * 16 / 9)           # window taller than 9:16: remove top and bottom bars
    top = (h - target_h) // 2
    return img.crop((0, top, w, top + target_h))


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("input")
    parser.add_argument("output")
    parser.add_argument("--width", type=int, default=720)
    args = parser.parse_args()
    os.makedirs(args.output, exist_ok=True)
    count = 0
    for name in sorted(os.listdir(args.input)):
        if not name.lower().endswith(".png"):
            continue
        img = Image.open(os.path.join(args.input, name)).convert("RGB")
        img = crop_nine_sixteen(img)
        height = round(args.width * 16 / 9)
        img = img.resize((args.width, height), Image.NEAREST)
        img.save(os.path.join(args.output, name), optimize=True)
        print(f"{name}: {img.size[0]}x{img.size[1]}")
        count += 1
    if count == 0:
        sys.exit("no PNG files found in " + args.input)


if __name__ == "__main__":
    main()
