"""Tests for comparing a process results file with the expected outcomes in check_results."""

import io
import json
import re
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
from typing import Any

import check_results as cr


def result(name: str, state: str, new_name: str = "", result_ok: bool = True) -> dict[str, Any]:
    return {
        "Result": result_ok,
        "OriginalFileName": f"/Test/Media/{name}",
        "NewFileName": f"/Test/Media/{new_name or name}",
        "Modified": bool(state),
        "State": state,
    }


def results(*items: dict[str, Any]) -> dict[str, Any]:
    return {"SchemaVersion": 1, "Results": {"Results": list(items)}}


class OutcomesTests(unittest.TestCase):
    def test_keys_by_file_name_and_sorts_state(self) -> None:
        found = cr.outcomes(results(result("a.MKV", "Verified, ReMuxed", "a.mkv")))
        self.assertEqual(
            found,
            {
                "a.MKV": {
                    "NewFileName": "a.mkv",
                    "Result": True,
                    "Modified": True,
                    "State": ["ReMuxed", "Verified"],
                    "FailedOperation": None,
                    "InvalidReason": None,
                }
            },
        )

    def test_empty_state(self) -> None:
        self.assertEqual(cr.outcomes(results(result("a.mkv", "")))["a.mkv"]["State"], [])

    def test_failed_operation(self) -> None:
        item = result("a.mkv", "FileDeleted", result_ok=False) | {"FailedOperation": "Verify"}
        self.assertEqual(cr.outcomes(results(item))["a.mkv"]["FailedOperation"], "Verify")

    def test_invalid_reason(self) -> None:
        item = result("a.mkv", "FileDeleted", result_ok=False) | {"InvalidReason": "Verify failed"}
        self.assertEqual(cr.outcomes(results(item))["a.mkv"]["InvalidReason"], "Verify failed")
        other = item | {"InvalidReason": "Decode failed"}
        self.assertEqual(
            cr.differences(cr.outcomes(results(item)), cr.outcomes(results(other))),
            ["a.mkv: InvalidReason expected Verify failed, found Decode failed"],
        )

    def test_duplicate_name_refused(self) -> None:
        dup = results(result("a.mkv", "Verified"), result("a.mkv", "Verified"))
        with self.assertRaises(ValueError):
            cr.outcomes(dup)


class DifferencesTests(unittest.TestCase):
    def setUp(self) -> None:
        self.expected = cr.outcomes(
            results(result("a.mkv", "Verified"), result("b.mkv", "ReMuxed"))
        )

    def test_match(self) -> None:
        self.assertEqual(cr.differences(self.expected, dict(self.expected)), [])

    def test_missing_and_unexpected(self) -> None:
        actual = cr.outcomes(results(result("a.mkv", "Verified"), result("c.mkv", "Verified")))
        found = cr.differences(self.expected, actual)
        self.assertEqual(len(found), 2)
        self.assertTrue(found[0].startswith("b.mkv: expected a result"))
        self.assertTrue(found[1].startswith("c.mkv: unexpected result"))

    def test_changed_state(self) -> None:
        actual = cr.outcomes(
            results(result("a.mkv", "Verified"), result("b.mkv", "ReMuxed, SetFlags"))
        )
        self.assertEqual(
            cr.differences(self.expected, actual),
            ["b.mkv: State expected ['ReMuxed'], found ['ReMuxed', 'SetFlags']"],
        )


class MainTests(unittest.TestCase):
    def setUp(self) -> None:
        self._dir = tempfile.TemporaryDirectory()
        self.addCleanup(self._dir.cleanup)
        self.root = Path(self._dir.name)
        self.results = self.root / "Results.json"
        self.expected = self.root / "expected.json"
        self.results.write_text(json.dumps(results(result("a.mkv", "Verified"))), encoding="utf-8")

    def run_main(self, *extra: str) -> int:
        args = ["--expected", str(self.expected), "--results", str(self.results), *extra]
        return cr.main(args)

    def test_write_then_check_passes(self) -> None:
        self.assertEqual(self.run_main("--write"), 0)
        self.assertEqual(self.run_main(), 0)

    def test_check_fails_on_difference(self) -> None:
        self.assertEqual(self.run_main("--write"), 0)
        self.results.write_text(json.dumps(results(result("a.mkv", "ReMuxed"))), encoding="utf-8")
        self.assertEqual(self.run_main(), 1)

    def test_unknown_argument_refused(self) -> None:
        with self.assertRaises(SystemExit) as raised:
            self.run_main("--bogus")
        self.assertNotEqual(raised.exception.code, 0)


TOOLS_LOG = """\
2026-01-01 00:00:00.000 +00:00 [INF] <1> FfMpeg : Version: "{ffmpeg}", Path: "ffmpeg"
2026-01-01 00:00:00.000 +00:00 [INF] <1> FfProbe : Version: "{ffmpeg}", Path: "ffprobe"
2026-01-01 00:00:00.000 +00:00 [INF] <1> MkvMerge : Version: "1.0", Path: "mkvmerge"
2026-01-01 00:00:00.000 +00:00 [INF] <1> MkvPropEdit : Version: "1.0", Path: "mkvpropedit"
2026-01-01 00:00:00.000 +00:00 [INF] <1> MediaInfo : Version: "2.0", Path: "mediainfo"
2026-01-01 00:00:00.000 +00:00 [INF] <1> HandBrake : Version: "3.0", Path: "HandBrakeCLI"
2026-01-01 00:00:00.000 +00:00 [INF] <1> SevenZip : Version: "4.0", Path: "7z"
"""
LOG = TOOLS_LOG + "".join(
    f"2026-01-01 00:00:01.000 +00:00 [DBG] <2> {text}\n"
    for text in (
        'Reading media info from tools : "/Test/Media/a.mkv"',
        'MkvMerge : Video : Format: "AVC", Codec: "V_MPEG4/ISO/AVC", Uid: 1 : "/Test/Media/a.mkv"',
    )
)


def build_info(**overrides: Any) -> dict[str, Any]:
    info: dict[str, Any] = {
        "Channel": "local",
        "Version": "0123456789",
        "Image": "plexcleaner:a",
        "Mode": "quick",
        "Corpus": "reduced",
        "CorpusSnapshot": "pool/corpus@one",
        "ProcessOptions": ["--parallel", "--threadcount", "8", "--testsnippets"],
        "SettingsSha256": "aa",
        "Coverage": False,
        "Plugins": [],
    }
    return info | overrides


class ToolVersionsTests(unittest.TestCase):
    def test_tools_match_application_tool_list(self) -> None:
        source = Path(__file__).resolve().parents[2] / "PlexCleaner" / "Tools.cs"
        found = re.search(r"GetToolList\(\) =>\s*\[([^\]]*)\]", source.read_text())
        assert found is not None
        self.assertEqual(tuple(name.strip() for name in found[1].split(",")), cr.TOOLS)

    def test_reads_each_tool_once(self) -> None:
        log = LOG.format(ffmpeg="8.1") + LOG.format(ffmpeg="9.0")
        self.assertEqual(cr.tool_versions(log)["FfMpeg"], "8.1")
        self.assertEqual(cr.tool_versions(log).keys(), set(cr.TOOLS))


class BaselineTests(unittest.TestCase):
    def setUp(self) -> None:
        self._dir = tempfile.TemporaryDirectory()
        self.addCleanup(self._dir.cleanup)
        self.root = Path(self._dir.name)
        self.baseline = self.write_run("baseline", build_info())

    def write_run(
        self,
        name: str,
        info: dict[str, Any] | None,
        state: str = "Verified",
        ffmpeg: str = "8.1",
    ) -> Path:
        run = self.root / name
        run.mkdir()
        if info is not None:
            (run / "buildinfo.json").write_text(json.dumps(info), encoding="utf-8")
        (run / "PlexCleaner_process.log").write_text(LOG.format(ffmpeg=ffmpeg), encoding="utf-8")
        found = run / "Results_process.json"
        found.write_text(json.dumps(results(result("a.mkv", state))), encoding="utf-8")
        return found

    def compare(self, candidate: Path) -> tuple[int, str]:
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            code = cr.main(["--baseline", str(self.baseline), "--results", str(candidate)])
        return code, out.getvalue() + err.getvalue()

    def test_match(self) -> None:
        code, text = self.compare(self.write_run("same", build_info(Image="plexcleaner:b")))
        self.assertEqual(code, 0)
        self.assertNotIn("::warning::", text)

    def test_outcome_difference(self) -> None:
        code, text = self.compare(self.write_run("changed", build_info(), state="ReMuxed"))
        self.assertEqual(code, 1)
        self.assertIn("a.mkv: State expected ['Verified'], found ['ReMuxed']", text)

    def test_input_mismatch_warns(self) -> None:
        cases = {
            "Corpus": build_info(Corpus="full"),
            "CorpusSnapshot": build_info(CorpusSnapshot="pool/corpus@two"),
            "ProcessOptions": build_info(ProcessOptions=["--parallel", "--threadcount", "4"]),
            "SettingsSha256": build_info(SettingsSha256="bb"),
            "Coverage": build_info(Coverage=True),
            "Mode": build_info(Mode="full"),
        }
        for field, info in cases.items():
            with self.subTest(field=field):
                code, text = self.compare(self.write_run(field, info))
                self.assertEqual(code, 0)
                self.assertIn(f"::warning::input {field}:", text)

    def test_plugin_difference_is_noted(self) -> None:
        code, text = self.compare(self.write_run("plugins", build_info(Plugins=["A"])))
        self.assertEqual(code, 0)
        self.assertIn("note: Plugins:", text)
        self.assertNotIn("::warning::", text)

    def test_missing_plugins_is_noted(self) -> None:
        info = build_info()
        del info["Plugins"]
        code, text = self.compare(self.write_run("noplugins", info))
        self.assertEqual(code, 0)
        self.assertIn("note: Plugins: not recorded by the results run", text)
        self.assertNotIn("::warning::", text)

    def test_tool_version_mismatch_warns(self) -> None:
        code, text = self.compare(self.write_run("tools", build_info(), ffmpeg="9.0"))
        self.assertEqual(code, 0)
        self.assertIn("::warning::input ToolVersions:", text)

    def test_input_warning_still_compares_outcomes(self) -> None:
        code, text = self.compare(self.write_run("both", build_info(Mode="full"), state="ReMuxed"))
        self.assertEqual(code, 1)
        self.assertIn("::warning::input Mode:", text)
        self.assertIn("a.mkv: State expected ['Verified'], found ['ReMuxed']", text)

    def test_missing_field_warns(self) -> None:
        info = build_info()
        del info["CorpusSnapshot"]
        code, text = self.compare(self.write_run("older", info))
        self.assertEqual(code, 0)
        self.assertIn("::warning::input CorpusSnapshot: not recorded by the results run", text)

    def test_missing_tool_versions_warns(self) -> None:
        candidate = self.write_run("nolog", build_info())
        (candidate.parent / "PlexCleaner_process.log").unlink()
        code, text = self.compare(candidate)
        self.assertEqual(code, 1)
        self.assertIn("::warning::input ToolVersions: not recorded by the results run", text)

    def test_missing_baseline_input_warns(self) -> None:
        info = build_info()
        del info["CorpusSnapshot"]
        (self.baseline.parent / "buildinfo.json").write_text(json.dumps(info), encoding="utf-8")
        (self.baseline.parent / "PlexCleaner_process.log").unlink()
        code, text = self.compare(self.write_run("current", build_info()))
        self.assertEqual(code, 1)
        for field in ("CorpusSnapshot", "ToolVersions"):
            with self.subTest(field=field):
                self.assertIn(f"::warning::input {field}: not recorded by the baseline run", text)

    def test_partial_tool_versions_warns(self) -> None:
        for run in (self.baseline, self.write_run("partial", build_info())):
            log = run.parent / "PlexCleaner_process.log"
            text = log.read_text(encoding="utf-8")
            kept = [line for line in text.splitlines(keepends=True) if "SevenZip" not in line]
            log.write_text("".join(kept), encoding="utf-8")
        code, text = self.compare(self.root / "partial" / "Results_process.json")
        self.assertEqual(code, 0)
        self.assertIn(
            "::warning::input ToolVersions: not recorded by the baseline or results run", text
        )

    def test_missing_build_info_warns(self) -> None:
        code, text = self.compare(self.write_run("bare", None))
        self.assertEqual(code, 0)
        self.assertIn("::warning::input Mode: not recorded by the results run", text)

    def test_write_with_baseline_refused(self) -> None:
        candidate = self.write_run("write", build_info())
        with redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as raised:
            cr.main(["--baseline", str(self.baseline), "--results", str(candidate), "--write"])
        self.assertNotEqual(raised.exception.code, 0)


def log_line(text: str, thread: int = 2, level: str = "DBG", second: int = 0) -> str:
    return f"2026-01-01 00:00:{second:02d}.123 -07:00 [{level}] <{thread}> {text}"


def track(kind: str, codec: str, uid: int = 1, name: str = "a.mkv") -> str:
    return (
        f'MkvMerge : {kind} : Format: "F", Codec: "{codec}", Language: "eng", '
        f'Uid: {uid}, Container: "Matroska" : "/Test/Media/{name}"'
    )


def dump(*tracks: str, name: str = "a.mkv") -> list[str]:
    return [f'Reading media info from tools : "/Test/Media/{name}"', *tracks]


class OutputTracksTests(unittest.TestCase):
    def test_last_dump_wins(self) -> None:
        texts = [
            *dump(track("Video", "V_MPEG2"), track("Audio", "A_AC3")),
            *dump(track("Video", "V_MPEG4/ISO/AVC")),
        ]
        log = "\n".join(log_line(text) for text in texts)
        self.assertEqual(
            cr.output_tracks(log),
            {"a.mkv": ['MkvMerge : Video : Format: "F", Codec: "V_MPEG4/ISO/AVC"']},
        )

    def test_escaped_quote_in_format(self) -> None:
        text = 'MkvMerge : Audio : Format: "AC-3 \\"Dolby\\"", Codec: "A_AC3" : "/Test/Media/a.mkv"'
        log = "\n".join(log_line(text) for text in [*dump(), text])
        self.assertEqual(
            cr.output_tracks(log),
            {"a.mkv": ['MkvMerge : Audio : Format: "AC-3 \\"Dolby\\"", Codec: "A_AC3"']},
        )

    def test_selection_and_information_lines_skipped(self) -> None:
        log = "\n".join(
            [
                log_line(f'"Keep" : {track("Video", "V_VP9")}', level="INF"),
                log_line(track("Video", "V_VP9"), level="INF"),
            ]
        )
        self.assertEqual(cr.output_tracks(log), {})


class NormalizedLinesTests(unittest.TestCase):
    def test_run_noise_normalized(self) -> None:
        def run(n: int) -> str:
            banner = [f"Application Version : 3.24.{n}", f"Runtime Version : .NET 10.0.{n}"]
            texts = [
                f"Settings : /Test/Config/3.24.{n}/PlexCleaner.json, ProcessId: {100 + n}",
                f'ProcessFiles ({n}.25%) Before : "/Test/Media/a.mkv"',
                f"{track('Video', 'V_VP9', uid=n)}",
                f"Exception at 0x7ff{n}a0 : [12:00:0{n}] fps= {n}.5 speed={n}.2x elapsed=0:00:0{n}.10",
                f'Process ({n}.50%) Elapsed : 00:00:0{n}.1 : After : "/Test/Media/a.mkv"',
                f"Processing time : 00:0{n}:00.5",
            ]
            lines = [log_line(text, thread=n, level="INF", second=n) for text in banner]
            lines += [log_line(text, thread=n, second=n) for text in texts]
            return "\n".join(lines)

        self.assertEqual(cr.normalized_lines(run(1)), cr.normalized_lines(run(2)))

    def test_order_ignored_and_change_kept(self) -> None:
        first = [log_line('ReMux : "/Test/Media/a.mkv"'), log_line('Verify : "/Test/Media/b.mkv"')]
        self.assertEqual(
            cr.normalized_lines("\n".join(first)), cr.normalized_lines("\n".join(reversed(first)))
        )
        changed = [first[0], log_line('ReEncode : "/Test/Media/b.mkv"')]
        self.assertNotEqual(
            cr.normalized_lines("\n".join(first)), cr.normalized_lines("\n".join(changed))
        )

    def test_bitrate_summary_gains_thread_file(self) -> None:
        log = "\n".join(
            [
                log_line('ProcessFiles (0.00%) Before : "/Test/Media/a.mkv"', thread=3),
                log_line('ProcessFiles (50.00%) Before : "/Test/Media/b.mkv"', thread=4),
                log_line('Verify : "/Test/Media/b.mkv"', thread=3),
                log_line('"Video" : Length: 00:00:10, Minimum: 1 Mbps', thread=3),
            ]
        )
        self.assertIn(
            '[DBG] <T> "Video" : Length: 00:00:10, Minimum: 1 Mbps : "a.mkv"',
            cr.normalized_lines(log),
        )


class LogComparisonTests(unittest.TestCase):
    def setUp(self) -> None:
        self._dir = tempfile.TemporaryDirectory()
        self.addCleanup(self._dir.cleanup)
        self.root = Path(self._dir.name)

    def write_run(self, name: str, texts: list[str], state: str = "Verified") -> Path:
        run = self.root / name
        run.mkdir()
        (run / "buildinfo.json").write_text(json.dumps(build_info()), encoding="utf-8")
        log = TOOLS_LOG.format(ffmpeg="8.1") + "\n".join(log_line(text) for text in texts) + "\n"
        (run / "PlexCleaner_process.log").write_text(log, encoding="utf-8")
        found = run / "Results_process.json"
        found.write_text(json.dumps(results(result("a.mkv", state))), encoding="utf-8")
        return found

    def compare(self, baseline: Path, candidate: Path) -> tuple[int, str]:
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            code = cr.main(["--baseline", str(baseline), "--results", str(candidate)])
        return code, out.getvalue() + err.getvalue()

    def test_codec_difference_fails(self) -> None:
        baseline = self.write_run("base", dump(track("Video", "V_MPEG2")))
        candidate = self.write_run("cand", dump(track("Video", "V_MPEG4/ISO/AVC")))
        code, text = self.compare(baseline, candidate)
        self.assertEqual(code, 1)
        self.assertIn("::error::a.mkv: tracks expected", text)

    def test_tracks_compare_by_file_name(self) -> None:
        baseline = self.write_run("base", dump(track("Video", "V_VP9")))
        moved = [text.replace("/Media/", "/Media/full/") for text in dump(track("Video", "V_VP9"))]
        code, text = self.compare(baseline, self.write_run("cand", moved))
        self.assertEqual(code, 0)
        self.assertNotIn("tracks expected", text)

    def test_track_missing_on_one_side_fails(self) -> None:
        baseline = self.write_run("base", dump(track("Video", "V_VP9"), track("Audio", "A_AC3")))
        candidate = self.write_run("cand", dump(track("Video", "V_VP9")))
        code, _ = self.compare(baseline, candidate)
        self.assertEqual(code, 1)

    def test_side_without_dump_is_unverified(self) -> None:
        baseline = self.write_run("base", dump(track("Video", "V_VP9")))
        candidate = self.write_run("cand", ['Verify : "/Test/Media/a.mkv"'])
        code, text = self.compare(baseline, candidate)
        self.assertEqual(code, 1)
        self.assertIn("::error::no Debug track dump in the results run", text)

    def test_missing_log_is_unverified(self) -> None:
        baseline = self.write_run("base", dump(track("Video", "V_VP9")))
        candidate = self.write_run("cand", dump(track("Video", "V_VP9")))
        (candidate.parent / "PlexCleaner_process.log").unlink()
        code, text = self.compare(baseline, candidate)
        self.assertEqual(code, 1)
        self.assertIn("::error::no process log in the results run", text)

    def test_track_reorder_fails(self) -> None:
        aac, ac3 = track("Audio", "A_AAC"), track("Audio", "A_AC3")
        code, text = self.compare(
            self.write_run("base", dump(aac, ac3)), self.write_run("cand", dump(ac3, aac))
        )
        self.assertEqual(code, 1)
        self.assertIn("::error::a.mkv: tracks expected", text)

    def test_last_read_without_tracks_fails(self) -> None:
        baseline = self.write_run("base", dump(track("Video", "V_VP9")))
        candidate = self.write_run("cand", [*dump(track("Video", "V_VP9")), *dump()])
        code, text = self.compare(baseline, candidate)
        self.assertEqual(code, 1)
        self.assertIn("found []", text)

    def test_renamed_bitrate_warning_shift_is_noted(self) -> None:
        state = "BitrateExceeded, ReEncoded, Verified"
        runs = []
        for name, peak in (("base", 5), ("cand", 6)):
            run = self.write_run(name, dump(track("Video", "V_VP9")), state)
            moved = results(result("a.avi", state, new_name="a.mkv"))
            run.write_text(json.dumps(moved), encoding="utf-8")
            warning = f'Maximum bitrate exceeded : {peak} Mbps > 4 Mbps : "/Test/Media/a.mkv"'
            with (run.parent / "PlexCleaner_process.log").open("a", encoding="utf-8") as log:
                log.write(log_line(warning, level="WRN") + "\n")
            runs.append(run)
        code, text = self.compare(*runs)
        self.assertEqual(code, 0)
        self.assertIn("note: bitrate figures of a.mkv shifted", text)
        self.assertNotIn("::warning::log", text)

    def test_log_difference_warns(self) -> None:
        baseline = self.write_run("base", [*dump(track("Video", "V_VP9")), "Warning : one"])
        candidate = self.write_run("cand", [*dump(track("Video", "V_VP9")), "Warning : two"])
        code, text = self.compare(baseline, candidate)
        self.assertEqual(code, 0)
        self.assertIn("::warning::log baseline only: [DBG] <T> Warning : one", text)
        self.assertIn("::warning::log results only: [DBG] <T> Warning : two", text)

    def bitrate_runs(self, state: str, candidate_state: str = "") -> tuple[int, str]:
        def texts(peak: int) -> list[str]:
            return [
                'ProcessFiles (0.00%) Before : "/Test/Media/a.mkv"',
                *dump(track("Video", "V_MPEG4/ISO/AVC")),
                f'"Video" : Length: 00:00:10, Minimum: 1 Mbps, Maximum: {peak} Mbps',
            ]

        baseline = self.write_run("base", texts(5), state)
        candidate = self.write_run("cand", texts(6), candidate_state or state)
        return self.compare(baseline, candidate)

    def test_reencoded_bitrate_shift_is_noted(self) -> None:
        code, text = self.bitrate_runs("DeInterlaced, Verified")
        self.assertEqual(code, 0)
        self.assertIn("note: bitrate figures of a.mkv shifted, and its outcome matches", text)
        self.assertNotIn("::warning::log", text)

    def test_bitrate_shift_without_reencode_warns(self) -> None:
        code, text = self.bitrate_runs("ReMuxed, Verified")
        self.assertEqual(code, 0)
        self.assertIn('::warning::log baseline only: [DBG] <T> "Video" : Length:', text)

    def test_bitrate_shift_with_changed_outcome_warns(self) -> None:
        code, text = self.bitrate_runs("DeInterlaced, Verified", "ReEncoded, Verified")
        self.assertEqual(code, 1)
        self.assertIn('::warning::log results only: [DBG] <T> "Video" : Length:', text)

    def test_repaired_bitrate_shift_is_noted(self) -> None:
        code, text = self.bitrate_runs("Repaired, Verified")
        self.assertEqual(code, 0)
        self.assertIn("note: bitrate figures of a.mkv shifted", text)

    def test_bitrate_warning_shift_is_noted(self) -> None:
        state = "BitrateExceeded, ReEncoded, Verified"
        runs = []
        for name, peak in (("base", 5), ("cand", 6)):
            run = self.write_run(name, dump(track("Video", "V_MPEG4/ISO/AVC")), state)
            warning = f'Maximum bitrate exceeded : {peak} Mbps > 4 Mbps : "/Test/Media/a.mkv"'
            with (run.parent / "PlexCleaner_process.log").open("a", encoding="utf-8") as log:
                log.write(log_line(warning, level="WRN") + "\n")
            runs.append(run)
        code, text = self.compare(*runs)
        self.assertEqual(code, 0)
        self.assertIn("note: bitrate figures of a.mkv shifted", text)
        self.assertNotIn("::warning::log", text)

    def test_resolution_not_read_as_address(self) -> None:
        lines = cr.normalized_lines(log_line('Size : 720x480 at 0x7ffd : "/Test/Media/a.mkv"'))
        self.assertIn('[DBG] <T> Size : 720x480 at 0xADDR : "/Test/Media/a.mkv"', lines)

    def test_shared_name_refused(self) -> None:
        other = 'Reading media info from tools : "/Test/Media/b/a.mkv"'
        texts = [*dump(track("Video", "V_VP9")), other]
        with self.assertRaises(ValueError):
            cr.output_tracks("\n".join(log_line(text) for text in texts))


if __name__ == "__main__":
    unittest.main()
