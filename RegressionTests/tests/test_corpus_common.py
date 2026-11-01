"""Tests for the pure parsing and classification helpers in corpus_common."""

import json
import os
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import corpus_common as cc


class HelperTests(unittest.TestCase):
    def test_stem_of(self) -> None:
        self.assertEqual(cc.stem_of("/a/b/Movie [x].mkv"), "Movie [x]")

    def test_hms(self) -> None:
        self.assertEqual(cc.hms(0), "00:00:00")
        self.assertEqual(cc.hms(59.9), "00:00:59")
        self.assertEqual(cc.hms(3661), "01:01:01")

    def test_is_filepath_quote(self) -> None:
        self.assertTrue(cc.is_filepath_quote("/x/y/file.MKV"))
        self.assertTrue(cc.is_filepath_quote("/x/y/file.tmp"))
        self.assertTrue(cc.is_filepath_quote("name.tmp12"))
        self.assertFalse(cc.is_filepath_quote("/x/y/file.txt"))
        self.assertFalse(cc.is_filepath_quote("plain error text"))


class ExtractErrorsTests(unittest.TestCase):
    def test_empty(self) -> None:
        self.assertEqual(cc.extract_errors(""), set())
        self.assertEqual(cc.extract_errors(None), set())

    def test_new_format_drops_path_and_splits(self) -> None:
        after = ': "err one | err two" : "/media/dir/file.mkv"'
        self.assertEqual(cc.extract_errors(after), {"err one", "err two"})

    def test_old_format_unquoted(self) -> None:
        self.assertEqual(cc.extract_errors("plain failure : /data/x.mkv"), {"plain failure"})

    def test_normalizes_paths_and_addresses(self) -> None:
        after = '"[h264 @ 0x7f12ab] bad in /Test/Media/a.mkv"'
        self.assertEqual(cc.extract_errors(after), {"[h264 @ 0xADDR] bad in <MEDIA>/a.mkv"})

    def test_benign_noise_dropped(self) -> None:
        self.assertEqual(cc.extract_errors('"Cannot load libcuda.so | real"'), {"real"})


class ErrorShapeTests(unittest.TestCase):
    def test_strips_context_and_numbers(self) -> None:
        raw = "[vist#0:0/h264 @ 0xADDR] error at MB 12 14, stream 3"
        self.assertEqual(cc.error_shape(raw), "[h264] error at MB N N, stream N")

    def test_collapses_corpus_subdir(self) -> None:
        self.assertEqual(cc.error_shape("<MEDIA>/full/a.mkv"), cc.error_shape("<MEDIA>/a.mkv"))

    def test_same_shape_across_sites(self) -> None:
        a = cc.error_shape("[h264 @ 0xADDR] mmco 5 at 100")
        b = cc.error_shape("[h264 @ 0xADDR] mmco 9 at 2048")
        self.assertEqual(a, b)


class ClassifyTests(unittest.TestCase):
    def test_classify_signature(self) -> None:
        errors = {"Non monotonically increasing DTS in stream", "Missing reference picture", "x"}
        self.assertEqual(
            cc.classify_signature(errors), {"DTS-NonMonotonic", "Decode-H264-RefPicture"}
        )

    def test_classify_signature_none(self) -> None:
        self.assertEqual(cc.classify_signature({"unrelated"}), set())

    def test_buckets_for(self) -> None:
        got = cc.buckets_for(
            ["ReEncoded", "Unknown"], ["Interlaced video detected", "Tags"], {"Decode-AAC"}, True
        )
        self.assertEqual(got, {"ReEncode", "Interlaced", "Tags", "Decode-AAC", "Error"})

    def test_buckets_for_empty(self) -> None:
        self.assertEqual(cc.buckets_for([], [], set(), False), set())


class ParseLogTests(unittest.TestCase):
    def test_missing_file(self) -> None:
        self.assertEqual(cc.parse_log("/nonexistent-dir-for-test/none.log"), {})

    def test_parse(self) -> None:
        lines = [
            '10:00:00 [INF] <1> ProcessFiles : Before : "/media/Show A.mkv"',
            "10:00:01 [INF] <1> Interlaced video detected",
            '10:00:02 [INF] <1> MkvMerge : Video : Format: "HEVC" : Interlaced: True',
            '10:00:02 [INF] <1> MkvMerge : Video : Format: "HEVC" : Interlaced: True',
            (
                "2026-01-02 10:00:03.123 -07:00 [ERR] <1> Failed execution of FfMpeg : Verify : "
                'ExitCode: 183 : "bad thing | other" : "/media/Show A.mkv"'
            ),
            '10:00:04 [INF] <2> ProcessFiles : Skipping non-MKV file : "/media/B.avi"',
            "10:00:05 [ERR] <2> Failed execution of FfMpeg : ExitCode: 1",
            "not a log line",
        ]
        with tempfile.TemporaryDirectory() as d:
            log = Path(d) / "run.log"
            log.write_text("\n".join(lines), encoding="utf-8")
            result = cc.parse_log(log)
        a = result["Show A"]
        self.assertEqual(a["detections"], {"Interlaced video"})
        self.assertEqual(a["tracks"], [{"type": "Video", "format": "HEVC", "interlaced": True}])
        self.assertEqual(a["errors"], {"bad thing", "other"})
        self.assertEqual(result["B"]["errors"], {"<silent-nonzero-exit>"})


class FindRunTests(unittest.TestCase):
    def test_explicit_absolute_and_relative(self) -> None:
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            self._run(root, "1.0.0-ga", 1000, {"Channel": "develop"})
            self.assertEqual(cc.find_run(str(root / "1.0.0-ga")), root / "1.0.0-ga")
            with mock.patch.object(cc, "REG_DIR", root):
                self.assertEqual(cc.find_run("1.0.0-ga"), root / "1.0.0-ga")

    def test_explicit_missing_dir_exits(self) -> None:
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            with mock.patch.object(cc, "REG_DIR", root), self.assertRaises(SystemExit) as cm:
                cc.find_run("0.0.0-gmissing")
        self.assertIn(
            str(root / "0.0.0-gmissing" / "PlexCleaner_process.log"), str(cm.exception.code)
        )

    def test_explicit_dir_without_log_exits(self) -> None:
        with tempfile.TemporaryDirectory() as d:
            run = Path(d) / "1.0.0-ga"
            run.mkdir()
            with self.assertRaises(SystemExit) as cm:
                cc.find_run(str(run))
        self.assertIn("no process log", str(cm.exception.code))

    @staticmethod
    def _run(root: Path, name: str, mtime: int, info: dict[str, str]) -> None:
        d = root / name
        d.mkdir()
        (d / "buildinfo.json").write_text(json.dumps(info))
        log = d / "PlexCleaner_process.log"
        log.write_text("")
        os.utime(log, (mtime, mtime))

    def test_newer_reduced_run_is_not_the_full_default(self) -> None:
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            self._run(root, "0.9.0-gz", 500, {"Channel": "develop", "Corpus": "full"})
            self._run(root, "1.0.0-ga", 1000, {"Channel": "develop", "Corpus": "full"})
            self._run(root, "1.0.1-gb-reduced", 2000, {"Channel": "develop", "Corpus": "reduced"})
            with mock.patch.object(cc, "REG_DIR", root):
                self.assertEqual(cc.find_run(None), root / "1.0.0-ga")
                self.assertEqual(cc.find_run(None, corpus="reduced"), root / "1.0.1-gb-reduced")

    def test_newer_run_on_another_channel_is_skipped(self) -> None:
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            self._run(root, "1.0.0-ga", 1000, {"Channel": "develop", "Corpus": "full"})
            self._run(root, "1.0.1-gb", 2000, {"Channel": "latest", "Corpus": "full"})
            with mock.patch.object(cc, "REG_DIR", root):
                self.assertEqual(cc.find_run(None), root / "1.0.0-ga")
                self.assertEqual(cc.find_run(None, channel="latest"), root / "1.0.1-gb")

    def test_run_without_corpus_field_is_full(self) -> None:
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            self._run(root, "1.0.0-ga", 1000, {"Channel": "develop"})
            self._run(root, "1.0.1-gb-reduced", 2000, {"Channel": "develop", "Corpus": "reduced"})
            with mock.patch.object(cc, "REG_DIR", root):
                self.assertEqual(cc.find_run(None), root / "1.0.0-ga")

    def test_no_matching_run_exits(self) -> None:
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            self._run(root, "1.0.1-gb-reduced", 2000, {"Channel": "develop", "Corpus": "reduced"})
            with mock.patch.object(cc, "REG_DIR", root), self.assertRaises(SystemExit) as cm:
                cc.find_run(None)
        self.assertIn("no develop full-corpus run", str(cm.exception.code))


class HeadClipTests(unittest.TestCase):
    def test_mkvmerge_command_and_numbered_rename(self) -> None:
        calls: list[list[str]] = []
        with tempfile.TemporaryDirectory() as d:
            src = Path(d) / "in.mkv"
            out = Path(d) / "out.mkv"

            def fake_run(cmd: list[str]) -> None:
                calls.append(cmd)
                out.with_name("out-001.mkv").write_bytes(b"data")

            self.assertTrue(cc.make_head_clip(src, out, 90, run=fake_run))
            self.assertTrue(out.exists())
        self.assertEqual(calls[0][0], "mkvmerge")
        self.assertIn("parts:00:00:00-00:01:30", calls[0])

    def test_ffmpeg_flags_and_stale_cleanup(self) -> None:
        calls: list[list[str]] = []
        with tempfile.TemporaryDirectory() as d:
            src = Path(d) / "in.mp4"
            out = Path(d) / "clip [1].mkv"
            stale = Path(d) / "clip [1].old"
            stale.write_bytes(b"x")

            def fake_run(cmd: list[str]) -> None:
                calls.append(cmd)

            self.assertFalse(cc.make_head_clip(src, out, 5, run=fake_run))
            self.assertFalse(stale.exists())
            cc.make_head_clip(src, out, 5, run=fake_run, cutter="ffmpeg-tags")
        self.assertIn("-bitexact", calls[0])
        self.assertNotIn("-bitexact", calls[1])


if __name__ == "__main__":
    unittest.main()
