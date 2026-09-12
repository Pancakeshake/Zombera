#!/usr/bin/env python3
"""
Regenerate Rider SARIF action artifacts under artifacts/rider-sarif-action.

This wrapper runs the existing sort/split scripts and also rebuilds the
scripts-only queue focused on Assets/Scripts and Assets/Editor.

Outputs recreated in the target folder:
- rider-inspections.sorted.by-file-line-issue.tsv
- rider-inspections.sorted.by-issue-file-line.tsv
- rider-inspections.summary.by-file.tsv
- rider-inspections.summary.by-file-and-issue.tsv
- rider-inspections.summary.by-issue.tsv
- rider-inspections.summary.txt
- rider-inspections.sorted.by-file-line-issue.part-1-of-3.tsv
- rider-inspections.sorted.by-file-line-issue.part-2-of-3.tsv
- rider-inspections.sorted.by-file-line-issue.part-3-of-3.tsv
- rider-inspections.sorted.by-file-line-issue.thirds-summary.txt
- rider-inspections.scripts-only.sorted.by-file-line-issue.tsv
- rider-inspections.scripts-only.sorted.by-file-line-issue.part-1-of-3.tsv
- rider-inspections.scripts-only.sorted.by-file-line-issue.part-2-of-3.tsv
- rider-inspections.scripts-only.sorted.by-file-line-issue.part-3-of-3.tsv
- rider-inspections.scripts-only.sorted.by-file-line-issue.thirds-summary.txt

Usage:
  python scripts/rebuild_rider_sarif_action.py
  python scripts/rebuild_rider_sarif_action.py rider-inspections.sarif
  python scripts/rebuild_rider_sarif_action.py rider-inspections.sarif artifacts/rider-sarif-action

If SARIF is missing, the script falls back to reusing the existing sorted TSV
in the output directory to recreate split and scripts-only files.
"""

from __future__ import annotations

import argparse
import csv
import pathlib
import subprocess
import sys


def _repo_root() -> pathlib.Path:
    return pathlib.Path(__file__).resolve().parents[1]


def _default_input_candidates(repo: pathlib.Path) -> list[pathlib.Path]:
    return [
        repo / "rider-inspections.sarif",
        repo / "artifacts" / "rider-inspections.sarif",
        repo / "qodana-results" / "rider-inspections.sarif",
    ]


def _resolve_input_path(repo: pathlib.Path, value: str | None) -> pathlib.Path:
    if value:
        candidate = pathlib.Path(value)
        if not candidate.is_absolute():
            candidate = repo / candidate
        if candidate.is_file():
            return candidate
        raise FileNotFoundError(f"Input SARIF not found: {candidate.as_posix()}")

    for candidate in _default_input_candidates(repo):
        if candidate.is_file():
            return candidate

    tried = "\n".join(f"  - {path.as_posix()}" for path in _default_input_candidates(repo))
    raise FileNotFoundError("No SARIF input file found. Tried:\n" + tried)


def _resolve_output_dir(repo: pathlib.Path, value: str | None) -> pathlib.Path:
    if value:
        candidate = pathlib.Path(value)
        if not candidate.is_absolute():
            candidate = repo / candidate
        return candidate
    return repo / "artifacts" / "rider-sarif-action"


def _run_step(label: str, command: list[str]) -> None:
    print(f"[run] {label}")
    subprocess.run(command, check=True)


def _is_scripts_only_path(file_path: str) -> bool:
    normalized = file_path.replace("\\", "/").lstrip("./")
    lowered = normalized.casefold()
    return lowered.startswith("assets/scripts/") or lowered.startswith("assets/editor/")


def _write_scripts_only_sorted(source_tsv: pathlib.Path, target_tsv: pathlib.Path) -> int:
    with source_tsv.open("r", encoding="utf-8", newline="") as source_file:
        reader = csv.DictReader(source_file, delimiter="\t")
        fieldnames = reader.fieldnames or []
        if not fieldnames:
            raise ValueError(f"Missing TSV header in {source_tsv.as_posix()}")

        target_tsv.parent.mkdir(parents=True, exist_ok=True)
        with target_tsv.open("w", encoding="utf-8", newline="") as target_file:
            writer = csv.DictWriter(target_file, fieldnames=fieldnames, delimiter="\t", extrasaction="ignore")
            writer.writeheader()

            count = 0
            for row in reader:
                file_path = str(row.get("file") or "")
                if not _is_scripts_only_path(file_path):
                    continue
                writer.writerow(row)
                count += 1

    return count


def _cleanup_split_outputs(stem: str, output_dir: pathlib.Path) -> None:
    for index in range(1, 4):
        path = output_dir / f"{stem}.part-{index}-of-3.tsv"
        if path.exists():
            path.unlink()


def _write_empty_split_summary(input_path: pathlib.Path) -> None:
    stem = input_path.stem
    summary_path = input_path.parent / f"{stem}.thirds-summary.txt"
    _cleanup_split_outputs(stem, input_path.parent)
    summary_text = "\n".join(
        [
            f"Input: {input_path.as_posix()} (tsv)",
            "Total data rows: 0",
            "Part 1 of 3: 0 rows",
            "Part 2 of 3: 0 rows",
            "Part 3 of 3: 0 rows",
            "",
            "Output files:",
            "  (no split files generated because there were no matching rows)",
            "",
        ]
    )
    summary_path.write_text(summary_text, encoding="utf-8")


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Regenerate rider-sarif-action reports (full + scripts-only)."
    )
    parser.add_argument(
        "input_sarif",
        nargs="?",
        help="Input SARIF path (defaults to rider-inspections.sarif candidates).",
    )
    parser.add_argument(
        "output_dir",
        nargs="?",
        help="Output directory (defaults to artifacts/rider-sarif-action).",
    )
    return parser.parse_args()


def main() -> int:
    args = _parse_args()
    repo = _repo_root()

    output_dir = _resolve_output_dir(repo, args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    full_sorted_tsv = output_dir / "rider-inspections.sorted.by-file-line-issue.tsv"

    input_sarif: pathlib.Path | None
    try:
        input_sarif = _resolve_input_path(repo, args.input_sarif)
    except FileNotFoundError as error:
        input_sarif = None
        if not full_sorted_tsv.is_file():
            print(str(error), file=sys.stderr)
            print(
                f"Fallback TSV not found: {full_sorted_tsv.as_posix()}",
                file=sys.stderr,
            )
            return 1
        print(f"[warn] {error}")
        print(f"[info] Reusing existing sorted TSV: {full_sorted_tsv.as_posix()}")

    sort_script = repo / "scripts" / "sort_rider_sarif_for_action.py"
    split_script = repo / "scripts" / "split_rider_sorted_tsv_into_thirds.py"
    python_exe = sys.executable

    if input_sarif is not None:
        _run_step(
            "Sort SARIF into action TSV reports",
            [
                python_exe,
                sort_script.as_posix(),
                input_sarif.as_posix(),
                output_dir.as_posix(),
            ],
        )

    _run_step(
        "Split full sorted TSV into thirds",
        [
            python_exe,
            split_script.as_posix(),
            full_sorted_tsv.as_posix(),
            output_dir.as_posix(),
        ],
    )

    scripts_only_sorted_tsv = output_dir / "rider-inspections.scripts-only.sorted.by-file-line-issue.tsv"
    scripts_only_count = _write_scripts_only_sorted(full_sorted_tsv, scripts_only_sorted_tsv)
    print(f"[info] scripts-only rows: {scripts_only_count}")

    if scripts_only_count > 0:
        _run_step(
            "Split scripts-only sorted TSV into thirds",
            [
                python_exe,
                split_script.as_posix(),
                scripts_only_sorted_tsv.as_posix(),
                output_dir.as_posix(),
            ],
        )
    else:
        _write_empty_split_summary(scripts_only_sorted_tsv)
        print("[info] scripts-only split skipped because no rows matched Assets/Scripts or Assets/Editor")

    print("[done] rider-sarif-action artifacts rebuilt")
    if input_sarif is not None:
        print(f"[done] input:  {input_sarif.as_posix()}")
    else:
        print(f"[done] input:  {full_sorted_tsv.as_posix()} (fallback TSV)")
    print(f"[done] output: {output_dir.as_posix()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())