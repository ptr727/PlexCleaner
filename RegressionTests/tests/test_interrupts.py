"""Tests that each tool dies by the signal it is sent, and the atomic write leaves no stray file."""

import json
import os
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path
from unittest import mock

from test_help import ROOT, clean_env

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "synthetic"))

import interrupts

STUB_SLEEP = "4311"


class AtomicWriteTests(unittest.TestCase):
    def test_rewrite_leaves_only_the_target(self) -> None:
        with tempfile.TemporaryDirectory() as d:
            target = Path(d) / "catalog.json"
            interrupts.atomic_write_text(target, "one")
            interrupts.atomic_write_text(target, "two")
            self.assertEqual(target.read_text(), "two")
            self.assertEqual([p.name for p in Path(d).iterdir()], ["catalog.json"])

    def test_failed_rename_keeps_the_old_content_and_leaves_no_temp(self) -> None:
        with tempfile.TemporaryDirectory() as d:
            target = Path(d) / "catalog.json"
            target.write_text("old")
            with (
                mock.patch("os.replace", side_effect=OSError("denied")),
                self.assertRaises(OSError),
            ):
                interrupts.atomic_write_text(target, "new")
            self.assertEqual(target.read_text(), "old")
            self.assertEqual([p.name for p in Path(d).iterdir()], ["catalog.json"])

    @unittest.skipIf(sys.platform == "win32", "needs POSIX file modes")
    def test_rewrite_keeps_the_target_mode(self) -> None:
        with tempfile.TemporaryDirectory() as d:
            target = Path(d) / "catalog.json"
            target.write_text("old")
            target.chmod(0o640)
            interrupts.atomic_write_text(target, "new")
            self.assertEqual(target.stat().st_mode & 0o777, 0o640)


@unittest.skipIf(sys.platform == "win32", "Windows cannot deliver SIGINT or SIGTERM to a script")
class ToolSignalTests(unittest.TestCase):
    def setUp(self) -> None:
        self._dir = tempfile.TemporaryDirectory()
        self.dir = Path(self._dir.name)
        self.stubs = self.dir / "bin"
        self.stubs.mkdir()
        self.tmp = self.dir / "tmp"
        self.tmp.mkdir()
        self.addCleanup(self._dir.cleanup)

    def write_stub(self, name: str, create_last_argument: bool = False) -> None:
        stub = self.stubs / name
        create = 'touch "${@: -1}"\n' if create_last_argument else ""
        stub.write_text(
            "#!/bin/bash\n"
            + create
            + 'echo $$ > "$STUB_PIDFILE.tmp"\n'
            + 'mv "$STUB_PIDFILE.tmp" "$STUB_PIDFILE"\n'
            + f"exec sleep {STUB_SLEEP} </dev/null >/dev/null 2>&1\n"
        )
        stub.chmod(0o755)

    def write_term_stub(self, name: str) -> None:
        # Leaves a marker only where it is sent SIGTERM, since SIGKILL cannot run the trap.
        stub = self.stubs / name
        stub.write_text(
            "#!/bin/bash\n"
            f"sleep {STUB_SLEEP} </dev/null >/dev/null 2>&1 &\n"
            "Sleeper=$!\n"
            'trap \'echo TERM > "$STUB_MARKER"; kill "$Sleeper"; exit 0\' TERM\n'
            'echo $$ > "$STUB_PIDFILE.tmp"\n'
            'mv "$STUB_PIDFILE.tmp" "$STUB_PIDFILE"\n'
            'wait "$Sleeper"\n'
        )
        stub.chmod(0o755)

    def env(self) -> dict[str, str]:
        return clean_env() | {
            "PATH": f"{self.stubs}{os.pathsep}{os.environ.get('PATH', '')}",
            "TMPDIR": str(self.tmp),
        }

    def interrupt(
        self, args: list[str], env: dict[str, str], sig: signal.Signals, pidfile: Path
    ) -> tuple[int, str]:
        proc = subprocess.Popen(
            [sys.executable, *args],
            env=env,
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            start_new_session=True,
        )
        try:
            deadline = time.monotonic() + 30
            while not pidfile.exists() and time.monotonic() < deadline:
                time.sleep(0.05)
            self.assertTrue(pidfile.exists(), "the tool never started its child")
            child = int(pidfile.read_text())
            proc.send_signal(sig)
            _, err = proc.communicate(timeout=30)
            self.assertTrue(self.gone(child), f"the child {child} outlived the tool")
        finally:
            try:
                os.killpg(proc.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            proc.wait()
            if pidfile.exists():
                try:
                    os.kill(int(pidfile.read_text()), signal.SIGKILL)
                except ProcessLookupError:
                    pass
        return proc.returncode, err

    @staticmethod
    def gone(pid: int) -> bool:
        # A child killed but not waited for lingers until init reaps it, so allow it a moment.
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            try:
                os.kill(pid, 0)
            except ProcessLookupError:
                return True
            time.sleep(0.05)
        return False

    def check(
        self, args: list[str], env: dict[str, str], tool: str, expect_term: bool = False
    ) -> None:
        cases = [signal.SIGTERM]
        if signal.getsignal(signal.SIGINT) is not signal.SIG_IGN:
            cases.append(signal.SIGINT)
        for sig in cases:
            with self.subTest(tool=tool, signal=sig.name):
                pidfile = self.dir / f"{tool}.{sig.name}.pid"
                marker = self.dir / f"{tool}.{sig.name}.marker"
                case_env = env | {"STUB_PIDFILE": str(pidfile), "STUB_MARKER": str(marker)}
                status, err = self.interrupt(args, case_env, sig, pidfile)
                self.assertEqual(status, -sig, err)
                self.assertNotIn("Traceback", err)
                self.assertEqual(err.count(f"{tool}: interrupted by {sig.name}"), 1, err)
                self.assertEqual(list(self.tmp.iterdir()), [])
                if expect_term:
                    self.assertTrue(marker.exists(), "the child was not sent SIGTERM")

    def test_locate_issue(self) -> None:
        corpus, rules = self.dir / "corpus", self.dir / "rules.json"
        (corpus / "full").mkdir(parents=True)
        (corpus / "full" / "a.mkv").write_bytes(b"")
        catalog = self.dir / "catalog.json"
        entry = {"file": "a.mkv", "decode_subtypes": ["x"], "state": []}
        catalog.write_text(json.dumps({"files": [entry]}))
        self.write_stub("docker")
        env = self.env() | {"REGRESSION_CORPUS_DIR": str(corpus)}
        args = [str(ROOT / "locate_issue.py"), "--catalog", str(catalog), "--full"]
        self.check([*args, "--write-rules", str(rules)], env, "locate_issue.py")
        self.assertFalse(rules.exists())
        self.assertEqual(sorted(p.name for p in self.dir.iterdir() if p.suffix == ".tmp"), [])

    def test_reduce_corpus(self) -> None:
        corpus, scratch = self.dir / "corpus", self.dir / "scratch"
        (corpus / "full").mkdir(parents=True)
        (corpus / "full" / "a.mp4").write_bytes(b"")
        scratch.mkdir()
        catalog = self.dir / "catalog.json"
        entry = {"file": "a.mp4", "decode_subtypes": [], "detections": [], "state": []}
        catalog.write_text(json.dumps({"files": [entry]}))
        self.write_term_stub("ffmpeg")
        env = self.env() | {
            "REGRESSION_CORPUS_DIR": str(corpus),
            "REGRESSION_SCRATCH_DIR": str(scratch),
        }
        args = [
            str(ROOT / "reduce_corpus.py"),
            "--catalog",
            str(catalog),
            "--settings",
            str(self.dir),
        ]
        self.check(args, env, "reduce_corpus.py", expect_term=True)
        self.assertEqual(list(scratch.glob("catalog.json*")), [])

    def test_synthesize(self) -> None:
        out = self.dir / "out.mp4"
        # The stub creates the partial file the build writes, so the signal lands with it on disk.
        self.write_stub("ffmpeg", create_last_argument=True)
        args = [str(ROOT / "synthetic" / "synthesize.py"), "container-mp4", "--out", str(out)]
        self.check(args, self.env(), "synthesize.py")
        self.assertEqual(sorted(p.name for p in self.dir.glob("out*")), [])


class SyntheticDirectoryTests(unittest.TestCase):
    def test_synthetic_scripts_run_from_a_copy_of_that_directory_alone(self) -> None:
        with tempfile.TemporaryDirectory() as d:
            copy = Path(d) / "synthetic"
            shutil.copytree(ROOT / "synthetic", copy, ignore=shutil.ignore_patterns("__pycache__"))
            for script in ("synthesize.py", "inject_cc_sei.py"):
                with self.subTest(script=script):
                    proc = subprocess.run(
                        [sys.executable, "-B", script, "--help"],
                        cwd=copy,
                        env=clean_env(),
                        capture_output=True,
                        text=True,
                        check=False,
                        timeout=60,
                    )
                    self.assertEqual(proc.returncode, 0, proc.stderr)
                    self.assertIn("usage:", proc.stdout)


if __name__ == "__main__":
    unittest.main()
