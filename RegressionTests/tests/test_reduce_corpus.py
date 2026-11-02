"""Tests for the helpers in reduce_corpus."""

import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import reduce_corpus


class ReduceCorpusTests(unittest.TestCase):
    def test_human(self) -> None:
        self.assertEqual(reduce_corpus.human(512), "512B")
        self.assertEqual(reduce_corpus.human(2048), "2K")
        self.assertEqual(reduce_corpus.human(5 * 1024**3), "5G")
        self.assertEqual(reduce_corpus.human(3 * 1024**5), "3P")

    def test_load_regions(self) -> None:
        with tempfile.TemporaryDirectory() as d:
            rules = Path(d) / "rules.json"
            self.assertEqual(reduce_corpus.load_regions(rules), {})
            rules.write_text(json.dumps({"regions": {"a": {"start": "5", "end": 9}}}))
            self.assertEqual(reduce_corpus.load_regions(rules), {"a": (5, 9)})

    def test_load_accept_validates(self) -> None:
        raw = {
            "accept": {
                "_comment": {},
                "good": {"state_missing": ["x"]},
                "bad": {"state_missing": "x"},
                "worse": "text",
            }
        }
        with tempfile.TemporaryDirectory() as d:
            rules = Path(d) / "rules.json"
            self.assertEqual(reduce_corpus.load_accept(rules), {})
            rules.write_text(json.dumps(raw))
            self.assertEqual(reduce_corpus.load_accept(rules), {"good": {"state_missing": ["x"]}})
            rules.write_text(json.dumps({"accept": []}))
            self.assertEqual(reduce_corpus.load_accept(rules), {})

    def test_stage_settings_copies_writable(self) -> None:
        with tempfile.TemporaryDirectory() as d:
            settings_dir, workdir = Path(d) / "settings", Path(d) / "work"
            settings_dir.mkdir()
            source = settings_dir / reduce_corpus.SETTINGS_NAME
            source.write_text(json.dumps({"SchemaVersion": 4}))
            source.chmod(0o444)
            config = reduce_corpus.stage_settings(settings_dir, workdir)
            staged = config / reduce_corpus.SETTINGS_NAME
            self.assertEqual(config, workdir / "config")
            self.assertTrue(os.access(staged, os.W_OK))
            staged.write_text(json.dumps({"SchemaVersion": 5}))
            self.assertEqual(json.loads(source.read_text()), {"SchemaVersion": 4})
            reduce_corpus.stage_settings(settings_dir, workdir)
            self.assertEqual(json.loads(staged.read_text()), {"SchemaVersion": 4})

    def test_process_clip_mounts_staged_settings_writable(self) -> None:
        with tempfile.TemporaryDirectory() as d:
            settings_dir, workdir = Path(d) / "settings", Path(d) / "work"
            settings_dir.mkdir()
            (settings_dir / reduce_corpus.SETTINGS_NAME).write_text("{}")
            results = {"Results": {"Results": [{"OriginalFileName": "/media/a.mkv"}]}}

            def run(args: list[str], **_: object) -> subprocess.CompletedProcess[str]:
                (workdir / "out" / "clip_results.json").write_text(json.dumps(results))
                return subprocess.CompletedProcess(args, 0, "")

            with (
                mock.patch.object(reduce_corpus.os, "getuid", return_value=1000, create=True),
                mock.patch.object(reduce_corpus.os, "getgid", return_value=100, create=True),
                mock.patch.object(reduce_corpus.subprocess, "run", side_effect=run) as fake,
            ):
                _, states = reduce_corpus.process_clip(workdir, settings_dir)
            args = fake.call_args.args[0]
            options = args[: args.index(reduce_corpus.IMAGE)]
            volumes = [options[i + 1] for i, arg in enumerate(options) if arg == "--volume"]
            self.assertIn(f"{workdir / 'config'}:/config:rw", volumes)
            self.assertEqual(options[options.index("--user") + 1], "1000:100")
            self.assertIn(f"--settingsfile=/config/{reduce_corpus.SETTINGS_NAME}", args)
            self.assertTrue((workdir / "config" / reduce_corpus.SETTINGS_NAME).is_file())
            self.assertEqual(states, {"a": set()})


if __name__ == "__main__":
    unittest.main()
