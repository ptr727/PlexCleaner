"""Tests that the harness and the Python tools answer --help and refuse unknown arguments."""

import os
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path

TOOLS = ["audit_physical.py", "catalog_corpus.py", "locate_issue.py", "reduce_corpus.py"]
INJECTORS = ["synthetic/inject_cc_sei.py", "synthetic/inject_hdr10plus_sei.py"]
ROOT = Path(__file__).resolve().parent.parent
HARNESS = ROOT / "regression-test.sh"
HARNESS_VARIABLES = [
    "REGRESSION_HOST_CONFIG",
    "REGRESSION_TEST_MEDIA",
    "REGRESSION_PLUGIN_BUILD_DIR",
    "REGRESSION_CORPUS_DATASET",
    "REGRESSION_TEST_DATASET",
    "REGRESSION_THREAD_COUNT",
    "REGRESSION_CORPUS_DIR",
    "REGRESSION_SCRATCH_DIR",
]


def clean_env() -> dict[str, str]:
    return {k: v for k, v in os.environ.items() if not k.startswith("REGRESSION_")}


class HelpTests(unittest.TestCase):
    def test_help_needs_no_environment(self) -> None:
        env = clean_env()
        for tool in TOOLS:
            with self.subTest(tool=tool):
                proc = subprocess.run(
                    [sys.executable, str(ROOT / tool), "--help"],
                    env=env,
                    capture_output=True,
                    text=True,
                    check=False,
                    timeout=60,
                )
                self.assertEqual(proc.returncode, 0, proc.stderr)
                self.assertIn("usage:", proc.stdout)

    def test_abbreviated_option_is_refused(self) -> None:
        for tool in [*TOOLS, "check_results.py", "synthetic/synthesize.py"]:
            with self.subTest(tool=tool):
                proc = subprocess.run(
                    [sys.executable, str(ROOT / tool), "--hel"],
                    env=clean_env(),
                    capture_output=True,
                    text=True,
                    check=False,
                    timeout=60,
                )
                self.assertEqual(proc.returncode, 2)
                self.assertNotIn("usage:", proc.stdout)

    def test_missing_variable_is_named(self) -> None:
        env = clean_env()
        proc = subprocess.run(
            [sys.executable, str(ROOT / "catalog_corpus.py")],
            env=env,
            capture_output=True,
            text=True,
            check=False,
            timeout=60,
        )
        self.assertNotEqual(proc.returncode, 0)
        self.assertRegex(proc.stderr, r"REGRESSION_[A-Z_]+ is not set")


class InjectorTests(unittest.TestCase):
    @staticmethod
    def run_injector(
        tool: str, *args: str, cwd: Path | None = None
    ) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [sys.executable, str(ROOT / tool), *args],
            cwd=cwd,
            capture_output=True,
            text=True,
            check=False,
            timeout=60,
        )

    def test_help_on_stdout(self) -> None:
        for tool in INJECTORS:
            for flag in ("--help", "-h"):
                with self.subTest(tool=tool, flag=flag):
                    proc = self.run_injector(tool, flag)
                    self.assertEqual(proc.returncode, 0, proc.stderr)
                    self.assertTrue(proc.stdout.startswith(f"usage: {Path(tool).name}"))

    def test_help_anywhere_writes_nothing(self) -> None:
        for tool in INJECTORS:
            with self.subTest(tool=tool), tempfile.TemporaryDirectory() as d:
                src, out = Path(d) / "in.hevc", Path(d) / "out.hevc"
                src.write_bytes(b"\x00\x00\x00\x01\x40\x01")
                long_form = self.run_injector(tool, "--help", str(out))
                short_form = self.run_injector(tool, str(src), "-h")
                for proc in (long_form, short_form):
                    self.assertEqual(proc.returncode, 0, proc.stderr)
                    self.assertTrue(proc.stdout.startswith(f"usage: {Path(tool).name}"))
                self.assertEqual(long_form.stdout, short_form.stdout)
                self.assertEqual(sorted(p.name for p in Path(d).iterdir()), ["in.hevc"])

    def test_abbreviated_help_is_refused(self) -> None:
        for tool in INJECTORS:
            with self.subTest(tool=tool):
                proc = self.run_injector(tool, "--hel")
                self.assertEqual(proc.returncode, 2)
                self.assertIn("unrecognized arguments: --hel", proc.stderr)

    def test_help_wins_over_an_unknown_option(self) -> None:
        for tool in INJECTORS:
            for args in (["--help", "--bogus"], ["--bogus", "--help"]):
                with self.subTest(tool=tool, args=args):
                    proc = self.run_injector(tool, *args)
                    self.assertEqual(proc.returncode, 0, proc.stderr)
                    self.assertTrue(proc.stdout.startswith(f"usage: {Path(tool).name}"))

    def test_dash_named_input_after_end_of_options(self) -> None:
        for tool in INJECTORS:
            with self.subTest(tool=tool), tempfile.TemporaryDirectory() as d:
                (Path(d) / "-in.hevc").write_bytes(
                    b"\x00\x00\x00\x01\x40\x01\x00\x00\x00\x01\x02\x01"
                )
                proc = self.run_injector(tool, "--", "-in.hevc", "out.hevc", cwd=Path(d))
                self.assertEqual(proc.returncode, 0, proc.stderr)
                self.assertTrue((Path(d) / "out.hevc").is_file())

    def test_missing_input_is_named_without_a_traceback(self) -> None:
        for tool in INJECTORS:
            with self.subTest(tool=tool), tempfile.TemporaryDirectory() as d:
                missing = Path(d) / "missing.hevc"
                proc = self.run_injector(tool, str(missing), str(Path(d) / "out.hevc"))
                self.assertEqual(proc.returncode, 1)
                self.assertIn(missing.name, proc.stderr)
                self.assertNotIn("Traceback", proc.stderr)
                self.assertEqual(list(Path(d).iterdir()), [])

    def test_unknown_argument_is_refused_before_any_write(self) -> None:
        for tool in INJECTORS:
            for extra in (["--bogus"], ["out.hevc", "-x"], ["-1"], ["-1", "out.hevc"]):
                with self.subTest(tool=tool, extra=extra), tempfile.TemporaryDirectory() as d:
                    src = Path(d) / "in.hevc"
                    src.write_bytes(b"\x00\x00\x00\x01\x40\x01")
                    proc = self.run_injector(
                        tool,
                        str(src),
                        *[str(Path(d) / e) if e.endswith(".hevc") else e for e in extra],
                    )
                    self.assertEqual(proc.returncode, 2)
                    self.assertIn("unrecognized arguments", proc.stderr)
                    self.assertEqual(sorted(p.name for p in Path(d).iterdir()), ["in.hevc"])

    def test_injects_into_named_output(self) -> None:
        for tool in INJECTORS:
            with self.subTest(tool=tool), tempfile.TemporaryDirectory() as d:
                src, out = Path(d) / "in.hevc", Path(d) / "out.hevc"
                src.write_bytes(b"\x00\x00\x00\x01\x40\x01\x00\x00\x00\x01\x02\x01")
                proc = self.run_injector(tool, str(src), str(out))
                self.assertEqual(proc.returncode, 0, proc.stderr)
                self.assertGreater(out.stat().st_size, src.stat().st_size)


@unittest.skipIf(sys.platform == "win32" or shutil.which("bash") is None, "needs Linux bash")
class HarnessTests(unittest.TestCase):
    def run_harness(self, *args: str, env: dict[str, str]) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            ["bash", str(HARNESS), *args],
            env=env,
            capture_output=True,
            text=True,
            check=False,
            timeout=60,
        )

    def test_help_lists_every_variable(self) -> None:
        proc = self.run_harness("--help", env=clean_env())
        self.assertEqual(proc.returncode, 0, proc.stderr)
        for name in HARNESS_VARIABLES:
            self.assertIn(name, proc.stdout)

    def test_missing_variable_is_named_before_any_host_lookup(self) -> None:
        proc = self.run_harness("quick", env=clean_env())
        self.assertEqual(proc.returncode, 2)
        self.assertIn("REGRESSION_HOST_CONFIG is not set", proc.stderr)

    def test_invalid_thread_count_is_named(self) -> None:
        env = clean_env() | {
            "REGRESSION_HOST_CONFIG": "/config",
            "REGRESSION_TEST_MEDIA": "/media",
            "REGRESSION_PLUGIN_BUILD_DIR": "/build",
            "REGRESSION_CORPUS_DATASET": "pool/corpus",
            "REGRESSION_TEST_DATASET": "pool/test",
            "REGRESSION_THREAD_COUNT": "0",
        }
        proc = self.run_harness("quick", env=env)
        self.assertEqual(proc.returncode, 2)
        self.assertIn("REGRESSION_THREAD_COUNT", proc.stderr)


HARNESS_ENV = {
    "REGRESSION_HOST_CONFIG": "/config",
    "REGRESSION_TEST_MEDIA": "/m/test",
    "REGRESSION_PLUGIN_BUILD_DIR": "/build",
    "REGRESSION_CORPUS_DATASET": "pool/corpus",
    "REGRESSION_TEST_DATASET": "pool/test",
    "REGRESSION_THREAD_COUNT": "4",
}


def write_stub(directory: Path, name: str, body: str) -> None:
    stub = directory / name
    stub.write_text("#!/bin/bash\n" + body)
    stub.chmod(0o755)


@unittest.skipIf(sys.platform == "win32" or shutil.which("bash") is None, "needs Linux bash")
class HarnessSignalTests(unittest.TestCase):
    def test_signal_stops_the_running_child(self) -> None:
        cases = [(signal.SIGTERM, 143, "SIGTERM")]
        if signal.getsignal(signal.SIGINT) is not signal.SIG_IGN:
            cases.append((signal.SIGINT, 130, "SIGINT"))
        for sig, status, name in cases:
            with self.subTest(signal=name), tempfile.TemporaryDirectory() as d:
                stubs = Path(d) / "bin"
                stubs.mkdir()
                pidfile = Path(d) / "child.pid"
                write_stub(
                    stubs,
                    "docker",
                    '[[ "$1" == pull ]] || exit 0\necho $$ > "$STUB_PIDFILE.tmp"\n'
                    'mv "$STUB_PIDFILE.tmp" "$STUB_PIDFILE"\nexec sleep 30\n',
                )
                write_stub(
                    stubs,
                    "id",
                    'case "$*" in "-u") echo 0 ;; "-u nonroot") echo 1001 ;; *) exit 1 ;; esac\n',
                )
                write_stub(stubs, "getent", "echo users:x:100:\n")
                env = clean_env() | HARNESS_ENV
                env["PATH"] = f"{stubs}{os.pathsep}{os.environ.get('PATH', '')}"
                env["STUB_PIDFILE"] = str(pidfile)
                proc = subprocess.Popen(
                    ["bash", str(HARNESS), "quick", "develop"],
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
                    self.assertTrue(pidfile.exists(), "the harness never started its child")
                    child = int(pidfile.read_text())
                    proc.send_signal(sig)
                    _, err = proc.communicate(timeout=30)
                finally:
                    try:
                        os.killpg(proc.pid, signal.SIGKILL)
                    except ProcessLookupError:
                        pass
                    proc.wait()
                self.assertEqual(proc.returncode, status, err)
                self.assertIn(f"interrupted by {name}", err)
                with self.assertRaises(ProcessLookupError):
                    os.kill(child, 0)


@unittest.skipIf(sys.platform == "win32" or shutil.which("bash") is None, "needs Linux bash")
class ProvisionDatasetTests(unittest.TestCase):
    ZFS = """echo "$*" >> "$STUB_LOG"
case "$*" in
"get -H -o value origin pool/test") echo "$STUB_ORIGIN" ;;
"get -H -o value origin pool/test-retired-1") echo pool/corpus@s1 ;;
"get -H -o value origin "*) echo - ;;
"get -H -o value mountpoint "*) echo /m/test ;;
"list -H -o name") printf '%s\\n' pool/test pool/test-retired-1 pool/test-retired-backup ;;
"list -H -t snapshot pool/test@backup") [[ -n "$STUB_HAS_BACKUP" ]] ;;
"list pool/test") [[ -n "$STUB_ORIGIN" ]] ;;
esac
"""

    def provision(
        self, origin: str, has_backup: bool
    ) -> tuple[subprocess.CompletedProcess[str], list[str]]:
        with tempfile.TemporaryDirectory() as d:
            stubs = Path(d) / "bin"
            stubs.mkdir()
            write_stub(stubs, "zfs", self.ZFS)
            log = Path(d) / "zfs.log"
            log.write_text("")
            script = (
                'source <(sed -n "/^CheckMountpoint()/,/^}/p;/^ProvisionDataset()/,/^}/p" "$1")\n'
                "TestDataset=pool/test Snapshot=pool/test@backup CorpusDataset=pool/corpus\n"
                "CorpusSnap=pool/corpus@s2 HostMedia=/m/test\n"
                "ProvisionDataset\n"
            )
            env = clean_env() | {
                "PATH": f"{stubs}{os.pathsep}{os.environ.get('PATH', '')}",
                "STUB_LOG": str(log),
                "STUB_ORIGIN": origin,
                "STUB_HAS_BACKUP": "1" if has_backup else "",
            }
            proc = subprocess.run(
                ["bash", "-Eeuo", "pipefail", "-c", script, "provision", str(HARNESS)],
                env=env,
                capture_output=True,
                text=True,
                check=False,
                timeout=60,
            )
            return proc, log.read_text().splitlines()

    def test_destroys_only_retired_corpus_clones(self) -> None:
        proc, calls = self.provision("pool/corpus@s1", has_backup=True)
        self.assertEqual(proc.returncode, 0, proc.stderr)
        destroyed = [c for c in calls if c.startswith("destroy")]
        self.assertEqual(destroyed, ["destroy -r pool/test-retired-1"])
        self.assertIn("Skipping pool/test-retired-backup", proc.stderr)

    def test_current_clone_without_backup_is_replaced(self) -> None:
        proc, calls = self.provision("pool/corpus@s2", has_backup=False)
        self.assertEqual(proc.returncode, 0, proc.stderr)
        self.assertTrue(any(c.startswith("rename pool/test pool/test-retired-") for c in calls))
        self.assertIn("clone pool/corpus@s2 pool/test", calls)
        self.assertIn("snapshot pool/test@backup", calls)
        self.assertFalse(any(c.startswith("rollback") for c in calls))

    def test_current_clone_with_backup_rolls_back(self) -> None:
        proc, calls = self.provision("pool/corpus@s2", has_backup=True)
        self.assertEqual(proc.returncode, 0, proc.stderr)
        self.assertIn("rollback pool/test@backup", calls)


if __name__ == "__main__":
    unittest.main()
