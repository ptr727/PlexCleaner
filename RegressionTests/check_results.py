"""Check a PlexCleaner process results file against the expected per-file outcome.

Each file is keyed by its original file name, without its directory, so a run in any media path compares.
Writing the expected file from a results file records an intended change in one step.
With --baseline, two regression harness runs compare directly, after a warning for each input that differs.
The two runs' process logs then compare too, for each file's output tracks and as normalized line sets.
"""

import argparse
import json
import re
import sys
from collections import Counter
from pathlib import Path
from typing import Any

from corpus_common import BEFORE_RE, LINE_FILE_RE, LINE_RE, state_flags

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
TOOLS = ("FfMpeg", "FfProbe", "MkvMerge", "MkvPropEdit", "MediaInfo", "HandBrake", "SevenZip")
TOOL_VERSION = re.compile(r'> (?P<tool>\w+) : Version: "?(?P<version>[^",]*)"?, Path: ')
QUOTED = r'"(?:[^"\\]|\\.)*"'
DUMP_START = " Reading media info from tools : "
DUMP_TRACK = re.compile(
    rf"\[DBG\] <\d+> (?P<track>MkvMerge : (?:Video|Audio|Subtitle) : "
    rf"Format: {QUOTED}, Codec: {QUOTED})"
)
BITRATE_SUMMARY = re.compile(r'\[DBG\] <(?:\d+|T)> "(?:Video|Audio|Combined)" : Length: ')
BITRATE = re.compile(
    rf"{BITRATE_SUMMARY.pattern}|\[WRN\] <(?:\d+|T)> "
    r"(?:Maximum bitrate exceeded|Audio bitrate exceeds Video bitrate) : "
)
REENCODED = {"ReEncoded", "DeInterlaced", "Repaired"}
NORMALIZE = tuple(
    (re.compile(pattern), repl)
    for pattern, repl in (
        (r"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+ [+-]\d{2}:\d{2} ", ""),
        (r"ProcessId: \d+", "ProcessId: N"),
        (r'/Test/Config/[^/ "]+/', "/Test/Config/VER/"),
        (r"^\[INF\] <\d+> Application Version : .*", "[INF] Application Version : <REDACTED>"),
        (r"^\[INF\] <\d+> Runtime Version : .*", "[INF] Runtime Version : <REDACTED>"),
        (r"<\d+>", "<T>"),
        (r"Uid: \d+", "Uid: U"),
        (r"\b0x[0-9a-fA-F]+", "0xADDR"),
        (r" \(\d+\.\d+%\) ", " (P%) "),
        (r" Elapsed : [\d:.]+ : After : ", " Elapsed : E : After : "),
        (r"Processing time : [\d:.]+$", "Processing time : E"),
        (r"\[\d{2}:\d{2}:\d{2}\]", "[HH:MM:SS]"),
        (r"fps= *[\d.]+", "fps=N"),
        (r"speed= *[\d.]+x", "speed=Nx"),
        (r"elapsed=[\d:.]+", "elapsed=E"),
    )
)


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


def read_log(run_dir: Path) -> str | None:
    """Return a run's process log text, or None when the run wrote no log."""
    log = run_dir / PROCESS_LOG
    return log.read_text(encoding="utf-8") if log.is_file() else None


def run_inputs(run_dir: Path, log: str | None) -> dict[str, Any]:
    """Return a run's recorded inputs, its tool versions read from its process log.

    The tool versions count as recorded only when the log names a version for every tool in TOOLS.
    """
    info = run_dir / BUILD_INFO
    inputs: dict[str, Any] = json.loads(info.read_text(encoding="utf-8")) if info.is_file() else {}
    versions = tool_versions(log) if log is not None else {}
    if all(versions.get(tool) for tool in TOOLS):
        inputs["ToolVersions"] = versions
    return inputs


def input_checks(baseline: dict[str, Any], results: dict[str, Any]) -> tuple[list[str], list[str]]:
    """Describe each input the two runs do not both record alike, as warnings and notes.

    A plugin difference is only a note, since plugins run after Results_process.json is written.
    """
    warnings: list[str] = []
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
        (notes if field == "Plugins" else warnings).append(found)
    return warnings, notes


def line_name(line: str) -> str | None:
    """Return the file name, without its directory, of the quoted path a log line ends with."""
    file = LINE_FILE_RE.search(line)
    return Path(file[1]).name if file else None


def output_tracks(log: str) -> dict[str, list[str]]:
    """Return each file's output tracks, from the last MkvMerge track dump the log holds for it.

    A dump is logged at Debug each time PlexCleaner reads a file, so the last one describes its output.
    Tracks keep their logged order, so a reorder within a type differs, and a read logging none keeps none.
    Each file is keyed by the name its dump lines end with, without its directory, as outcomes are.
    Two paths sharing a name would mix their dumps, so that refuses.
    """
    dumps: dict[str, list[str]] = {}
    paths: dict[str, str] = {}
    for line in log.splitlines():
        file = LINE_FILE_RE.search(line)
        if file is None:
            continue
        name = Path(file[1]).name
        if DUMP_START in line:
            if paths.setdefault(name, file[1]) != file[1]:
                raise ValueError(f"track dumps for {paths[name]} and {file[1]} share a name")
            dumps[name] = []
        elif track := DUMP_TRACK.search(line):
            dumps.setdefault(name, []).append(track["track"])
    return dumps


def track_differences(expected: dict[str, list[str]], actual: dict[str, list[str]]) -> list[str]:
    """Describe each file whose output tracks differ between two runs, empty when they match.

    A file one run dumped and the other did not differs, even where the one dump holds no tracks.
    """
    found: list[str] = []
    for name in sorted(expected.keys() | actual.keys()):
        if name not in actual:
            found.append(f"{name}: no track dump in the results run")
        elif name not in expected:
            found.append(f"{name}: no track dump in the baseline run")
        elif expected[name] != actual[name]:
            found.append(f"{name}: tracks expected {expected[name]}, found {actual[name]}")
    return found


def normalized_lines(log: str) -> Counter[str]:
    """Return a log's lines with the fields that differ between two runs of one build normalized.

    A parallel run interleaves files differently each time, so the lines compare as a multiset.
    A bitrate summary names no file, so it gains the file its thread last began processing.
    """
    thread_file: dict[str, str] = {}
    found: Counter[str] = Counter()
    for line in log.splitlines():
        if thread := LINE_RE.match(line):
            if before := BEFORE_RE.search(thread[2]):
                thread_file[thread[1]] = Path(before[1]).name
            elif BITRATE_SUMMARY.search(line) and thread[1] in thread_file:
                line = f'{line} : "{thread_file[thread[1]]}"'
        for pattern, repl in NORMALIZE:
            line = pattern.sub(repl, line)
        found[line] += 1
    return found


def bitrate_noise(expected: dict[str, Outcome], actual: dict[str, Outcome]) -> set[str]:
    """Return the names of the re-encoded files whose outcome matches in both runs.

    A re-encode is not byte-identical from run to run, so such a file's bitrate summary can shift.
    """
    found: set[str] = set()
    for name in expected.keys() & actual.keys():
        if expected[name] == actual[name] and REENCODED & set(expected[name]["State"]):
            found |= {name, expected[name]["NewFileName"]}
    return found


def log_differences(
    expected: Counter[str], actual: Counter[str], noise: set[str]
) -> tuple[list[str], list[str]]:
    """Describe each normalized log line one run holds more often, as differences and noise notes."""
    found: list[str] = []
    shifted: set[str] = set()
    for side, lines in (("baseline", expected - actual), ("results", actual - expected)):
        for line in sorted(lines.elements()):
            name = line_name(line)
            if name is not None and name in noise and BITRATE.search(line):
                shifted.add(name)
            else:
                found.append(f"{side} only: {line}")
    notes = [
        f"bitrate figures of {name} shifted, and its outcome matches" for name in sorted(shifted)
    ]
    return found, notes


def undumped_results(outcomes_: dict[str, Outcome], tracks: dict[str, list[str]]) -> list[str]:
    """Return the names of a run's results its log holds no track dump for, a deleted file aside.

    A deleted file is never read, and every other file is read from its tools first, so it logs a dump.
    """
    return sorted(
        name
        for name, outcome in outcomes_.items()
        if "FileDeleted" not in outcome["State"]
        and name not in tracks
        and outcome["NewFileName"] not in tracks
    )


def compare_logs(
    baseline: str | None,
    results: str | None,
    expected: dict[str, Outcome],
    actual: dict[str, Outcome],
) -> list[str]:
    """Compare two runs' process logs, printing each log finding, and return the track failures.

    A track check that cannot run is a failure, so a run with no track evidence never reads as a match.
    """
    if baseline is None or results is None:
        missing = [
            side for side, log in (("baseline", baseline), ("results", results)) if log is None
        ]
        return [
            f"no process log in the {' or '.join(missing)} run, so the track check is unverified"
        ]

    try:
        tracks = {"baseline": output_tracks(baseline), "results": output_tracks(results)}
    except ValueError as error:
        found = [f"{error}, so the track check is unverified"]
    else:
        undumped = [side for side, dumps in tracks.items() if not dumps]
        if undumped:
            found = [
                f"no Debug track dump in the {' or '.join(undumped)} run, so the track check is unverified"
            ]
        else:
            found = track_differences(tracks["baseline"], tracks["results"])
            for side, outcomes_ in (("baseline", expected), ("results", actual)):
                found += [
                    f"{name}: no track dump in the {side} run, so its track check is unverified"
                    for name in undumped_results(outcomes_, tracks[side])
                ]

    lines, notes = log_differences(
        normalized_lines(baseline), normalized_lines(results), bitrate_noise(expected, actual)
    )
    for line in notes:
        print(f"note: {line}")
    for line in lines:
        print(f"::warning::log {line}")
    if lines:
        print(
            f"{len(lines)} normalized log lines differ, so judge each against the change under test"
        )
    return found


def compare_runs(baseline: Path, results: Path) -> int:
    """Compare a results file against a baseline run's, warning first of each input that differs."""
    baseline_log, results_log = read_log(baseline.parent), read_log(results.parent)
    warnings, notes = input_checks(
        run_inputs(baseline.parent, baseline_log), run_inputs(results.parent, results_log)
    )
    for line in notes:
        print(f"note: {line}")
    for line in warnings:
        print(f"::warning::input {line}")
    if warnings:
        print(f"{len(warnings)} input differences, so judge each outcome difference against them")

    expected = outcomes(json.loads(baseline.read_text(encoding="utf-8")))
    actual = outcomes(json.loads(results.read_text(encoding="utf-8")))
    found = differences(expected, actual)
    found += compare_logs(baseline_log, results_log, expected, actual)
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
