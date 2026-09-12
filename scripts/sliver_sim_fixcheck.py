"""Verify root cause: <=1m remainder columns/strips from the `> 1f` guard,
then re-run with fixes and report remaining slivers.
"""
import random
import math
import importlib.util

spec = importlib.util.spec_from_file_location("sim", "c:/Zombera/scripts/sliver_sim.py")
sim = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sim)

FIXED = True


def build_front_divs_fixed(fmin, fmax, rng):
    divs = [fmin]
    cursor = fmin
    while cursor < fmax - sim.EFF_MIN_W:
        remaining = fmax - cursor
        raw = sim.EFF_MIN_W + rng.random() * (sim.MAXW - sim.EFF_MIN_W)
        lw = min(remaining, sim.snap_up(raw))
        if lw < sim.EFF_MIN_W:
            break
        cursor += lw
        divs.append(cursor)
    rem = fmax - cursor
    if rem > 0.001:
        if len(divs) > 1 and rem < sim.EFF_MIN_W:
            divs[-1] = fmax  # absorb
        else:
            divs.append(fmax)
    return divs


def build_depth_strips_fixed(dmin, dmax, rng):
    divs = [dmin]
    total = dmax - dmin
    target = sim.EFF_MIN_D + rng.random() * max(1.0, sim.MAXW - sim.EFF_MIN_D)
    max_rows = max(1, math.floor(total / sim.EFF_MIN_D))
    rc = max(1, min(round(total / target), min(max_rows, 2)))
    row_depth = min(total, sim.snap_up(total / rc))
    cursor = dmin
    while cursor < dmax - sim.EFF_MIN_D:
        remaining = dmax - cursor
        sd = min(remaining, row_depth)
        if sd < sim.EFF_MIN_D:
            break
        cursor += sd
        divs.append(cursor)
    rem = dmax - cursor
    if rem > 0.001:
        if len(divs) > 1 and rem < sim.EFF_MIN_D:
            divs[-1] = dmax
        else:
            divs.append(dmax)
    return divs


def try_split_long_axis_fixed(b, min_w, min_d):
    """Require the short axis >= its min as well, so no below-min lots are produced."""
    w, h = b[2] - b[0], b[3] - b[1]
    vertical = w > h
    if vertical and w >= min_w * 2 and h >= min_d:
        mid = b[0] + w * 0.5
        return (b[0], b[1], mid, b[3]), (mid, b[1], b[2], b[3])
    if not vertical and h >= min_d * 2 and w >= min_w:
        mid = b[1] + h * 0.5
        return (b[0], b[1], b[2], mid), (b[0], mid, b[2], b[3])
    return None


def pipeline_fixed(front, depth, seed):
    rng = random.Random(seed)
    ds = build_depth_strips_fixed(0.0, depth, rng)
    fd = build_front_divs_fixed(0.0, front, rng)
    lots = sim.cells(ds, fd)
    block = (0.0, 0.0, front, depth)

    # 1-2 fold + absorb (same as before)
    changed = True
    while changed:
        changed = False
        for i in range(len(lots) - 1, -1, -1):
            if sim.touches_boundary(lots[i], block):
                continue
            ti = -1
            src = lots[i]
            for j in range(len(lots)):
                if j == i or not sim.touches_boundary(lots[j], block):
                    continue
                if sim.same_column(src, lots[j]):
                    ti = j
                    break
            if ti < 0:
                continue
            lots[ti] = sim.union(lots[ti], lots[i])
            lots.pop(i)
            changed = True
    for _ in range(2):
        changed = True
        while changed:
            changed = False
            for i in range(len(lots) - 1, -1, -1):
                if sim.touches_boundary(lots[i], block):
                    continue
                ti = sim.best_merge_target(lots, i, block, float("inf"), float("inf"))
                if ti < 0:
                    continue
                lots[ti] = sim.union(lots[ti], lots[i])
                lots.pop(i)
                changed = True

    # 3 split elongated with short-axis guard
    changed = True
    while changed:
        changed = False
        for i in range(len(lots) - 1, -1, -1):
            b = lots[i]
            w, h = b[2] - b[0], b[3] - b[1]
            if max(w, h) / max(0.5, min(w, h)) <= 2.5:
                continue
            parts = try_split_long_axis_fixed(b, sim.MINW, sim.MINW)
            if parts is None:
                continue
            ha, hb = parts
            if not sim.touches_boundary(ha, block) or not sim.touches_boundary(hb, block):
                continue
            lots[i] = ha
            lots.insert(i + 1, hb)
            changed = True

    # 4 absorb
    changed = True
    while changed:
        changed = False
        for i in range(len(lots) - 1, -1, -1):
            if sim.touches_boundary(lots[i], block):
                continue
            ti = sim.best_merge_target(lots, i, block, float("inf"), float("inf"))
            if ti < 0:
                continue
            lots[ti] = sim.union(lots[ti], lots[i])
            lots.pop(i)
            changed = True

    # 5 merge slivers capped + uncapped fallback
    changed = True
    while changed:
        changed = False
        for i in range(len(lots) - 1, -1, -1):
            b = lots[i]
            if b[2] - b[0] >= sim.MINW and b[3] - b[1] >= sim.MINW:
                continue
            ti = sim.best_merge_target(lots, i, block, sim.MAXW, sim.MAXW)
            if ti < 0:
                ti = sim.best_merge_target(lots, i, block, float("inf"), float("inf"))
            if ti < 0:
                continue
            lots[ti] = sim.union(lots[ti], b)
            lots.pop(i)
            changed = True

    # 6 split oversized (random, with short-axis guard)
    changed = True
    while changed:
        changed = False
        for i in range(len(lots) - 1, -1, -1):
            b = lots[i]
            w, h = b[2] - b[0], b[3] - b[1]
            if w <= sim.MAXW and h <= sim.MAXW:
                continue
            vertical = w > h
            if vertical and w >= sim.MINW * 2 and h >= sim.MINW:
                sx = b[0] + sim.MINW + rng.random() * max(0.5, w - sim.MINW * 2)
                ha, hb = (b[0], b[1], sx, b[3]), (sx, b[1], b[2], b[3])
            elif not vertical and h >= sim.MINW * 2 and w >= sim.MINW:
                sz = b[1] + sim.MINW + rng.random() * max(0.5, h - sim.MINW * 2)
                ha, hb = (b[0], b[1], b[2], sz), (b[0], sz, b[2], b[3])
            else:
                continue
            if not sim.touches_boundary(ha, block) or not sim.touches_boundary(hb, block):
                continue
            lots[i] = ha
            lots.insert(i + 1, hb)
            changed = True

    # 7 absorb
    changed = True
    while changed:
        changed = False
        for i in range(len(lots) - 1, -1, -1):
            if sim.touches_boundary(lots[i], block):
                continue
            ti = sim.best_merge_target(lots, i, block, float("inf"), float("inf"))
            if ti < 0:
                continue
            lots[ti] = sim.union(lots[ti], lots[i])
            lots.pop(i)
            changed = True

    return lots


def run(use_fixed):
    fn = pipeline_fixed if use_fixed else sim.pipeline
    small = []
    for front in range(24, 181):
        for depth in range(12, 91):
            for seed in range(4):
                result = fn(front, depth, seed)
                lots = result if use_fixed else result[0]
                for l in lots:
                    w, h = l[2] - l[0], l[3] - l[1]
                    m = min(w, h)
                    if m < 13.0:
                        small.append((m, front, depth, seed, f"{w:.1f}x{h:.1f}"))
    small.sort()
    print(("FIXED" if use_fixed else "ORIGINAL"), "lots with min dim < 13:", len(small))
    for row in small[:15]:
        print(row)


run(False)
run(True)
