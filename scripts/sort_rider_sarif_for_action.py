#!/usr/bin/env python3
"""
Create actionable, sorted issue reports from Rider / InspectCode SARIF.

Primary goal:
- Sort issues by file, then line, then issue so cleanup can be worked file-by-file.

Outputs (TSV):
- rider-inspections.sorted.by-file-line-issue.tsv
- rider-inspections.sorted.by-issue-file-line.tsv
- rider-inspections.summary.by-file-and-issue.tsv
- rider-inspections.summary.by-issue.tsv
- rider-inspections.summary.by-file.tsv
- rider-inspections.summary.txt

Usage:
  python scripts/sort_rider_sarif_for_action.py
  python scripts/sort_rider_sarif_for_action.py rider-inspections.sarif
  python scripts/sort_rider_sarif_for_action.py rider-inspections.sarif artifacts/rider-sarif-action
"""

from __future__ import annotations

import csv
import json
import pathlib
import sys
from collections import Counter, defaultdict
from typing import Any


def _normalize_uri(uri: str) -> str:
    value = (uri or "").replace("\\", "/").strip()
    if value.startswith("./"):
        value = value[2:]
    return value


def _to_int(value: Any) -> int:
    if value is None:
        return 0
    try:
        return int(value)
    except (TypeError, ValueError):
        return 0


def _message_text(result: dict[str, Any]) -> str:
    message = result.get("message") or {}
    text = message.get("text") or message.get("markdown") or ""
    return " ".join(str(text).split())


def _first_location(result: dict[str, Any]) -> tuple[str, int, int, int, int]:
    locations = result.get("locations") or []
    if not locations:
        return "", 0, 0, 0, 0

    physical = (locations[0] or {}).get("physicalLocation") or {}
    artifact = physical.get("artifactLocation") or {}
    region = physical.get("region") or {}

    uri = _normalize_uri(str(artifact.get("uri") or ""))
    start_line = _to_int(region.get("startLine"))
    start_column = _to_int(region.get("startColumn"))
    end_line = _to_int(region.get("endLine"))
    end_column = _to_int(region.get("endColumn"))

    return uri, start_line, start_column, end_line, end_column


def _rule_lookup(run: dict[str, Any], rule_index: Any) -> tuple[str, str]:
    if not isinstance(rule_index, int):
        return "", ""

    rules = ((run.get("tool") or {}).get("driver") or {}).get("rules") or []
    if rule_index < 0 or rule_index >= len(rules):
        return "", ""

    rule = rules[rule_index] or {}
    rule_name = str(rule.get("name") or "")
    short_desc = (rule.get("shortDescription") or {}).get("text") or ""
    return rule_name, " ".join(str(short_desc).split())


def _parse_rows(raw: str) -> list[dict[str, Any]]:
    data = json.loads(raw)
    runs = data.get("runs") or []
    rows: list[dict[str, Any]] = []

    for run_index, run in enumerate(runs):
        results = run.get("results") or []
        for result_index, result in enumerate(results):
            uri, line, column, end_line, end_column = _first_location(result)
            rule_id = str(result.get("ruleId") or "")
            rule_name, rule_help = _rule_lookup(run, result.get("ruleIndex"))
            issue = rule_id or rule_name or "UnknownIssue"

            rows.append(
                {
                    "runIndex": run_index,
                    "resultIndex": result_index,
                    "file": uri,
                    "line": line,
                    "column": column,
                    "endLine": end_line,
                    "endColumn": end_column,
                    "issue": issue,
                    "ruleId": rule_id,
                    "ruleName": rule_name,
                    "level": str(result.get("level") or ""),
                    "message": _message_text(result),
                    "ruleHelp": rule_help,
                }
            )

    return rows


def _sort_by_file_line_issue(row: dict[str, Any]) -> tuple[str, int, str, int, str]:
    return (
        str(row.get("file") or "").casefold(),
        _to_int(row.get("line")),
        str(row.get("issue") or "").casefold(),
        _to_int(row.get("column")),
        str(row.get("message") or "").casefold(),
    )


def _sort_by_issue_file_line(row: dict[str, Any]) -> tuple[str, str, int, int]:
    return (
        str(row.get("issue") or "").casefold(),
        str(row.get("file") or "").casefold(),
        _to_int(row.get("line")),
        _to_int(row.get("column")),
    )


def _write_tsv(path: pathlib.Path, rows: list[dict[str, Any]], fieldnames: list[str]) -> None:
    with path.open("w", encoding="utf-8", newline="") as file:
        writer = csv.DictWriter(file, fieldnames=fieldnames, delimiter="\t", extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow(row)


def _default_input_candidates(repo: pathlib.Path) -> list[pathlib.Path]:
    return [
        repo / "rider-inspections.sarif",
        repo / "artifacts" / "rider-inspections.sarif",
        repo / "qodana-results" / "rider-inspections.sarif",
    ]


def _resolve_input_path(repo: pathlib.Path, args: list[str]) -> pathlib.Path:
    if args:
        candidate = pathlib.Path(args[0])
        if not candidate.is_absolute():
            candidate = repo / candidate
        if candidate.is_file():
            return candidate
        raise FileNotFoundError(f"Input SARIF not found: {candidate}")

    for candidate in _default_input_candidates(repo):
        if candidate.is_file():
            return candidate

    tried = "\n".join(f"  - {path.as_posix()}" for path in _default_input_candidates(repo))
    raise FileNotFoundError(
        "No SARIF input file found. Provide a path as the first argument. Tried:\n" + tried
    )


def _resolve_output_dir(repo: pathlib.Path, args: list[str]) -> pathlib.Path:
    if len(args) > 1:
        output = pathlib.Path(args[1])
        if not output.is_absolute():
            output = repo / output
        return output
    return repo / "artifacts" / "rider-sarif-action"


def _build_summary(rows_by_file: list[dict[str, Any]]) -> tuple[list[dict[str, Any]], list[dict[str, Any]], list[dict[str, Any]], str]:
    file_issue_counter: Counter[tuple[str, str]] = Counter()
    issue_counter: Counter[str] = Counter()
    file_counter: Counter[str] = Counter()
    first_line_by_file_issue: dict[tuple[str, str], int] = {}

    for row in rows_by_file:
        file_name = str(row.get("file") or "")
        issue = str(row.get("issue") or "")
        line = _to_int(row.get("line"))

        file_issue_counter[(file_name, issue)] += 1
        issue_counter[issue] += 1
        file_counter[file_name] += 1

        key = (file_name, issue)
        if key not in first_line_by_file_issue or line < first_line_by_file_issue[key]:
            first_line_by_file_issue[key] = line

    by_file_issue_rows: list[dict[str, Any]] = []
    for (file_name, issue), count in sorted(
        file_issue_counter.items(), key=lambda item: (item[0][0].casefold(), item[0][1].casefold())
    ):
        by_file_issue_rows.append(
            {
                "file": file_name,
                "issue": issue,
                "count": count,
                "firstLine": first_line_by_file_issue.get((file_name, issue), 0),
            }
        )

    by_issue_rows = [
        {"issue": issue, "count": count}
        for issue, count in sorted(issue_counter.items(), key=lambda item: (-item[1], item[0].casefold()))
    ]

    by_file_rows = [
        {"file": file_name, "count": count}
        for file_name, count in sorted(file_counter.items(), key=lambda item: (-item[1], item[0].casefold()))
    ]

    top_issues = by_issue_rows[:20]
    top_files = by_file_rows[:20]

    summary_lines = [
        f"Total issues: {len(rows_by_file)}",
        f"Unique files: {len(file_counter)}",
        f"Unique issues: {len(issue_counter)}",
        "",
        "Top issues:",
    ]
    for row in top_issues:
        summary_lines.append(f"  {row['count']:>5}  {row['issue']}")

    summary_lines.append("")
    summary_lines.append("Top files:")
    for row in top_files:
        summary_lines.append(f"  {row['count']:>5}  {row['file']}")

    summary_text = "\n".join(summary_lines) + "\n"
    return by_file_issue_rows, by_issue_rows, by_file_rows, summary_text


def main() -> int:
    repo = pathlib.Path(__file__).resolve().parents[1]
    args = sys.argv[1:]

    try:
        input_path = _resolve_input_path(repo, args)
    except FileNotFoundError as error:
        print(str(error), file=sys.stderr)
        return 1

    output_dir = _resolve_output_dir(repo, args)
    output_dir.mkdir(parents=True, exist_ok=True)

    raw = input_path.read_text(encoding="utf-8").lstrip("\ufeff")
    try:
        rows = _parse_rows(raw)
    except json.JSONDecodeError as error:
        print(f"Failed to parse SARIF JSON: {error}", file=sys.stderr)
        return 1

    if not rows:
        print("No SARIF issues found.", file=sys.stderr)
        return 1

    by_file = sorted(rows, key=_sort_by_file_line_issue)
    by_issue = sorted(rows, key=_sort_by_issue_file_line)

    issue_fieldnames = [
        "file",
        "line",
        "column",
        "endLine",
        "endColumn",
        "issue",
        "level",
        "ruleId",
        "ruleName",
        "message",
        "ruleHelp",
        "runIndex",
        "resultIndex",
    ]

    _write_tsv(output_dir / "rider-inspections.sorted.by-file-line-issue.tsv", by_file, issue_fieldnames)
    _write_tsv(output_dir / "rider-inspections.sorted.by-issue-file-line.tsv", by_issue, issue_fieldnames)

    by_file_issue_rows, by_issue_rows, by_file_rows, summary_text = _build_summary(by_file)

    _write_tsv(
        output_dir / "rider-inspections.summary.by-file-and-issue.tsv",
        by_file_issue_rows,
        ["file", "issue", "count", "firstLine"],
    )
    _write_tsv(
        output_dir / "rider-inspections.summary.by-issue.tsv",
        by_issue_rows,
        ["issue", "count"],
    )
    _write_tsv(
        output_dir / "rider-inspections.summary.by-file.tsv",
        by_file_rows,
        ["file", "count"],
    )

    summary_path = output_dir / "rider-inspections.summary.txt"
    summary_path.write_text(summary_text, encoding="utf-8")

    print(f"Input:  {input_path.as_posix()}")
    print(f"Output: {output_dir.as_posix()}")
    print(summary_text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
