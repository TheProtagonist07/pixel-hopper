#!/usr/bin/env python3
"""Check that edited files differ from a git revision only by comments and whitespace.

Use it before committing a "comments only" change to prove no code was altered by accident.

Usage (from anywhere):
    python3 tools/verify_comment_only.py Assets/Scripts/Game.cs Assets/Scripts/Sfx.cs
    python3 tools/verify_comment_only.py --ref origin/main Assets/Scripts/Game.cs

How it works: for each file it removes C# / JavaScript / CSS / HTML comments (while leaving text inside string
literals alone), collapses all whitespace, and compares the working copy with the same file at the chosen git
revision (default HEAD). Exit code 0 = code identical, 1 = something besides comments changed (the first
difference is printed so it is easy to find).

Limits: it understands normal and interpolated C# strings but not verbatim @"..." strings (the project does not use any).
"""
import argparse
import os
import re
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def strip_cs(src: str) -> str:
    """Remove // and /* */ comments from C#-like source, keeping string and char literals intact."""
    out, i, n = [], 0, len(src)
    while i < n:
        c = src[i]
        two = src[i:i + 2]
        if two == "//":
            while i < n and src[i] != "\n":
                i += 1
        elif two == "/*":
            j = src.find("*/", i + 2)
            i = n if j < 0 else j + 2
        elif c in ('"', "'"):
            quote = c
            out.append(c)
            i += 1
            while i < n:
                out.append(src[i])
                if src[i] == "\\":          # an escaped character: copy it too so \" does not end the string
                    i += 1
                    if i < n:
                        out.append(src[i])
                elif src[i] == quote:
                    break
                i += 1
            i += 1
        else:
            out.append(c)
            i += 1
    return "".join(out)


def strip_html(src: str) -> str:
    """Remove HTML comments, CSS/JS block comments and whole-line JS comments."""
    src = re.sub(r"<!--.*?-->", "", src, flags=re.S)
    src = re.sub(r"/\*.*?\*/", "", src, flags=re.S)
    src = re.sub(r"(?m)^\s*//.*$", "", src)
    return src


def normalize(text: str) -> str:
    """Collapse every run of whitespace to one space so formatting changes do not count."""
    return re.sub(r"\s+", " ", text).strip()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("paths", nargs="+", help="files relative to the repository root")
    parser.add_argument("--ref", default="HEAD", help="git revision to compare against (default: HEAD)")
    args = parser.parse_args()

    bad = 0
    for p in args.paths:
        with open(os.path.join(REPO, p), encoding="utf-8") as f:
            working = f.read()
        old = subprocess.run(["git", "-C", REPO, "show", f"{args.ref}:{p}"], capture_output=True, text=True, check=True).stdout
        strip = strip_html if p.endswith(".html") else strip_cs
        a, b = normalize(strip(old)), normalize(strip(working))
        if a == b:
            print(f"OK        {p}  (code identical)")
        else:
            bad += 1
            k = next((i for i in range(min(len(a), len(b))) if a[i] != b[i]), min(len(a), len(b)))
            print(f"DIFFERS   {p}\n   {args.ref:<7}: ...{a[max(0, k - 60):k + 80]}\n   WORKING: ...{b[max(0, k - 60):k + 80]}")
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
