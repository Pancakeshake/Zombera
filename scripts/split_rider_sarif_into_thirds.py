#!/usr/bin/env python3
"""
Split InspectCode / Rider SARIF (or Rider `rz_issues.txt` text export) into three equal partitions.

Ordering: alphabetical by artifact URI (Assets/...), then start line, then ruleId.
Use this to parallelize cleanup (e.g. different agents take part 1 / 2 / 3).

Usage:
  python scripts/split_rider_sarif_into_thirds.py
  python scripts/split_rider_sarif_into_thirds.py path/to/report.sarif out/dir
  python scripts/split_rider_sarif_into_thirds.py rz_issues.txt
  Get-Content report.sarif -Raw | python scripts/split_rider_sarif_into_thirds.py -
"""

from __future__ import annotations

import csv
import json
import pathlib
import re
import sys
from typing import Any

_RE_RZ_HEADER = re.compile(r"^===\s*(.+?)\s*===\s*$")
_RE_RZ_LINE = re.compile(r"^\s*L(\d+)\s+\[([^\]]+)\]\s*(.*)$")


def _message_text(result: dict[str, Any]) -> str:
    msg = result.get("message") or {}
    text = msg.get("text") or ""
    return " ".join(text.split())


def _sarif_result_to_row(r: dict[str, Any]) -> dict[str, Any]:
    locs = r.get("locations") or []
    uri = ""
    line: str | int = ""
    if locs:
        pl = locs[0].get("physicalLocation") or {}
        uri = (pl.get("artifactLocation") or {}).get("uri") or ""
        region = pl.get("region") or {}
        line = region.get("startLine", "")
    return {
        "level": r.get("level", ""),
        "ruleId": r.get("ruleId", ""),
        "uri": uri,
        "startLine": line,
        "message": _message_text(r),
    }


def _parse_sarif_results(raw: str) -> list[dict[str, Any]]:
    data = json.loads(raw)
    runs = data.get("runs") or []
    if not runs:
        raise ValueError("No runs in SARIF.")
    return [_sarif_result_to_row(r) for r in (runs[0].get("results") or [])]


def _parse_rz_issues(raw: str) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    current_uri = ""
    for line in raw.splitlines():
        hm = _RE_RZ_HEADER.match(line)
        if hm:
            current_uri = hm.group(1).strip()
            continue
        lm = _RE_RZ_LINE.match(line)
        if lm and current_uri:
            rows.append(
                {
                    "level": "",
                    "ruleId": lm.group(2).strip(),
                    "uri": current_uri,
                    "startLine": int(lm.group(1)),
                    "message": lm.group(3).strip(),
                }
            )
    return rows


def _detect_and_parse(raw: str) -> tuple[list[dict[str, Any]], str]:
    stripped = raw.lstrip("\ufeff").lstrip()
    if stripped.startswith("{"):
        return _parse_sarif_results(raw.lstrip("\ufeff")), "sarif"
    rz = _parse_rz_issues(raw)
    if rz:
        return rz, "rz_issues"
    raise ValueError(
        "Could not parse input: expected SARIF JSON (object starting with '{') "
        "or Rider text export (lines like '=== path/to/file ===' and '  L12 [RuleId] message')."
    )


def _row_sort_key(row: dict[str, Any]) -> tuple[str, int, str]:
    line = row.get("startLine")
    try:
        ln = int(line) if line != "" and line is not None else 0
    except (TypeError, ValueError):
        ln = 0
    uri = str(row.get("uri") or "")
    rid = str(row.get("ruleId") or "")
    return (uri.casefold(), ln, rid.casefold())


def _row_uri_line_rule(row: dict[str, Any]) -> tuple[str, int, str]:
    uri = str(row.get("uri") or "")
    line = row.get("startLine", "")
    try:
        ln = int(line) if line != "" and line is not None else 0
    except (TypeError, ValueError):
        ln = 0
    return uri, ln, str(row.get("ruleId") or "")


def _default_input_candidates(repo: pathlib.Path) -> list[pathlib.Path]:
    return [
        repo / "rider-inspections.sarif",
        repo / "artifacts" / "rider-inspections.sarif",
        repo / "rz_issues.txt",
    ]


def main() -> int:
    repo = pathlib.Path(__file__).resolve().parents[1]
    args = sys.argv[1:]

    input_label: str
    if not args:
        in_path: pathlib.Path | None = None
        for c in _default_input_candidates(repo):
            if c.is_file():
                in_path = c
                break
        if in_path is None:
            tried = "\n".join(f"  - {p.as_posix()}" for p in _default_input_candidates(repo))
            print(
                "No input file found. Use one of these paths, or pass a file / stdin (-):\n"
                f"{tried}\n"
                "  Or: Get-Content path\\to\\report.sarif -Raw | python scripts/split_rider_sarif_into_thirds.py -",
                file=sys.stderr,
            )
            return 1
        input_label = in_path.as_posix()
        raw = in_path.read_text(encoding="utf-8").lstrip("\ufeff")
        out_dir = repo / "artifacts" / "rider-sarif-thirds"
    elif args[0] == "-":
        input_label = "<stdin>"
        raw = sys.stdin.read().lstrip("\ufeff")
        out_dir = pathlib.Path(args[1]) if len(args) > 1 else repo / "artifacts" / "rider-sarif-thirds"
    else:
        in_path = pathlib.Path(args[0])
        if not in_path.is_file():
            print(f"Input not found: {in_path}", file=sys.stderr)
            return 1
        input_label = in_path.as_posix()
        raw = in_path.read_text(encoding="utf-8").lstrip("\ufeff")
        out_dir = pathlib.Path(args[1]) if len(args) > 1 else repo / "artifacts" / "rider-sarif-thirds"

    try:
        rows, source_kind = _detect_and_parse(raw)
    except (json.JSONDecodeError, ValueError) as e:
        print(f"Parse error: {e}", file=sys.stderr)
        return 1

    rows.sort(key=_row_sort_key)
    n = len(rows)
    if n == 0:
        print("No results to split.", file=sys.stderr)
        return 1

    base = n // 3
    remainder = n % 3
    sizes = [base, base, base]
    for i in range(remainder):
        sizes[i] += 1

    i0 = 0
    i1 = i0 + sizes[0]
    i2 = i1 + sizes[1]
    chunks = (rows[i0:i1], rows[i1:i2], rows[i2:])
    assert sum(sizes) == n

    out_dir.mkdir(parents=True, exist_ok=True)

    fieldnames = ("level", "ruleId", "uri", "startLine", "message")

    for idx, chunk in enumerate(chunks, start=1):
        out_path = out_dir / f"rider-inspections.part-{idx}-of-3.tsv"
        with out_path.open("w", encoding="utf-8", newline="") as f:
            w = csv.DictWriter(f, fieldnames=fieldnames, delimiter="\t", extrasaction="ignore")
            w.writeheader()
            for r in chunk:
                w.writerow(
                    {
                        "level": r.get("level", ""),
                        "ruleId": r.get("ruleId", ""),
                        "uri": r.get("uri", ""),
                        "startLine": r.get("startLine", ""),
                        "message": r.get("message", ""),
                    }
                )

    summary_path = out_dir / "rider-inspections.thirds-summary.txt"
    lines = [
        f"Input: {input_label} ({source_kind})",
        f"Total results: {n}",
        f"Part 1 of 3: {len(chunks[0])} issues",
        f"Part 2 of 3: {len(chunks[1])} issues",
        f"Part 3 of 3: {len(chunks[2])} issues",
        "",
        "Alphabetical ranges (by uri, first and last in each part):",
    ]
    for idx, chunk in enumerate(chunks, start=1):
        if not chunk:
            lines.append(f"  Part {idx}: (empty)")
            continue
        first_uri, first_line, _ = _row_uri_line_rule(chunk[0])
        last_uri, last_line, _ = _row_uri_line_rule(chunk[-1])
        lines.append(f"  Part {idx}: {first_uri}:{first_line}  -->  {last_uri}:{last_line}")

    lines.append("")
    lines.append("Output files:")
    for idx in range(1, 4):
        lines.append(f"  {(out_dir / f'rider-inspections.part-{idx}-of-3.tsv').as_posix()}")

    summary_path.write_text("\n".join(lines) + "\n", encoding="utf-8")

    print(summary_path.read_text(encoding="utf-8"))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
