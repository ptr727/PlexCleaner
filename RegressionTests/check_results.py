"""Check a PlexCleaner process results file against the expected per-file outcome.

Each file is keyed by its original file name, without its directory, so a run in any media path compares.
Writing the expected file from a results file records an intended change in one step.
With --baseline, two regression harness runs compare directly, once their recorded inputs match.
"""

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any

from corpus_common import state_flags

Outcome = dict[str, Any]

BUILD_INFO = "buildinfo.json"
PROCESS_LOG = "PlexCleaner_process.log"
INPUT_FIELDS = (
    "Mode",
    "Corpus",
    "CorpusSnapshot",
    "ProcessOptions",
    "SettingsSha256",
    "Coverage",
)
TOOL_VERSION = re.compile(r'> (?P<tool>\w+) : Version: "?(?P<version>[^",]*)"?, Path: ')


def outcomes(results: dict[str, Any]) -> dict[str, Outcome]:
    """Return each file's outcome from a parsed results file, keyed by its original file name."""
    found: dict[str, Outcome] = {}
    for item in results["Results"]["Results"]:
        name = Path(item["OriginalFileName"]).name
        if name in found:
            raise ValueError(f"duplicate result for {name}")
        found[name] = {
            "NewFileName": Path(item["NewFileName"]).name,
            "Result": item["Result"],
            "Modified": item["Modified"],
            "State": sorted(state_flags(item.get("State"))),
            "FailedOperation": item.get("FailedOperation"),
            "InvalidReason": item.get("InvalidReason"),
        }
    return found


def differences(expected: dict[str, Outcome], actual: dict[str, Outcome]) -> list[str]:
    """Describe every way the actual outcomes differ from the expected ones, empty when they match."""
    found: list[str] = []
    for name in sorted(expected.keys() - actual.keys()):
        found.append(f"{name}: expected a result, found none")
    for name in sorted(actual.keys() - expected.keys()):
        found.append(f"{name}: unexpected result {actual[name]}")
    for name in sorted(expected.keys() & actual.keys()):
        for field in sorted(expected[name].keys() | actual[name].keys()):
            want, got = expected[name].get(field), actual[name].get(field)
            if want != got:
                found.append(f"{name}: {field} expected {want}, found {got}")
    return found


def tool_versions(log: str) -> dict[str, str]:
    """Return each media tool's version from the startup lines of a PlexCleaner log."""
    found: dict[str, str] = {}
    for match in TOOL_VERSION.finditer(log):
        found.setdefault(match["tool"], match["version"])
    return found


def run_inputs(run_dir: Path) -> dict[str, Any]:
    """Return a run's recorded inputs, its tool versions read from its process log."""
    inputs: dict[str, Any] = json.loads((run_dir / BUILD_INFO).read_text(encoding="utf-8"))
    log = run_dir / PROCESS_LOG
    versions = tool_versions(log.read_text(encoding="utf-8")) if log.is_file() else {}
    if versions:
        inputs["ToolVersions"] = versions
    return inputs


def input_checks(baseline: dict[str, Any], results: dict[str, Any]) -> tuple[list[str], list[str]]:
    """Compare two runs' inputs, returning the mismatches and the notes.

    A plugin difference is only a note, since plugins run after Results_process.json is written.
    """
    mismatches: list[str] = []
    notes: list[str] = []
    for field in (*INPUT_FIELDS, "ToolVersions", "Plugins"):
        missing = [
            side for side, run in (("baseline", baseline), ("results", results)) if field not in run
        ]
        if missing:
            found = f"{field}: not recorded by the {' or '.join(missing)} run"
        elif baseline[field] == results[field]:
            continue
        else:
            found = f"{field}: baseline {baseline[field]}, results {results[field]}"
        (notes if field == "Plugins" else mismatches).append(found)
    return mismatches, notes


def compare_runs(baseline: Path, results: Path) -> int:
    """Compare a results file against a baseline run's, refusing runs whose inputs differ."""
    for run in (baseline, results):
        if not (run.parent / BUILD_INFO).is_file():
            print(
                f"::error::no {BUILD_INFO} beside {run}, so its inputs are unknown", file=sys.stderr
            )
            return 1
    mismatches, notes = input_checks(run_inputs(baseline.parent), run_inputs(results.parent))
    for line in notes:
        print(f"note: {line}")
    if mismatches:
        for line in mismatches:
            print(f"::error::input {line}")
        print(f"{len(mismatches)} input mismatches, the runs are not comparable", file=sys.stderr)
        return 1

    expected = outcomes(json.loads(baseline.read_text(encoding="utf-8")))
    actual = outcomes(json.loads(results.read_text(encoding="utf-8")))
    found = differences(expected, actual)
    for line in found:
        print(f"::error::{line}")
    if found:
        print(f"{len(found)} differences from the baseline {baseline}", file=sys.stderr)
        return 1
    print(f"all {len(actual)} outcomes match the baseline {baseline}")
    return 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    against = ap.add_mutually_exclusive_group(required=True)
    against.add_argument("--expected", type=Path, help="expected outcomes JSON file")
    against.add_argument(
        "--baseline",
        type=Path,
        help="a harness run's Results_process.json, beside its buildinfo.json and process log",
    )
    ap.add_argument("--results", required=True, type=Path, help="PlexCleaner --resultsfile output")
    ap.add_argument(
        "--write", action="store_true", help="write the expected file from the results instead"
    )
    args = ap.parse_args(argv)

    if args.baseline is not None:
        if args.write:
            ap.error("--write records an --expected file, and has no meaning with --baseline")
        return compare_runs(args.baseline, args.results)

    actual = outcomes(json.loads(args.results.read_text(encoding="utf-8")))
    if args.write:
        args.expected.write_text(
            json.dumps(actual, indent=2, sort_keys=True) + "\n", encoding="utf-8"
        )
        print(f"wrote {len(actual)} outcomes to {args.expected}")
        return 0

    expected = json.loads(args.expected.read_text(encoding="utf-8"))
    found = differences(expected, actual)
    for line in found:
        print(f"::error::{line}")
    if found:
        print(f"{len(found)} differences, see --help to record an intended change", file=sys.stderr)
        return 1
    print(f"all {len(actual)} outcomes match {args.expected}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
