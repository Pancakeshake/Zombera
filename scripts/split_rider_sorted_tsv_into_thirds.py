#!/usr/bin/env python3
"""
Split a sorted Rider inspections TSV into three near-equal parts.

This script is line-based on purpose: it preserves the exact original header and
row text from the input file, then writes three output TSV files with the same
header and partitioned body rows.

Usage:
  python scripts/split_rider_sorted_tsv_into_thirds.py
  python scripts/split_rider_sorted_tsv_into_thirds.py artifacts/rider-sarif-action/rider-inspections.sorted.by-file-line-issue.tsv
  python scripts/split_rider_sorted_tsv_into_thirds.py <input.tsv> <output_dir>
"""

from __future__ import annotations

import pathlib
import sys


def _default_input_candidates(repo: pathlib.Path) -> list[pathlib.Path]:
    return [
        repo / "artifacts" / "rider-sarif-action" / "rider-inspections.sorted.by-file-line-issue.tsv",
        repo / "rider-inspections.sorted.by-file-line-issue.tsv",
    ]


def _resolve_input_path(repo: pathlib.Path, args: list[str]) -> pathlib.Path:
    if args:
        candidate = pathlib.Path(args[0])
        if not candidate.is_absolute():
            candidate = repo / candidate
        if candidate.is_file():
            return candidate
        raise FileNotFoundError(f"Input TSV not found: {candidate}")

    for candidate in _default_input_candidates(repo):
        if candidate.is_file():
            return candidate

    tried = "\n".join(f"  - {path.as_posix()}" for path in _default_input_candidates(repo))
    raise FileNotFoundError("No default input TSV found. Tried:\n" + tried)


def _resolve_output_dir(input_path: pathlib.Path, repo: pathlib.Path, args: list[str]) -> pathlib.Path:
    if len(args) > 1:
        output = pathlib.Path(args[1])
        if not output.is_absolute():
            output = repo / output
        return output
    return input_path.parent


def _partition_sizes(total: int, parts: int) -> list[int]:
    base = total // parts
    remainder = total % parts
    sizes = [base for _ in range(parts)]
    for index in range(remainder):
        sizes[index] += 1
    return sizes


def main() -> int:
    repo = pathlib.Path(__file__).resolve().parents[1]
    args = sys.argv[1:]

    try:
        input_path = _resolve_input_path(repo, args)
    except FileNotFoundError as error:
        print(str(error), file=sys.stderr)
        return 1

    output_dir = _resolve_output_dir(input_path, repo, args)
    output_dir.mkdir(parents=True, exist_ok=True)

    lines = input_path.read_text(encoding="utf-8").splitlines()
    if not lines:
        print(f"Input is empty: {input_path.as_posix()}", file=sys.stderr)
        return 1

    header = lines[0]
    rows = lines[1:]
    total_rows = len(rows)

    if total_rows == 0:
        print(f"Input has header only and no data rows: {input_path.as_posix()}", file=sys.stderr)
        return 1

    sizes = _partition_sizes(total_rows, 3)

    split_points = [0]
    for size in sizes:
        split_points.append(split_points[-1] + size)

    parts = [
        rows[split_points[0] : split_points[1]],
        rows[split_points[1] : split_points[2]],
        rows[split_points[2] : split_points[3]],
    ]

    output_paths: list[pathlib.Path] = []
    for index, part_rows in enumerate(parts, start=1):
        out_path = output_dir / f"{input_path.stem}.part-{index}-of-3.tsv"
        payload = "\n".join([header, *part_rows]) + "\n"
        out_path.write_text(payload, encoding="utf-8")
        output_paths.append(out_path)

    summary_path = output_dir / f"{input_path.stem}.thirds-summary.txt"
    summary_lines = [
        f"Input: {input_path.as_posix()} (tsv)",
        f"Total data rows: {total_rows}",
        f"Part 1 of 3: {len(parts[0])} rows",
        f"Part 2 of 3: {len(parts[1])} rows",
        f"Part 3 of 3: {len(parts[2])} rows",
        "",
        "Output files:",
    ]
    for path in output_paths:
        summary_lines.append(f"  {path.as_posix()}")

    summary_text = "\n".join(summary_lines) + "\n"
    summary_path.write_text(summary_text, encoding="utf-8")

    print(summary_text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
