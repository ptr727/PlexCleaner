"""Check a PlexCleaner process results file against the expected per-file outcome.

Each file is keyed by its original file name, without its directory, so a run in any media path compares.
Writing the expected file from a results file records an intended change in one step.
"""

import argparse
import json
import sys
from pathlib import Path
from typing import Any

from corpus_common import state_flags

Outcome = dict[str, Any]


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


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--expected", required=True, type=Path, help="expected outcomes JSON file")
    ap.add_argument("--results", required=True, type=Path, help="PlexCleaner --resultsfile output")
    ap.add_argument(
        "--write", action="store_true", help="write the expected file from the results instead"
    )
    args = ap.parse_args(argv)

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
