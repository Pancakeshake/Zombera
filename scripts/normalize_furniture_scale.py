#!/usr/bin/env python3
"""
Normalize Furniture Mega Pack prefab scale.

Finds the one overscaled child transform (~220x) in each prefab and
multiplies it by 0.45 to bring it down to correct real-world size.
Root stays at {1,1,1}. Nothing else is touched.

Run:  python scripts/normalize_furniture_scale.py
"""

import os
import re
import glob

PROPS_ROOT = "Assets/02_Shared/Prefabs/Props"
SCALE_FACTOR = 0.454545  # 1/2.2 — brings 220x down to 100x (correct)


def fix_prefab(path: str) -> bool:
    """Multiply the overscaled child's scale by SCALE_FACTOR."""
    with open(path, "r", encoding="utf-8") as f:
        lines = f.readlines()

    # Parse Transform blocks: fileID → (line_idx, sx, sy, sz)
    transforms = {}
    i = 0
    while i < len(lines):
        if not lines[i].startswith("--- !u!4 &"):
            i += 1
            continue
        m = re.match(r"--- !u!4 &(\d+)", lines[i].strip())
        if not m:
            i += 1
            continue
        tf_id = m.group(1)
        j = i + 1
        while j < len(lines) and not lines[j].startswith("--- !u!"):
            j += 1
        for k in range(i + 1, j):
            s = lines[k].strip()
            if s.startswith("m_LocalScale:"):
                sm = re.search(
                    r"\{x:\s*([\-\d.eE+]+),\s*y:\s*([\-\d.eE+]+),\s*z:\s*([\-\d.eE+]+)\}",
                    s,
                )
                if sm:
                    sx, sy, sz = float(sm.group(1)), float(sm.group(2)), float(sm.group(3))
                    if sx != 1.0 or sy != 1.0 or sz != 1.0:
                        transforms[tf_id] = (k, sx, sy, sz)
                break
        i = j

    if not transforms:
        return False

    changed = False
    for tf_id, (line_idx, sx, sy, sz) in transforms.items():
        new_sx = sx * SCALE_FACTOR
        new_sy = sy * SCALE_FACTOR
        new_sz = sz * SCALE_FACTOR
        indent = lines[line_idx][: len(lines[line_idx]) - len(lines[line_idx].lstrip())]
        lines[line_idx] = (
            f"{indent}m_LocalScale: {{x: {new_sx:.9g}, y: {new_sy:.9g}, z: {new_sz:.9g}}}\n"
        )
        changed = True

    if not changed:
        return False

    with open(path, "w", encoding="utf-8") as f:
        f.writelines(lines)
    return True


def main():
    fixed = 0
    for prefab_path in glob.glob(os.path.join(PROPS_ROOT, "**", "*.prefab"), recursive=True):
        if fix_prefab(prefab_path):
            fixed += 1
            print(f"  {os.path.relpath(prefab_path)}")
    print(f"\nDone. Prefabs fixed: {fixed}")


if __name__ == "__main__":
    main()
