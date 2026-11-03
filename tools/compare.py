"""Compare two page images (same size, e.g. pdftoppm -r 150) and report differing pixels.

A pixel differs when any colour channel differs by more than 10% (26/255), like
ImageMagick's `compare -metric AE -fuzz 10%`. Prints the count and the regions that differ,
and writes a side-by-side image (reference | candidate) when --side is given.

usage: python3 tools/compare.py candidate.png reference.png [--limit N] [--side out.png]
"""
import argparse
import sys

import numpy as np
from PIL import Image


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("candidate")
    ap.add_argument("reference")
    ap.add_argument("--limit", type=int, default=None)
    ap.add_argument("--side")
    a = ap.parse_args()

    cand = Image.open(a.candidate).convert("RGB")
    ref = Image.open(a.reference).convert("RGB")
    if cand.size != ref.size:
        print(f"size differs: {cand.size} vs {ref.size}")
        return 1

    diff = np.abs(np.asarray(cand, dtype=np.int16) - np.asarray(ref, dtype=np.int16)).max(axis=2) > 26
    count = int(diff.sum())
    print(f"{count} pixels differ ({100 * count / diff.size:.3f}%)")

    # Differing regions on a 50 px grid, largest first (pixel coordinates at the given dpi).
    h, w = diff.shape
    cells = []
    for y in range(0, h, 50):
        for x in range(0, w, 50):
            n = int(diff[y:y + 50, x:x + 50].sum())
            if n:
                cells.append((n, x, y))
    for n, x, y in sorted(cells, reverse=True)[:12]:
        print(f"  {n:6d} px in cell x {x}-{x + 50}, y {y}-{y + 50}")

    if a.side:
        side = Image.new("RGB", (w * 2, h), "white")
        side.paste(ref, (0, 0))
        side.paste(cand, (w, 0))
        side.resize((w, h // 2)).save(a.side)

    if a.limit is not None and count > a.limit:
        print(f"FAIL: more than {a.limit} pixels differ")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
