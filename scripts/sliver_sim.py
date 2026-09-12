"""Mirror of CityPrefabResidentialLotBuilder.LotSubdivision straight-block pipeline.

Residential defaults: min 16x16, max 32x32, effectiveMinWidth = max(20, 16) = 20,
effectiveMinDepth = max(10, 16*0.4) = 10. streetFrontX assumed True (front on X).
"""
import math
import random

SNAP = 0.05
GRID = 3.0
MINW, MAXW = 16.0, 32.0          # config min/max
EFF_MIN_W = max(20.0, MINW)      # 20
EFF_MIN_D = max(10.0, MINW * 0.4)  # 10


def snap_up(m):
    s = math.ceil(m / GRID) * GRID
    return max(GRID, s)


def build_depth_strips(dmin, dmax, rng):
    divs = [dmin]
    total = dmax - dmin
    target = EFF_MIN_D + rng.random() * max(1.0, MAXW - EFF_MIN_D)
    max_rows = max(1, math.floor(total / EFF_MIN_D))
    row_count = int(min(max_rows, 2) and max(1, min(max_rows, 2))) if False else None
    rc = max(1, min(round(total / target), min(max_rows, 2)))
    row_depth = min(total, snap_up(total / rc))
    cursor = dmin
    while cursor < dmax - EFF_MIN_D:
        remaining = dmax - cursor
        sd = min(remaining, row_depth)
        if sd < EFF_MIN_D:
            break
        cursor += sd
        divs.append(cursor)
    if dmax - cursor > 1.0 and len(divs) > 1:
        if dmax - cursor < EFF_MIN_D:
            divs[-1] = dmax
        else:
            divs.append(dmax)
    elif cursor < dmax:
        divs.append(dmax)
    return divs


def build_front_divs(fmin, fmax, rng):
    divs = [fmin]
    cursor = fmin
    while cursor < fmax - EFF_MIN_W:
        remaining = fmax - cursor
        raw = EFF_MIN_W + rng.random() * (MAXW - EFF_MIN_W)
        lw = min(remaining, snap_up(raw))
        if lw < EFF_MIN_W:
            break
        cursor += lw
        divs.append(cursor)
    if fmax - cursor > 1.0 and len(divs) > 1:
        if fmax - cursor < EFF_MIN_W:
            divs[-1] = fmax
        else:
            divs.append(fmax)
    elif cursor < fmax:
        divs.append(fmax)
    return divs


def cells(depth_strips, front_divs):
    out = []
    for s in range(len(depth_strips) - 1):
        for d in range(len(front_divs) - 1):
            out.append((front_divs[d], depth_strips[s], front_divs[d + 1], depth_strips[s + 1]))
    return out


def touches_boundary(r, block):
    (bx0, bz0, bx1, bz1) = block
    return (abs(r[0] - bx0) < SNAP or abs(r[2] - bx1) < SNAP or
            abs(r[1] - bz0) < SNAP or abs(r[3] - bz1) < SNAP)


def shared_edge(a, b):
    ox = max(0.0, min(a[2], b[2]) - max(a[0], b[0]))
    oz = max(0.0, min(a[3], b[3]) - max(a[1], b[1]))
    if ox > 0.5 and (abs(a[3] - b[1]) < SNAP or abs(b[3] - a[1]) < SNAP):
        return ox
    if oz > 0.5 and (abs(a[2] - b[0]) < SNAP or abs(b[2] - a[0]) < SNAP):
        return oz
    return 0.0


def union(a, b):
    return (min(a[0], b[0]), min(a[1], b[1]), max(a[2], b[2]), max(a[3], b[3]))


def same_column(a, b):
    return abs(a[0] - b[0]) < SNAP and abs(a[2] - b[2]) < SNAP


def best_merge_target(lots, i, block, max_w, max_d):
    src = lots[i]
    best_i, best_score = -1, float("-inf")
    for j in range(len(lots)):
        if j == i:
            continue
        nb = lots[j]
        e = shared_edge(src, nb)
        if e < 0.5:
            continue
        u = union(nb, src)
        if u[2] - u[0] > max_w + 0.5 or u[3] - u[1] > max_d + 0.5:
            continue
        score = e
        if touches_boundary(nb, block):
            score += 1000.0
        if same_column(src, nb):
            score += 500.0
        score -= (nb[2] - nb[0]) * (nb[3] - nb[1]) * 0.001
        if score > best_score:
            best_score, best_i = score, j
    return best_i


def pipeline(front, depth, seed):
    rng = random.Random(seed)
    ds = build_depth_strips(0.0, depth, rng)
    fd = build_front_divs(0.0, front, rng)
    lots = cells(ds, fd)
    block = (0.0, 0.0, front, depth)
    trace = {"init": [str(l) for l in lots]}

    # 1 fold interior
    changed = True
    while changed:
        changed = False
        for i in range(len(lots) - 1, -1, -1):
            if touches_boundary(lots[i], block):
                continue
            ti = -1
            src = lots[i]
            for j in range(len(lots)):
                if j == i:
                    continue
                if not touches_boundary(lots[j], block):
                    continue
                if same_column(src, lots[j]):
                    ti = j
                    break
            if ti < 0:
                continue
            lots[ti] = union(lots[ti], lots[i])
            lots.pop(i)
            changed = True

    # 2 absorb landlocked (no cap)
    changed = True
    while changed:
        changed = False
        for i in range(len(lots) - 1, -1, -1):
            if touches_boundary(lots[i], block):
                continue
            ti = best_merge_target(lots, i, block, float("inf"), float("inf"))
            if ti < 0:
                continue
            lots[ti] = union(lots[ti], lots[i])
            lots.pop(i)
            changed = True

    # 3 split elongated
    changed = True
    while changed:
        changed = False
        for i in range(len(lots) - 1, -1, -1):
            b = lots[i]
            w, h = b[2] - b[0], b[3] - b[1]
            ratio = max(w, h) / max(0.5, min(w, h))
            if ratio <= 2.5:
                continue
            vertical = w > h
            if vertical and w >= MINW * 2:
                mid = b[0] + w * 0.5
                ha, hb = (b[0], b[1], mid, b[3]), (mid, b[1], b[2], b[3])
            elif not vertical and h >= MINW * 2:
                mid = b[1] + h * 0.5
                ha, hb = (b[0], b[1], b[2], mid), (b[0], mid, b[2], b[3])
            else:
                continue
            if not touches_boundary(ha, block) or not touches_boundary(hb, block):
                continue
            lots[i] = ha
            lots.insert(i + 1, hb)
            changed = True

    # 4 absorb again
    changed = True
    while changed:
        changed = False
        for i in range(len(lots) - 1, -1, -1):
            if touches_boundary(lots[i], block):
                continue
            ti = best_merge_target(lots, i, block, float("inf"), float("inf"))
            if ti < 0:
                continue
            lots[ti] = union(lots[ti], lots[i])
            lots.pop(i)
            changed = True

    # 5 merge slivers (with caps)
    changed = True
    while changed:
        changed = False
        for i in range(len(lots) - 1, -1, -1):
            b = lots[i]
            if b[2] - b[0] >= MINW and b[3] - b[1] >= MINW:
                continue
            ti = best_merge_target(lots, i, block, MAXW, MAXW)
            if ti < 0:
                continue
            lots[ti] = union(lots[ti], b)
            lots.pop(i)
            changed = True

    # 6 split oversized (random split)
    changed = True
    while changed:
        changed = False
        for i in range(len(lots) - 1, -1, -1):
            b = lots[i]
            w, h = b[2] - b[0], b[3] - b[1]
            if w <= MAXW and h <= MAXW:
                continue
            vertical = w > h
            if vertical and w >= MINW * 2:
                sx = b[0] + MINW + rng.random() * max(0.5, w - MINW * 2)
                ha, hb = (b[0], b[1], sx, b[3]), (sx, b[1], b[2], b[3])
            elif not vertical and h >= MINW * 2:
                sz = b[1] + MINW + rng.random() * max(0.5, h - MINW * 2)
                ha, hb = (b[0], b[1], b[2], sz), (b[0], sz, b[2], b[3])
            else:
                continue
            if not touches_boundary(ha, block) or not touches_boundary(hb, block):
                continue
            lots[i] = ha
            lots.insert(i + 1, hb)
            changed = True

    # 7 absorb
    changed = True
    while changed:
        changed = False
        for i in range(len(lots) - 1, -1, -1):
            if touches_boundary(lots[i], block):
                continue
            ti = best_merge_target(lots, i, block, float("inf"), float("inf"))
            if ti < 0:
                continue
            lots[ti] = union(lots[ti], lots[i])
            lots.pop(i)
            changed = True

    return lots, trace


def main():
    worst = []
    for front in range(24, 181):
        for depth in range(12, 91):
            for seed in range(4):
                lots, _ = pipeline(front, depth, seed)
                for l in lots:
                    w, h = l[2] - l[0], l[3] - l[1]
                    if w < 13.0 or h < 13.0:
                        worst.append((min(w, h), front, depth, seed, f"{w:.1f}x{h:.1f}"))
    worst.sort()
    print("smallest surviving lots (min dim < 13):", len(worst))
    for row in worst[:40]:
        print(row)
    if not worst:
        print("none — straight path never produces lots with min dimension < 13")
    # also count how many lots overall fall under 16
    under16 = 0
    total = 0
    for front in range(24, 181):
        for depth in range(12, 91):
            for seed in range(4):
                lots, _ = pipeline(front, depth, seed)
                for l in lots:
                    w, h = l[2] - l[0], l[3] - l[1]
                    total += 1
                    if w < 16.0 or h < 16.0:
                        under16 += 1
    print(f"lots under config min (16): {under16}/{total}")


if __name__ == "__main__":
    main()
