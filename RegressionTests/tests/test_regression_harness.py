"""Tests that the regression harness script answers --help, validates its inputs, and stops on a signal."""

import os
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path

from test_help import ROOT, clean_env

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


@unittest.skipUnless(
    sys.platform.startswith("linux") and shutil.which("bash") is not None, "needs Linux with bash"
)
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
        self.assertEqual(proc.returncode, 64)
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
        self.assertEqual(proc.returncode, 64)
        self.assertIn("REGRESSION_THREAD_COUNT", proc.stderr)


@unittest.skipUnless(
    sys.platform.startswith("linux") and shutil.which("bash") is not None, "needs Linux with bash"
)
class HarnessSignalTests(unittest.TestCase):
    def test_signal_stops_the_running_child(self) -> None:
        cases = [(signal.SIGTERM, "SIGTERM")]
        if signal.getsignal(signal.SIGINT) is not signal.SIG_IGN:
            cases.append((signal.SIGINT, "SIGINT"))
        for sig, name in cases:
            with self.subTest(signal=name), tempfile.TemporaryDirectory() as d:
                stubs = Path(d) / "bin"
                stubs.mkdir()
                pidfile = Path(d) / "child.pid"
                write_stub(
                    stubs,
                    "docker",
                    '[[ "$1" == pull ]] || exit 0\necho $$ > "$STUB_PIDFILE.tmp"\n'
                    'mv "$STUB_PIDFILE.tmp" "$STUB_PIDFILE"\nexec sleep 120 </dev/null >/dev/null 2>&1\n',
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
                    with self.assertRaises(ProcessLookupError):
                        os.kill(child, 0)
                finally:
                    try:
                        os.killpg(proc.pid, signal.SIGKILL)
                    except ProcessLookupError:
                        pass
                    proc.wait()
                self.assertEqual(proc.returncode, -sig, err)
                self.assertIn(f"interrupted by {name}", err)


@unittest.skipUnless(
    sys.platform.startswith("linux") and shutil.which("bash") is not None, "needs Linux with bash"
)
class ProvisionDatasetTests(unittest.TestCase):
    ZFS = """echo "$*" >> "$STUB_LOG"
case "$*" in
"get -H -o value origin pool/test") echo "$STUB_ORIGIN" ;;
"get -H -o value origin pool/test-retired-1") echo pool/corpus@s1 ;;
"get -H -o value origin "*) echo - ;;
"get -H -o value mountpoint "*) echo "${STUB_MOUNTPOINT:-/m/test}" ;;
"list -H -o name") printf '%s\\n' pool/test pool/test-retired-1 pool/test-retired-backup ;;
"list -H -t snapshot pool/test@backup") [[ -n "$STUB_HAS_BACKUP" ]] ;;
"list pool/test") [[ -n "$STUB_ORIGIN" ]] ;;
esac
"""

    def provision(
        self, origin: str, has_backup: bool, mountpoint: str = ""
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
                "STUB_MOUNTPOINT": mountpoint,
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

    def test_wrong_mountpoint_is_refused_before_any_change(self) -> None:
        proc, calls = self.provision("pool/corpus@s2", has_backup=True, mountpoint="/elsewhere")
        self.assertEqual(proc.returncode, 1, proc.stderr)
        self.assertIn("is not the mountpoint of pool/test", proc.stderr)
        changes = ("rollback", "rename", "clone", "snapshot", "destroy")
        self.assertEqual([c for c in calls if c.startswith(changes)], [])

    def test_dataset_that_is_not_a_corpus_clone_is_refused(self) -> None:
        proc, calls = self.provision("-", has_backup=False)
        self.assertEqual(proc.returncode, 1, proc.stderr)
        self.assertIn("is not a clone of pool/corpus", proc.stderr)
        changes = ("rollback", "rename", "clone", "snapshot", "destroy")
        self.assertEqual([c for c in calls if c.startswith(changes)], [])


if __name__ == "__main__":
    unittest.main()
